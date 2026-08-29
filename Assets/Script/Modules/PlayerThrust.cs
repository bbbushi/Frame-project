using System;
using System.Collections;
using UnityEngine;
using Config;
using ActComponents;
using PlayerSystem;
namespace Modules
{
    /// <summary>
    /// 突刺模块 — 短距离突刺位移 + 伤害。
    /// 整合了原 Dash（冲刺）和 Attack（攻击）的核心逻辑。
    /// </summary>
    [Serializable]
    public class PlayerThrust : PlayerModule
    {
        [HideInInspector]public Displacement thrustForce;
        [SerializeField] private float thrustCooldown = 0.5f;
        [SerializeField] private float thrustDamage = 15f;

        public bool IsThrusting { get; private set; }
        public float ThrustDamage => thrustDamage;
        // 冷却通过 AddIgnore(Dash, thrustCooldown) 实现：冷却期内 CanThrust 为 false
        public bool CanThrust => !Owner.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Dash);

        /// <summary>加载突刺配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            thrustForce = cfg.thrustForce;
            thrustCooldown = cfg.thrustCooldown;
            thrustDamage = cfg.thrustDamage;
        }


        /// <summary>
        /// 命令入口：冷却检查 → 请求动作层进入 Thrust 状态 → 占用冷却。
        /// 切状态成功才占冷却，避免守卫拒绝时白白吞掉一次突刺。
        /// </summary>
        public void StartThrust()
        {
            if (!CanThrust) return;
            if (Players.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.Thrust))
                Players.actionIgnoreComponent.AddIgnore(thrustCooldown, ActionIgnoreTag.Dash);
        }

        /// <summary>
        /// 突刺核心：ForceMove 位移 + 重力归零协程。不切状态、不占冷却 —— 处决链（ExecuteChain）直接复用。
        /// </summary>
        public void ThrustCore()
        {
            if (IsThrusting) return;
            IsThrusting = true;
            Players.locomotionComponent.ForceMove(thrustForce);
            Players.StartCoroutine(ThrustRoutine(thrustForce));
        }

        private IEnumerator ThrustRoutine(Displacement thurstForce)
        {
            float originalGravity = Players.rb.gravityScale;
            Players.rb.gravityScale = 0f;

            yield return new WaitForSeconds(thurstForce.length);

            Players.rb.gravityScale = originalGravity;
            IsThrusting = false;
        }
    }
}
