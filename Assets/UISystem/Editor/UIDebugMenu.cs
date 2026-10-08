using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Managers.UI;
using PlayerSystem;
/// <summary>
/// UI 调试菜单：
/// - 打开/关闭 TestPanel、关闭全部面板（Play Mode 用）
/// - 校验注册表（非 Play 可用：prefab 非空 / 挂 UIPanel / id 非空唯一 / 与组件 PanelId 一致）
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

    [MenuItem("UI/调试/玩家受伤10点")]     // 验证 HUD：直调 TakeDamage——故意不走 DamageComponent，
    public static void HurtPlayer()
    {
        if(EnsurePlaying()) Player.Instance?.healthManageComponent?.ApplyDamage(10f, null);   // 所以【不飘字】，这正是数据源分离的验证点
    }
       
    [MenuItem("UI/调试/玩家治疗回满")]     // 测死亡边界后拉回来反复测
    public static void HealPlayer()
    {
        if(EnsurePlaying()) Player.Instance?.healthManageComponent?.ApplyHeal(float.MaxValue, null);
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
        FloatingTextManager.Show("999", Player.Instance.ChestPosition, Color.yellow);
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
