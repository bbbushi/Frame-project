namespace Frame.Player
{
    public class JumpCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.Locomotion.RemainingJumps > 0 && !player.Combat.IsBusy;
            
        }

        public void Execute(Player player)
        {
            player.StateMachine.ChangeState(player.JumpState);
            player.Locomotion.ApplyJump();
        }
    }
}
