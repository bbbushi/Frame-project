using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Config;
using enemy;
using ActComponents;
namespace Modules
{
    /// <summary>
    /// 子弹时间模块 — 瞄准、标记敌人、链式突刺执行。
    /// 数据与逻辑完全内聚，不依赖输入层。
    /// </summary>
    [Serializable]
    public class PlayerBulletTime : PlayerModule
    {
        // ═══════════════════════════════════════════════════
        //  Inspector 参数
        // ═══════════════════════════════════════════════════
        [SerializeField] private float bulletTimeScale = 0.2f;
        [SerializeField] private float aimArcAngle = 120f;
        [SerializeField] private int maxMarks = 3;
        [SerializeField] private float markRange = 10f;
        [SerializeField] private float chainDelay = 0.15f;
        [SerializeField] private LayerMask enemyLayer;
        public LayerMask EnemyLayer => enemyLayer;

        // ═══════════════════════════════════════════════════
        //  运行时状态
        // ═══════════════════════════════════════════════════
        public bool IsActive { get; private set; }
        public float AimAngle { get; set; } // 当前瞄准角度（基于玩家朝向的偏移，范围 [-arcAngle/2, +arcAngle/2]）
        public List<Transform> MarkedTargets { get; } = new();
        public int CurrentChainIndex { get; private set; } = -1; // -1 = 未在执行
        public bool IsExecuting => CurrentChainIndex >= 0;

        // 公开参数（只读）
        public float AimArcAngle => aimArcAngle;
        public int MarkCount => MarkedTargets.Count;
        public bool CanMarkMore => MarkCount < maxMarks && IsActive && !IsExecuting;

        // ═══════════════════════════════════════════════════
        //  配置加载
        // ═══════════════════════════════════════════════════
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            bulletTimeScale = cfg.bulletTimeScale;
            aimArcAngle = cfg.aimArcAngle;
            maxMarks = cfg.maxMarkTargets;
            markRange = cfg.markRange;
            chainDelay = cfg.chainDelay;
        }

        // ═══════════════════════════════════════════════════
        //  进入 / 退出
        // ═══════════════════════════════════════════════════
        public void EnterBulletTime()
        {
            IsActive = true;
            ClearAllMarks();
            CurrentChainIndex = -1;
            AimAngle = 0f;
            TimeManager.SetSlowScaleSmooth(bulletTimeScale, 0.15f);
            CreateAimArc();
        }

        public void CancelBulletTime()
        {
            if (!IsActive) return;
            ClearAllMarks();
            DestroyAimArc();
            ExitBulletTimeInternal();
        }

        /// <summary>由 ExecutionState.Enter 调用，开始链式处决</summary>
        public void BeginExecution()
        {
            DestroyAimArc();
            IsActive = false;
            TimeManager.SetSlowScaleSmooth(1f, 0.3f);
            Players.StartCoroutine(ExecuteChain());
        }

        private void ExitBulletTimeInternal()
        {
            IsActive = false;
            DestroyAimArc();
            TimeManager.SetSlowScaleSmooth(1f, 0.3f);
        }

        private LineRenderer aimArcRenderer;

        private void CreateAimArc()
        {
            var arcObj = new GameObject("AimArc");
            arcObj.transform.SetParent(Players.transform);
            arcObj.transform.localPosition = Vector3.zero;
            arcObj.transform.localRotation = Quaternion.identity;

            aimArcRenderer = arcObj.AddComponent<LineRenderer>();
            aimArcRenderer.useWorldSpace = false;
            aimArcRenderer.loop = false;
            aimArcRenderer.startWidth = 0.05f;
            aimArcRenderer.endWidth = 0.05f;
            aimArcRenderer.material = new Material(Shader.Find("Sprites/Default"));
            aimArcRenderer.startColor = new Color(1f, 1f, 0f, 0.3f);
            aimArcRenderer.endColor = new Color(1f, 1f, 0f, 0.3f);

            DrawArc();
        }

        private void DestroyAimArc()
        {
            if (aimArcRenderer != null)
            {
                UnityEngine.Object.Destroy(aimArcRenderer.gameObject);
                aimArcRenderer = null;
            }
        }

        private void DrawArc()
        {
            if (aimArcRenderer == null) return;

            int segments = 30;
            float halfArc = aimArcAngle * 0.5f * Mathf.Deg2Rad;
            float startAngle = -halfArc;
            float angleStep = (halfArc * 2f) / segments;

            aimArcRenderer.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + angleStep * i;
                float x = Mathf.Cos(angle) * markRange;
                float y = Mathf.Sin(angle) * markRange;
                aimArcRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        // ═══════════════════════════════════════════════════
        //  标记系统
        // ═══════════════════════════════════════════════════

        /// <summary>检查目标是否可标记：在扇形视野内且未被遮挡</summary>
        public bool CanMark(Transform enemy)
        {
            if (enemy == null) return false;
            if (!CanMarkMore) return false;
            if (MarkedTargets.Contains(enemy)) return false;

            Vector2 playerPos = Players.transform.position;
            Vector2 toEnemy = (Vector2)enemy.position - playerPos;

            // 距离检测
            if (toEnemy.magnitude > markRange) return false;

            // 扇形角度检测：相对于玩家朝向的世界方向
            float playerFacingAngle = Players.locomotionComponent.FacingDirection > 0 ? 0f : 180f;
            float worldAim = playerFacingAngle + AimAngle;
            Vector2 aimWorldDir = new Vector2(Mathf.Cos(worldAim * Mathf.Deg2Rad), Mathf.Sin(worldAim * Mathf.Deg2Rad));
            float angleToEnemy = Vector2.Angle(aimWorldDir, toEnemy.normalized);

            if (angleToEnemy > aimArcAngle * 0.5f) return false;

            // 遮挡检测：忽略玩家自身碰撞体
            var lineHits = Physics2D.RaycastAll(playerPos, toEnemy.normalized, toEnemy.magnitude);
            foreach (var h in lineHits)
            {
                if (h.collider.transform == Players.transform) continue;
                if (h.collider.transform != enemy)
                    return false;
            }

            return true;
        }

        /// <summary>尝试标记敌人，成功返回 true</summary>
        public bool TryMark(Transform enemy)
        {
            if (!CanMark(enemy)) return false;
            MarkedTargets.Add(enemy);
            enemy.GetComponent<Enemy>()?.ShowMark(MarkCount);
            return true;
        }

        private void ClearAllMarks()
        {
            foreach (var t in MarkedTargets)
            {
                if (t != null)
                    t.GetComponent<Enemy>()?.ClearMark();
            }
            MarkedTargets.Clear();
        }

        // ═══════════════════════════════════════════════════
        //  链式突刺执行（瞬移 + 贯穿）
        // ═══════════════════════════════════════════════════
        private IEnumerator ExecuteChain()
        {
            CurrentChainIndex = 0;
            float approachDistance = 2f;   // 瞬移到目标前方的距离

            while (CurrentChainIndex < MarkedTargets.Count)
            {
                var target = MarkedTargets[CurrentChainIndex];
                if (target == null)
                {
                    CurrentChainIndex++;
                    continue;
                }

                // 水平方向：目标在玩家左侧还是右侧
                Vector2 targetPos = target.position;
                Vector2 dir = targetPos.x > Players.transform.position.x ? Vector2.right : Vector2.left;

                // 瞬移到目标前方（同高度，水平偏移）
                Players.transform.position = new Vector2(targetPos.x - dir.x * approachDistance, targetPos.y);

                // 朝向目标
                if (dir.x * Players.locomotionComponent.FacingDirection < 0f)
                    Players.locomotionComponent.Flip();

                // 水平贯穿突刺：直接复用突刺核心（不切状态、不占冷却），
                // Execution 状态由状态机全程持有，突刺动画直接 CrossFade 到 Dash 维持视觉
                Players.ModuleControlComponent.Thrust.ThrustCore();
                if (Players.anim != null)
                    Players.anim.CrossFade("Dash", Players.AnimatorComponent.CrossFadeDuration);

                // 伤害
                target.GetComponent<Enemy>()?.Hit(new Damage(Players.ModuleControlComponent.Thrust.ThrustDamage, Players, DamageType.Melee ,ImpactType.Light));

                // 突刺期间每隔 interval 生成残影
                float elapsed = 0f;
                float shadowInterval = 0.03f;
                while (elapsed < Players.ModuleControlComponent.Thrust.thrustForce.length)
                {
                    ShadowPool.Instance?.GetShadow(Players.transform);
                    elapsed += shadowInterval;
                    yield return new WaitForSeconds(shadowInterval);
                }
                // 姿态交还不在此处理：链结束 IsExecuting=false → ExecutionState 回 None，
                // None.Enter 按移动层当前状态 CrossFade 回 GroundMove/Jumping
                // 链间延迟
                yield return new WaitForSeconds(chainDelay);

                CurrentChainIndex++;
            }

            ClearAllMarks();
            CurrentChainIndex = -1;
        }
    }
}
