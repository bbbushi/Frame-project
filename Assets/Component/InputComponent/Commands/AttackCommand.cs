using PlayerSystem;
using State_Player;
using ActComponents;

namespace Commands
{
    /// <summary>
    /// 普通攻击命令 — 鼠标左键触发，仅地面且动作层空闲时可用。
    /// 子弹时间期间左键重定向为标记（点空无副作用），退出自动恢复攻击。
    /// </summary>
    public class AttackCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            var bt = player.ModuleControlComponent?.BulletTime;
            if (bt != null && bt.IsActive) return true;   // 子弹时间：左键 = 标记
            return player.AnimatorComponent.ActionMachine.CurrentId == ActionStateId.None
                && player.locomotionComponent.IsGrounded
                && !player.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Action);
        }

        public void Execute(Player player)
        {
            var bt = player.ModuleControlComponent.BulletTime;
            if (bt != null && bt.IsActive)
            {
                bt.TryMarkAtMouse();   // 子弹时间：重定向为标记
                return;
            }
            player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.Attack);
        }
    }
}
