namespace Frame.Player
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
            base.Update();


            if (Xinput == 0f)
                stateMachine.ChangeState(player.IdleState);
        }
    }
}
