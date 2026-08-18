using Frame_Player;
using UnityEngine;
namespace State_Player
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
            Debug.Log("PlayerIdleState Enter");
            player.locomotionComponent.ZeroVelocity();
        }

        public override void Update()
        {
            var stateBeforeBase = stateMachine.CurrentState;
            base.Update();

            // base.Update() 可能已切换状态（如 GroundState→AirState），
            // 此时不应再在本帧内做子类状态转换，避免覆盖 base 的转换
            if (stateMachine.CurrentState != stateBeforeBase) return;

            if (Xinput != 0f && !player.ModuleControlComponent.Combat.IsBusy)
                stateMachine.ChangeState(player.AnimatorComponent.MoveState);
        }
    }
}
