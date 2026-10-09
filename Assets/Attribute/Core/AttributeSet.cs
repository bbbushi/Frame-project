using System;
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    /// <summary>
    /// 属性修改器：
    /// 支持增量（伤害/治疗）和直接设值（复活/调试），
    /// 支持上限 clamp（血量不能超过 MaxHealth），
    /// 支持死亡免疫（死后再受伤无效），支持复活重置死亡标记（死而复生后能再次被打死）。
    /// 支持持续效果（GameplayEffect）挂载、周期生效、到期回退。
    /// 支持事件总线：本实体任一属性变化（值真变了才发）和全局总线（飘字等表现层订阅一次即可监听所有实体）。
    /// </summary>
    public class AttributeSet
    {
        Entity _owner; 
        readonly Dictionary<AttributeType, Attribute> _attrs = new();
        readonly List<ActiveEffect> _effects = new();
        /// <summary>
        /// 当前挂着的持续效果（只读视图：冒烟计数 / 将来 buff UI）。
        /// </summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _effects;
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
        ///（「药水复活」语义，动作游戏常见；若要禁止，把死亡免疫条件改成 _diedField 全拒）。
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
            // Current 跟降不跟升：上限/基值降了 Current 连带落下（Math.Min 才能触发判变），
            // 升了不动（要涨走 Heal/Revive）。原写法 Apply(current) 值没变→零事件→连带 clamp 也失效
            bool changed = Apply(type, attr, Mathf.Min(attr.currentValue, attr.baseValue), ctx);
            if(type == AttributeType.MaxHealth)   // 上限变了，Health 连带重 clamp（否则血量悬在新上限外）
            {
                var hp = _attrs[AttributeType.Health];
                changed |= Apply(AttributeType.Health, hp, hp.currentValue, ctx);
            }
            return changed;
        }
        //  ── 管线核心：clamp → 判变 → 发事件 → 维护死亡标记 ──
        /// <summary>
        /// 应用属性值的修改，并触发相应的事件。
        /// </summary>
        /// <param name="type"></param>
        /// <param name="attr"></param>
        /// <param name="newCurrent"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        bool Apply(AttributeType type, Attribute attr, float newCurrent, ChangeContext ctx)
        {
            float v = attr.Clamp(newCurrent);
            if(type == AttributeType.Health)
                // 下限用属性自身 Min（沙包 immortal 锁 1；普通怪 Min=0，行为不变）
                v = Mathf.Clamp(v, attr.MinValue, GetCurrentValue(AttributeType.MaxHealth));

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

        
        /// <summary>
        /// 复活语义：先清效果回退（带毒进池、复活又毒 3 秒是诡异状态——「干净重生」），
        /// 再 Current 回满（到 base）、死亡标记重置。血量变化自然发事件。
        /// </summary>
        public bool Revive(ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(AttributeType.Health, out var attr)) return false;
            ClearEffects();
            _diedField = false;
            return Apply(AttributeType.Health, attr, attr.baseValue,
                new ChangeContext(ChangeReason.Revive, ctx.source));
        }
        public bool IsDead => _attrs.TryGetValue(AttributeType.Health, out var a) && a.currentValue <= 0f;  
        /// <summary>
        /// 挂效果。Instant：逐 modifier Modify 完即走（不进列表）。
        /// Duration：应用 modifiers 并记录落地 delta，进列表等 Tick 回退。
        /// 目标已死：照挂（Defense buff 挂死人无意义但也无害；回池时 Revive 会清）。
        /// </summary>
        public bool ApplyEffect(GameplayEffect effect, ChangeContext ctx)
        {
            if (effect == null) return false;
            var active = new ActiveEffect(effect);
            foreach (var m in effect.modifiers)
                ApplyModifier(active, m, ctx);
            if (effect.durationType == EffectDurationType.Instant) return true;   // 即时效完即走
            _effects.Add(active);
            return true;
        }
        /// <summary>
        /// 周期/到期推进。由宿主 RefreshFixedUpdate 转发，dt 用 FrameInterval（跟随实体 TimeScale）。
        /// 惯例：先推进计时再处理到期——同帧挂同帧过期等价于没挂（Instant 语义兜底）。
        /// </summary>
        public void TickEffects(float dt)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)      // 倒序：到期移除不打乱遍历
            {
                var e = _effects[i];
                var ctx = new ChangeContext(ChangeReason.Effect, null);
                if (e.ConsumePeriod(dt, out _))                 // 周期点：periodModifiers 逐跳即时生效
                    foreach (var m in e.source.periodModifiers)
                        Modify(m.type, m.magnitude, ctx);       // 不记录/不回退——每跳独立
                e.remaining -= dt;
                if (e.remaining <= 0f)
                {
                    for (int k = e.applied.Count - 1; k >= 0; k--)          // 逆序回退落地 delta
                        Modify(e.applied[k].type, -e.applied[k].delta, ctx);
                    _effects.RemoveAt(i);
                }
            }
        }
        /// <summary>
        /// 清空全部效果并回退（复活的「干净重生」语义）。Revive 里第一行调用。
        /// </summary>
        public void ClearEffects()
        {
            var ctx = new ChangeContext(ChangeReason.Effect, null);
            for (int i = _effects.Count - 1; i >= 0; i--)
                for (int k = _effects[i].applied.Count - 1; k >= 0; k--)
                    Modify(_effects[i].applied[k].type, -_effects[i].applied[k].delta, ctx);
            _effects.Clear();
        }
        /// <summary>
        /// 单个修饰落地：走 Modify 管线（clamp/判变全在），落地成功才记录 delta。
        /// </summary>
        void ApplyModifier(ActiveEffect e, Modifier m, ChangeContext ctx)
        {
            if (!_attrs.TryGetValue(m.type, out var attr)) return;
            float before = attr.currentValue;
            if (!Modify(m.type, m.magnitude, ctx)) return;      // 值没变（clamp 抵消）→ 无事发生不记录
            e.applied.Add((m.type, attr.currentValue - before));
        } 
    }
}