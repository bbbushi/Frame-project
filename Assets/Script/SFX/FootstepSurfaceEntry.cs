using System;
using UnityEngine;

[Serializable]
/// <summary>
/// 脚步表面配置条目。
/// 用于把具体的地面（通过 Collider2D 或 Tag）映射到一组脚步音和其随机化参数。
/// 在 SFXManager 的脚步检测中使用该配置来选择合适的音效。
/// </summary>
public class FootstepSurfaceEntry
{
    /// <summary>此表面的标识（仅用于在 Inspector 中识别）。</summary>
    public string id = "Surface";

    [Tooltip("推荐拖 TilemapCollider2D（或其他地面Collider）进来，匹配最稳定。")]
    /// <summary>优先使用的 Collider2D，用于精确匹配地面。</summary>
    public Collider2D surfaceCollider;

    [Tooltip("可选：如果不填 collider，可通过 Tag 匹配。")]
    /// <summary>备用匹配方式：当未指定 collider 时，可以通过物体的 Tag 匹配表面。</summary>
    public string requiredTag;

    /// <summary>该表面的可用 AudioClip 列表，会随机选择其中一个播放。</summary>
    public AudioClip[] clips;

    /// <summary>音量随机范围（Inspector 可视化）。</summary>
    [Range(0f, 1f)] public float minVolume = 0.9f;
    [Range(0f, 1f)] public float maxVolume = 1f;

    /// <summary>音高随机范围（Inspector 可视化）。</summary>
    [Range(-3f, 3f)] public float minPitch = 0.95f;
    [Range(-3f, 3f)] public float maxPitch = 1.05f;

    /// <summary>
    /// 判断传入的碰撞体是否与本条目匹配。
    /// 优先比较 <see cref="surfaceCollider"/> 是否相同；若未指定 collider，则使用 <see cref="requiredTag"/> 进行 Tag 匹配。
    /// </summary>
    public bool Match(Collider2D hit)
    {
        if (hit == null) return false;
        if (surfaceCollider != null && hit == surfaceCollider) return true;
        return !string.IsNullOrEmpty(requiredTag) && hit.CompareTag(requiredTag);
    }

    /// <summary>从 clips 中随机返回一个 AudioClip，未配置时返回 null。</summary>
    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    /// <summary>在 minVolume/maxVolume 范围内返回一个随机音量值。</summary>
    public float GetRandomVolume() =>
        UnityEngine.Random.Range(Mathf.Min(minVolume, maxVolume), Mathf.Max(minVolume, maxVolume));

    /// <summary>在 minPitch/maxPitch 范围内返回一个随机音高值。</summary>
    public float GetRandomPitch() =>
        UnityEngine.Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
}
