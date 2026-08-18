using Frame_Player;
namespace Commands
{
    /// <summary>
    /// 玩家命令接口 — 命令模式中的 Command 抽象。
    /// 命令直接访问 Player 的领域模块来判断和执行操作。
    /// </summary>
    public interface IPlayerCommand
    {
        bool CanExecute(Player player);
        void Execute(Player player);
    }
}
