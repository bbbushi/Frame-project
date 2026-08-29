using UnityEngine;
using Managers;
using Config;
using Components;
namespace PlayerSystem
{
    /// <summary>
    /// 玩家宿主 — 组装所有领域模块并协调状态机。
    /// 每个模块自包含其数据和逻辑，Player 只负责组装和生命周期的调度。
    /// </summary>
    public class Player : Entity 
    {
        static Player _instance;

        public static Player Instance
        {
            get
            {
                if (_instance == null) _instance = FindObjectOfType<Player>();
                return _instance;
            }
        }

        [HideInInspector]public PlayerAnimatorComponent AnimatorComponent;
        [HideInInspector]public PlayerModuleControlComponent ModuleControlComponent;
        [HideInInspector]public PlayerCameraComponent CameraComponent;

        
        // ═══════════════════════════════════════════════════
        //  Inspector 配置
        // ═══════════════════════════════════════════════════

        [Header("敌人检测")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Vector2 alertBoxCenter;
        [SerializeField] private Vector2 alertBoxSize = Vector2.one;

        /// <summary>强类型玩家控制器配置（Inspector 挂的是 EntityControllerConfig 时为 null）</summary>
        public PlayerControllerData PlayerConfig => controllerData as PlayerControllerData;

        // ═══════════════════════════════════════════════════
        //  初始化
        // ═══════════════════════════════════════════════════
        protected override void Awake()
        {
            // 1. base.Awake() 缓存组件 + 绑定 Health/Locomotion 到 Entity
            base.Awake();
            AnimatorComponent = GetComponentInChildren<PlayerAnimatorComponent>();
            ModuleControlComponent = GetComponentInChildren<PlayerModuleControlComponent>();
            CameraComponent = GetComponentInChildren<PlayerCameraComponent>();
            // 组件缺失时停用整个对象（而非仅 enabled=false——
            // Player.Instance 用 FindObjectOfType，会找到 disabled 但 GO 激活的组件，
            // 导致 PlayerInputController 每帧对 ModuleControlComponent 解引用 NRE）
            if (AnimatorComponent == null || ModuleControlComponent == null)
            {
                Debug.LogError("[Player] 预制体缺少 PlayerAnimatorComponent 或 PlayerModuleControlComponent，已停用该 Player 对象");
                gameObject.SetActive(false);
                return;
            }

            // 2. 绑定 Player 独有模块
            AnimatorComponent.Init();
            ModuleControlComponent.Init();
            CameraComponent.Init();
            // 3.订阅时间缩放事件（必须在 null 检查之后：未订阅则 OnDestroy 的 -= 也是安全空操作）
            TimeManager.OnLocalTimeScaleChanged += OnLocalTimeScaleChanged;
        }

        protected override void Start()
        {
            base.Start();
            AnimatorComponent.InitializeStateMachine();
        }

        protected override void Update()
        {
            base.Update();
            // 冷却更新 + 状态机驱动（物理检测已移至 Entity.FixedUpdate()）
            AnimatorComponent.RefreshUpdate();
            ModuleControlComponent.RefreshUpdate();
            CameraComponent.RefreshUpdate();
            
        }

        protected void OnDestroy()
        {
            TimeManager.OnLocalTimeScaleChanged -= OnLocalTimeScaleChanged;
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

        

        /// <summary>忙碌协程（攻击硬直等）</summary>
        
        /// <summary>响应 TimeManager.LocalTimeScale 变更事件</summary>
        private void OnLocalTimeScaleChanged(float newScale, float ratio)
        {
            anim.speed = newScale;
            locomotionComponent.Velocity *= ratio;
        }
        protected override void SetLocalTimaScale(float previousTimeScale)
        {
            base.SetLocalTimaScale(previousTimeScale);
            if (anim != null) anim.speed = LocalTimeScale;
            float velocityRatio = previousTimeScale != 0f ? LocalTimeScale / previousTimeScale : 1f;
            locomotionComponent.Velocity *= velocityRatio;
        }
        

    }

}