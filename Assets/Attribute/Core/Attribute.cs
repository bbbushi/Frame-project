using UnityEngine;
namespace Attributes
{
    /// <summary>实体属性标识。起步三个，新属性加枚举值即可（二期 Effect 直接引用）。</summary>
    public enum AttributeType
    {
        Health,
        MaxHealth,
        AttackPower,
        Defense
        // 其他属性类型...
    }
    /// <summary>
    /// 单属性。Base=永久值（升级/装备），Current=运行值（伤害/治疗/临时 buff）。
    /// GAS 语义预埋：Instant Effect 改 Current；Duration Effect 改 Current、到期回 Base（二期实现）；
    /// 只有"学习/装备"类永久变化才改 Base。
    /// </summary>
    public class Attribute
    {
        public float baseValue;
        public float currentValue;
        public float MinValue;  // Health=0；MaxHealth=1（防除零）；AttackPower=0
        /// <summary>
        /// Clamp 到最小值。二期 Effect 也走这条，保证 Current 不低于 Min。
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public float Clamp(float v) => Mathf.Max(MinValue, v);
    }
} 