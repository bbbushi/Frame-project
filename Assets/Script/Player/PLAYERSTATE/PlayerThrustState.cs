using UnityEngine;
using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 突刺状态 — 短距离水平位移 + 伤害。
    /// 逻辑完全委托给 PlayerThrust 模块。
    /// </summary>
    public class PlayerThrustState : PlayerState
    {
        public PlayerThrustState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();

            // 委托给突刺模块执行具体逻辑
            player.ModuleControlComponent.Thrust.StartThrust();

            // TODO: 伤害检测 — 待补充敌人伤害接口后实现
            // float dir = player.Locomotion.FacingDirection;
            // float dist = 1.5f;
            // var hit = Physics2D.OverlapCircle(
            //     (Vector2)player.transform.position + new Vector2(dir * dist, 0f),
            //     0.6f, player.enemyLayer);
            // if (hit != null && hit.TryGetComponent<IDamageable>(out var target))
            //     target.TakeDamage(player.Thrust.ThrustDamage);
        }

        public override void Exit()
        {
            base.Exit();
        }

        public override void Update()
        {
            base.Update();

            // 模块的突刺协程结束后，IsThrusting 变为 false → 状态切换
            if (!player.ModuleControlComponent.Thrust.IsThrusting)
            {
                if (!player.locomotionComponent.IsGrounded)
                    stateMachine.ChangeState(player.AnimatorComponent.AirState);
                else
                    stateMachine.ChangeState(player.AnimatorComponent.IdleState);
                return;
            }

            // 突刺中碰到墙
            if (!player.locomotionComponent.IsGrounded && player.locomotionComponent.IsTouchingWall)
            {
                // 暂时不做墙壁反弹，正常退出
            }
        }
    }
}
