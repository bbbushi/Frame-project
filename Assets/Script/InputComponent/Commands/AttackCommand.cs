using Frame_Player;
using State_Player;
namespace Commands
{
    /// <summary>
    /// 普通攻击命令 — 鼠标左键触发，仅地面可用。
    /// </summary>
    public class AttackCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.AnimatorComponent.StateMachine.CurrentState is PlayerGroundState;
        }

        public void Execute(Player player)
        {
            player.AnimatorComponent.StateMachine.ChangeState(player.AnimatorComponent.AttackState);
        }
    }
}
