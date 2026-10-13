using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using InputComponent;
using Managers;
using Managers.UI;
using UnityEditor;
using UnityEngine;
using UI;

/// <summary>
/// 导航冒烟（纯逻辑，非 Play）：面板栈 / 模态 / 输入仲裁 / 暂停语义——UI 四期核心调度逻辑。
///
/// 迷你 MOMS 搭法（MOMS 管理器在非 Play 下的通用测试套路，可复用）：
/// UIManager 等管理器的静态门面全走 GameManager.Instance——编辑器态 Instance 为 null 门面全短路。
/// 关键手法：GameObject 建成 inactive 再 AddComponent&lt;GameManager&gt;，Awake 整段被跳过——
/// 非 Play 下 StartCoroutine 行为不定、Object.Destroy 必抛异常，这两颗雷就都不在执行路径上；
/// 再反射直写 Instance 与 managers 字典（字段初始化器在构造时已把集合 new 好，直接往里塞），
/// 手动泵三个全同步 Initialize（InputManager/TimeManager/UIManager，手动序即拓扑序）。
/// TimeManager/InputManager 一并注册的收益：不止断言 Desired 镜像，真闸门（PauseScale/IsInputEnabled）也能断言。
/// ⚠ 清场顺序：先复位闸门（此时 Instance 还活着）→ 清 Instance → 拆 GO → 按名扫尾 UIRoot
///   （UIManager.Deinitialize 用 Object.Destroy，编辑态抛异常，索性不调它，根节点 DestroyImmediate 扫尾）。
///
/// Exclusive 面板的来源：真实注册表只有 TestPanel/HudPanel（PausePanel prefab 未建）——
/// 冒烟动态注册三个自带 UIPanel 子类的模板，反射设 modal/pause 私有序列化字段后塞进 _registry。
/// </summary>
public static class NavigationSmokeTest
{
    private static int _failed;

    // ── 冒烟专用面板子类：UIPanel 是 abstract，AddComponent 需要具体类 ──
    private class SmokePopupPanel : UIPanel { }
    private class SmokeExclusivePanel : UIPanel { }
    private class SmokeHudPanel : UIPanel { }

    [MenuItem("UI/导航冒烟（纯逻辑）")]
    public static void SmokeTest()
    {
        // Play 挡板：冒烟会接管 GameManager.Instance、末尾还要 DestroyImmediate 全部 UIRoot——
        // 在 Play 里跑等于把正在运行的游戏 UI 静默拆掉（之后 UIManager.Show 全走死门面，面板不开零报错）
        if (Application.isPlaying)
        {
            Debug.LogError("[导航冒烟] 请退出 Play 再运行——本冒烟会销毁运行时 UIRoot 并清 Instance");
            return;
        }
        _failed = 0;
        GameObject gmGo = null;
        GameManager gm = null;
        UIPanel popupTemplate = null, exclusiveTemplate = null, hudTemplate = null;
        try
        {
            // ═══ 迷你 MOMS ═══
            gm = SetupMiniMoms(out gmGo);

            var ui = GameManager.Get<UIManager>();
            var input = GameManager.Get<InputManager>();
            Assert(ui != null && input != null, "环境：UIManager/InputManager 注册成功");

            // ═══ 动态注册三个冒烟面板（modal/pause 经反射注入；PanelEntry 字段全 public 直接构造）═══
            popupTemplate     = MakeTemplate<SmokePopupPanel>("SmokePopup", PanelModal.Popup, PanelPause.None);
            exclusiveTemplate = MakeTemplate<SmokeExclusivePanel>("SmokeExclusive", PanelModal.Exclusive, PanelPause.World);
            hudTemplate       = MakeTemplate<SmokeHudPanel>("SmokeHud", PanelModal.None, PanelPause.None);
            Register(ui, popupTemplate);
            Register(ui, exclusiveTemplate);
            Register(ui, hudTemplate);

            UIManager.CloseAllPanels();   // 干净基线：Initialize 的 autoShow 面板（HUD 若 modal=Popup 默认值）会占栈底
            Assert(ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 1f) && StackDepth(ui) == 0, "基线：无栈=输入开/不暂停");

            // ── ① Popup：不夺输入不停世界 ──
            UIManager.Show("SmokePopup");
            Assert(ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 1f), "① Popup 开：镜像输入开/不暂停");

            // ── ② Exclusive+World：镜像与真闸门双断言 ──
            UIManager.Show("SmokeExclusive");
            Assert(!ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 0f), "② 镜像：Exclusive+World → 输入关/暂停0");
            Assert(!input.IsInputEnabled && Eq(TimeManager.PauseScale, 0f), "② 真闸门：IsInputEnabled=false / PauseScale=0");

            // ── ③ Popup 叠在 Exclusive 上：二次 Show 走缓存命中分支（回归：二次开不进栈的必炸 bug）──
            UIManager.Show("SmokePopup");
            Assert(StackTop(ui) == "SmokePopup" && StackDepth(ui) == 2, $"③ 叠开置顶（缓存命中分支压栈），栈深2（实际 {StackDepth(ui)}）");
            Assert(!ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 0f), "③ 下层 Exclusive 仍生效（输入关/暂停0）");

            // ── ④ Esc 退弹窗：世界必须仍暂停（派生式的关键回归点，命令式在这里必翻车）──
            UIManager.HandleCancel();
            Assert(StackTop(ui) == "SmokeExclusive", "④ Esc 退栈顶 Popup");
            Assert(!ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 0f), "④ 退弹窗后下层 Exclusive 仍生效（世界仍暂停）");

            // ── ⑤ 再 Esc 退 Exclusive：全恢复、无脏状态 ──
            UIManager.HandleCancel();
            Assert(StackDepth(ui) == 0, "⑤ 栈空");
            Assert(ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 1f), "⑤ 镜像全恢复");
            Assert(input.IsInputEnabled && Eq(TimeManager.PauseScale, 1f), "⑤ 真闸门全恢复");

            // ── ⑥ Toggle 幂等（背包键范式：B 开 B 关；覆盖 Show 缓存命中/Hide 双路径反复进出）──
            UIManager.Toggle("SmokeExclusive");   // 开
            UIManager.Toggle("SmokeExclusive");   // 关
            UIManager.Toggle("SmokeExclusive");   // 开
            Assert(!ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 0f), "⑥ Toggle 奇数次=开，闸门正确");
            UIManager.Toggle("SmokeExclusive");   // 关
            Assert(ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 1f) && StackDepth(ui) == 0, "⑥ Toggle 偶数次=关，回基线");

            // ── ⑦ None 面板：不进栈、Esc 不退它；CloseAll 全恢复 ──
            UIManager.Show("SmokeHud");
            int depth = StackDepth(ui);
            UIManager.HandleCancel();
            Assert(StackDepth(ui) == depth && UIManager.IsOpen("SmokeHud"), "⑦ None 不进栈，Esc 不退它");
            UIManager.CloseAllPanels();
            Assert(StackDepth(ui) == 0 && ui.DesiredInputEnabled && Eq(ui.DesiredPauseScale, 1f),
                "⑦ CloseAllPanels：清栈 + 输入/暂停全恢复（回归：漏重算=关了面板世界还停着）");
        }
        finally
        {
            // ═══ 清场：顺序敏感——复位闸门（Instance 还活着才写得到）→ 防 OnDestroy 反初始化 → 清 Instance → 拆对象 ═══
            if (gm != null)
            {
                TimeManager.PauseScale = 1f;   // Destroy 之后再设会踩 Instance fake-null 陷阱（?. 对 Unity 假 null 不短路）
                (Field(gm, "initializationOrder") as IList)?.Clear();   // GO 全程 inactive，OnDestroy 理应不跑；万一跑了，
            }                                                           // 别让 UIManager.Deinit 的 Object.Destroy（编辑态必抛）炸红字
            ResetInstance();
            if (gmGo != null) Object.DestroyImmediate(gmGo);   // inactive GO，无 Awake/OnDestroy 生命周期
            foreach (var t in new[] { popupTemplate, exclusiveTemplate, hudTemplate })
                if (t != null) Object.DestroyImmediate(t.gameObject);
            CleanupOrphans();   // UIRoot（含挂在它下面的三层画布/EventSystem/面板实例）按名 DestroyImmediate 扫尾
        }

        if (_failed == 0) Debug.Log("[导航冒烟] 7 组用例全部通过 ✓");
        else Debug.LogError($"[导航冒烟] {_failed} 项失败，见上方红字");
    }

    // ═══════════ 迷你 MOMS ═══════════

    /// <summary>搭迷你环境：inactive GO 挂 GameManager（Awake 不跑，绕开编辑态协程/Destroy 雷），
    /// 反射注册三个 manager（手动序即拓扑序：Input/Time 无依赖在前，UIManager 依赖 Input 在后），反射直写 Instance</summary>
    private static GameManager SetupMiniMoms(out GameObject gmGo)
    {
        gmGo = new GameObject("SmokeGameManager");
        gmGo.SetActive(false);   // 必须先 inactive：AddComponent 时 GO 激活就会立刻跑 Awake（StartCoroutine 那行雷）
        var gm = gmGo.AddComponent<GameManager>();   // 字段初始化器照跑：managers/order 集合已 new 好，Awake 整段被跳过

        var managers = (Dictionary<System.Type, IManager>)Field(gm, "managers");
        var order = (List<IManager>)Field(gm, "initializationOrder");
        foreach (var m in new IManager[] { new InputManager(), new TimeManager(), new UIManager() })
        {
            var e = m.Initialize();
            while (e.MoveNext()) { }   // 三个全是同步 Initialize（yield break 直通）；将来哪个真异步了这里会先暴露
            managers[m.GetType()] = m;
            order.Add(m);
        }

        // Instance 反射直写：UIManager.Show/HandleCancel/TimeManager.PauseScale 等静态门面全靠它
        typeof(GameManager)
            .GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, gm);
        return gm;
    }

    /// <summary>清 GameManager.Instance 静态残留（auto-property backing field 反射置 null），
    /// 防冒烟结束后 fake-null 实例污染后续门面调用</summary>
    private static void ResetInstance()
    {
        typeof(GameManager)
            .GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, null);
    }

    /// <summary>按名扫尾 UIRoot/UIEventSystem——不调 UIManager.Deinitialize（Object.Destroy 编辑态抛异常），
    /// 根节点连同三层画布/EventSystem/面板实例一并 DestroyImmediate</summary>
    private static void CleanupOrphans()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && (go.name == "UIRoot" || go.name == "UIEventSystem"))
                Object.DestroyImmediate(go);
    }

    // ═══════════ 冒烟面板模板 ═══════════

    /// <summary>建模板实例并反射注入 modal/pause（UIPanel 的私有序列化字段，运行时只读）</summary>
    private static UIPanel MakeTemplate<T>(string name, PanelModal modal, PanelPause pause) where T : UIPanel
    {
        var go = new GameObject(name, typeof(RectTransform));
        var panel = go.AddComponent<T>();
        typeof(UIPanel).GetField("modal", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(panel, modal);
        typeof(UIPanel).GetField("pause", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(panel, pause);
        return panel;
    }

    /// <summary>塞进 UIManager._registry：Show 走真管线（注册表→Instantiate→缓存→RegisterOpen），
    /// Instantiate 出的副本带模板的 modal/pause 字段值</summary>
    private static void Register(UIManager ui, UIPanel template)
    {
        var registry = Field(ui, "_registry") as Dictionary<string, UIManagerConfig.PanelEntry>;
        registry[template.gameObject.name] = new UIManagerConfig.PanelEntry
        {
            panelId = template.gameObject.name,
            prefab = template,
            autoShow = false
        };
    }

    // ═══════════ 白盒读取 ═══════════

    private static int StackDepth(UIManager ui) => (Field(ui, "_stack") as List<UIPanel>).Count;
    private static string StackTop(UIManager ui) => (Field(ui, "_stack") as List<UIPanel>)[^1].PanelId;

    private static object Field(object obj, string name)
        => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);

    // ═══════════ 断言辅助（对齐 AttributeSmokeTest / LayoutSmokeTest 风格）═══════════

    private static bool Eq(float a, float b) => Mathf.Approximately(a, b);

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[导航冒烟] 失败：{label}"); }
    }
}
