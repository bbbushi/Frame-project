namespace Frame.Player
{
    /// <summary>
    /// 普通攻击命令 — 鼠标左键触发，仅地面可用。
    /// </summary>
    public class AttackCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.StateMachine.CurrentState is PlayerGroundState;
        }

        public void Execute(Player player)
        {
            player.StateMachine.ChangeState(player.AttackState);
        }
    }
}
