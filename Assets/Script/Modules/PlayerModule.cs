using System;
using PlayerSystem;
using Modules;
namespace Modules
{
    /// <summary>
    /// 玩家模块基类 — 提供对宿主 Player 的反向引用。
    /// 所有领域模块（Health、Locomotion、Combat、Thrust）继承此类。
    /// </summary>
    [Serializable]
    public abstract class PlayerModule: EntityModule
    {
        /// <summary>宿主 Player 引用，由 Player.Awake() 通过 Bind() 注入</summary>
        protected Player Players => (Player) base.Owner;
        
    }
}
