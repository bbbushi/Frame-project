using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Config;
using ActComponents;
using enemy;
using Effects;
using PlayerSystem;

namespace Modules
{
    /// <summary>
    /// 子弹时间模块 — 右键开关全局慢动作 + 鼠标向瞄准标记 + E 键处决链 + 锁链视觉。
    /// 锁链：进子弹时间出现瞄准链（头=玩家/链尾敌人，尾=鼠标）；每标一个敌人，
    /// 从当前链头生成一条到该敌人的标记链，链头更新为该敌人。纯视觉，无物理。
    /// 处决链：对标记敌人依次连续冲刺穿越必杀（协程驱动，Dash 姿态 + 残影），蓝本 git 7e29547^。
    /// </summary>
    [Serializable]
    public class PlayerBulletTime : PlayerModule
    {
        [SerializeField] private float bulletTimeScale = 0.2f;
        private const float EnterDuration = 0.15f;
        private const float ExitDuration = 0.3f;
        private const float ExecHitRadius = 0.8f;       // 抵达判定：位移到目标该距离内即出判定
        private const float ExecOvershoot = 0.8f;       // 冲过目标的穿越余量（斩击惯性）
        private const float ExecShadowInterval = 0.03f; // 残影间隔
        private const float ExecKillDamage = 9999f;     // 处决必杀（走正常 Hit 管线保留受击表现）

        [Header("处决")]
        [SerializeField] private float execDashSpeed = 18f;       // 处决冲刺速度（世界单位/秒），独立于普通突刺
        [SerializeField] private Material pierceEffectMaterial;   // 穿刺光针材质（BABEL/FX/Pierce），Inspector 拖入

        [Header("锁链")]
        [SerializeField] private Material chainMaterial;          // 锁链材质（BABEL/FX/Chain），Inspector 拖入
        [SerializeField] private float chainThickness = 0.25f;    // 链条粗细（世界单位）
        [SerializeField] private float chainLinkLength = 0.5f;    // 单节链环长度（世界单位）

        private readonly List<Transform> _marked = new();
        private readonly Dictionary<Transform, (Effects.ChainLink link, Transform from)> _markChains = new();
        private BulletTimeStage _stage;
        private Effects.ChainLink _aimChain;   // 瞄准链：头=玩家或链尾敌人，尾=鼠标
        private Transform _chainHead;          // 当前链头（玩家 或 最后标记的敌人）
        private int _maxMarks = 3;
        private float _chainDelay = 0.15f;
        private int _chainIndex = -1;   // -1 = 未在执行

        public bool IsActive { get; private set; }
        public IReadOnlyList<Transform> MarkedTargets => _marked;
        public bool CanMarkMore => IsActive && _marked.Count < _maxMarks;
        public bool IsExecuting => _chainIndex >= 0;
        public int MarkCount => _marked.Count;

        /// <summary>加载子弹时间配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            bulletTimeScale = cfg.bulletTimeScale;
            _maxMarks = Mathf.Max(1, cfg.maxMarkTargets);
            _chainDelay = Mathf.Max(0f, cfg.chainDelay);
        }

        public void Toggle()
        {
            if (IsActive) CancelBulletTime();
            else EnterBulletTime();
        }

        public void EnterBulletTime()
        {
            IsActive = true;
            _stage = BulletTimeStage.Create(Players, this);
            _chainHead = Players.transform;
            _aimChain = Effects.ChainLink.Spawn(chainMaterial, Players.transform.position,
                Players.transform.position, chainThickness, chainLinkLength);
            TimeManager.SetSlowScaleSmooth(bulletTimeScale, EnterDuration);
        }

        public void CancelBulletTime()
        {
            if (!IsActive) return;
            IsActive = false;
            ClearAllMarks();
            DestroyAimChain();
            if (_stage != null)
                UnityEngine.Object.Destroy(_stage.gameObject);
            _stage = null;
            TimeManager.SetSlowScaleSmooth(1f, ExitDuration);
        }

        // ═══════════════════════════════════════════════════
        //  标记系统（左键重定向入口：AttackCommand 子弹时间期间调用）
        // ═══════════════════════════════════════════════════

        public bool IsMarked(Transform t) => _marked.Contains(t);

        /// <summary>敌人离开扇形范围时清除其标记、编号与锁链（BulletTimeStage 每帧回调）</summary>
        public void Unmark(Transform t)
        {
            if (_marked.Remove(t))
            {
                t?.GetComponentInParent<Enemy>()?.ClearMark();
                if (_markChains.Remove(t, out var c) && c.link != null)
                    UnityEngine.Object.Destroy(c.link.gameObject);
                _chainHead = Players.transform;   // 链头回退到玩家
            }
        }

        /// <summary>清理已死亡/回池的标记目标，释放名额（编号图标由 Enemy.Die 自清）</summary>
        public void PruneDeadMarks()
        {
            for (int i = _marked.Count - 1; i >= 0; i--)
            {
                var t = _marked[i];
                if (t == null || !t.gameObject.activeInHierarchy)
                {
                    Unmark(t);
                }
            }
        }

        /// <summary>鼠标点击处尝试标记：扇形内的可标记敌人上编号；点中盾牌碰撞盒则被格挡</summary>
        public void TryMarkAtMouse()
        {
            if (!CanMarkMore) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            var hits = Physics2D.OverlapPointAll(mouseWorld);
            // 先扫盾：点中盾牌碰撞盒即被格挡（不依赖碰撞器的返回顺序）
            foreach (var hit in hits)
                if (hit.GetComponent<enemy.ShieldCollider>() != null)
                    return;
            // 再尝试标记敌人
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (enemy != null && TryMark(enemy)) return;
            }
        }

        /// <summary>标记条件：可标记、未超上限、未重复、在扇形内、与玩家间无遮挡（盾牌碰撞盒算遮挡）</summary>
        public bool TryMark(Enemy enemy)
        {
            if (enemy == null || !enemy.CanBeMarked || !CanMarkMore) return false;
            if (_marked.Contains(enemy.transform)) return false;
            if (_stage == null || !_stage.InArc(enemy.transform.position)) return false;

            // 遮挡检测（旧蓝本）：玩家到敌人之间不得有地形/墙体；盾牌碰撞盒优先判定
            //（目标是盾兵自己时正面也格挡，目标是别人时盾挡住其身后的敌人）；普通敌人之间不互挡
            Vector2 playerPos = Players.transform.position;
            Vector2 toEnemy = (Vector2)enemy.transform.position - playerPos;
            float dist = toEnemy.magnitude;
            foreach (var hit in Physics2D.RaycastAll(playerPos, toEnemy.normalized, dist))
            {
                if (hit.collider.transform.IsChildOf(Players.transform)) continue;
                if (hit.collider.GetComponent<enemy.ShieldCollider>() != null) return false;   // 盾牌格挡（须在"跳过目标自身"之前）
                if (hit.collider.transform.IsChildOf(enemy.transform)) continue;
                if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
                return false;
            }

            _marked.Add(enemy.transform);
            enemy.ShowMark(_marked.Count);

            // 锁链：从当前链头连到新标记的敌人，链头更新为该敌人
            var link = Effects.ChainLink.Spawn(chainMaterial, _chainHead.position,
                enemy.transform.position, chainThickness, chainLinkLength);
            if (link != null)
                _markChains[enemy.transform] = (link, _chainHead);
            _chainHead = enemy.transform;
            return true;
        }

        private void ClearAllMarks()
        {
            foreach (var t in _marked)
                if (t != null)
                    t.GetComponentInParent<Enemy>()?.ClearMark();
            DestroyMarkChains();
            _marked.Clear();
        }

        private void DestroyMarkChains()
        {
            foreach (var kv in _markChains)
                if (kv.Value.link != null)
                    UnityEngine.Object.Destroy(kv.Value.link.gameObject);
            _markChains.Clear();
            _chainHead = Players.transform;
        }

        private void DestroyAimChain()
        {
            if (_aimChain != null)
                UnityEngine.Object.Destroy(_aimChain.gameObject);
            _aimChain = null;
        }

        /// <summary>每帧由 BulletTimeStage 驱动：瞄准链尾随鼠标，标记链跟随玩家/敌人位置</summary>
        public void RefreshChains(Vector2 mouseWorld)
        {
            if (!IsActive) return;
            _aimChain?.SetEnds(_chainHead.position, mouseWorld);
            foreach (var kv in _markChains)
                if (kv.Value.link != null)
                    kv.Value.link.SetEnds(kv.Value.from.position, kv.Key.position);
        }

        // ═══════════════════════════════════════════════════
        //  处决链（E 键 → ExecutionState.Enter → BeginExecution 协程驱动）
        // ═══════════════════════════════════════════════════

        /// <summary>由 ExecutionState.Enter 调用：退出子弹时间、暗幕/锁链退役，启动链式冲刺</summary>
        public void BeginExecution()
        {
            IsActive = false;
            // 暗幕/扇形/锁链退役：恢复全部高亮对象的原层原序（含玩家），避免链后层泄漏
            if (_stage != null)
            {
                _stage.RestoreAll();
                UnityEngine.Object.Destroy(_stage.gameObject);
                _stage = null;
            }
            DestroyAimChain();
            DestroyMarkChains();
            TimeManager.SetSlowScaleSmooth(1f, ExitDuration);   // 处决在常速下演出
            Players.StartCoroutine(ExecuteChain());
        }

        /// <summary>链式冲刺：对准标记敌人依次突进穿越必杀，全程 Dash 姿态 + 残影拖尾</summary>
        private IEnumerator ExecuteChain()
        {
            float speed = Mathf.Max(execDashSpeed, 1f);

            float originalGravity = Players.rb.gravityScale;
            Players.rb.gravityScale = 0f;
            _chainIndex = 0;
            float shadowTimer = ExecShadowInterval;   // 段首立即出一道残影
            bool interrupted = false;

            for (int i = 0; i < _marked.Count; i++)
            {
                _chainIndex = i;
                var target = _marked[i];
                // 池化防御：目标回池/销毁则跳过；玩家死亡则中断整条链
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                if (Players.healthManageComponent != null && Players.healthManageComponent.IsDead)
                {
                    interrupted = true;
                    break;
                }

                Vector2 targetPos = target.position;
                Vector2 toTarget = targetPos - (Vector2)Players.transform.position;
                float travel = toTarget.magnitude;
                if (travel < ExecHitRadius)
                {
                    Strike(target, toTarget.normalized);
                    yield return new WaitForSeconds(_chainDelay);
                    continue;
                }
                Vector2 dir = toTarget / travel;
                Players.locomotionComponent.SetFacing(dir.x >= 0f ? 1f : -1f);
                // 每段从头重播 Dash：CrossFade 到已在播放的同名状态不会重启，Play(normalizedTime=0) 才会
                if (Players.anim != null) Players.anim.Play("Dash", -1, 0f);

                // 连续冲刺：位移到目标位置后出 HitBox + 穿刺特效（走 Hitbox 系统，保留受击表现），再冲过余量
                float totalDist = travel + ExecOvershoot;
                float travelled = 0f;
                bool struck = false;
                while (travelled < totalDist)
                {
                    float dt = Time.deltaTime;
                    float step = Mathf.Min(speed * dt, totalDist - travelled);
                    Players.transform.position += (Vector3)(dir * step);
                    travelled += step;

                    if (!struck && travelled >= travel)   // 抵达目标位置：出判定与特效
                    {
                        struck = true;
                        Strike(target, dir);
                    }

                    shadowTimer += dt;
                    if (shadowTimer >= ExecShadowInterval)
                    {
                        shadowTimer = 0f;
                        ShadowPool.Instance?.GetShadow(Players.transform);
                    }
                    yield return null;
                }

                yield return new WaitForSeconds(_chainDelay);
            }

            if (interrupted)
                TimeManager.SetSlowScaleSmooth(1f, ExitDuration);
            ClearAllMarks();
            _chainIndex = -1;
            Players.rb.gravityScale = originalGravity;
        }

        /// <summary>
        /// 处决击杀：抵达敌人位置后出玩家攻击 HitBox（必杀伤害、朝冲刺方向）+ 穿刺光针特效；
        /// 无 hitbox prefab 时兜底直接必杀
        /// </summary>
        private void Strike(Transform target, Vector2 dir)
        {
            Vector3 pos = target.position;
            float degree = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var prefab = Players.detection != null ? Players.detection.hitboxPrefab : null;
            if (prefab != null)
            {
                var hitbox = Hitbox.GenerateHitbox(prefab, Players, null, ExecKillDamage, pos, degree);
                if (hitbox != null) hitbox.transform.rotation = Quaternion.Euler(0f, 0f, degree);
            }
            else
            {
                Kill(target);
            }
            Effects.PierceEffect.Spawn(pierceEffectMaterial, pos, dir);
        }

        private void Kill(Transform target)
        {
            target?.GetComponentInParent<Enemy>()?.Hit(new Damage(ExecKillDamage, Players, DamageType.Melee, ImpactType.Light));
        }
    }
}
