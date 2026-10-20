using System.Collections.Generic;
using System.IO;
using Managers.GameFlow;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 场景注册表校验（五期方案 §五.5.2）：注册表自检 + Build Settings 对齐。
/// 治"注册表登记了但忘了加 Build"的病（本项目转场全废的原病根）——登记和 Build 是两处手工维护，
/// 靠人脑对齐必漏，一键校验兜底。
/// Validate 是纯函数（流程冒烟直接调）；菜单做双向检查后输出。
/// </summary>
public static class FlowValidation
{
    /// <summary>注册表自检（纯逻辑，可测）：空条目 / 空 sceneId / 重复 sceneId</summary>
    public static List<string> Validate(SceneRegistry registry)
    {
        var errs = new List<string>();
        if (registry?.scenes == null) { errs.Add("注册表为空"); return errs; }
        var seen = new HashSet<string>();
        for (int i = 0; i < registry.scenes.Count; i++)
        {
            var e = registry.scenes[i];
            if (e == null) { errs.Add($"第 {i + 1} 条是空条目"); continue; }
            if (string.IsNullOrEmpty(e.sceneId)) { errs.Add($"第 {i + 1} 条 sceneId 为空"); continue; }
            if (!seen.Add(e.sceneId)) errs.Add($"sceneId 重复：{e.sceneId}");
        }
        return errs;
    }

    /// <summary>
    /// Build 对齐（Editor API）：注册表每条都必须能在 Build Settings 里找到且勾了 enabled——
    /// 缺了正是 LoadSceneAsync 返 null 的直接原因（GameFlowManager.Routine 的报错路径兜的就是它）。
    /// 反向（Build 有但注册表没登记）是信息级，由菜单单独输出，不进错误列表。
    /// </summary>
    public static List<string> ValidateBuildList(SceneRegistry registry)
    {
        var errs = new List<string>();
        if (registry?.scenes == null) return errs;
        var build = EditorBuildSettings.scenes;
        foreach (var e in registry.scenes)
        {
            if (e == null || string.IsNullOrEmpty(e.sceneId)) continue;   // 自检已报，此处不重复
            var hit = System.Array.Find(build, s => !string.IsNullOrEmpty(s.path)
                && Path.GetFileNameWithoutExtension(s.path) == e.sceneId);
            if (hit == null)
                errs.Add($"「{e.sceneId}」不在 Build Settings 列表（LoadSceneAsync 会返 null，转场必失败）");
            else if (!hit.enabled)
                errs.Add($"「{e.sceneId}」在 Build 列表但未勾选 enabled（同样加载不到）");
        }
        return errs;
    }

    [MenuItem("Flow/校验场景注册表")]
    public static void ValidateMenu()
    {
        var reg = Resources.Load<SceneRegistry>("data/Scene/SceneRegistry");
        if (reg == null) { Debug.LogError("[Flow校验] 未找到 Resources/data/Scene/SceneRegistry.asset（还没建？）"); return; }

        int bad = 0;
        foreach (var e in Validate(reg)) { Debug.LogError($"[Flow校验] {e}"); bad++; }
        foreach (var e in ValidateBuildList(reg)) { Debug.LogWarning($"[Flow校验] {e}"); }

        // 反向信息：Build 里 enabled 的场景若没登记进注册表，流程系统就"看不见"它
        var registered = new HashSet<string>();
        foreach (var e in reg.scenes)
            if (e != null && !string.IsNullOrEmpty(e.sceneId)) registered.Add(e.sceneId);
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && !string.IsNullOrEmpty(s.path)
                && !registered.Contains(Path.GetFileNameWithoutExtension(s.path)))
                Debug.Log($"[Flow校验] 信息：Build 场景「{Path.GetFileNameWithoutExtension(s.path)}」未登记进注册表（流程系统无法切换到它）");

        if (bad == 0) Debug.Log($"[Flow校验] 注册表 {reg.scenes.Count} 条，自检通过 ✓（Build 对齐看上方警告/信息，无即全对齐）");
    }
}
