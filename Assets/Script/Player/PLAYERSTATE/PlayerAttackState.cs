using UnityEngine;
using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 攻击状态 — 执行普通攻击，动画事件触发退出。
    /// 攻击逻辑委托给 PlayerCombat 模块。
    /// </summary>
    public class PlayerAttackState : PlayerState
    {
        public PlayerAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            Xinput = 0f;

            // 委托给战斗模块处理攻击逻辑
            player.ModuleControlComponent.Combat.ExecuteCombo();
        }

        public override void Exit()
        {
            base.Exit();
            player.StartCoroutine(player.BusyFor(0.2f));
        }

        public override void Update()
        {
            base.Update();
            player.locomotionComponent.ZeroVelocity();

            if (AnimEndTrigger)
            {
                // 根据是否在地面选择目标状态，避免空中恢复成地面状态导致免费跳跃重置
                if (player.locomotionComponent.IsGrounded)
                    stateMachine.ChangeState(player.AnimatorComponent.IdleState);
                else
                    stateMachine.ChangeState(player.AnimatorComponent.AirState);
            }
        }
    }
}
