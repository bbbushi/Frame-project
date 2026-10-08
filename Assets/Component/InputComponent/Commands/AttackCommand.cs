using PlayerSystem;
using State_Player;
using ActComponents;

namespace Commands
{
    /// <summary>
    /// 普通攻击命令 — 鼠标左键触发，仅地面且动作层空闲时可用。
    /// </summary>
    public class AttackCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.AnimatorComponent.ActionMachine.CurrentId == ActionStateId.None
                && player.locomotionComponent.IsGrounded
                && !player.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Action);
        }

        public void Execute(Player player)
        {
            player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.Attack);
        }
    }
}
