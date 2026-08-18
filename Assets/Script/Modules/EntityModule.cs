using System;
namespace Modules
{
    /// <summary>
    /// 实体模块基类 — 提供对宿主 Entity 的反向引用。
    /// 所有领域模块（Health、Locomotion、Combat、Thrust）继承此类。
    /// </summary>
    [Serializable]
    public abstract class EntityModule
    {
        /// <summary>宿主 Entity 引用，由 Entity.Awake() 通过 Bind() 注入</summary>
        Entity entity;
        protected virtual Entity Owner => entity;

        /// <summary>绑定到宿主，由 Entity.Awake() 调用</summary>
        public virtual void Bind(Entity e) => entity = e;
    }
}