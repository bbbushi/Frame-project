namespace Frame.Player
{
    /// <summary>
    /// 待机状态 — 静止站立，有输入→移动，有敌人→警戒。
    /// </summary>
    public class PlayerIdleState : PlayerGroundState
    {
        public PlayerIdleState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            player.Locomotion.Stop();
        }

        public override void Update()
        {
            base.Update();

            if (Xinput != 0f && !player.Combat.IsBusy)
                stateMachine.ChangeState(player.MoveState);
        }
    }
}
