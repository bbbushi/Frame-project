using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Managers{

/// <summary>
/// 背景音乐管理器（MOMS 风格：实现 IManager 和 IUpdatable）。
/// 双 AudioSource 交叉淡入淡出，通过 OnUpdate 驱动渐变（无协程分配）。
/// 配置通过 MusicManagerConfig ScriptableObject 加载（Assets/Resources/MusicManagerConfig.asset）。
/// </summary>

public enum MusicType
{
    Normal,
    Start,
    fight
}

public class MusicManager : IManager, IUpdatable
{
    // 配置（从 Resources 加载）
    [SerializeField] private MusicManagerConfig _config;
    public string Name => "MusicManager";

    private static readonly List<Type> _dependencies = new();
    public List<Type> Dependencies => _dependencies;

   

    // 根 GameObject ： 承载两个 AudioSource，防止被场景切换销毁
    private GameObject _root;

    // 待播放和播放中双音频源
    private AudioSource _activeSource;
    private AudioSource _inactiveSource;

    // 当前音乐类型
    private MusicType _currentMusicType = (MusicType)(-1);

    //-------- 淡入淡出状态机（OnUpdate 驱动）
    private bool _fadeActive;
    private AudioSource _fadeNewSource;
    private AudioSource _fadeOldSource;
    private float _fadeDuration;
    private float _fadeElapsed;
    private bool _fadeHasOldSource;
    //---------


    // ====== IManager 生命周期 ======

    //初始化音乐配置资源并且初始化根 GameObject 和两个 AudioSource
    public IEnumerator Initialize()
    {
        _config = Resources.Load<MusicManagerConfig>("data/MusicManagerConfig");
        if (_config == null)
        {
            Debug.LogError("[MusicManager] 未找到 MusicManagerConfig 资产！请在 Assets/Resources/ 中创建。");
            yield break;
        }

        _root = new GameObject("MusicManagerRoot");
        UnityEngine.Object.DontDestroyOnLoad(_root);

        _activeSource = _root.AddComponent<AudioSource>();
        _inactiveSource = _root.AddComponent<AudioSource>();

        ConfigureSource(_activeSource);
        ConfigureSource(_inactiveSource);

        _activeSource.volume = 1f;
        _inactiveSource.volume = 0f;

        yield break;
    }

    //销毁根 GameObject 并停止播放音乐
    public void Deinitialize()
    {
        if (_activeSource != null) { _activeSource.Stop(); _activeSource.clip = null; }
        if (_inactiveSource != null) { _inactiveSource.Stop(); _inactiveSource.clip = null; }
        _fadeActive = false;

        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
        _config = null;
    }

    /// <summary>
    /// 在 GameManager 注入完成后首帧调用，自动播放默认音乐。
    /// 通过一个延迟标记避免 Initialize 还未完全结束就播放。
    /// Update期间会常驻处理淡入淡出状态机，确保音乐平滑过渡。
    /// </summary>
    private bool _started;
    public void OnUpdate(float deltaTime)
    {
        // 延迟启动：首帧播放默认音乐
        if (!_started)
        {
            _started = true;
            PlayNormalMusic();
        }

        // 淡入淡出状态机
        if (_fadeActive)
        {
            _fadeElapsed += deltaTime;
            float t = _fadeElapsed / _fadeDuration;

            if (_fadeNewSource != null)
                _fadeNewSource.volume = Mathf.Lerp(0f, 1f, t);

            if (_fadeHasOldSource && _fadeOldSource != null)
                _fadeOldSource.volume = Mathf.Lerp(1f, 0f, t);

            if (_fadeElapsed >= _fadeDuration)
            {
                // 淡入淡出完成
                if (_fadeNewSource != null) _fadeNewSource.volume = 1f;
                if (_fadeHasOldSource && _fadeOldSource != null)
                {
                    _fadeOldSource.volume = 0f;
                    _fadeOldSource.Stop();
                    _fadeOldSource.clip = null;
                }

                // 交换 active/inactive
                AudioSource temp = _activeSource;
                _activeSource = _fadeNewSource;
                _inactiveSource = _fadeHasOldSource ? _fadeOldSource : temp;

                _fadeActive = false;
            }
        }
    }

    // ====== 初始化音频设置 ======

    private void ConfigureSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    // ====== 公共 API ======

    public void PlayMusic(MusicType type, float fadeDuration = -1f)
    {
        if (fadeDuration < 0) fadeDuration = _config.defaultFadeDuration;

        if (_currentMusicType == type && !_fadeActive)
            return;

        AudioClip targetClip = GetMusicClip(type);
        if (targetClip == null) return;

        _currentMusicType = type;

        // 如之前有淡入淡出正在进行，强行结束旧渐变
        if (_fadeActive && _fadeOldSource != null)
        {
            _fadeOldSource.Stop();
            _fadeOldSource.clip = null;
            _fadeOldSource.volume = 0f;
        }

        StartCrossFade(targetClip, Mathf.Max(0.01f, fadeDuration));
    }

    public void PlayNormalMusic(float fadeDuration = -1f) => PlayMusic(MusicType.Normal, fadeDuration);
    public void PlayLoginMusic(float fadeDuration = -1f) => PlayMusic(MusicType.Start, fadeDuration);
    public void PlayFightMusic(float fadeDuration = -1f) => PlayMusic(MusicType.fight, fadeDuration);

    public void PauseMusic()
    {
        if (_activeSource != null && _activeSource.isPlaying)
            _activeSource.Pause();
    }

    public void ResumeMusic()
    {
        if (_activeSource != null && !_activeSource.isPlaying)
            _activeSource.UnPause();
    }

    public void StopMusic()
    {
        _fadeActive = false;
        if (_activeSource != null) { _activeSource.Stop(); _activeSource.clip = null; _activeSource.volume = 0f; }
        if (_inactiveSource != null) { _inactiveSource.Stop(); _inactiveSource.clip = null; _inactiveSource.volume = 0f; }
        _currentMusicType = (MusicType)(-1);
    }

    public MusicType GetCurrentMusicType() => _currentMusicType;

    // ====== 内部 ======

    private AudioClip GetMusicClip(MusicType type)
    {
        return type switch
        {
            MusicType.Normal => _config.normalMusic,
            MusicType.Start => _config.startMusic,
            MusicType.fight => _config.fightMusic,
            _ => null
        };
    }

    private void StartCrossFade(AudioClip newClip, float duration)
    {
        AudioSource newSource = _inactiveSource;
        AudioSource oldSource = _activeSource;

        newSource.clip = newClip;
        newSource.volume = 0f;
        newSource.Play();

        _fadeHasOldSource = oldSource.isPlaying && oldSource.clip != null;

        _fadeNewSource = newSource;
        _fadeOldSource = _fadeHasOldSource ? oldSource : null;
        _fadeDuration = duration;
        _fadeElapsed = 0f;
        _fadeActive = true;
    }
}
}