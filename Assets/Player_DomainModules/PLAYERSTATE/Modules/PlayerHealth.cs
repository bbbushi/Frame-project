using System;
using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 血量模块 — 管理血量、受伤、治疗、死亡。
    /// 数据 + 逻辑 + 事件全在一个类中，修改血量只需看这个文件。
    /// </summary>
    [Serializable]
    public class PlayerHealth : PlayerModule
    {
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float currentHP;

        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public float Ratio => maxHP > 0f ? currentHP / maxHP : 0f;
        public bool IsDead => currentHP <= 0f;

        public event Action<float, float> OnChanged;   // (current, max)
        public event Action OnDied;
        public event Action OnRevived;

        /// <summary>从 ScriptableObject 加载初始值</summary>
        public void LoadConfig(PlayerCharacterData cfg)
        {
            if (cfg == null) return;
            maxHP = cfg.maxHealth;
            currentHP = maxHP;
        }

        /// <summary>受到伤害</summary>
        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            currentHP = Mathf.Max(0f, currentHP - damage);
            OnChanged?.Invoke(currentHP, maxHP);
            if (currentHP <= 0f) OnDied?.Invoke();
        }

        /// <summary>治疗</summary>
        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            OnChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>直接设置血量（用于复活、检查点恢复）</summary>
        public void SetHP(float value)
        {
            currentHP = Mathf.Clamp(value, 0f, maxHP);
            OnChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>复活</summary>
        public void Revive()
        {
            currentHP = maxHP;
            OnChanged?.Invoke(currentHP, maxHP);
            OnRevived?.Invoke();
        }
    }
}
