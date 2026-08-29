using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers{

/// <summary>
/// 场景过渡管理器（MOMS 风格：实现 IManager）。
/// 负责黑屏淡入淡出、场景加载过渡、传送过渡。
/// </summary>
public class FadeManager : IManager
{
    public string Name => "FadeManager";

    private static readonly List<Type> _dependencies = new() {  };
    public List<Type> Dependencies => _dependencies;

    // 配置
    public GameObject fadeCanvasPrefab;

    // 内部根 GameObject
    private GameObject _root;
    private CanvasGroup _fadeCanvasGroup;
    
    public string fromScene;
    private Transform _fromTransform;

    // ====== IManager 生命周期 ======

    public IEnumerator Initialize()
    {
        _root = new GameObject("FadeManagerRoot");
        UnityEngine.Object.DontDestroyOnLoad(_root);

        if (fadeCanvasPrefab != null)
        {
            var canvas = UnityEngine.Object.Instantiate(fadeCanvasPrefab, _root.transform);
            _fadeCanvasGroup = canvas.GetComponent<CanvasGroup>();
            if (_fadeCanvasGroup == null)
                _fadeCanvasGroup = canvas.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 0f;
        }

        yield break;
    }

    public void Deinitialize()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
        _fadeCanvasGroup = null;
    }

    // ====== 场景过渡 ======

    public void LoadSceneWithFade(string sceneName, float delayBeforeFade = 0.2f, float fadeDuration = 0.5f)
    {
        fromScene = SceneManager.GetActiveScene().name;
        GameManager.Instance.StartCoroutine(TransitionRoutine(sceneName, delayBeforeFade, fadeDuration));
    }

    public IEnumerator FadeIn(float duration = 0.5f)
    {
        if (_fadeCanvasGroup == null) yield break;
        float time = 0;
        while (time < duration)
        {
            _fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        _fadeCanvasGroup.alpha = 1;
    }

    public IEnumerator FadeOut(float duration = 0.5f)
    {
        if (_fadeCanvasGroup == null) yield break;
        float time = 0;
        while (time < duration)
        {
            _fadeCanvasGroup.alpha = Mathf.Lerp(1, 0, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        _fadeCanvasGroup.alpha = 0;
    }

    private IEnumerator TransitionRoutine(string sceneName, float delayBeforeFade, float fadeDuration)
    {
        yield return GameManager.Instance.StartCoroutine(FadeIn(fadeDuration));
        SceneManager.LoadScene(sceneName);
        yield return GameManager.Instance.StartCoroutine(FadeOut(fadeDuration));
    }

    // ====== 传送 ======

    // public void Tele(Transform target, float delayBeforeFade = 0.2f, float fadeDuration = 0.5f)
    // {
    //     GameManager.Instance.StartCoroutine(Teleporter(target, delayBeforeFade, fadeDuration));
    // }

    // private IEnumerator Teleporter(Transform target, float delayBeforeFade, float fadeDuration)
    // {
    //     yield return GameManager.Instance.StartCoroutine(FadeIn(fadeDuration));
    //     GameManager.Get<PlayerManager>().SetPlayerPosition(target.position);
    //     yield return GameManager.Instance.StartCoroutine(FadeOut(fadeDuration));
    // }

    public void GetLastLocation(Transform transform)
    {
        _fromTransform = transform;
    }
}
}