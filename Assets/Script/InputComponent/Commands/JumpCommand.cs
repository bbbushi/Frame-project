using PlayerSystem;
namespace Commands
{
    public class JumpCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.ModuleControlComponent.Locomotion.CanJump;
            
        }

        public void Execute(Player player)
        {
            
            player.ModuleControlComponent.Locomotion.ApplyJump();
        }
    }
}
