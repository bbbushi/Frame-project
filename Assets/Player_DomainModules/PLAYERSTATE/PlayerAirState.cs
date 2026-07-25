namespace Frame.Player
{
    /// <summary>
    /// 空中状态基类 — 空中水平移动、墙壁检测、落地检测。
    /// </summary>
    public class PlayerAirState : PlayerState
    {
        public PlayerAirState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Update()
        {
            base.Update();
            // 检测墙壁
            if (player.Locomotion.IsTouchingWall)
            {
                // stateMachine.ChangeState(player.WallSlideState);  // 待实现
            }

            // 落地 → 待机
            if (player.Locomotion.IsGrounded && UnityEngine.Mathf.Abs(player.rb.velocity.y) < 0.01f)
                stateMachine.ChangeState(player.IdleState);
        }
    }
}
