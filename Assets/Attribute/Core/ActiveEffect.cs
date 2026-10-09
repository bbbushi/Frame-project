using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    /// <summary>
    /// 一个正在生效的效果实例。SO 是配置（共享只读），本类持运行时状态——
    /// 同一 SO 可以同时挂在多个实体上互不干扰。
    /// </summary>
    public class ActiveEffect
    {
        public readonly GameplayEffect source;
        public float remaining;                      // Duration 倒计时
        float _periodTimer;                          // 周期累计器

        /// <summary>
        /// 持续型修饰 实际生效 的 delta（clamp 后的落地值），到期按它逆运算回退——
        /// 不回退"配置值"而回退"落地值"，防御下限 clamp 等管线行为不会造成漂移
        /// </summary>
        public readonly List<(AttributeType type, float delta)> applied = new();

        public ActiveEffect(GameplayEffect src) { source = src; remaining = src.duration; }

        /// <summary>
        /// 是否到周期点（到点即重置累计器）。返回 false = 本跳无周期触发
        /// </summary>
        public bool ConsumePeriod(float dt, out float interval)
        {
            interval = source.period;
            if (source.durationType != EffectDurationType.Duration || interval <= 0f) return false;
            _periodTimer += dt;
            if (_periodTimer < interval) return false;
            _periodTimer -= interval;
            return true;
        }
    }
}
