using System;
using enemy.ai;
using UnityEngine;

namespace Config
{
    public enum AttackType { Melee, Ranged }

    /// <summary>
    /// 敌人行为配置（纯数据）：同一份代码配不同 SO = 不同怪（扩展多种敌人的支点）。
    /// 与 characterData/controllerData 分离——移动物理与行为策略独立演化。
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyAI", menuName = "Project/EnemyAIConfig")]
    public class EnemyAIConfig : ScriptableObject
    {
        [Header("根状态")] public EnemyStateId rootState = EnemyStateId.Patrol;   // Idle=哨兵 / Patrol=巡逻

        [Header("感知")] public float visionRange = 6f;           // 前向视野宽
        public float visionHeight = 3f;
        public float perceptionInterval = 0.15f;                  // 感知轮询间隔

        [Serializable] public class PatrolSettings
        {
            public Vector2 moveTimeRange = new Vector2(3f, 6f);   // 走路时长区间
            public Vector2 waitTimeRange = new Vector2(0.5f, 2f); // 停顿时长区间
            public float speedRatio = 0.5f;                       // × locomotion.moveSpeed
        }
        public PatrolSettings patrol = new PatrolSettings();

        [Serializable] public class ChaseSettings
        {
            public float speedRatio = 1.1f;
            public float memoryTime = 2.5f;                       // 丢失视野后的记忆期
            public float leashRange = 12f;                        // 离家超过即放弃追击
        }
        public ChaseSettings chase = new ChaseSettings();

        [Serializable] public class AttackSettings
        {
            public AttackType type = AttackType.Melee;
            public float range = 1.2f;
            public float windup = 0.35f;                          // 前摇
            public float cooldown = 1.2f;                         // 出招后冷却
            public GameObject bulletPrefab;                       // type=Ranged 时用（HitboxBullet prefab）
        }
        public AttackSettings attack = new AttackSettings();

        [Header("调试")] public bool debugLog = false;
    }
}
