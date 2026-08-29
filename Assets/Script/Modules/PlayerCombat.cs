using System;
using UnityEngine;
using Config;
using ActComponents;
using Components;
namespace Modules
{
    /// <summary>
    /// 战斗模块 — 管理攻击、连击计数、攻击位移。
    /// </summary>
    [Serializable]
    public class PlayerCombat : PlayerModule
    {
        [SerializeField] private float attackDamage;
        [SerializeField] private float comboWindow;
        private Displacement attackForce;
        private GameObject attackHitboxPrefab => Players.detection.hitboxPrefab;
        private Hitbox hitbox;

        public int ComboCount { get; private set; }
        public float LastAttackTime { get; private set; }

        /// <summary>加载战斗配置</summary>
        public void LoadConfig(PlayerCharacterData charCfg, PlayerControllerData ctrlCfg)
        {
            if (charCfg != null)
                attackDamage = charCfg.attackDamage;
            if (ctrlCfg != null)
                attackForce = ctrlCfg.attackForce;
        }
        /// <summary>攻击起手（由 AttackState.Enter 调用）：记录时间 + 攻击前冲。动作忽略检查在命令层完成。</summary>
        public void BeginAttack()
        {
            LastAttackTime = Time.time;
            ComboEffect();
        }
        public void HitDetect()
        {
            hitbox = Hitbox.GenerateHitbox(attackHitboxPrefab, Owner, Owner.detection.GetAttackSocket(), attackDamage, Owner.detection.GetAttackSocket().position);
        }
        private void ComboEffect()
        {
            Players.locomotionComponent.ForceMove(attackForce);
        }
        
    }
}
