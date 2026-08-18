using Frame_Player;
namespace Commands
{
    public class JumpCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.ModuleControlComponent.Locomotion.RemainingJumps > 0 && !player.ModuleControlComponent.Combat.IsBusy;
            
        }

        public void Execute(Player player)
        {
            player.AnimatorComponent.StateMachine.ChangeState(player.AnimatorComponent.JumpState);
            player.ModuleControlComponent.Locomotion.ApplyJump();
        }
    }
}
