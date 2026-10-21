using PlayerSystem;
using State_Player;

namespace Commands
{
    /// <summary>处决命令 — E 键触发：对全部已标记敌人发起链式冲刺处决（仅子弹时间中有标记时可用）。</summary>
    public class ExecutionCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            var bt = player?.ModuleControlComponent?.BulletTime;
            return bt != null && bt.IsActive && bt.MarkCount > 0 && !bt.IsExecuting;
        }

        public void Execute(Player player)
        {
            player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.Execution);
        }
    }
}
