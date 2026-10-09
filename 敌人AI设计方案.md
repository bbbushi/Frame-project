# 敌人 AI 设计方案（一期·v4 ·定稿）

> **v4 修订**：本系统由 AI 执行实现。设计硬约束调整为：**严格 SOLID、简单不复杂、可读性第一**。
> 为此：① 放弃复用玩家状态机（不提接口、不改玩家系统），改用专用 40 行小机器——可读性优先于 DRY，敌人用不上玩家的三层守卫复杂度（YAGNI）；② 一类一文件；③ 禁炫技。

---

## 一、设计原则（SOLID 落实表）

| 原则 | 本设计的落实 |
|---|---|
| S 单一职责 | `EnemyVision` 只感知；`EnemyStateMachine` 只管转换；每块积木只表达一种行为；`EnemyAIComponent` 只做宿主+装配 |
| O 开闭 | 加新怪 = 新 SO / 新积木类注册进装配点，既有积木零修改 |
| L 里氏替换 | 任何 `IEnemyBrain` 实现（敌人/将来的友军 NPC）都可驱动同一积木库 |
| I 接口隔离 | `IEnemyState` 三方法；`IEnemyBrain` 只暴露积木真正需要的能力 |
| D 依赖倒置 | 积木依赖 `IEnemyBrain` 抽象，不依赖 `EnemyAIComponent` 具体类 |

**可读性纪律**（AI 实现时的自我约束，同样约束未来的修改者）：
- 一类一文件，文件名=类名；中文注释讲 why 不复述 what
- 方法短小直白；无 LINQ 链、无反射、无委托嵌套、无泛型（除 SO 泛型基类外零泛型）
- 显式优于聪明：`Tick(float dt)` 显式传帧间隔，不搞隐式取时
- 行为刻意简单：巡逻随机走停、追击直线、攻击三段（前摇/出招/冷却）

---

## 二、核心决策记录

| 决策 | 选择 | 理由 |
|---|---|---|
| 复用玩家 `PlayerStateMachine<TId>`？ | **否，专用 `EnemyStateMachine`（~45 行）** | 玩家机的状态字典锁死 `PlayerState`（继承耦合）；提接口要动玩家系统+双泛型阅读成本。敌人的转换需求极简（一条终态守卫+防重入），40 行专用机从头读到尾，零玩家回归风险。DRY 让位于可读性（YAGNI） |
| 行为树？ | 否 | 手写节点/黑板/滴答调度与"简单"直接冲突；状态机表达不了再升级（立碑） |
| SO 定义转换表？ | 否 | 条件求值器基础设施比状态库本身大；转换逻辑留在积木内，参数化用开关不用表格 |
| 状态 ScriptableObject 化？ | 否 | 行为进资产断点难打、调试痛；装配用代码显式可见 |
| Melee/Ranged 两个攻击积木？ | 分开 | 同构但出招不同；各 40 行直读优于一类内分支；新攻击类型=新积木（O 原则） |

---

## 三、装配模型与依赖方向

```
状态槽位（枚举固定）： Root ──▶ Chase ──▶ Attack ──▶ Death
   Root = Idle 或 Patrol（config.rootState 决定）
   Attack 槽 = MeleeAttackState 或 RangedAttackState（config.attack.type 决定）

依赖方向（严格单向，无横向）：
  Enemy.cs / AIDebugMenu（装配与调试）
      │
      ▼
  EnemyAIComponent（宿主：生命周期 + 装配 + IEnemyBrain 实现）
      │ 组合
      ├──▶ EnemyStateMachine ──▶ IEnemyState ◀──实现── 6 块积木
      └──▶ EnemyVision（纯 C# 感知）
                 ▲
                 │ 依赖倒置：积木只依赖 IEnemyBrain，不依赖任何具体类
  积木 ──▶ IEnemyBrain
```

三种示例怪（同代码三份 SO）：`MeleeBrute`（Patrol+近战）、`RangedKiter`（Patrol+远程 range 6）、`Sentry`（Idle 根+远程+leash 3）。

---

## 四、文件清单（一类一文件）

```
Assets/Script/Enemy/AI/
  EnemyStateId.cs          // 枚举
  IEnemyState.cs           // 积木契约：Enter / Tick(float dt) / Exit
  IEnemyBrain.cs           // 宿主契约（积木对外的全部需求）
  EnemyStateMachine.cs     // 专用小机器（~45 行）
  EnemyVision.cs           // 感知（纯 C#）
  EnemyAIComponent.cs      // 宿主（MonoBehaviour）
  IdleState.cs / PatrolState.cs / ChaseState.cs
  MeleeAttackState.cs / RangedAttackState.cs / DeathState.cs
Assets/Config/EnemyAIConfig.cs
Assets/Resources/data/ai/MeleeBrute.asset / RangedKiter.asset / Sentry.asset
Assets/Script/Enemy/Editor/AIDebugMenu.cs
```

namespace `enemy.ai`（EntityComponent 族不受 MOMS 命名空间扫描限制）。⚠ dt 一律帧间隔 `FixedFrameInterval`，不是 `TimeScale`（毒瞬间结算案教训，HealthManageComponent.cs:57 同款注释）。

---

## 五、核心类型规格

### 5.1 契约（两个接口）

```csharp
namespace enemy.ai
{
    public enum EnemyStateId { Idle, Patrol, Chase, Attack, Death }

    /// <summary>状态积木契约。Tick 的 dt 是物理帧间隔，由宿主喂入</summary>
    public interface IEnemyState
    {
        void Enter();
        void Tick(float dt);
        void Exit();
    }

    /// <summary>积木对宿主的全部需求。EnemyAIComponent 实现它；
    /// 将来友军/NPC 宿主实现同一接口即可复用整个积木库</summary>
    public interface IEnemyBrain
    {
        Entity Owner { get; }                  // 移动/朝向/距离/攻击挂点从这拿
        EnemyAIConfig Cfg { get; }
        // 感知缓存（EnemyVision 产出）
        Entity Target { get; }
        Vector3 LastKnownPosition { get; }
        bool HasLineOfSightNow { get; }
        Vector3 HomeAnchor { get; }            // leash 基准
        // 转换与记忆
        bool TryEnter(EnemyStateId id);        // 未注册槽位静默失败，积木走自己的 fallback
        void TryEnterRoot();
        void ForgetTarget();
        // 动画收口（换表现方案只改宿主实现，积木不动）
        void SetAnimBool(string name, bool value);
        void SetAnimTrigger(string name);
    }
}
```

> Cfg 给整份而非逐段窄化：config 是纯数据，依赖数据形状不算行为耦合；接口窄在行为能力上。

### 5.2 EnemyStateMachine（专用，~45 行）

```csharp
namespace enemy.ai
{
    /// <summary>敌人专用状态机：注册表 + 终态守卫 + 防重入。玩家机的三层守卫/互斥组用不上，不搬。</summary>
    public class EnemyStateMachine
    {
        readonly Dictionary<EnemyStateId, IEnemyState> states = new Dictionary<EnemyStateId, IEnemyState>();
        readonly List<EnemyStateId> terminalStates = new List<EnemyStateId>();   // 只会放 Death
        bool transitioning;   // Exit/Enter 期间拒绝并发切换（防嵌套切状态）

        public EnemyStateId CurrentId { get; private set; }
        public IEnemyState Current { get; private set; }
        public event Action<EnemyStateId, EnemyStateId> Transitioned;   // (from, to)，转换完成后触发

        public void Register(EnemyStateId id, IEnemyState state) => states[id] = state;
        public void MarkTerminal(EnemyStateId id) => terminalStates.Add(id);

        public void Initialize(EnemyStateId start)
        {
            CurrentId = start; Current = states[start]; Current.Enter();
        }

        /// <summary>请求切换。自转静默 true；未注册/终态/转换中 → false（调用方走 fallback）</summary>
        public bool ChangeState(EnemyStateId id)
        {
            if (transitioning) return false;
            if (id == CurrentId) return true;
            if (!states.ContainsKey(id) || terminalStates.Contains(CurrentId)) return false;

            var from = CurrentId;
            transitioning = true;
            try { Current.Exit(); CurrentId = id; Current = states[id]; Current.Enter(); }
            finally { transitioning = false; }
            Transitioned?.Invoke(from, id);
            return true;
        }

        public void Tick(float dt) => Current?.Tick(dt);
    }
}
```

### 5.3 EnemyVision（感知，纯 C#）

```csharp
namespace enemy.ai
{
    /// <summary>感知器：前向视野盒 + 视线校验 + 目击记忆。只认 (Entity, Config)，无状态机知识，可单测</summary>
    public class EnemyVision
    {
        public Entity Target { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasLineOfSightNow { get; private set; }

        readonly Entity owner;
        readonly EnemyAIConfig cfg;
        float pollTimer;

        public EnemyVision(Entity owner, EnemyAIConfig cfg) { this.owner = owner; this.cfg = cfg; }

        /// <summary>宿主每物理帧喂 dt；内部按 perceptionInterval 轮询省物理查询</summary>
        public void Tick(float dt)
        {
            pollTimer -= dt;
            if (pollTimer > 0f) return;
            pollTimer = cfg.perceptionInterval;

            // 前向视野盒（中心沿面朝偏移 → 背后看不见）
            Vector2 center = (Vector2)owner.transform.position
                + new Vector2(owner.locomotionComponent.FacingDirection * cfg.visionRange * 0.5f, 0.5f);
            var hit = Physics2D.OverlapBox(center, new Vector2(cfg.visionRange, cfg.visionHeight), 0f,
                LayerMask.GetMask("player"));
            if (hit != null)
            {
                var player = hit.GetComponentInParent<Entity>();
                if (player != null && owner.HasLineOfSight(player))   // Entity.cs:159 现成
                {
                    Target = player;
                    LastKnownPosition = player.transform.position;
                    HasLineOfSightNow = true;
                    return;
                }
            }
            HasLineOfSightNow = false;   // Target 保留——记忆期由 Chase 用 LastKnownPosition
        }

        public void Forget() => Target = null;
        public void Reset() { Forget(); HasLineOfSightNow = false; pollTimer = 0f; }
    }
}
```

### 5.4 EnemyAIComponent（宿主 + 装配 + brain 实现）

```csharp
namespace enemy.ai
{
    /// <summary>敌人 AI 宿主：持有感知与状态机（组合），按 config 装配积木，实现 IEnemyBrain 供积木回调</summary>
    public class EnemyAIComponent : EntityComponent, IEnemyBrain
    {
        [SerializeField] private EnemyAIConfig config;    // MonoBehaviour 可 SerializeField
        public EnemyAIConfig Cfg => config != null ? config
            : (config = Resources.Load<EnemyAIConfig>("data/ai/MeleeBrute"));   // 空则兜底

        EnemyStateMachine machine;
        EnemyVision vision;
        public EnemyStateId CurrentId => machine.CurrentId;

        // ── IEnemyBrain：转发（宿主是积木与各子系统之间的唯一通道）──
        public Entity Owner => base.Owner;               // 按基类实际成员名调整
        public Entity Target => vision.Target;
        public Vector3 LastKnownPosition => vision.LastKnownPosition;
        public bool HasLineOfSightNow => vision.HasLineOfSightNow;
        public Vector3 HomeAnchor { get; private set; }

        public override void Init()
        {
            HomeAnchor = Owner.transform.position;
            vision = new EnemyVision(Owner, Cfg);
            machine = new EnemyStateMachine();

            // ── 装配：全项目唯一依赖具体积木类的位置（组合模式的合法落点）──
            machine.Register(EnemyStateId.Patrol, new PatrolState(this));
            machine.Register(EnemyStateId.Idle,   new IdleState(this));
            machine.Register(EnemyStateId.Chase,  new ChaseState(this));
            machine.Register(EnemyStateId.Attack, Cfg.attack.type == AttackType.Melee
                ? (IEnemyState)new MeleeAttackState(this)
                : new RangedAttackState(this));
            machine.Register(EnemyStateId.Death,  new DeathState(this));
            machine.MarkTerminal(EnemyStateId.Death);
            machine.Transitioned += (from, to) => { if (Cfg.debugLog) Debug.Log($"[AI]{Owner.name}: {from} → {to}"); };
            machine.Initialize(Cfg.rootState);
        }

        public override void RefreshFixedUpdate()   // 由 Enemy.FixedUpdate 驱动（§8 接线）
        {
            // 受击硬直门：ActionIgnore 期间决策整体暂停——受击不建独立状态的原因
            if (Owner.actionIgnoreComponent != null && Owner.IsIgnore(ActionIgnoreTag.All)) return;

            vision.Tick(FixedFrameInterval);
            machine.Tick(FixedFrameInterval);
        }

        public bool TryEnter(EnemyStateId id) => machine.ChangeState(id);
        public void TryEnterRoot() => machine.ChangeState(Cfg.rootState);
        public void ForgetTarget() => vision.Forget();
        public void OnOwnerDied() => machine.ChangeState(EnemyStateId.Death);
        public void SetAnimBool(string name, bool value) { if (Owner.anim != null) Owner.anim.SetBool(name, value); }
        public void SetAnimTrigger(string name) { if (Owner.anim != null) Owner.anim.SetTrigger(name); }

        /// <summary>池化取出时重置（Death 是终态转不出，直接 Initialize）</summary>
        public void ResetAI()
        {
            vision.Reset();
            HomeAnchor = Owner.transform.position;
            machine.Initialize(Cfg.rootState);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() { /* 视野盒双色（发现红/未发现青）、攻击距离线黄、leash 圈灰 */ }
#endif
    }
}
```

---

## 六、六块积木规格（每块一类一文件，刻意精简）

统一形状（无基类、构造注入、自持计时器——计时器用纯 float，不走 Timer：Timer 注册进 TimeManager 需显式销毁）：

```csharp
public class XxxState : IEnemyState
{
    readonly IEnemyBrain brain;
    float timer;

    public XxxState(IEnemyBrain brain) { this.brain = brain; }

    public void Enter() { timer = 0f; /* 进场动作 */ }
    public void Tick(float dt) { timer += dt; /* 分段/转换判定 */ }
    public void Exit() { /* 收尾动作（动画参数复位等） */ }
}
```

**IdleState**（站桩哨兵）
```
Enter: Owner.locomotionComponent.Stop()；brain.SetAnimBool("IsMove", false)
Tick:  Target != null && HasLineOfSightNow → brain.TryEnter(Chase)
```

**PatrolState**（走停巡逻）
```
Enter: 随机方向 SetFacing(±1)；随机走路时长 = Rand(patrol.moveTimeRange)；Velocity = patrol.speedRatio
Tick:  [走路] ApplyHorizontal(FacingDirection)
        撞墙(Owner.detection.IsFacingWall) 或 前下方无地 → SetFacing(反向)
        timer 到 → 切[等待]：Stop + 随机等待时长 = Rand(patrol.waitTimeRange)
       [等待] timer 到 → 回 Enter 逻辑重新选方向
       Target 可见 → brain.TryEnter(Chase)
```
⚠ 悬崖用前探点：`Physics2D.OverlapPoint(pos + Facing*0.5f + down*0.5f, Ground)`——判"再走会掉"，不是"已经掉了"（`!IsOnGround` 是后者）。

**ChaseState**（直追 + 三出口）
```
Enter: Velocity = chase.speedRatio；brain.SetAnimBool("IsMove", true)；lostTimer = 0
Tick:  可见 → SetFacing(朝目标) + ApplyHorizontal(朝向)；lostTimer = 0
       不可见 → lostTimer += dt；追 LastKnownPosition（接近即 Stop 干等）
       出口① GetDistance(Target) ≤ attack.range 且可见 → TryEnter(Attack)
       出口② lostTimer > chase.memoryTime → ForgetTarget → TryEnterRoot()
       出口③ 离 HomeAnchor 距离 > chase.leashRange → ForgetTarget → TryEnterRoot()
Exit:  brain.SetAnimBool("IsMove", false)
```

**MeleeAttackState**（前摇/出招/冷却，timer 分段，不依赖动画事件）
```
Enter: Stop()；SetFacing(朝目标)；SetAnimTrigger("Attack")；phase = Windup
Tick:  Windup 到(attack.windup) → 出招：
           Hitbox.GenerateHitbox(Owner.detection.hitboxPrefab, Owner,
               Owner.detection.GetAttackSocket().position, 0f)
           phase = Cooldown
       Cooldown 到(attack.cooldown) →
           Target 仍在 attack.range → 再一轮（重置 Enter 逻辑）
           可见 → TryEnter(Chase)；否则 TryEnterRoot()
```

**RangedAttackState** —— 与 Melee 同构，仅出招一行不同：
```
Hitbox.GenerateHitbox(brain.Cfg.attack.bulletPrefab, Owner, socket.position, 0f)
```
（bulletPrefab 由 config 提供；发射物初速/伤害在 Hitbox prefab 自身配置）

**DeathState**（终态）
```
Enter: Owner.locomotionComponent.Stop()；brain.SetAnimTrigger("Die")
Tick:  空（池回收由 Enemy 订阅 OnDied 的 Die() 触发，状态只管停表现）
```

⚠ FSM 正确性不依赖动画事件——出招判定用 timer，动画只推表现（PlayerState.AnimEndTrigger 同款结论）。

---

## 七、EnemyAIConfig（SO，嵌套参数组）

```csharp
public enum AttackType { Melee, Ranged }

[CreateAssetMenu(fileName = "NewEnemyAI", menuName = "Project/EnemyAIConfig")]
public class EnemyAIConfig : ScriptableObject
{
    [Header("根状态")] public EnemyStateId rootState = EnemyStateId.Patrol;   // Idle=哨兵 / Patrol=巡逻

    [Header("感知")] public float visionRange = 6f;         // 前向视野宽
                    public float visionHeight = 3f;
                    public float perceptionInterval = 0.15f; // 轮询间隔

    [Serializable] public class PatrolSettings
    { public Vector2 moveTimeRange = new Vector2(3f, 6f);    // 走路时长区间
      public Vector2 waitTimeRange = new Vector2(0.5f, 2f); // 停顿时长区间
      public float speedRatio = 0.5f; }                     // × locomotion.moveSpeed
    public PatrolSettings patrol = new PatrolSettings();

    [Serializable] public class ChaseSettings
    { public float speedRatio = 1.1f;
      public float memoryTime = 2.5f;                       // 丢失后记忆期
      public float leashRange = 12f; }                      // 离家超过即放弃
    public ChaseSettings chase = new ChaseSettings();

    [Serializable] public class AttackSettings
    { public AttackType type = AttackType.Melee;
      public float range = 1.2f;
      public float windup = 0.35f;                          // 前摇
      public float cooldown = 1.2f;                         // 出招后冷却
      public GameObject bulletPrefab; }                     // type=Ranged 时用
    public AttackSettings attack = new AttackSettings();

    [Header("调试")] public bool debugLog = false;
}
```

放 `Resources/data/ai/`。与 characterData/controllerData 分离：移动物理与行为策略独立演化——同一物理怪配不同行为 SO（扩展多种敌人的支点）。

---

## 八、Enemy.cs 接线（⚠ 本项目两次翻车都在"漏接线"，逐条对照）

```csharp
// 1) 字段：      public EnemyAIComponent aiComponent;
// 2) Awake：     aiComponent = GetComponentInChildren<EnemyAIComponent>();     // base.Awake() 后
// 3) Start：     if (aiComponent != null) aiComponent.Init();                 // base.Start() 后
// 4) FixedUpdate：if (aiComponent != null) aiComponent.RefreshFixedUpdate();  // base.FixedUpdate() 后（毒不跳案同款位）
// 5) OnEnable：  if (aiComponent != null) healthManageComponent.OnDied += aiComponent.OnOwnerDied;
//    OnDisable 对称退订（与现有订阅同块——池化订阅标准位）
// 6) OnSpawnFromPool：if (aiComponent != null) aiComponent.ResetAI();
```

顺手删旧代码：`Wait()/Move()/RemTimer`（Timer 若废弃记得 Destroy——注册过 TimeManager）、注释掉的 `Detection()`、`IsplayerInRange/GetPlayerTransform/OnDrawGizmosSelected`（被 EnemyVision 取代）。

**动画参数**（Animator 补三个参数，缺剪辑先不连）：`IsMove`(bool)、`Attack`(trigger)、`Die`(trigger)——全部经 brain 收口。

---

## 九、调试工装（AIDebugMenu）

- `AI/打印选中敌人状态`：CurrentId / Target / LastKnownPosition / 离 HomeAnchor 距离（非 Play 可看）
- `AI/强制进入追击`：选中敌人 `ai.ResetAI(); ai.TryEnter(Chase)`（纯数据操作不受编辑器上下文限制——⚠ 菜单里 Instantiate UGUI 才有渲染坑）
- Gizmo 三件套进 `EnemyAIComponent.OnDrawGizmosSelected`

---

## 十、验收（AI 实现的完成定义）

**编译**：离线全项目编译零错误（CLAUDE.md 的 csc 命令）。

**冒烟手册（Play）**：
1. 巡逻走停自然、撞墙/悬崖回头、背后走过玩家不反应
2. 进前向视野 Gizmo 变红 → 追；跑出 leash → 放弃回巡逻
3. 躲墙后（LOS 断）→ 追最后目击点 → 干等 memoryTime → 回巡逻
4. 贴脸 → 前摇 → 玩家掉血 + 飘字（GAS Damage 全链路）→ 冷却不连打
5. 玩家打敌人 → 闪红 + 硬直期 AI 静止（ActionIgnore 门）→ 恢复继续
6. 打死 → 池回收 → 再取出：状态=根状态、HomeAnchor 重锚、无残留目标
7. 回归：飘字/铁壁/中毒菜单照常

**扩展性验收**：
8. `RangedKiter` 挂第二个敌人 prefab → 零代码改动，远程怪成立
9. `Sentry` → 站桩哨兵：不巡逻、玩家跑远即回位

---

## 十一、二期展望（留桩不立项）

- 新积木：FleeState / SummonState / JumpChase——每块 50-80 行注册即用；新宿主实现 IEnemyBrain（友军/NPC）白拿积木库
- KeepDistance 积木（风筝走位，RangedKiter 完全体）
- GAS 联动：AI 吃 Effect（减速→speedRatio 动态降、眩晕→ActionIgnore 即眩晕语义）
- 巡逻路径点（PatrolState 支持 waypoint）
- 行为树（状态机表达不了再升级，先立碑）
