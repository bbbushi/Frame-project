using Frame_Player;
using Modules_Player;
using UnityEngine;
using Config;
namespace Components
{
    public class PlayerModuleControlComponent : PlayerComponent
    {
        [SerializeField] public PlayerCombat Combat = new();
        [SerializeField] public PlayerThrust Thrust = new();
        [SerializeField] public PlayerBulletTime BulletTime = new();
        [SerializeField] public PlayerHealth Health = new();
        [SerializeField] public PlayerLocomotion Locomotion = new();
        public override void Init()
        {
            base.Init();
            Bind();
            LoadConfig(Owner.characterData as PlayerCharacterData, Owner.controllerData as PlayerControllerData);     
        }
        public void Bind()
        {
            Combat.Bind(Owner);
            Thrust.Bind(Owner);
            BulletTime.Bind(Owner);
            Health.Bind(Owner);
            Locomotion.Bind(Owner);
        }
        public void LoadConfig(PlayerCharacterData characterData, PlayerControllerData controllerData)
        {
            Health.LoadConfig(characterData);
            Locomotion.LoadConfig(controllerData);
            Combat.LoadConfig(characterData, controllerData);
            Thrust.LoadConfig(controllerData);
            BulletTime.LoadConfig(controllerData);
        }
        public override void RefreshUpdate()
        {
            Thrust.UpdateCooldown(Time.deltaTime);
        }
    }
}