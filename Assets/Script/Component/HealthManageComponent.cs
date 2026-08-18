using UnityEngine;
using System;
using Config;
namespace Components
{
    public class HealthManageComponent : EntityComponent
    {
        public event Action<float, float> OnChanged;   // (current, max)
        public event Action OnDied;
        public event Action OnRevived;
        [SerializeField] protected float maxHP = 100f;
        [SerializeField] protected float currentHP;

        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public float Ratio => maxHP > 0f ? currentHP / maxHP : 0f;
        public bool IsDead => currentHP <= 0f;
        public override void Init()
        {
            base.Init();
            LoadConfig(Owner.characterData);
        }
        /// <summary>从 ScriptableObject 加载初始值</summary>
        public void LoadConfig(EntityCharacterConfig cfg)
        {
            if (cfg == null) return;
            maxHP = cfg.maxHealth;
            currentHP = maxHP;
        }

        /// <summary>受到伤害</summary>
        public virtual void TakeDamage(float damage)
        {
            if (IsDead) return;
            currentHP = Mathf.Max(0f, currentHP - damage);
            // OnChanged?.Invoke(currentHP, maxHP);
            // if (currentHP <= 0f) OnDied?.Invoke();
            OnChanged?.Invoke(currentHP, maxHP);
            if (currentHP <= 0f) OnDied?.Invoke();
        }

        /// <summary>治疗</summary>
        public virtual void Heal(float amount)
        {
            
            if (IsDead) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            OnChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>直接设置血量（用于复活、检查点恢复）</summary>
        public virtual void SetHP(float value)
        {
            
            currentHP = Mathf.Clamp(value, 0f, maxHP);
            OnChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>复活</summary>
        public virtual void Revive()
        {
            currentHP = maxHP;
            OnChanged?.Invoke(currentHP, maxHP);
            OnRevived?.Invoke();
        }
    }
}