using System;
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    /// <summary>
    /// 持续语义：Instant=modifiers 一次性生效完事；Duration=modifiers 持续，到期回退，
    /// 期间每 period 秒 periodModifiers 即时生效一次（period<=0 则不周期触发）。
    /// </summary>
    public enum EffectDurationType
    {
        Instant,
        Duration
    }
    /// <summary>
    /// 修改器：描述一个属性的修改，包括修改的类型和幅度。
    /// </summary>
    [Serializable]
    public struct Modifier
    {
        public AttributeType type;
        public float magnitude;     // 正=增益 负=减损；Additive 语义，二期不做乘/覆盖
    }
    /// <summary>效果配置资产。所有数值修改最终都走 AttributeSet.Modify —— 不存在第四条修改路径。</summary>
    [CreateAssetMenu(menuName = "MOMS/GameplayEffect")]
    public class GameplayEffect : ScriptableObject
    {
        public EffectDurationType durationType = EffectDurationType.Instant;

        [Tooltip("Duration 总时长（秒）。Instant 忽略")]
        public float duration = 5f;

        [Tooltip("周期触发间隔（秒）。<=0 = 不周期触发。Instant 忽略")]
        public float period = 0f;

        [Tooltip("持续型修饰：挂上即生效，Duration 到期按记录 delta 回退。⚠️ 别放 Health（到期回退可能把人'回退死'），持续回血/掉血走 periodModifiers")]
        public List<Modifier> modifiers = new();

        [Tooltip("周期型修饰：每 period 秒即时生效一次，逐跳独立、不回退（中毒/持续回血放这）")]
        public List<Modifier> periodModifiers = new();
    }

}