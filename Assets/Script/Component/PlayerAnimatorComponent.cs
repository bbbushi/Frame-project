using State_Player;
using Frame_Player;
using Modules_Player;
namespace Components
{
    public class PlayerAnimatorComponent : PlayerComponent
    {
        public PlayerStateMachine StateMachine { get; private set; }

            // ═══════════════════════════════════════════════════
            //  状态实例
            // ═══════════════════════════════════════════════════
        public PlayerIdleState IdleState { get; private set; }
        public PlayerMoveState MoveState { get; private set; }
        public PlayerJumpState JumpState { get; private set; }
        public PlayerAirState AirState { get; private set; }
        public PlayerThrustState ThrustState { get; private set; }
        public PlayerAttackState AttackState { get; private set; }
        public PlayerDeathState DeathState { get; private set; }
        public PlayerExecutionState ExecutionState { get; private set; }
        
        /// <summary>攻击动画事件回调</summary>
        public void AnimEnd() => StateMachine?.CurrentState?.Trigger();
        

        public override void Init() { 
    // 5. 创建状态机
            StateMachine = new PlayerStateMachine();

                // 6. 创建所有状态
            IdleState = new PlayerIdleState(Owner, StateMachine, "Idle");
            MoveState = new PlayerMoveState(Owner, StateMachine, "Move");
            JumpState = new PlayerJumpState(Owner, StateMachine, "Jump");
            AirState = new PlayerAirState(Owner, StateMachine, "Jump");
            ThrustState = new PlayerThrustState(Owner, StateMachine, "Thrust");
            AttackState = new PlayerAttackState(Owner, StateMachine, "Attack");
            DeathState = new PlayerDeathState(Owner, StateMachine, "Death");
            ExecutionState = new PlayerExecutionState(Owner, StateMachine, "Execution");

                // 7. 模块间事件联动
            Owner.healthManageComponent.OnDied += () => StateMachine.ChangeState(DeathState);  
                
        } 
        public void InitializeStateMachine()
        {
            StateMachine.Initialize(IdleState);
        } 
        public override void RefreshUpdate()
        {
            StateMachine?.CurrentState?.Update();
        }
        public void ChangeState(PlayerState newState)
        {
            StateMachine.ChangeState(newState);
        }
        public void AttackTrigger() => Owner.ModuleControlComponent.Combat.HitDetect();
    }    
}
