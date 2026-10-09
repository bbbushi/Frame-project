using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Managers.UI;
using PlayerSystem;
using Attributes;
/// <summary>
/// UI 调试菜单：
/// - 打开/关闭 TestPanel、关闭全部面板（Play Mode 用）
/// - 校验注册表（非 Play 可用：prefab 非空 / 挂 UIPanel / id 非空唯一 / 与组件 PanelId 一致）
/// - 属性/效果调试：受伤、治疗、飘字、铁壁/中毒 GameplayEffect（Play Mode 用）
/// </summary>
public static class UIDebugMenu
{
    [MenuItem("UI/打开面板/TestPanel")]
    public static void ShowTestPanel()
    {
        if (EnsurePlaying()) UIManager.Show("TestPanel");
    }

    [MenuItem("UI/关闭面板/TestPanel")]
    public static void HideTestPanel()
    {
        if (EnsurePlaying()) UIManager.Hide("TestPanel");
    }

    [MenuItem("UI/关闭全部面板")]
    public static void CloseAll()
    {
        if (EnsurePlaying()) UIManager.CloseAllPanels();
    }

    [MenuItem("UI/校验注册表")]
    public static void ValidateConfig()
    {
        var config = Resources.Load<UIManagerConfig>("data/ui/UIManagerConfig");
        if (config == null) { Debug.LogError("[UI校验] 未找到 data/UIManagerConfig"); return; }
        if (config.panels.Count == 0) { Debug.LogWarning("[UI校验] 注册表为空（还没登记任何面板）"); return; }

        var seen = new HashSet<string>();
        int bad = 0;
        foreach (var e in config.panels)
        {
            if (e?.prefab == null)
            { Debug.LogError("[UI校验] 存在空条目（或 prefab 未拖）"); bad++; continue; }

            var panel = e.prefab.GetComponent<UI.UIPanel>();
            if (panel == null)
            { Debug.LogError($"[UI校验] {e.prefab.name} 根节点缺 UIPanel 组件"); bad++; continue; }

            if (string.IsNullOrEmpty(e.panelId))
            { Debug.LogWarning($"[UI校验] {e.prefab.name} 未填 panelId（运行时将回退为「{panel.PanelId}」）"); continue; }

            if (!seen.Add(e.panelId))
            { Debug.LogError($"[UI校验] id 重复：「{e.panelId}」"); bad++; continue; }

            if (e.panelId != panel.PanelId)
                Debug.LogWarning($"[UI校验] id「{e.panelId}」与组件 PanelId「{panel.PanelId}」不一致（以注册表为准）");
        }

        if (bad == 0) Debug.Log($"[UI校验] 注册表 {config.panels.Count} 条，全部通过 ✓");
    }

    [MenuItem("UI/调试/玩家受伤10点")]     // 直调属性宿主（绕过 DamageComponent 的击退/粒子等表现）。
    public static void HurtPlayer()
    {
        if(EnsurePlaying()) Player.Instance?.healthManageComponent?.ApplyDamage(10f, null);   // GAS 化后飘字走事件总线：这条也会飘字（数据/表现分离的验收点）
    }
       
    [MenuItem("UI/调试/玩家治疗回满")]     // 测死亡边界后拉回来反复测
    public static void HealPlayer()
    {
        if(EnsurePlaying()) Player.Instance?.healthManageComponent?.ApplyHeal(float.MaxValue, null);
    }

    [MenuItem("UI/调试/玩家复活（清效果+回满）")]   // 真·Revive：与治疗的区别=ClearEffects。毒没跳完就治疗会"回满又掉"，那是 Heal 语义不是 bug
    public static void RevivePlayer()
    {
        if(!EnsurePlaying()) return;
        var h = Player.Instance?.healthManageComponent;
        if(h == null) { Debug.LogWarning("[UI] 场景里没有 Player"); return; }
        h.Revive();
        Debug.Log($"[UI] 已复活：HP {h.CurrentHP:0}/{h.MaxHP:0}，残留效果 {h.Set.ActiveEffects.Count} 条");
    }

    [MenuItem("UI/调试/铁壁（防+50 持续5秒）")]   // 挂上→受伤10 飘 7；等 5 秒过期→再受伤 飘 10（减伤闭环）
    public static void ApplyIronWall()
    {
        if(!EnsurePlaying()) return;
        var fx = Resources.Load<GameplayEffect>("data/effect/IronWall");
        if(fx == null) { Debug.LogWarning("[UI] 缺 Resources/data/effect/IronWall.asset"); return; }
        if(Player.Instance?.healthManageComponent?.ApplyEffect(fx, null) == true)
            Debug.Log($"[UI] 已挂 {fx.name}（{fx.durationType}, 时长{fx.duration}s, 周期{fx.period}s, 持续修饰{fx.modifiers.Count}条, 周期修饰{fx.periodModifiers.Count}条）");
    }

    [MenuItem("UI/调试/中毒（每秒-5 持续3秒）")]  // 每秒血条 -5 且不飘字（Effect 不在飘字白名单）
    public static void ApplyPoison()
    {
        if(!EnsurePlaying()) return;
        var fx = Resources.Load<GameplayEffect>("data/effect/Poison");
        if(fx == null) { Debug.LogWarning("[UI] 缺 Resources/data/effect/Poison.asset"); return; }
        if(Player.Instance?.healthManageComponent?.ApplyEffect(fx, null) == true)
            Debug.Log($"[UI] 已挂 {fx.name}（{fx.durationType}, 时长{fx.duration}s, 周期{fx.period}s, 持续修饰{fx.modifiers.Count}条, 周期修饰{fx.periodModifiers.Count}条）");
    }
        
    [MenuItem("UI/调试/测试飘字")]         // 不用打怪就能验证坐标链
    public static void TestFloatingText()
    {
        if(!EnsurePlaying()) return;       // 先挡 Play：编辑器下非 Play 也能 FindObjectOfType 到场景里的 Player，
        if(Player.Instance == null)        // 顺序反了会误报「没有 Player」
        {
            Debug.LogWarning("[UI] 场景里没有 Player");
            return;
        }
        Debug.Log($"[UI] 测试飘字：在 Player.ChestPosition {Player.Instance.ChestPosition} 显示白色「999」（下一帧出字）");
        // 菜单回调属于编辑器上下文——UGUI 对象在玩家循环外 Instantiate 会"活着但不渲染"
        // （Awake/Update 照常跑，CanvasRenderer 却始终不出几何体，零警告零报错）。
        // 必须延迟一帧让 Show 落回玩家循环执行。别把这层延迟当多余优化掉！
        GameManager.Instance.StartCoroutine(ShowNextFrame());
    }

    private static System.Collections.IEnumerator ShowNextFrame()
    {
        yield return null;    // 等一帧，回到玩家循环
        if(Player.Instance != null)
            FloatingTextManager.Show("999", Player.Instance.ChestPosition, Color.white);
    }
    

    /// <summary>
    /// 确保当前处于 Play Mode，否则弹出警告并返回 false
    /// </summary>
    /// <returns></returns>
    private static bool EnsurePlaying()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[UI] 请先进 Play Mode 再使用此菜单");
        return false;
    }
}
