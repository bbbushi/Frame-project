using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 跳跃状态 — 施加垂直速度，下落时切换到空中状态。
    /// </summary>
    public class PlayerJumpState : PlayerAirState
    {
        public PlayerJumpState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
        }

        public override void Update()
        {
            base.Update();

            if (player.rb.velocity.y <= 0.01f)
                stateMachine.ChangeState(player.AirState);
        }
    }
}
