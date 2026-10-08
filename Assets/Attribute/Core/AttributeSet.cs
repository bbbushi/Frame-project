using System;
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    public class AttributeSet
    {
        Entity _owner; 
        readonly Dictionary<AttributeType, Attribute> _attrs = new();
        bool _diedField;    // 死亡标记：只触发一次语义，见 Apply ②
        /// <summary>
        /// 实例事件：本实体任一属性变化（值真变了才发，见 Apply ①）
        /// </summary>
        public event Action<AttributeChangedEventArgs>Changed;
        /// <summary>
        /// 静态总线：全局表现层（飘字等）订阅一次即可监听所有实体
        /// </summary>
        public static event Action<AttributeChangedEventArgs> AnyChanged;
        public AttributeSet(Entity owner) => _owner = owner;
        /// <summary>
        /// 定义属性（Init 时调用）。base 值同时灌入 current。
        /// 走 Define 不发事件——进场满血不该触发任何表现。
        /// </summary>
        public void Define(AttributeType type, float baseValue, float min = 0f)
        {
            _attrs[type] = new Attribute { baseValue = baseValue, currentValue = baseValue, MinValue = min };

        }
        public float GetCurrentValue(AttributeType type) => _attrs[type].currentValue;
        public float GetBaseValue(AttributeType type) => _attrs[type].baseValue;
        // ── 三条修改路径（二期 Effect 也走这三条，不新增第四条）──

        /// <summary>
        /// 增量修改 Current（伤害/治疗/临时效果）。死亡后免疫再伤害（old IsDead 语义）。
        /// ⚠️ 行为变化：旧 Heal 对死尸 return（只能 Revive），新版正增量放行——治疗可拉活
        ///（「药水复活」语义，动作游戏常见；若要禁止，把死亡免疫条件改成 _diedFired 全拒）。
        /// </summary>
        public bool Modify(AttributeType type, float delta, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            if(type == AttributeType.Health && _diedField && delta < 0f) return false;// 死亡免疫
            return Apply(type, attr, attr.currentValue + delta, ctx);
        }
        ///<summary>直接设 Current（复活置满、调试）。</summary>
        public bool SetCurrent(AttributeType type, float value, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            return Apply(type, attr, value, ctx);
        }
        /// <summary>
        /// 设 Base（装备改变上限等）。Current 保留但被新上限 clamp——
        /// 上限降了血跟着掉，符合直觉；上限升了血不自动涨（要涨请 Revive/Heal）。
        /// </summary>
        public bool SetBase(AttributeType type, float value, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            attr.baseValue = attr.Clamp(value);
            bool changed = Apply(type, attr, attr.currentValue, ctx);   // 触发 clamp 检查与事件
            if(type == AttributeType.MaxHealth)   // 上限变了，Health 连带重 clamp（否则血量悬在新上限外）
            {
                var hp = _attrs[AttributeType.Health];
                changed |= Apply(AttributeType.Health, hp, hp.currentValue, ctx);
            }
            return changed;
        }
        //  ── 管线核心：clamp → 判变 → 发事件 → 维护死亡标记 ──
        bool Apply(AttributeType type, Attribute attr, float newCurrent, ChangeContext ctx)
        {
            float v = attr.Clamp(newCurrent);
            if(type == AttributeType.Health)
                v = Mathf.Clamp(v, 0f, GetCurrentValue(AttributeType.MaxHealth));

            // ① 值没变不发事件：满血再治疗无事件 → 天然不飘字
            if(Mathf.Approximately(v, attr.currentValue)) return false;
            float old = attr.currentValue;
            attr.currentValue = v;

            var args = new AttributeChangedEventArgs
            { owner = _owner, type = type, oldValue = old, newValue = v, delta = v - old, context = ctx };
            Changed?.Invoke(args);
            AnyChanged?.Invoke(args);

            // ② 死亡标记：Health 跨过 0 线**双向**维护——
            //    跌破 0：置 true（Modify 从此拒负增量 = 死亡免疫，OnDied 由宿主推导只发一次）
            //    从 0 拉正（SetCurrent/治疗）：置 false——不重置的话「死而复生」后永远打不掉血
            if(type == AttributeType.Health)
            {
                if(old > 0f && v <= 0f) _diedField = true;
                else if(old <= 0f && v > 0f) _diedField = false;
            }
            return true;
        }

        /// <summary>复活语义：Current 回满（到 base）、死亡标记重置。血量变化自然发事件。</summary>
        public bool Revive(ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(AttributeType.Health, out var attr)) return false;
            _diedField = false;
            return Apply(AttributeType.Health, attr, attr.baseValue,
                new ChangeContext(ChangeReason.Revive, ctx.source));
        }
        public bool IsDead => _attrs.TryGetValue(AttributeType.Health, out var a) && a.currentValue <= 0f;  
    }
}