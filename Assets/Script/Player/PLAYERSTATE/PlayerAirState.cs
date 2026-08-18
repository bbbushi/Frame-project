using Frame_Player;
namespace State_Player
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
            if(player.rb.velocity.y > 0.01f)
            {
                stateMachine.ChangeState(player.AnimatorComponent.JumpState);
            }
            // 检测墙壁
            if (player.locomotionComponent.IsTouchingWall)
            {
                // stateMachine.ChangeState(player.WallSlideState);  // 待实现
            }

            // 落地 → 待机（只要在地面且不在上升即可，允许向下速度）
            if (player.locomotionComponent.IsGrounded && player.rb.velocity.y <= 0.01f)
            {
                if(stateTimer <= 0)
                {
                    player.anim.SetTrigger("Landing");
                    stateTimer = 1.5f;
                }
                if (AnimEndTrigger)
                {
                    stateMachine.ChangeState(player.AnimatorComponent.IdleState);
                }                
            }
        }
    }
}
