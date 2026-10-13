using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 画面过渡门面：全项目渐变动画的唯一入口。
    /// 本类是 UI 系统的静态门面，不持有任何状态，只提供简单的渐变接口。
    /// 黑幕本体（FadeVeil）挂在 UIManager 的 Overlay 层随 UIRoot 跨场景，
    /// 本类只做静态原语。消费者（Flow/死亡表现/传送触发器）各自 yield 本协程组合自己的编排，
    /// 本类不做任何业务调度（编排归业务，§三.7 过渡三维度）。
    /// ⚠ 时钟一律 unscaledDeltaTime：过渡期 PauseScale 常为 0（LoadingPanel 是 Exclusive+World），
    /// Time.deltaTime 会永久定格在第一帧——黑屏死锁、零报错。
    /// </summary>
    public static class UIFade
    {
        private const string VeilName = "FadeVeil";
        private static CanvasGroup _veil;   // 懒找缓存；Unity fake-null 时（UIRoot 重建）判空自动重找
        private static int _token;          // 并发护栏：新渐变顶旧渐变（死亡时切场景不至于两层动画打架）

        private static CanvasGroup Veil
        {
            get
            {
                if (_veil == null)
                {
                    // 黑幕永远 active（视觉走 alpha 不走 SetActive），按名 Find 可靠
                    var go = GameObject.Find(VeilName);
                    if (go != null) _veil = go.GetComponent<CanvasGroup>();
                }
                return _veil;
            }
        }

        /// <summary>
        /// 渐变到目标 alpha（0=全透明 1=全黑），返回协程供消费者 yield
        /// </summary>
        public static IEnumerator FadeTo(float alpha, float duration = 0.3f)
        {
            var veil = Veil;
            if (veil == null) yield break;              // 门面判空静默（对齐 UIManager 静态门面惯例）
            if (duration <= 0f) { SetAlpha(veil, alpha); yield break; }

            int token = ++_token;
            float start = veil.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (token != _token) yield break;       // 被新调用顶掉，旧协程立即退场
                SetAlpha(veil, Mathf.Lerp(start, alpha, t / duration));
                yield return null;
            }
            SetAlpha(veil, alpha);
        }

        /// <summary>
        /// 变黑（全屏遮罩）。死亡红屏等色彩扩展位：
        /// 将来加 FadeToColor(Color, ...) 即可，门面签名不破坏现有消费者
        /// </summary>
        public static IEnumerator FadeIn(float duration = 0.3f) => FadeTo(1f, duration);

        /// <summary>变亮（揭幕）</summary>
        public static IEnumerator FadeOut(float duration = 0.3f) => FadeTo(0f, duration);

        private static void SetAlpha(CanvasGroup veil, float a)
        {
            veil.alpha = a;
            veil.blocksRaycasts = a > 0.001f;   // 全黑挡点击（过渡中本就不该点东西），全透明放行——
                                                // 漏了复位 = 黑幕隐形但吃掉全部点击（§八验证清单回归项）
        }
    }
}
