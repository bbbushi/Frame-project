using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
namespace Managers
{

// MOMS 风格：非 MonoBehaviour 的 Manager，实现 IManager 和 IUpdatable
public class TimeManager : IManager, IUpdatable
{
    public string Name => "TimeManager";

    private static readonly List<Type> _dependencies = new() {  };
    public List<Type> Dependencies => _dependencies;

    // 私有 resolve，供静态属性内部使用。外部访问请用 GameManager.Get<TimeManager>()
    private static TimeManager Resolve => GameManager.Instance?.GetManager<TimeManager>();
    static List<Timer> timers;
    public static List<Timer> Timers
    {
        get
        {
            if (timers == null) timers = new List<Timer>();
            return timers;
        }
    }
    public static void ResetTimers()
    {
        timers = new List<Timer>();
    }

    // 内部因子（非序列化字段，因为不是 MonoBehaviour）
    private float globalTimeScale = 1f;
    private float frameFreezeScale = 1f;   // 帧冻结因子（0=冻结）
    private float pauseScale = 1f;         // 暂停因子
    private float slowScale = 1f;          // 慢动作因子（子弹时间）
    private float debugScale = 1f;         // 调试因子

    // 冻结相关（使用真实时间计时，替代协程）
    private float freezeEndRealtime = 0f;
    private float frameFreezeOriginal = 1f;
    private float localTimeScale = 1f; // 本地时间缩放（不受冻结影响）

    // 慢动作平滑过渡
    private float targetSlowScale = 1f;
    private float slowTransitionSpeed = 0f; // 0 = 即时，非0 = 1/过渡时长

    // 事件：本地时间缩放变更时通知订阅者（如 Player）
    /// <summary>(newScale, ratio) — ratio 用于调整速度等依赖项</summary>
    public static event Action<float, float> OnLocalTimeScaleChanged;

    // 公共静态属性，外部通过 TimeManager.FrameFreezeScale = x 操作
    public static float FrameFreezeScale
    {
        get => Resolve != null ? Resolve.frameFreezeScale : 1f;
        set
        {
            var m = Resolve;
            if (m == null) return;
            m.frameFreezeScale = value;
            m.ResetGlobalScale();
        }
    }

    public static float PauseScale
    {
        get => Resolve != null ? Resolve.pauseScale : 1f;
        set { var m = Resolve; if (m == null) return; m.pauseScale = value; m.ResetGlobalScale(); }
    }

    public static float SlowScale
    {
        get => Resolve != null ? Resolve.slowScale : 1f;
        set
        {
            var m = Resolve;
            if (m == null) return;
            m.slowScale = value;
            // 直接设置时取消平滑过渡
            m.targetSlowScale = value;
            m.slowTransitionSpeed = 0f;
            m.ResetGlobalScale();
        }
    }

    public static float DebugScale
    {
        get => Resolve != null ? Resolve.debugScale : 1f;
        set { var m = Resolve; if (m == null) return; m.debugScale = value; m.ResetGlobalScale(); }
    }

    public static float GlobalTimeScale => Resolve != null ? Resolve.globalTimeScale : 1f;

    public static float LocalTimeScale
    {
        get => Resolve != null ? Resolve.localTimeScale : 1f;
        set
        {
            var m = Resolve;
            if (m == null) return;

            float previous = m.localTimeScale;
            m.localTimeScale = value;
            float ratio = (previous != 0f) ? value / previous : 1f;

            OnLocalTimeScaleChanged?.Invoke(value, ratio);
        }
    }

    /// <summary> 真实时间 deltaTime（不受 timeScale 影响），透传 Unity 的 unscaledDeltaTime </summary>
    public static float UnscaledDeltaTime => Time.unscaledDeltaTime;

    /// <summary> 真实时间（不受 timeScale 影响），透传 Unity 的 unscaledTime </summary>
    public static float UnscaledTime => Time.unscaledTime;

    /// <summary> 是否处于帧冻结中（frameFreezeScale 为 0 即为冻结，不要求必须有计时器）</summary>
    public static bool IsFrozen
    {
        get
        {
            var m = Resolve;
            return m != null && m.frameFreezeScale == 0f;
        }
    }

    /// <summary> 帧冻结剩余时间（秒，真实时间）。未冻结时返回 0 </summary>
    public static float FreezeTimeRemaining
    {
        get
        {
            var m = Resolve;
            if (m == null || m.frameFreezeScale != 0f || m.freezeEndRealtime <= 0f)
                return 0f;
            return Mathf.Max(0f, m.freezeEndRealtime - Time.realtimeSinceStartup);
        }
    }

    // 初始化（由 GameManager 调用）
    public IEnumerator Initialize()
    {
        ResetGlobalScale();
        yield break;
    }

    public void Deinitialize()
    {
        // 需要时释放资源
    }

    // MOMS 更新接口：由 GameManager 每帧调用
    public void OnUpdate(float dt)
    {
        // 慢动作平滑过渡（使用 unscaledDeltaTime 保证过渡在冻结/暂停时也能继续）
        if (slowTransitionSpeed > 0f)
        {
            float newSlow = Mathf.MoveTowards(slowScale, targetSlowScale,
                Time.unscaledDeltaTime * slowTransitionSpeed);
            if (Mathf.Approximately(newSlow, targetSlowScale))
            {
                newSlow = targetSlowScale;
                slowTransitionSpeed = 0f;
            }
            slowScale = newSlow;
            ResetGlobalScale();
        }

        // 检查冻结超时（使用真实时间）
        if (frameFreezeScale == 0f && freezeEndRealtime > 0f && Time.realtimeSinceStartup >= freezeEndRealtime)
        {
            // 恢复冻结前的 scale 值，并重置 original 防止残留
            frameFreezeScale = frameFreezeOriginal;
            frameFreezeOriginal = 1f;
            freezeEndRealtime = 0f;
            ResetGlobalScale();
        }

        DriveTimers();
    }

    /// <summary>驱动所有计时器走时（由 OnUpdate 每帧调用）</summary>
    private static void DriveTimers()
    {
        // 倒序遍历：Tick 回调中 new Timer 会 Add 到列表尾部，倒序保证新 Timer 不被本帧误 Tick
        List<Timer> timerList = Timers;
        for (int i = timerList.Count - 1; i >= 0; i--)
        {
            Timer timer = timerList[i];
            if (timer == null || timer.needToDestroy) continue;

            // 按 TimerType 分发对应的时间增量
            float delta = timer.type switch
            {
                TimerType.unscaled     => Time.unscaledDeltaTime,
                TimerType.fixedDelta   => Time.fixedDeltaTime,
                TimerType.fixedUnscale => Time.fixedUnscaledDeltaTime,
                _                      => Time.deltaTime, // TimerType.normal
            };

            // 回调异常不能打断 GameManager.Update 中其余 Manager 的更新
            try { timer.Tick(delta); }
            catch (Exception ex)
            {
                Debug.LogError($"[TimeManager] Timer Tick 回调异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // 统一清理已销毁/失效的计时器（Tick 内 Destroy() 只置标志，不修改集合）
        timerList.RemoveAll(t => t == null || t.needToDestroy);
    }

    private void ResetGlobalScale()
    {
        float scale = 1f;
        scale *= slowScale;
        scale *= pauseScale;
        scale *= frameFreezeScale;
        scale *= debugScale;
        Time.timeScale = scale;
        globalTimeScale = scale;
    }

    /// <summary>
    /// 设置慢动作缩放并平滑过渡（子弹时间缓入/缓出）。
    /// duration 为过渡时长（秒，真实时间），传 0 或负数则立即生效。
    /// </summary>
    public static void SetSlowScaleSmooth(float target, float duration)
    {
        var m = Resolve;
        if (m == null) return;

        m.targetSlowScale = target;
        m.slowTransitionSpeed = (duration > 0f) ? 1f / duration : 0f;

        if (m.slowTransitionSpeed == 0f)
        {
            // 即时生效
            m.slowScale = target;
            m.ResetGlobalScale();
        }
    }

    /// <summary>
    /// 冻结全局时间一段真实时间（hit-stop / 帧冻结效果）。
    /// interrupt=true 时覆盖当前冻结（重新计时）；false 时延长冻结（取最远结束时间）。
    /// </summary>
    public void FreezeTimeFor(float duration, bool interrupt = true)
    {
        if (duration <= 0f) return;

        bool alreadyFrozen = frameFreezeScale == 0f;

        if (!alreadyFrozen)
        {
            // 仅在非冻结状态下记录原始值，避免将 0 记录为恢复目标
            frameFreezeOriginal = frameFreezeScale;
            frameFreezeScale = 0f;
        }
        // 已冻结时：保留 frameFreezeOriginal，只更新时间终点

        var newEnd = Time.realtimeSinceStartup + duration;
        freezeEndRealtime = interrupt ? newEnd : Math.Max(freezeEndRealtime, newEnd);
        ResetGlobalScale();
    }

    [Obsolete("Use FreezeTimeFor instead. FreezeFrameFor will be removed in a future version.")]
    public void FreezeFrameFor(float duration, bool interrupt = true)
        => FreezeTimeFor(duration, interrupt);

}
}
