using UnityEngine;
using System;
using Config;
using Attributes;
namespace Components
{
    public class HealthManageComponent : EntityComponent
    {
        // public event Action<float, float> OnChanged;   // (current, max)
        public event Action OnDied;
        public event Action OnRevived;
        AttributeSet _set;
        public AttributeSet Set => _set;

        
        public override void Init()
        {
            base.Init();
            _set = new AttributeSet(Owner);
            // 从配置灌值（旧 LoadConfig 并入）。min：MaxHealth≥1 防除零
            var cfg = Owner.characterData;
            float maxHP = cfg != null ? cfg.maxHealth : 100f;   // 无配置兜底=旧字段默认值
            _set.Define(AttributeType.MaxHealth, maxHP, min: 1f);
            _set.Define(AttributeType.Health, maxHP);
            _set.Define(AttributeType.AttackPower, cfg != null ? cfg.attackDamage : 1f);
            // OnDied 推导：从结构化事件里识别「血量跌破 0」这一次性语义
            _set.Changed += e =>
            {
                if(e.type == AttributeType.Health && e.oldValue > 0f && e.newValue <= 0f)
                    OnDied?.Invoke();
            };
        }

        // ── 新写路径（带来源——这是本期改造的_point_）──
        public void ApplyDamage(float amount, Entity source)
            => _set.Modify(AttributeType.Health, -amount, new ChangeContext(ChangeReason.Damage, source));
        public void ApplyHeal(float amount, Entity source)
            => _set.Modify(AttributeType.Health, amount, new ChangeContext(ChangeReason.Heal, source));

        // ── 保留的读路径/语义糖（转发 Set；_set 为 null = Init 前访问，返回安全默认）──
        public void Revive()
        {
            _set?.Revive(new ChangeContext(ChangeReason.Revive));
            OnRevived?.Invoke();
        }
        public float CurrentHP   => _set?.GetCurrentValue(AttributeType.Health) ?? 0f;
        public float MaxHP       => _set?.GetCurrentValue(AttributeType.MaxHealth) ?? 0f;
        public float Ratio       => MaxHP > 0f ? CurrentHP / MaxHP : 0f;
        public float AttackPower => _set?.GetCurrentValue(AttributeType.AttackPower) ?? 0f;
        public bool  IsDead      => _set != null && _set.IsDead;
        
    }
}