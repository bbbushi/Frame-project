using System;
using System.Collections;
using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 突刺模块 — 短距离突刺位移 + 伤害。
    /// 整合了原 Dash（冲刺）和 Attack（攻击）的核心逻辑。
    /// </summary>
    [Serializable]
    public class PlayerThrust : PlayerModule
    {
        [SerializeField] private float thrustForce = 10f;
        [SerializeField] private float thrustDuration = 0.15f;
        [SerializeField] private float thrustCooldown = 0.5f;
        [SerializeField] private float thrustDamage = 15f;

        public bool IsThrusting { get; private set; }
        public float CooldownTimer { get; private set; }
        public float ThrustDamage => thrustDamage;
        public float ThrustDuration => thrustDuration;
        public bool CanThrust => CooldownTimer <= 0f && !IsThrusting && !player.Locomotion.IsTouchingWall && !player.Combat.IsBusy;

        /// <summary>加载突刺配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            thrustForce = cfg.thrustForce;
            thrustDuration = cfg.thrustDuration;
            thrustCooldown = cfg.thrustCooldown;
            thrustDamage = cfg.thrustDamage;
        }

        /// <summary>启动突刺</summary>
        public void StartThrust()
        {
            IsThrusting = true;
            CooldownTimer = thrustCooldown;

            float dir = player.Locomotion.FacingDirection;
            player.rb.velocity = new Vector2(dir * thrustForce, 0f);
            Debug.Log($"Thrust started. Direction: {dir}, Velocity: {player.rb.velocity}");
            player.StartCoroutine(ThrustRoutine());

            // TODO: 伤害检测 — 对前方敌人造成 thrustDamage 伤害
            // float dist = 1.5f;
            // var hit = Physics2D.OverlapCircle(
            //     (Vector2)player.transform.position + new Vector2(dir * dist, 0f),
            //     0.6f, player.enemyLayer);
            // if (hit != null && hit.TryGetComponent<IDamageable>(out var target))
            //     target.TakeDamage(thrustDamage);
        }

        private IEnumerator ThrustRoutine()
        {
            float originalGravity = player.rb.gravityScale;
            player.rb.gravityScale = 0f;

            yield return new WaitForSeconds(thrustDuration);

            player.rb.gravityScale = originalGravity;
            IsThrusting = false;
        }

        /// <summary>每帧更新冷却计时器</summary>
        public void UpdateCooldown(float deltaTime)
        {
            if (CooldownTimer > 0f)
                CooldownTimer -= deltaTime;
            if (CooldownTimer < 0f)
                CooldownTimer = 0f;
        }
    }
}
