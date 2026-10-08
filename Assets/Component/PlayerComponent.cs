using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using Config;
namespace Components
{
    public class PlayerComponent : EntityComponent
    {
        public new Player Owner => (Player)base.Owner;
    }    
}

