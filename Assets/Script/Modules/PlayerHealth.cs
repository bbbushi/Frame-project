using System;
using UnityEngine;
using Config;
namespace Modules
{
    /// <summary>
    /// 血量模块 — 管理血量、受伤、治疗、死亡。
    /// 数据 + 逻辑 + 事件全在一个类中，修改血量只需看这个文件。
    /// </summary>
    [Serializable]
    public class PlayerHealth : PlayerModule
    {

        /// <summary>从 ScriptableObject 加载初始值</summary>
        public void LoadConfig(PlayerCharacterData cfg)
        {
            
        }

       
    }
}
