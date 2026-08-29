using PlayerSystem;
namespace Commands
{
    /// <summary>
    /// 突刺命令 — 按下 S 键触发短距离突刺（位移 + 伤害）。
    /// 整合了原 Dash 和 Attack 的输入逻辑。
    /// </summary>
    public class ThrustCommand : IPlayerCommand
    {
        public bool CanExecute(Player player)
        {
            if (player == null) return false;
            return player.ModuleControlComponent.Thrust.CanThrust;
        }

        public void Execute(Player player)
        {
            player.ModuleControlComponent.Thrust.StartThrust();
        }
    }
}
