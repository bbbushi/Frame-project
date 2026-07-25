using UnityEngine;

namespace Frame.Player
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
            player.Combat.ExecuteCombo();
        }

        public override void Exit()
        {
            base.Exit();
            player.StartCoroutine(player.BusyFor(0.2f));
        }

        public override void Update()
        {
            base.Update();
            player.Locomotion.ZeroVelocity();

            if (AnimEndTrigger)
                stateMachine.ChangeState(player.IdleState);
        }
    }
}
