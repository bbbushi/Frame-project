namespace Frame.Player
{
    /// <summary>
    /// 玩家状态机 — 管理状态切换。
    /// 轻量级 FSM，不直接依赖任何模块。
    /// </summary>
    public class PlayerStateMachine
    {
        public PlayerState CurrentState { get; private set; }

        public void Initialize(PlayerState startState)
        {
            CurrentState = startState;
            CurrentState.Enter();
        }

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == newState) return;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
    }
}
