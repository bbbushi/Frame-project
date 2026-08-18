
using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 地面状态基类 — 进入时重置跳跃次数，下落时自动切换到空中。
    /// </summary>
    public class PlayerGroundState : PlayerState
    {
        public PlayerGroundState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
            : base(_player, _stateMachine, _animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            player.ModuleControlComponent.Locomotion.ResetJumps();
        }

        public override void Update()
        {
            base.Update();
            if (UnityEngine.Mathf.Abs(player.rb.velocity.y) > 0.01f)
                stateMachine.ChangeState(player.AnimatorComponent.AirState);
        }
    }
}
