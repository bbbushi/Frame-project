using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 玩家宿主 — 组装所有领域模块并协调状态机。
    /// 每个模块自包含其数据和逻辑，Player 只负责组装和生命周期的调度。
    /// </summary>
    public class Player : MonoBehaviour
    {
        public static Player instance { get; private set; }

        // ═══════════════════════════════════════════════════
        //  Unity 组件（模块和状态可直接访问）
        // ═══════════════════════════════════════════════════
        [HideInInspector] public Rigidbody2D rb;
        [HideInInspector] public Animator anim;
        [HideInInspector] public Collider2D col;
        [HideInInspector] public SpriteRenderer spriteRenderer;

        // ═══════════════════════════════════════════════════
        //  领域模块（Inspector 中可折叠查看和调整参数）
        // ═══════════════════════════════════════════════════
        [SerializeField] public PlayerHealth Health = new();
        [SerializeField] public PlayerLocomotion Locomotion = new();
        [SerializeField] public PlayerCombat Combat = new();
        [SerializeField] public PlayerThrust Thrust = new();

        // ═══════════════════════════════════════════════════
        //  状态机
        // ═══════════════════════════════════════════════════
        public PlayerStateMachine StateMachine { get; private set; }

        // ═══════════════════════════════════════════════════
        //  状态实例
        // ═══════════════════════════════════════════════════
        public PlayerIdleState IdleState { get; private set; }
        public PlayerMoveState MoveState { get; private set; }
        public PlayerJumpState JumpState { get; private set; }
        public PlayerAirState AirState { get; private set; }
        public PlayerThrustState ThrustState { get; private set; }
        public PlayerAttackState AttackState { get; private set; }
        public PlayerDeathState DeathState { get; private set; }

        // ═══════════════════════════════════════════════════
        //  Inspector 配置
        // ═══════════════════════════════════════════════════
        [SerializeField] private PlayerCharacterData characterData;
        [SerializeField] private PlayerControllerData controllerData;

        [Header("敌人检测")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Vector2 alertBoxCenter;
        [SerializeField] private Vector2 alertBoxSize = Vector2.one;

        // ═══════════════════════════════════════════════════
        //  初始化
        // ═══════════════════════════════════════════════════
        protected void Awake()
        {
            instance = this;

            // 缓存 Unity 组件
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            // 绑定模块到宿主并加载配置
            foreach (var mod in new PlayerModule[] { Health, Locomotion, Combat, Thrust })
                mod.Bind(this);

            Health.LoadConfig(characterData);
            Locomotion.LoadConfig(controllerData);
            Combat.LoadConfig(characterData, controllerData);
            Thrust.LoadConfig(controllerData);

            // 创建状态机
            StateMachine = new PlayerStateMachine();

            // 创建所有状态（状态只需一个 player 引用）
            IdleState = new PlayerIdleState(this, StateMachine, "Idle");
            MoveState = new PlayerMoveState(this, StateMachine, "Move");
            JumpState = new PlayerJumpState(this, StateMachine, "Jump");
            AirState = new PlayerAirState(this, StateMachine, "Jump");
            ThrustState = new PlayerThrustState(this, StateMachine, "Thrust");
            AttackState = new PlayerAttackState(this, StateMachine, "Attack");
            DeathState = new PlayerDeathState(this, StateMachine, "Death");

            // 模块间事件联动（低耦合，通过事件连接 Health → StateMachine）
            Health.OnDied += () => StateMachine.ChangeState(DeathState);
        }

        protected void Start()
        {
            StateMachine.Initialize(IdleState);
        }

        protected void Update()
        {
            // 先刷新物理检测（状态执行前需要最新的地面/墙壁信息）
            Locomotion.UpdatePhysics();
            Thrust.UpdateCooldown(Time.deltaTime);

            StateMachine.CurrentState?.Update();
            Locomotion.DrawGizmos(this);
        }

        // ═══════════════════════════════════════════════════
        //  工具方法
        // ═══════════════════════════════════════════════════
        /// <summary>检测附近是否有敌人</summary>
        public bool IsEnemyNearby()
        {
            var hits = Physics2D.OverlapBoxAll(
                (Vector2)transform.position + alertBoxCenter,
                alertBoxSize, 0f, enemyLayer);
            return hits.Length > 0;
        }

        /// <summary>攻击动画事件回调</summary>
        public void TriggerAttack() => StateMachine.CurrentState?.Trigger();

        /// <summary>忙碌协程（攻击硬直等）</summary>
        public System.Collections.IEnumerator BusyFor(float seconds)
        {
            Combat.IsBusy = true;
            yield return new WaitForSeconds(seconds);
            Combat.IsBusy = false;
        }

        // ═══════════════════════════════════════════════════
        //  编辑器可视化
        // ═══════════════════════════════════════════════════
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Locomotion == null) return;

            Vector2 pos = transform.position;

            // 地面检测区域
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawWireCube(pos + Locomotion.groundCheckOffset, Locomotion.groundCheckSize);

            // 墙壁检测区域
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            var wallOffset = Vector2.right * Locomotion.FacingDirection * 0.5f;
            Gizmos.DrawWireCube(pos + wallOffset, Locomotion.wallCheckSize);
        }
#endif
    }
}
