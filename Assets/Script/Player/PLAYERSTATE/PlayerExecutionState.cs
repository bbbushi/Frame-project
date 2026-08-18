using Frame_Player;

namespace State_Player
{
    /// <summary>
    /// 处决状态 — 按编号顺序对标记目标执行链式突刺。
    /// 逻辑委托给 PlayerBulletTime.BeginExecution 协程，完成后自动回到 Idle。
    /// </summary>
    public class PlayerExecutionState : PlayerState
    {
        public PlayerExecutionState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            player.ModuleControlComponent.BulletTime.BeginExecution();
        }

        public override void Update()
        {
            base.Update();

            // 链式突刺完成 → 根据地面状态切换
            if (!player.ModuleControlComponent.BulletTime.IsExecuting)
            {
                if (player.locomotionComponent.IsGrounded)
                    stateMachine.ChangeState(player.AnimatorComponent.IdleState);
                else
                    stateMachine.ChangeState(player.AnimatorComponent.AirState);
            }
        }
    }
}
