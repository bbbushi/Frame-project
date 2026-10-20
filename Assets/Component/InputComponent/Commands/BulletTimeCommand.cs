using PlayerSystem;

namespace Commands
{
    /// <summary>子弹时间开关 — 右键切换全局慢动作。</summary>
    public class BulletTimeCommand : IPlayerCommand
    {
        public bool CanExecute(Player player) => player != null
            && player.ModuleControlComponent?.BulletTime != null;

        public void Execute(Player player) => player.ModuleControlComponent.BulletTime.Toggle();
    }
}
