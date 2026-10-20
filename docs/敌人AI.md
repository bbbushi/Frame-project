# 敌人 AI — 设计与工作逻辑

> 代码：`Assets/Script/Enemy/Enemy.cs` + `Assets/Script/Enemy/AI/`（EnemyAIComponent、EnemyVision、EnemyStateMachine、各状态积木）、`Assets/Config/EnemyAIConfig.cs`
> 定位：积木式 AI——感知 + 状态机两个纯 C# 对象组合而成，配置 SO 驱动三种性格。
> 前置阅读：《实体与组件模型》（刷新链、池化）、《打击盒与动作系统》（ActionIgnore/GenerateHitbox）

---

## 一、设计思路（为什么这么设计）

**1. 组合 + 依赖倒置。**
EnemyAIComponent 持有感知（EnemyVision）与状态机（EnemyStateMachine）两个纯 C# 对象，自身实现 `IEnemyBrain` 供积木回调（EnemyAIComponent.cs:8-12）。积木只认 IEnemyBrain 接口不认具体宿主——"将来友军/NPC 宿主实现同一接口即可复用整个积木库"。Enemy.cs 只做三根线：Init / RefreshFixedUpdate / ResetAI。

**2. 刻意不复用玩家状态机。**
EnemyStateMachine 类头注释（:7-10）："玩家的三层守卫/互斥组这里用不上，40 行从头读到尾优于跨系统耦合（可读性 > DRY）"。保留的仅是共性教训：**终态守卫 + transitioning 防重入**（"Exit 里再切状态是常见翻车点"）。

**3. 状态即积木。**
IEnemyState = Enter/Tick(dt)/Exit（IEnemyState.cs:7-12）。约定：构造注入 IEnemyBrain；自持计时器**纯 float 不走 Timer**；"Tick 的 dt 由宿主显式喂入——显式传参优于隐式取时"。状态机"只推进当前积木，不做任何决策——转换判定是积木自己的事"（EnemyStateMachine.cs:49-50）。

**4. 无"受击状态"是有意设计。**
受击硬直门放在 EnemyAIComponent.RefreshFixedUpdate 开头：`IsIgnore(All) → return`——"硬直期间 AI 决策整体暂停，击退位移由 Locomotion 自己跑，AI 不抢速度。不建独立受击状态的原因就在这"（:82-84）。硬直由 DamageComponent.Hit 受击时 `AddIgnore(hitRepel.length, All)` 打上。

**5. 配置驱动性格。**
EnemyAIConfig 纯数据 SO——"同一份代码配不同 SO = 不同怪（扩展多种敌人的支点）。与 characterData/controllerData 分离——移动物理与行为策略独立演化"（EnemyAIConfig.cs:9-12）。三种性格 MeleeBrute/Sentry/RangedKiter 代码零分支，全靠 SO 差异。

**6. Melee/Ranged 刻意不合并。**
"与 MeleeAttackState 同构，仅出招一行不同（发弹 vs 挥击）。刻意不合并成一个类加分支——各 40 行直读优于类内分支，新攻击类型 = 新积木（开闭原则）"（RangedAttackState.cs:6-8）。装配点全项目唯一：EnemyAIComponent.Init 按 `Cfg.attack.type` 二选一装进 Attack 槽（:67-69）。

**7. FSM 正确性不挂动画。**
"出招判定用 timer 不依赖动画事件——FSM 正确性不挂在动画上（PlayerState 同款结论）"（MeleeAttackState.cs:7-8）。动画仅是表现（SetAnimTrigger("attack")）。

---

## 二、工作逻辑（怎么运转）

### 驱动链

```
Enemy.FixedUpdate (Enemy.cs:29-33)
  → base.FixedUpdate（Entity 六步组件链）
  → aiComponent.RefreshFixedUpdate (EnemyAIComponent.cs:80-90):
      ① 受击硬直门: IsIgnore(All) → return     [AI 整体暂停，含感知轮询]
      ② dt = FixedFrameInterval                ["帧间隔，不是 TimeScale"]
      ③ vision.Tick(dt)                        [感知先跑——积木本帧读到最新视线]
      ④ machine.Tick(dt) → Current.Tick(dt)
```

### 装配（Enemy.Start → aiComponent.Init，"在所有组件 Init 之后"）

```
HomeAnchor = Owner.transform.position;        [锚：出生点=家]
vision = new EnemyVision(Owner, Cfg);         [Cfg 懒加载，兜底 Resources/data/ai/MeleeBrute]
machine = new EnemyStateMachine();
Register(Idle/Patrol/Chase/Death) + Register(Attack, type==Melee ? Melee : Ranged)
MarkTerminal(Death); Initialize(Cfg.rootState);
```

### EnemyVision 感知细节（EnemyVision.cs:27-50）

- **轮询节流**：pollTimer 按 perceptionInterval=0.15s 到点才做物理查询（省开销）；
- **前向视野盒**：`center = position + FacingDirection × visionRange × 0.5（沿面朝偏半宽 → 背后自然看不见）+ (0, 0.5)`，OverlapBox 打 player 层；
- **LOS**：命中后还须 RootPosition 间 Raycast 不被 SightObstacle 层遮挡；
- **目击记忆**：命中→Target=player + LastKnownPosition + HasLineOfSightNow=true；未命中→**只清 HasLineOfSightNow，Target 保留**——"记忆期由 Chase 用 LastKnownPosition 追最后目击点"，何时 Forget 是 Chase 的决策，感知器不做主；
- `Reset()`（池化）：Forget + 清视线 + pollTimer=0（"下帧立即轮询"）。

### 状态转换条件全表

| 状态 | 进入 | 退出 |
|---|---|---|
| Idle（哨兵） | rootState=Idle | 见目标且有视线 → Chase |
| Patrol（巡逻） | rootState=Patrol | 走/停每段开头先查目标→Chase；无目标按 phaseTime 循环走↔停；撞墙或悬崖（前探射线 0.5m）立即掉头 |
| Chase | Idle/Patrol 见目标；或 Attack 冷却完脱离射程 | ①有视线且进 attack.range → Attack ②lostTimer>memoryTime(2.5s) → Forget+回根 ③离家>leashRange → Forget+回根（防追出活动区）④Target==null（死亡/销毁）→ 回根 |
| Attack（Melee/Ranged） | Chase 出口① | 冷却完：仍在射程+有视线 → 重走 Enter（再来一轮）；否则 → Chase |
| Death（终态） | OnOwnerDied | **无出口**：MarkTerminal 一切 ChangeState 被拒 |

Chase 内部：有视线 lostTimer=0、FaceTo、追击速度倍率 1.1；无视线 lostTimer+=dt，距 LastKnownPosition>0.4 追最后目击点，到点 Stop 干等（"视野盒还在扫，扫到自动续追"）。**速度倍率是 LocomotionComponent 的公共状态**：进状态设 speedRatio（巡逻 0.5/追击 1.1），出状态必须还原 Velocity=1 + Is Move=false。

### MeleeAttackState 三段实现（timer + Phase{Windup, Cooldown}）

```
Enter: Target 空→TryEnterRoot；Stop + FaceTo(Target) + SetAnimTrigger("attack")
Tick:  Windup   && timer >= windup(0.35s)  → Swing() + phase=Cooldown
       Cooldown && timer >= cooldown(1.2s)  → 仍射程有视线 ? 重走 Enter : TryEnter(Chase)
Swing: Hitbox.GenerateHitbox(detection.hitboxPrefab, owner, null,
           healthManageComponent.AttackPower, detection.GetAttackSocket().position)
       [伤害走 GAS；prefab 没配判定框就不出招，不报错]
```
RangedAttackState 仅 Swing→Shoot 换成发 bulletPrefab（远程弹还没建——不出招不报错，配好 asset 即生效）。

### 受击硬直门的两端

```
上锁侧：DamageComponent.Hit → Owner.Interrupt() + Hit 动画 + 面向攻击方
        + ForceMove(hitRepel, knockDir) + AddIgnore(hitRepel.length, All)
解锁侧：EnemyAIComponent.RefreshFixedUpdate 开头 IsIgnore(All) → return
        （击退位移由 LocomotionComponent 跑——它在 Entity 链里先于 AI 刷新，不受门影响）
表现：Enemy.Hit override 先 Flash()（红 0.1s 协程）再 base.Hit（Enemy.cs:73-77）
```

### 死亡与池化时序

```
血量跌破 0 → OnDied（OnEnable 订阅，顺序即执行顺序，Enemy.cs:80-87）：
  ① aiComponent.OnOwnerDied → machine.ChangeState(Death)   [先进死亡态]
  ② Enemy.Die → ObjectPoolManager.Release(gameObject)      [再回收进池]
OnDisable 对称退订。
订阅放 OnEnable/OnDisable 而非 Start/Awake——"池化复用第二只起就收不到事件了"。

OnSpawnFromPool：Revive（内部先 ClearEffects：带毒尸体进池复出的清理）
  → ResetAI → ClearBattleTimers → 变白（清 Flash 残留）
OnReturnToPool：StopAllCoroutines（终止 Flash）+ 清 Timer + 速度清零 + 变白
ResetAI()：vision.Reset() + HomeAnchor=当前位置（池取出位置即新家）+ machine.Initialize(rootState)
  ——必须走 Initialize 直接置位："Death 是终态转不出，重置只能走这"
  （Initialize 绕过终态守卫覆写 Current，EnemyStateMachine.cs:25-26）
```

### 三种性格配置（Resources/data/ai/）

| | MeleeBrute | Sentry | RangedKiter |
|---|---|---|---|
| rootState | Patrol | **Idle** | Patrol |
| visionRange / leashRange | 6 / 12 | 7 / **3** | 7 / 12 |
| attack.type / range | **Melee** / 1.2 | **Ranged** / 6 | **Ranged** / 6 |
| windup / cooldown | 0.35 / 1.2 | 0.5 / 1.6 | 0.5 / 1.6 |

Sentry=Idle 根 + 短 leash（"离家 3 就放弃"的站桩哨兵）；RangedKiter=巡逻根+远程弹+长 leash——性格全在 SO。

---

## 三、关键类型速查

| 类型 | 位置 | 职责 |
|---|---|---|
| Enemy | Enemy/Enemy.cs:11 | Entity+IPoolable 宿主：AI 三根线、OnDied→Death+回收、Flash |
| EnemyStateMachine | Enemy/AI/EnemyStateMachine.cs:11 | 注册表+终态守卫+防重入；Initialize 绕守卫置位（池化重置用） |
| IEnemyState | AI/IEnemyState.cs:7 | 积木契约 Enter/Tick(dt)/Exit |
| IEnemyBrain | AI/IEnemyBrain.cs:10 | 积木对宿主的需求接口（Owner/Cfg/感知缓存/TryEnter/动画收口） |
| EnemyAIComponent | AI/EnemyAIComponent.cs:12 | AI 宿主：装配、硬直门、感知先跑、OnOwnerDied、ResetAI、Gizmo |
| EnemyVision | AI/EnemyVision.cs:10 | 轮询节流+前向视野盒+LOS+目击记忆（纯 C# 可单测） |
| IdleState / PatrolState / ChaseState | AI/ | 哨兵 / 走停巡逻+掉头 / 直线追击+三出口 |
| MeleeAttackState / RangedAttackState | AI/ | 三段攻击（Windup→Swing/Shoot→Cooldown），一行之差刻意不合并 |
| DeathState | AI/DeathState.cs:7 | 终态：Stop+播倒地，Tick 空——回收交 Enemy.Die |
| EnemyAIConfig | Config/EnemyAIConfig.cs:14 | rootState/感知/patrol/chase/attack 嵌套 SO |
| AIDebugMenu / DummyDebugMenu | Enemy/Editor/ | 打印状态+ForceChase / 生成 immortal 不倒翁沙包 |

---

## 四、坑与约定

1. **动画收口的已知坑**：参数名对齐 Skeleton Sword.controller——`Is Move`(bool)/`attack`/`Dead`(trigger)。控制器 Entry 转换第 4 条"无条件→Dead 兜底"：idle 与 Is Move 同时 false 的瞬间（生成首评/池化取出）会直接播倒地——所以 `SetAnimBool("Is Move", v)` 内同步喂 `idle = !v`（EnemyAIComponent.cs:44-55）。
2. 积木碰动画的唯一通道是 IEnemyBrain 的 SetAnimBool/SetAnimTrigger——"换表现方案只改宿主实现，积木不动"。
3. 新攻击类型 = 新积木类 + Init 装配点加一个分支；别在现有 AttackState 里加 type 判断。
4. 出状态必须还原 Locomotion 的公共状态（Velocity/speedRatio、Is Move）——忘还原=下个状态速度错。
5. Chase 的 leash/memory/Forget 决策都在 Chase；感知器只负责"看见/没看见"——扩展感知（听觉等）时守住这条线。
6. 远程弹 asset 未建时 RangedAttackState 不出招不报错——远程怪不攻击先查 bulletPrefab 是否已配。

## 五、与相邻模块的边界

- 上游《实体与组件模型》：AI 挂 Entity 刷新链尾；硬直门=ActionIgnoreComponent(All)。
- 下游《打击盒与动作系统》：出招走同一个 GenerateHitbox，伤害取 GAS AttackPower。
- 对照《玩家系统》：两套 FSM 共享的只有教训（终态守卫/防重入/FSM 不挂动画），代码零共享——可读性优先于 DRY 的自觉选择。
