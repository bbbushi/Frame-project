using System;
using Frame_Player;
namespace State_Player
{
    /// <summary>
    /// 状态切换 — 用于在状态机中切换状态。
    /// </summary>
    public class PlayerTranState : PlayerState
    {
        public PlayerTranState(Player _player, PlayerStateMachine _stateMachine , string _transitionAnimName , PlayerState _nextState)
            : base(_player, _stateMachine, null )
        {
            AnimName = _transitionAnimName;
            NextState = _nextState;
        }
        public PlayerState NextState;
        public String AnimName;
        public override void Enter()
        {
            base.Enter();
            if (player.anim != null && !string.IsNullOrEmpty(AnimName))
                player.anim.Play(AnimName);
        }
        public override void Update()
        {
            if (AnimEndTrigger)
            {
                stateMachine.ChangeState(NextState);
            }
        }
        public override void Exit()
        {
            
        }
        public void SetNextState(PlayerState _nextState)
        {
            NextState = _nextState;
        }
        
        
    }
}