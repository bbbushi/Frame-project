using ActComponents;
using Components;
using Config;
using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 敌人 AI 宿主：持有感知与状态机（组合，不继承），按 config 装配积木，
    /// 实现 IEnemyBrain 供积木回调。由 Enemy.cs 接线驱动（Init/RefreshFixedUpdate/ResetAI）。
    /// </summary>
    public class EnemyAIComponent : EntityComponent, IEnemyBrain
    {
        const string DefaultConfigPath = "data/ai/MeleeBrute";   // 没配 config 时的兜底怪

        [SerializeField] private EnemyAIConfig config;

        EnemyStateMachine machine;
        EnemyVision vision;

        public EnemyStateId CurrentId => machine.CurrentId;

        // ── IEnemyBrain：转发（宿主是积木与各子系统之间的唯一通道）──
        // 基类 EntityComponent.Owner 是 protected，这里以 public 重新发布（new 显式声明遮蔽）
        public new Entity Owner => base.Owner;

        public EnemyAIConfig Cfg
        {
            get
            {
                if (config == null) config = Resources.Load<EnemyAIConfig>(DefaultConfigPath);
                return config;
            }
        }
        public Entity Target => vision.Target;
        public Vector3 LastKnownPosition => vision.LastKnownPosition;
        public bool HasLineOfSightNow => vision.HasLineOfSightNow;
        public Vector3 HomeAnchor { get; private set; }

        public bool TryEnter(EnemyStateId id) => machine.ChangeState(id);
        public void TryEnterRoot() => machine.ChangeState(Cfg.rootState);
        public void ForgetTarget() => vision.Forget();

        // 动画参数名跟 Skeleton Sword.controller（敌人 override 控制器的基底）参数表对齐：
        // "Is Move"(bool) / "attack"(trigger) / "Dead"(trigger)。换表现方案只改这两个方法，积木不动。
        // ⚠ 控制器 Entry 转换按序评估：attack→Attack、idle→Idle、Is Move→Walk，第 4 条【无条件→Dead 兜底】——
        // idle 与 Is Move 同时为 false 的进层瞬间（生成首评/池化取出）会直接播倒地动画。
        // 旧 Enemy.Wait() 靠 SetBool("idle", true) 喂这条线；现统一在此收口：Is Move 的反面同步喂 idle
        public void SetAnimBool(string name, bool value)
        {
            if (Owner.anim == null) return;
            Owner.anim.SetBool(name, value);
            if (name == "Is Move") Owner.anim.SetBool("idle", !value);
        }
        public void SetAnimTrigger(string name) { if (Owner.anim != null) Owner.anim.SetTrigger(name); }

        public override void Init()
        {
            HomeAnchor = Owner.transform.position;
            vision = new EnemyVision(Owner, Cfg);
            machine = new EnemyStateMachine();

            // ── 装配：全项目唯一依赖具体积木类的位置（组合模式的合法落点）──
            machine.Register(EnemyStateId.Idle,   new IdleState(this));
            machine.Register(EnemyStateId.Patrol, new PatrolState(this));
            machine.Register(EnemyStateId.Chase,  new ChaseState(this));
            machine.Register(EnemyStateId.Attack, Cfg.attack.type == AttackType.Melee
                ? (IEnemyState)new MeleeAttackState(this)
                : new RangedAttackState(this));
            machine.Register(EnemyStateId.Death,  new DeathState(this));
            machine.MarkTerminal(EnemyStateId.Death);

            machine.Transitioned += (from, to) =>
            {
                if (Cfg.debugLog) Debug.Log($"[AI]{Owner.name}: {from} → {to}");
            };
            machine.Initialize(Cfg.rootState);
        }

        public override void RefreshFixedUpdate()   // 由 Enemy.FixedUpdate 接线驱动
        {
            // 受击硬直门：受击时 DamageComponent 会 AddIgnore(All)——硬直期间 AI 决策整体暂停，
            // 击退位移由 Locomotion 自己跑，AI 不抢速度。不建独立「受击状态」的原因就在这
            if (Owner.actionIgnoreComponent != null && Owner.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.All))
                return;

            float dt = FixedFrameInterval;   // 帧间隔，不是 TimeScale（毒瞬间结算案教训）
            vision.Tick(dt);                 // 感知先跑：积木本帧读到的是最新视线
            machine.Tick(dt);
        }

        /// <summary>死亡入口：Enemy.OnEnable 订阅 OnDied 时接的这根线。Death 是终态，进了就出不来</summary>
        public void OnOwnerDied() => machine.ChangeState(EnemyStateId.Death);

        /// <summary>池化取出时重置：Death 终态转不出，直接 Initialize 回根；重锚 HomeAnchor、清目标记忆</summary>
        public void ResetAI()
        {
            vision.Reset();
            HomeAnchor = Owner.transform.position;
            machine.Initialize(Cfg.rootState);
        }

#if UNITY_EDITOR
        /// <summary>Gizmo 三件套：视野盒（红=锁目标/青=扫视）、攻击距离线（黄）、leash 圈（灰）</summary>
        void OnDrawGizmosSelected()
        {
            var cfg = Cfg;
            if (cfg == null) return;

            float facing = Application.isPlaying && Owner != null
                ? Owner.locomotionComponent.FacingDirection : 1f;
            Vector3 center = transform.position + new Vector3(facing * cfg.visionRange * 0.5f, 0.5f, 0f);
            Gizmos.color = vision != null && vision.Target != null ? Color.red : Color.cyan;
            Gizmos.DrawWireCube(center, new Vector3(cfg.visionRange, cfg.visionHeight, 0f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.right * cfg.attack.range);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, cfg.chase.leashRange);
        }
#endif
    }
}
