using System.Collections.Generic;
using System.Reflection;
using GameFlowSystem;
using Managers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 流程冒烟（纯逻辑，非 Play）：FlowState 转换表 / 门面校验 / 注册表——五期 GameFlowManager 调度逻辑。
///
/// ⚠ 红线（方案 §三.2）：绝不走任何"允许路径"的转场——TransitionTo 走通就会 StartCoroutine(Routine)
/// → LoadSceneAsync 真换编辑器场景（未保存改动丢失）。全部用例只打「拒绝路径 + 状态位」：
/// 所有拒绝分支都在协程启动前 return（StartLevel 的注册表/同场景校验、TransitionTo 的 Loading 并发拒绝），
/// 本冒烟同时是"拒绝发生在协程之前"这条保证的回归防线。
/// 允许路径（真正切场景）全部留给 Play 手测（方案 §八.3）。
///
/// 迷你 MOMS（NavigationSmokeTest 同款，只注 GameFlowManager）：inactive GM 绕开 Awake 的
/// StartCoroutine 编辑态雷，反射注入 managers 字典 + 手动泵 Initialize；
/// State 是 private set、_currentScene/_registry 是私有序列化字段——全部反射驱动，
/// registry 用内存 SO（不依赖用户是否已建资产）。
/// </summary>
public static class FlowSmokeTest
{
    private static int _failed;

    [MenuItem("Flow/流程冒烟（纯逻辑）")]
    public static void SmokeTest()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[流程冒烟] 请退出 Play 再运行——本冒烟会接管 GameManager.Instance");
            return;
        }
        _failed = 0;
        GameObject gmGo = null;
        GameFlowManager flow = null;
        SceneRegistry injected = null;
        try
        {
            // A. 门面判空约定：环境未搭时 Current 不炸且返 MainMenu
            Assert(GameFlowManager.Current == FlowState.MainMenu, "A 门面判空：无环境时 Current 返 MainMenu");

            // ═══ 迷你 MOMS ═══
            gmGo = new GameObject("FlowSmokeGM");
            gmGo.SetActive(false);
            var gm = gmGo.AddComponent<GameManager>();
            flow = SetupMiniMoms(gmGo, out injected);

            var state = typeof(GameFlowManager).GetProperty("State");
            Assert((FlowState)state.GetValue(flow) == FlowState.MainMenu, "B 初始态 MainMenu");

            SetScene(flow, "SceneA");

            // C1. 转换表：Playing 下重复开始当前关卡 → 拒绝（重开语义归 ReloadCurrent）
            state.SetValue(flow, FlowState.Playing);
            GameFlowManager.StartLevel("SceneA");
            Assert((FlowState)state.GetValue(flow) == FlowState.Playing, "C1 Playing 下 StartLevel(当前关卡) 拒绝");

            // C2. 入口校验：未注册的 sceneId → 拒绝（拼写错误在入口报）
            GameFlowManager.StartLevel("不存在");
            Assert((FlowState)state.GetValue(flow) == FlowState.Playing, "C2 未注册 sceneId 拒绝");

            // C3. GameOver 状态位：仅 Playing 可设；重复调用幂等（GameOver 下再设被拒）
            GameFlowManager.GameOver();
            Assert((FlowState)state.GetValue(flow) == FlowState.GameOver, "C3a GameOver 状态位落定");
            GameFlowManager.GameOver();
            Assert((FlowState)state.GetValue(flow) == FlowState.GameOver, "C3b 重复 GameOver 幂等");

            // C4. ReloadCurrent 状态校验：MainMenu 下拒绝（主菜单没有"重开"语义）
            state.SetValue(flow, FlowState.MainMenu);
            GameFlowManager.ReloadCurrent();
            Assert((FlowState)state.GetValue(flow) == FlowState.MainMenu, "C4 MainMenu 下 ReloadCurrent 拒绝");

            // C5. Loading 并发拒绝：转场中任何门面全拒（双 Routine 并发 = 乱套）
            state.SetValue(flow, FlowState.Loading);
            GameFlowManager.StartLevel("SceneB");
            GameFlowManager.ToMainMenu();
            Assert((FlowState)state.GetValue(flow) == FlowState.Loading, "C5 Loading 中并发转场全拒");

            // C6. ToMainMenu 入口校验：注册表无 isMainMenu 条目 → 拒绝
            state.SetValue(flow, FlowState.Playing);
            Field(flow, "_registry").SetValue(flow, null);
            GameFlowManager.ToMainMenu();
            Assert((FlowState)state.GetValue(flow) == FlowState.Playing, "C6 无主菜单条目 ToMainMenu 拒绝");

            // ═══ D. FlowValidation 纯函数（注册表自检规则）═══
            var bad = ScriptableObject.CreateInstance<SceneRegistry>();
            bad.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "", displayName = "空id" });
            bad.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "A" });
            bad.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "A" });   // 重复
            bad.scenes.Add(null);                                             // 空条目
            Assert(FlowValidation.Validate(bad).Count >= 3, "D 坏注册表至少检出 3 类问题");

            var good = ScriptableObject.CreateInstance<SceneRegistry>();
            good.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "A" });
            good.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "B", isMainMenu = true });
            Assert(FlowValidation.Validate(good).Count == 0, "D 合法注册表零问题");

            // ═══ E. 真资产实检（用户建了 SceneRegistry.asset 才跑；Build 对齐只警告不断言——手工维护的面板）═══
            var real = Resources.Load<SceneRegistry>("data/Scene/SceneRegistry");
            if (real == null)
            {
                Debug.Log("[流程冒烟] 跳过真资产实检（尚未创建 Resources/data/Scene/SceneRegistry）");
            }
            else
            {
                var realErrs = FlowValidation.Validate(real);
                Assert(realErrs.Count == 0,
                    $"E 真实 SceneRegistry 自检（{(realErrs.Count > 0 ? string.Join("；", realErrs.ToArray()) : "过")}）");
                foreach (var e in FlowValidation.ValidateBuildList(real))
                    Debug.LogWarning($"[流程冒烟] Build 对齐：{e}");
            }
        }
        finally
        {
            flow?.Deinitialize();   // 对称退订 sceneLoaded（纪律；非 Play 下也不会真触发）
            ResetInstance();
            if (gmGo != null) Object.DestroyImmediate(gmGo);
            if (injected != null) Object.DestroyImmediate(injected);
        }

        if (_failed == 0) Debug.Log("[流程冒烟] 转换表/门面校验/注册表 全部通过 ✓");
        else Debug.LogError($"[流程冒烟] {_failed} 项失败，见上方红字");
    }

    // ═══════════ 迷你 MOMS ═══════════

    /// <summary>inactive GM + 反射注入 GameFlowManager（手动序泵 Initialize）+ 内存注册表 + 直写 Instance</summary>
    private static GameFlowManager SetupMiniMoms(GameObject gm, out SceneRegistry injected)
    {
        var flow = new GameFlowManager();
        var e = flow.Initialize();          // Resources.Load(可能 null 只警告) + sceneLoaded 订阅——编辑器态全安全
        while (e.MoveNext()) { }

        injected = ScriptableObject.CreateInstance<SceneRegistry>();
        injected.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "SceneA" });
        injected.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "SceneB" });
        injected.scenes.Add(new SceneRegistry.SceneEntry { sceneId = "Menu", isMainMenu = true });
        Field(flow, "_registry").SetValue(flow, injected);
        Field(flow, "_currentScene").SetValue(flow, "SceneA");

        var managers = (Dictionary<System.Type, IManager>)Field(gm, "managers").GetValue(gm);
        var order = (List<IManager>)Field(gm, "initializationOrder").GetValue(gm);
        managers[typeof(GameFlowManager)] = flow;
        order.Add(flow);
        typeof(GameManager)
            .GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, gm);
        return flow;
    }

    private static void SetScene(GameFlowManager flow, string sceneId)
        => Field(flow, "_currentScene").SetValue(flow, sceneId);

    /// <summary>清 GameManager.Instance 静态残留（auto-property backing field 反射置 null）</summary>
    private static void ResetInstance()
    {
        typeof(GameManager)
            .GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, null);
    }

    private static FieldInfo Field(object obj, string name)
        => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

    // ═══════════ 断言辅助（对齐 NavigationSmokeTest / LayoutSmokeTest 风格）═══════════

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[流程冒烟] 失败：{label}"); }
    }
}
