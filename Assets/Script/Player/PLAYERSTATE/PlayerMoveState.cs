using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 移动状态 — 地面水平移动，无输入时回待机。
    /// </summary>
    public class PlayerMoveState : PlayerGroundState
    {
        public PlayerMoveState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Update()
        {
            var stateBeforeBase = stateMachine.CurrentState;
            base.Update();

            // base.Update() 可能已切换状态（如 GroundState→AirState），
            // 此时不应再在本帧内做子类状态转换，避免覆盖 base 的转换
            if (stateMachine.CurrentState != stateBeforeBase) return;

            if (Xinput == 0f)
                stateMachine.ChangeState(player.AnimatorComponent.IdleState);
        }
    }
}
