using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Frame.Player;
namespace Managers
{

// MOMS 风格：非 MonoBehaviour 的 Manager，实现 IManager 和 IUpdatable
public class TimeManager : IManager, IUpdatable
{
    public string Name => "TimeManager";

    private static readonly List<Type> _dependencies = new() {  };
    public List<Type> Dependencies => _dependencies;

    // 私有 resolve，供静态属性内部使用。外部访问请用 GameManager.Get&lt;TimeManager&gt;()
    private static TimeManager Resolve => GameManager.Instance?.GetManager<TimeManager>();

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
        set { var m = Resolve; if (m == null) return; m.slowScale = value; m.ResetGlobalScale(); }
    }

    public static float DebugScale
    {
        get => Resolve != null ? Resolve.debugScale : 1f;
        set { var m = Resolve; if (m == null) return; m.debugScale = value; m.ResetGlobalScale(); }
    }

    public static float GlobalTimeScale => Resolve != null ? Resolve.globalTimeScale : 1f;
    public static float LocalTimeScale{
        get => Resolve != null ? Resolve.localTimeScale : 1f;
        set
        {
            float previousTimeScale = Resolve != null ? Resolve.localTimeScale : 1f;
            if (Resolve != null) Resolve.localTimeScale = value;
            Player.instance.anim.speed = Resolve != null ? Resolve.localTimeScale : 1f; // 同步玩家动画速度
            float scaleRatio = (Resolve != null && previousTimeScale != 0f) ? value / previousTimeScale : 1f;
            // Player.GetPlayer().// 同步玩家速度
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
        // 检查冻结超时（使用真实时间）
        if (frameFreezeScale == 0f && freezeEndRealtime > 0f && Time.realtimeSinceStartup >= freezeEndRealtime)
        {
            // 仅当当前仍为冻结状态时恢复为原始值
            frameFreezeScale = frameFreezeOriginal;
            freezeEndRealtime = 0f;
            ResetGlobalScale();
        }
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
    /// 冻结一段真实时间（不受 Time.timeScale 影响）。
    /// 如果 interrupt 为 true，会覆盖当前冻结；否则延长冻结时间（保留最远结束时间）。
    /// </summary>
    public void FreezeFrameFor(float duration, bool interrupt = true)
    {
        if (duration <= 0f) return;

        if (interrupt || frameFreezeScale != 0f)
        {
            // 记录原始值并立即冻结
            frameFreezeOriginal = frameFreezeScale;
            frameFreezeScale = 0f;
        }

        var newEnd = Time.realtimeSinceStartup + duration;
        freezeEndRealtime = interrupt ? newEnd : Math.Max(freezeEndRealtime, newEnd);
        ResetGlobalScale();
    }

}
}