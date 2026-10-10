using UnityEditor;
using UnityEngine;
using enemy.ai;

/// <summary>
/// AI 调试菜单：查状态 / 强制切状态。全部纯数据操作（不 Instantiate），
/// 不受「编辑器上下文 UGUI 不渲染」坑影响；状态机在 Play 模式 Init 后才存在，非 Play 只提示。
/// </summary>
public static class AIDebugMenu
{
    [MenuItem("AI/调试/打印选中敌人状态")]
    public static void PrintSelectedState()
    {
        EnemyAIComponent ai = FindSelectedAI();
        if (ai == null) return;
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AI] 状态机只在 Play 模式装配（Start → Init），请先进 Play");
            return;
        }

        float homeDist = Vector2.Distance(ai.Owner.transform.position, ai.HomeAnchor);
        Debug.Log($"[AI] {ai.Owner.name}: 状态={ai.CurrentId}" +
                  $" 目标={(ai.Target != null ? ai.Target.name : "无")}" +
                  $" 最后目击={ai.LastKnownPosition}" +
                  $" 当前有视线={ai.HasLineOfSightNow}" +
                  $" 离家={homeDist:F1}（leash {ai.Cfg.chase.leashRange}）");
    }

    [MenuItem("AI/调试/强制进入追击")]
    public static void ForceChase()
    {
        EnemyAIComponent ai = FindSelectedAI();
        if (ai == null) return;
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AI] 请先进 Play 再强制切状态");
            return;
        }

        ai.ResetAI();
        ai.TryEnter(EnemyStateId.Chase);
        Debug.Log($"[AI] {ai.Owner.name} → Chase（HomeAnchor 已重锚到脚下）");
    }

    static EnemyAIComponent FindSelectedAI()
    {
        var go = Selection.activeGameObject;
        var ai = go != null ? go.GetComponentInParent<EnemyAIComponent>() : null;
        if (ai == null) Debug.LogWarning("[AI] 先在 Hierarchy 选中敌人（或其任意子物体）");
        return ai;
    }
}
