using System.Collections;
using System.Collections.Generic;
using ActComponents;
using UnityEngine;
namespace Config
{
    [CreateAssetMenu(fileName = "NewPlayerControllerData", menuName = "Game/Player Controller Data")]
    public class PlayerControllerData : EntityControllerConfig
    {
        
        public float jumpforce; // 跳跃强度
        public Displacement attackForce; // 攻击位移力度
        public Displacement thrustForce; // 受击位移力度
        public float thrustDamage;    // 突刺伤害
        public float thrustCooldown;  // 突刺冷却

        [Header("攻击/死亡")]
        public float attackIgnoreDuration = 0.3f;  // 攻击期间动作忽略（All）时长
        public float attackRecovery = 0.2f;        // 攻击收招后的额外硬直
        public float deathAnimationDuration = 1.5f; // 死亡到场景重载的延迟


        [Header("子弹时间")]
        public float bulletTimeScale = 0.2f;         // 子弹时间缩放比例
        public float aimArcAngle = 120f;             // 瞄准扇形角度
        public int maxMarkTargets = 3;               // 标记上限
        public float markRange = 10f;                // 标记检测距离
        public float chainDelay = 0.15f;             // 连杀间隔
    }
}