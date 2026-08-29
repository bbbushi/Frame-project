using UnityEngine;
using Managers;
using Config;
using Components;
using ActComponents;
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Entity : MonoBehaviour,iDamagable
{
    //是否加入单位统计
    public bool countByManager = true;

    // ═══════════════════════════════════════════════════
    //  Unity 组件（模块和状态可直接访问）
    // ═══════════════════════════════════════════════════
        [HideInInspector] public Rigidbody2D rb;
        [HideInInspector] public Animator anim;
        [HideInInspector] public SpriteRenderer spriteRenderer;
        [HideInInspector] public DetectionComponent detection;
        [HideInInspector] public DamageComponent damageComponent;
        [HideInInspector] public LocomotionComponent locomotionComponent;
        [HideInInspector] public HealthManageComponent healthManageComponent;
        [HideInInspector] public ActionIgnoreComponent actionIgnoreComponent;
        public EntityCharacterConfig characterData;
        public EntityControllerConfig controllerData;

    //————————阵营————————
    [SerializeField] Faction faction;
    public Faction Faction { get => faction; }

    //————————阵营————————

    [SerializeField] float invincibleTimer = -1;
    [SerializeField] float blockTimer = -1;
    public void SetInvincible(float time) => invincibleTimer = Mathf.Max(invincibleTimer, time);
    public bool IsInvincible => invincibleTimer > 0;
    public void SetBlock(float time) => blockTimer = Mathf.Max(blockTimer, time);
    public bool IsBlock => blockTimer > 0;

    /// <summary>清空无敌与格挡计时（对象池回收/复用前重置用）</summary>
    public void ClearBattleTimers()
    {
        invincibleTimer = -1f;
        blockTimer = -1f;
    }

    //————————时间相关————————

    //时间倍率
    [SerializeField] float localTimeScale = 1;
    public float TimeScale => localTimeScale * TimeManager.GlobalTimeScale;
    public float LocalTimeScale
    {
        get => localTimeScale;
        set
        {
            float previousTimeScale = localTimeScale;
            localTimeScale = value;
            SetLocalTimaScale(previousTimeScale);      
        }
    }
    protected virtual void SetLocalTimaScale(float previousTimeScale){}
    
    //帧间隔
    public float FixedFrameInterval => Time.fixedDeltaTime * LocalTimeScale;

    public float FrameInterval => Time.deltaTime * LocalTimeScale;

    //————————时间相关————————


    //————————位置相关————————

    public Vector2Int GridPosition
    {
        get => (transform.position + new Vector3(0, 0.5f, 0)).GetMapGridPos();
    }

    public Vector3 RootPosition
    {
        get => transform.position + new Vector3(0f, 0.3f, 0);
    }
    public virtual Vector2 ChestPosition => transform.position + new Vector3(0, 0.5f, 0);

    public Vector2 HitboxCenter => ChestPosition;

    //————————位置相关————————
           

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        detection = GetComponentInChildren<DetectionComponent>();
        damageComponent = GetComponentInChildren<DamageComponent>();
        locomotionComponent = GetComponentInChildren<LocomotionComponent>();
        healthManageComponent = GetComponentInChildren<HealthManageComponent>();
        actionIgnoreComponent = GetComponentInChildren<ActionIgnoreComponent>();
        
    }

    protected virtual void Start()
    {
        detection.Init();
        damageComponent.Init();
        locomotionComponent.Init();
        healthManageComponent.Init();
        actionIgnoreComponent.Init();
    }

    protected virtual void Update()
    {
        //刷新角色动画

    }
    protected virtual void LateUpdate()
    {
    }

    protected virtual void FixedUpdate()
    {
        //刷新无敌计时器
        invincibleTimer -= FrameInterval;
        blockTimer -= FrameInterval;
        //刷新物理检测
        if (detection != null)
            detection.RefreshFixedUpdate();
        //刷新强制位移（击退等）
        if (locomotionComponent != null)
            locomotionComponent.RefreshFixedUpdate();
        //刷新血液特效
        if (damageComponent != null)
            damageComponent.RefreshFixedUpdate();
        //刷新动作忽略标签
        if (actionIgnoreComponent != null)
            actionIgnoreComponent.RefreshActionIgnore();
    }
    public virtual void Interrupt()
    {
        EntityComponent[] components = GetComponentsInChildren<EntityComponent>();
        foreach (EntityComponent component in components)
        {
            component.Interrupt();
        }
    }
    public virtual void BusyFor(float seconds)
    {
        actionIgnoreComponent.AddIgnore(seconds, ActionIgnoreTag.All);
    }

    public virtual bool HasLineOfSight(Entity target)
    {
        Vector2 direction;
        bool hasLOS = false;
        direction = target.RootPosition - RootPosition;
        hasLOS |= !Physics2D.Raycast(RootPosition + new Vector3(0, 0.1f, 0), direction.normalized, direction.magnitude,
            LayerMaskPreset.SightObstacle);

        return hasLOS;
    }
    public virtual bool HasLineOfSight(Vector2 target)
    {
        Vector2 direction;
        bool hasLOS = false;
        direction = target - ChestPosition;
        hasLOS |= !Physics2D.Raycast(ChestPosition, direction.normalized, direction.magnitude,
            UnityEngine.LayerMask.GetMask("Ground", "Wall"));

        return hasLOS;
    }

    public virtual float GetDistance(Entity character)
    {
        return GetDistance(character.ChestPosition);
    }
    public virtual float GetDistance(Vector2 target)
    {
        return (target - ChestPosition).magnitude;
    }
    public virtual HitResult Hit(Damage damage)
    {
        if (IsBlock)
            return new HitResult(0, HitResultType.Blocked);
        else if (IsInvincible)
            return new HitResult(0, HitResultType.Miss);
        else
            return damageComponent.Hit(damage);        
    }
    
}