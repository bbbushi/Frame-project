using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraShaker
    : Singleton<CameraShaker>
{
    public static float shakeFactor = 0.8f;


    //相机锁定计时器（懒创建，见 GetLockTimer）
    Timer CameraLockTimer;
    //当前相机的所有抖动效果（内联初始化，消除 Awake→Start 空窗）
    List<ShakeInfo> shakeInfos = new List<ShakeInfo>();

    protected override void Awake()
    {
        base.Awake();
        //原 Start() 逻辑移入。不在 Awake 中 new Timer：重复实例的 Destroy 延迟到帧末，
        //其余 Awake 代码仍会执行，会向静态 TimeManager.Timers 注入永不清理的计时器
        shakeFactor = 1;
    }

    /// <summary>懒创建锁定计时器：避免初始化时序问题，也避免重复实例污染静态 Timers 列表</summary>
    private Timer GetLockTimer()
    {
        if (CameraLockTimer == null)
            CameraLockTimer = new Timer(0, TimerType.normal);
        return CameraLockTimer;
    }


    private void LateUpdate()
    {
        if (shakeInfos == null) return;
        Vector2 offset = new Vector2();
        for (int i = shakeInfos.Count - 1; i >= 0; i--)
        {
            ShakeInfo info = shakeInfos[i];

            float progress = Mathf.Clamp01(info.t / info.time);
            float y = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * info.repeat * progress)), 0.5f) * (1 - progress);
            offset += info.direction * info.magnitude * y;

            info.t += Time.unscaledDeltaTime;
            if(info.t > info.time)
                shakeInfos.RemoveAt(i);
        }
        shakeOffset = offset;
    }

    //相机抖动
    [SerializeField] Vector2 shakeOffset = new Vector2(0, 0);
    public static Vector2 ShakeOffset
    {
        //场景中不止存在一个虚拟相机，但是所有相机的抖动都统一从这里获取
        //此处仅计算偏移量，但是不具体应用到相机上
        get => Instance != null ? Instance.shakeOffset * shakeFactor : Vector2.zero;
    }

    public static void CameraLock(float f)
    {
        var inst = Instance;
        if (inst == null) return;
        if (inst.GetLockTimer() < f)
            inst.GetLockTimer().Set(f);
    }


    public static void ShakeRandom(float magnitude, int repeat, float time)
    {
        //幅度、次数和时间都不能为0
        if (magnitude == 0 || repeat == 0 || time <= 0)
            return;
        float angle = Random.Range(0, 2 * Mathf.PI);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Shake(magnitude, repeat, time, dir);
    }
    public static void Shake(float magnitude, int repeat, float time, Vector2 dir)
    {
        var inst = Instance;
        if (inst == null) return;
        //如果在锁定状态，不允许新加抖动，但是已有的抖动可以继续
        if (inst.GetLockTimer().InTime)
            return;
        //幅度、次数和时间都不能为0
        if (magnitude == 0 || repeat == 0 || time <= 0)
            return;
        //添加新的抖动信息
        ShakeInfo info = new ShakeInfo(magnitude, repeat, time, dir);
        inst.shakeInfos.Add(info);
        Debug.Log($"CameraShaker: Shake added. Magnitude: {magnitude}, Repeat: {repeat}, Time: {time}, Direction: {dir}");
    }
}

public class ShakeInfo
{
    public float magnitude;
    public int repeat;
    public float time;
    public Vector2 direction;
    public float t;

    public ShakeInfo(float magnitude, int repeat, float time, Vector2 dir)
    {
        this.magnitude = magnitude;
        this.repeat = repeat;
        this.time = time;
        this.direction = dir;
        this.t = 0;
    }
}
