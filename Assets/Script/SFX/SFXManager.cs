using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Config;
using Random = UnityEngine.Random;

namespace Managers
{

/// <summary>
/// 全局短音效管理器（MOMS 风格：实现 IManager 和 IUpdatable）。
/// 维护一个可复用的 AudioSource 池，通过 id 查找并播放 SFX，支持脚步音选择。
/// 配置通过 SFXManagerConfig ScriptableObject 加载（Assets/Resources/SFXManagerConfig.asset）。
/// </summary>
public class SFXManager : IManager, IUpdatable
{
    public string Name => "SFXManager";

    private static readonly List<Type> _dependencies = new();
    public List<Type> Dependencies => _dependencies;

    // 配置（从 Resources 加载）
    private SFXManagerConfig _config;

    // 内部根 GameObject，挂载 AudioSource 子对象
    private GameObject _root;

    // 池中空闲的 AudioSource
    private readonly Queue<AudioSource> _availableSources = new();

    // 正在播放的 AudioSource（由 OnUpdate 统一回收，不再使用协程）
    private readonly List<ActiveSource> _activeSources = new();

    // 运行时 id → SFXBankEntry 查找表
    private readonly Dictionary<string, SFXBankEntry> _sfxLookup = new(StringComparer.Ordinal);

    /// <summary>追踪正在播放的 AudioSource 及其预计回收时间。</summary>
    private class ActiveSource
    {
        public AudioSource source;
        public float returnTime; // Time.unscaledTime + clip.length / pitch
    }

    // ====== IManager 生命周期 ======

    public IEnumerator Initialize()
    {
        _config = Resources.Load<SFXManagerConfig>("data/SFXManagerConfig");
        if (_config == null)
        {
            Debug.LogError("[SFXManager] 未找到 SFXManagerConfig 资产！请在 Assets/Resources/ 中创建。");
            yield break;
        }

        _root = new GameObject("SFXManagerRoot");
        if (_config.dontDestroyOnLoad)
            UnityEngine.Object.DontDestroyOnLoad(_root);

        RebuildLookup();
        WarmPool();
        yield break;
    }

    public void Deinitialize()
    {
        foreach (var active in _activeSources)
        {
            if (active.source != null)
            {
                active.source.Stop();
                active.source.clip = null;
            }
        }
        _activeSources.Clear();

        while (_availableSources.Count > 0)
        {
            var src = _availableSources.Dequeue();
            if (src != null)
                UnityEngine.Object.Destroy(src.gameObject);
        }

        _sfxLookup.Clear();

        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
        _config = null;
    }

    // ====== IUpdatable ======

    public void OnUpdate(float deltaTime)
    {
        float now = Time.unscaledTime;
        for (int i = _activeSources.Count - 1; i >= 0; i--)
        {
            if (now >= _activeSources[i].returnTime)
            {
                ReleaseSource(_activeSources[i].source);
                _activeSources.RemoveAt(i);
            }
        }
    }

    // ====== 初始化辅助 ======

    private void RebuildLookup()
    {
        _sfxLookup.Clear();
        if (_config?.sfxBank == null) return;

        foreach (var entry in _config.sfxBank)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                continue;

            if (_sfxLookup.ContainsKey(entry.id) && _config.verboseLog)
                Debug.LogWarning($"[SFXManager] 重复SFX id: {entry.id}，后面的会覆盖前面的。");

            _sfxLookup[entry.id] = entry;
        }
    }

    private void WarmPool()
    {
        int size = _config?.initialPoolSize ?? 8;
        for (int i = 0; i < size; i++)
        {
            _availableSources.Enqueue(CreateAudioSource());
        }
    }

    private AudioSource CreateAudioSource()
    {
        var go = new GameObject($"SFX_Source_{_availableSources.Count + _activeSources.Count}");
        go.transform.SetParent(_root.transform, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = _config?.spatialBlend ?? 0f;
        return source;
    }

    // ====== 池管理 ======

    private AudioSource AcquireSource()
    {
        while (_availableSources.Count > 0)
        {
            var cached = _availableSources.Dequeue();
            if (cached != null)
                return cached;
        }

        int maxSize = _config?.maxPoolSize ?? 24;
        int total = _availableSources.Count + _activeSources.Count;
        if (total < maxSize)
            return CreateAudioSource();

        if (_config != null && _config.verboseLog)
            Debug.LogWarning("[SFXManager] AudioSource 池已满，丢弃本次SFX。");
        return null;
    }

    private void ReleaseSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.clip = null;
        source.volume = 1f;
        source.pitch = 1f;
        _availableSources.Enqueue(source);
    }

    // ====== 公共 API ======

    /// <summary>在根位置播放指定 id 的音效。</summary>
    public void PlaySFX(string id)
    {
        PlaySFX(id, _root != null ? _root.transform.position : Vector3.zero, 1f);
    }

    /// <summary>在指定世界位置播放 id 对应的随机音效。</summary>
    public void PlaySFX(string id, Vector3 worldPosition, float volumeScale = 1f)
    {
        if (!_sfxLookup.TryGetValue(id, out var entry) || entry == null)
        {
            if (_config != null && _config.verboseLog)
                Debug.LogWarning($"[SFXManager] 未找到SFX id: {id}");
            return;
        }

        var clip = entry.GetRandomClip();
        if (clip == null)
        {
            if (_config != null && _config.verboseLog)
                Debug.LogWarning($"[SFXManager] SFX id {id} 未配置AudioClip。");
            return;
        }

        float volume = Mathf.Clamp01(entry.GetRandomVolume() * Mathf.Max(0f, volumeScale));
        float pitch = Mathf.Clamp(entry.GetRandomPitch(), -3f, 3f);
        PlayClipInternal(clip, worldPosition, volume, pitch);
    }

    /// <summary>直接播放指定 AudioClip（不通过 id）。</summary>
    public void PlayClip(AudioClip clip, Vector3 worldPosition, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        PlayClipInternal(clip, worldPosition, Mathf.Clamp01(volume), Mathf.Clamp(pitch, -3f, 3f));
    }

    /// <summary>在给定位置向下 Raycast，根据地面 Collider 选择脚步音效。</summary>
    public void PlayFootstep(Vector2 origin)
    {
        if (_config == null) return;

        int mask = _config.footstepGroundMask.value == 0
            ? Physics2D.DefaultRaycastLayers
            : _config.footstepGroundMask.value;
        var hit = Physics2D.Raycast(origin, Vector2.down, _config.footstepRayDistance, mask);

        if (_config.drawFootstepRay)
        {
            Color c = hit.collider != null ? Color.green : Color.yellow;
            Debug.DrawRay(origin, Vector2.down * _config.footstepRayDistance, c, 0.2f);
        }

        if (TryPlayFootstepBySurface(hit.collider, origin))
            return;

        if (_config.defaultFootstepClips == null || _config.defaultFootstepClips.Length == 0)
            return;

        var defaultClip = _config.defaultFootstepClips[Random.Range(0, _config.defaultFootstepClips.Length)];
        float volume = Random.Range(
            Mathf.Min(_config.defaultFootstepMinVolume, _config.defaultFootstepMaxVolume),
            Mathf.Max(_config.defaultFootstepMinVolume, _config.defaultFootstepMaxVolume));
        float pitch = Random.Range(
            Mathf.Min(_config.defaultFootstepMinPitch, _config.defaultFootstepMaxPitch),
            Mathf.Max(_config.defaultFootstepMinPitch, _config.defaultFootstepMaxPitch));
        PlayClipInternal(defaultClip, origin, volume, pitch);
    }

    // ====== 内部实现 ======

    private bool TryPlayFootstepBySurface(Collider2D hitCollider, Vector2 origin)
    {
        if (hitCollider == null || _config?.footstepSurfaces == null) return false;

        foreach (var surface in _config.footstepSurfaces)
        {
            if (surface == null || !surface.Match(hitCollider))
                continue;

            var clip = surface.GetRandomClip();
            if (clip == null) return false;

            PlayClipInternal(clip, origin, surface.GetRandomVolume(), surface.GetRandomPitch());
            return true;
        }
        return false;
    }

    /// <summary>获取 AudioSource、设置属性并播放。播放完毕后由 OnUpdate 自动回收。</summary>
    private void PlayClipInternal(AudioClip clip, Vector3 worldPosition, float volume, float pitch)
    {
        if (clip == null) return;

        var source = AcquireSource();
        if (source == null) return;

        source.transform.position = worldPosition;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = false;
        source.Play();

        _activeSources.Add(new ActiveSource
        {
            source = source,
            returnTime = Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch))
        });
    }
}
}