# Bug 清单 — 待修复

> 首次生成: 2026-08-06（代码审查发现 68 项，当时已修复 H1-H6、M4、M9）
> 最近复核: 2026-08-18 — 逐一核对当前代码（含 ForceMove 位移重构、Entity 组件层重构后的变化），**所有路径与行号均已更新为当前值**
>
> | 状态 | 数量 | 明细 |
> |------|------|------|
> | ❌ 仍存在 | **35** | High 10 · Medium 16 · Low 9 |
> | ✅ 已修复 | 22 | 2026-08-06 前：H1-H6、M4、M9；2026-08-17 复核确认：C4、M6、M12、M14、L13；2026-08-17 修复：C1-C3、C5-C15、H9（随 C9）；2026-08-18 复核确认：M22、L6、N1 |
> | 💀 随重构消失 | 3 | L3、L5、H11 |
> | 🆕 新发现 | 1 | N2 |

图例：❌ 仍存在 · 🔀 已变化/衍生 · ✅ 已修复 · 💀 已随重构消失

---

## 🔴 CRITICAL — ✅ 全部已修复（2026-08-17）

C1-C3、C5-C12 见下方修复记录；C13-C15 经查证为官方包 `cn.unity.uos.launcher` 自动导入产物（项目零调用、无 UOSSettings.asset、手改会被包覆盖），已随 UOS Launcher 包整体移除处置（manifest.json 依赖 + `Assets/UOSLauncherEncrypt/` 目录 + `UNITY_UOS_SECURITY` 宏）。

---

## 🟠 HIGH — 仍存在 10

| # | 文件 | 行号 | 描述 |
|---|------|------|------|
| H7 | `Assets/Script/Component/PlayerAnimatorComponent.cs` | 41 | ❌ lambda 订阅 `healthManageComponent.OnDied` 无对应退订 → 内存泄漏（全库无 `-=` 取消；订阅对象已随重构从 Health 模块改为 Entity 层 healthManageComponent）|
| H8 | `Assets/Script/Time/Timer.cs` | 82 | ❌ `Destroy()` 仅置标志位，`TimerTimeOut/TimerTick` 委托未置空（备注：`needToDestroy` 标志已随 C9 修复被 `DriveTimers` 消费，仅剩委托未置空问题）|
| H10 | `Assets/Script/pool/ObjectPoolManager.cs` | 30、172 | ❌ `_spawnedToType` 映射仅在回收路径移除；已生成对象被外部 `Destroy`（含 Release 回退分支 172 行）后条目永久残留 |
| H12 | `Assets/Faction.cs` | 24-33 | 🔀（已文档化）`friendly.Hostile(player)` 落到兜底 `return true`，与 `GetHostileFaction(friendly)==enemy` 矛盾；方法注释已明确声明"不可用于判断 friendly 与 all"，兜底行为本身未改 |
| H13 | `Assets/Script/pool/ShadowPool.cs` | 48-52 | 🔀（衍生）`GetShadow` 参数已加空校验，但同步改由 Shadow.OnEnable 自读全局 `Player.Instance`（Shadow.cs:21）：传入任意非空 Transform 仍生成玩家残影，且 `Player.Instance` 为空时 OnEnable 直接 NRE |
| H14 | `Assets/Script/Modules/PlayerModule.cs` | 14 | ❌ `player => (Player)base.Owner` 在 Bind() 未调用时静默返回 null，无守卫或诊断 |
| H15 | `Assets/Effect/BloodParticleGenerator.cs` | 35 | ❌ `Random.Range(-1, 1)` 整数版只产生 -1/0，经 `*2-1` 映射为 {-3,-1}，注释声称的"±1 随机方向"永不含 1（应为 `Random.Range(0, 2) * 2 - 1`）|
| H16 | `Assets/Effect/BloodParticleGenerator.cs` | 38、55、69 | ❌ 三处裸 `Instantiate`，无对象池复用（对比项目已有 ShadowPool/ObjectPoolManager）|
| H17 | `Assets/Script/Singleton.cs` | 14 | ❌ `as T` 软转换静默失败，泛型参数配错时无任何诊断 |
| H18 | `Assets/Script/Tools/MathTools.cs` | 71 | ❌ `Acos(dot / (|a|·|b|))` 零向量除零得 NaN，且未 clamp 到 [-1,1]，浮点误差亦会产生 NaN |

---

## 🟡 MEDIUM — 仍存在 16（另 4 已修复）

| # | 文件 | 行号 | 描述 |
|---|------|------|------|
| M1 | `Assets/Script/GameManager.cs` | 12 | ❌ `[SerializeField] List<Type>` Unity 无法序列化，Inspector 赋值无效 |
| M2 | `Assets/Script/GameManager.cs` | 161-166 | ❌ 循环依赖时 LogError 后仍返回未排序原始列表作后备 |
| M3 | `Assets/Script/GameManager.cs` | 184-188 | ❌ `dt` 在循环前一次性捕获，TimeManager 改 `Time.timeScale` 只影响下一帧，各 Manager 拿到的 dt 与执行顺序相关 |
| M5 | `Assets/Script/Time/TimeManager.cs` | 57-59（及 67/75/87 等）| ❌ 所有静态 scale setter 在 `Resolve` 为 null 时静默丢弃赋值 |
| M7 | `Assets/Script/Time/Timer.cs` | 96-100 | ❌ `operator -` 修改原对象 remainTime 并返回自身，而非返回新对象 |
| M8 | `Assets/Script/Time/Timer.cs` | 59、68 | ❌ `inTime = remainTime > 0` 先行判定，remainTime==0 构造的 Timer 永不触发 TimerTimeOut |
| M10 | `Assets/Script/Player/Player.cs` 15-22 + `PlayerInputController.cs` 42-46 | — | ❌ Player 未生成时 `Player.Instance` getter 每帧执行 `FindObjectOfType` |
| M11 | `Assets/Script/InputComponent/Commands/AttackCommand.cs` | 13 | ❌ CanExecute 仅检查地面状态，无冷却/连击判断（PlayerCombat.ExecuteCombo 连击窗口逻辑已被注释，PlayerCombat.cs:33-38、50-69）|
| M13 | `Assets/Script/pool/ShadowPool.cs` | 6 | ❌ 单例为 `public static` 可写字段，任意代码可覆盖 |
| M15 | `Assets/Faction.cs` | 9-21 | ❌ `Contains` 不对称：`all`/`friendly` 为 origin 时命中，但 `player.Contains(all)` / `player.Contains(friendly)` 返回 false |
| M16 | `Assets/Faction.cs` | 43 | ❌ `GetHostileFaction()` 末尾兜底 `return all`，neutral（及 all）输入回退返回 all |
| M17 | `Assets/Script/Camera/PlayerCameraComponent.cs` | 21、43 | ❌（文件已改名 PlayerCamera → PlayerCameraComponent）`positionLastFrame` 未在 `Init()` 初始化，首帧从 (0,0,0) 开始 lerp |
| M18 | `Assets/Script/Camera/PlayerCameraComponent.cs` | 37、82 | ❌（文件已改名）`Screen.height` 可能为 0，除以零 |
| M19 | `Assets/Script/Config/EntityCharacterConfig.cs` | 9-10 | ❌ `maxHealth`/`attackDamage` 无默认值，新资产默认 0 |
| M20 | `Assets/Script/Config/SFXManagerConfig.cs` | 24 | ❌ `footstepGroundMask = -1` 默认 Everything |
| M21 | `Assets/Script/Config/SFXManagerConfig.cs` | 30-31 | ❌ pitch `[Range(-3,3)]` 允许负值（音频倒放；SFXManager 的 Clamp 同样放行）|

---

## 🔵 LOW — 仍存在 9（另 2 已修复、2 消失）

| # | 文件 | 行号 | 描述 |
|---|------|------|------|
| L1 | `Assets/Script/Player/PLAYERSTATE/PlayerDeathState.cs` | 37-45 | ❌ `stateTimer` 被 base.Enter() 重置为 0 后无人设正值，递减分支永不可达（死代码，延迟实际由协程承担）|
| L2 | `Assets/Script/Config/PlayerControllerData.cs` | 11 | ❌ `jumpforce` 命名应为 `jumpForce`（PlayerLocomotion.cs:33 仍在消费）|
| L4 | `Assets/Script/Player/PLAYERSTATE/PlayerIdleState.cs` | 16 | ❌ `Debug.Log` 遗留在生产代码 |
| L7 | `Assets/Script/Config/PlayerInputData.cs` | 29 | ❌ CreateAssetMenu 路径 `"Data/..."` vs 其他配置均用 `"Game/..."` |
| L8 | `Assets/Script/InputComponent/PlayerAction.cs` | 6-17 | ❌ 枚举无显式数值，经 ActionBinding 序列化进 .asset，插值重排会错位 |
| L9 | `Assets/Script/InputComponent/PlayerAction.cs` | 6-17 | ❌ 无 None/Invalid 哨兵值，0 号位被 MoveLeft 占用 |
| L10 | `Assets/Script/InputComponent/InputManager.cs` | 96 | ❌ `Enum.GetValues` 位于每帧调用的 ResetFrameStates()，装箱产生 GC |
| L11 | `Assets/Script/InputComponent/InputManager.cs` | 21 | ❌ `Dependencies => new()` 每次访问新建 List |
| L12 | `Assets/Script/Player/PLAYERSTATE/PlayerState.cs` | 51-52 | ❌ `stateTimer` 递减用 `Time.deltaTime` 而非缩放后帧间隔 |

---

## 🆕 新发现

### 2026-08-18

| # | 文件 | 行号 | 级别 | 描述 |
|---|------|------|------|------|
| N2 | `Assets/Script/InputComponent/InputManager.cs` | 49 | 🟡 MEDIUM | `Resources.Load<PlayerInputData>("PlayerInputData")` 路径与资产实际位置不匹配：资产在 `Assets/Resources/data/PlayerInputData.asset`，正确加载路径应为 `"data/PlayerInputData"`（SFXManager 的 `"data/SFXManagerConfig"` 是对的）→ **键位资产永远加载失败**，每次初始化走硬编码后备键位，Inspector 中修改 .asset 完全不生效（因后备键位与资产默认值恰好一致，暂无可见功能差异，故定为 MEDIUM）|

> 原备注（2026-08-17：DamageComponent 受击时的 `AddIgnore` 被注释、功能禁用）**已解除** — 现于 `DamageComponent.cs:37` 生效：`Owner.AddIgnore(hitRepel.length, ActionIgnoreTag.All)`。

---

## ✅ 修复记录

| 日期 | 编号 | 修复内容 |
|------|------|----------|
| 2026-08-06 前 | H1-H6, M4, M9 | 首轮修复（见 2026-08-06 版清单）|
| 2026-08-17 | C1 | `Player.Awake` 组件缺失时 LogError + `gameObject.SetActive(false)`（必须失活 GO 而非仅 enabled=false：`FindObjectOfType` 会找到 disabled 组件导致 PlayerInputController 每帧 NRE）|
| 2026-08-17 | C2 | `ChangeState` 两个重载 + `Initialize` 加 null 守卫（LogWarning/LogError + return）；主重载 exitState 链加 `CurrentState != null` 条件 |
| 2026-08-17 | C3 | `RefreshUpdate`/`AnimEnd` 改为 `StateMachine?.CurrentState?.` 两级条件调用 |
| 2026-08-17 | C5 | 实际调用点在 `BloodParticle.cs`（原 Enemy.cs 行号已过时）：Instance null 时跳过生成 + 仅首次 LogWarning，粒子仍自毁；BloodExample.cs 两处同型顺带修复 |
| 2026-08-17 | C6 | `EntityComponent` 三个时间属性 Owner null 时降级（GlobalTimeScale / fixedDeltaTime / deltaTime）+ Owner 查找失败仅首次警告 |
| 2026-08-17 | C7 | `PlayerState` Enter/Update/Exit 三处 `player.anim` 加 null 守卫；`PlayerTranState.Enter` 的 `anim.Play` 同型顺带修复 |
| 2026-08-17 | C8 | CameraShaker 四个静态入口加 Instance 防护（ShakeOffset 返回零向量）；`shakeInfos` 内联初始化；锁定计时器改 `GetLockTimer()` 懒创建（避免重复实例在 Awake 污染静态 Timers 列表）|
| 2026-08-17 | C9 | `TimeManager.OnUpdate` 末尾新增 `DriveTimers()`：倒序遍历按 TimerType 分发 dt 调 `Tick()`（回调异常 try/catch 隔离）+ `RemoveAll` 清理 needToDestroy；连带恢复 CameraLock/相机抖动锁定功能 |
| 2026-08-17 | C10 | `ReadKey` 增加 `InputTriggerType trigger` 参数按相位单查；资产/后备键位均透传 trigger；顺带补全 `PlayerInputData.asset` 与代码默认列表缺失的 Execution/Mark 绑定（修复前处决/标记功能实际完全失效）|
| 2026-08-17 | C11 | `EnemyPoolInfo` 加 `maxSize`（默认 50）并传入池构造；`actionOnDestroy` 补 `_spawnedToType.Remove`（池满丢弃路径映射残留）；ShadowPool 同型顺带修复 |
| 2026-08-17 | C12 | `Enemy : Entity, IPoolable`：取出时 Revive/清标记/清无敌格挡/复位颜色，回收时另停协程清速度；`Entity` 新增 `ClearBattleTimers()`；`Die()` 改走 `ObjectPoolManager.Release`（非池对象回退 Destroy）|
| 2026-08-17 | C13-C15 | 随 `cn.unity.uos.launcher` 包整体移除处置（官方包自动导入产物，项目零调用，手改会被覆盖）：manifest.json 删依赖、删 `Assets/UOSLauncherEncrypt/`、清 `UNITY_UOS_SECURITY` 宏、更新 CLAUDE.md |
| 2026-08-17 | H9 | 随 C9 修复：`DriveTimers` 驱动 Tick + `RemoveAll` 清理，timers 列表不再只增不减 |
| 2026-08-17 复核确认 | C4 | `Entity` 基类已加 `[RequireComponent(typeof(Rigidbody2D))]`，`rb` 保证存在，NRE 风险消除 |
| 2026-08-17 复核确认 | M6 | `Player.OnDestroy` 已配对退订 `OnLocalTimeScaleChanged` |
| 2026-08-17 复核确认 | M12 | PlayerControllerData 废弃字段已随重构清除，现存字段均被消费 |
| 2026-08-17 复核确认 | M14 | SFXManager Acquire/Release 池计数逻辑已修正 |
| 2026-08-17 复核确认 | L13 | BloodParticle `existTime` 默认 0.75f + 索引 Clamp 保护 |
| 2026-08-18 复核确认 | M22 | `Damage.damage` 已为 float，`PlayerBulletTime.cs:230` 直接传 `Thrust.ThrustDamage`（float），无 `(int)` 截断 |
| 2026-08-18 复核确认 | N1 / L6 | `LocomotionComponent.Velocity` 默认值已改 `1f`（LocomationComponent.cs:12）— Component 层移动恒为 0 的故障消除；隐式倍率设计保留（`ApplyJump` 等仍在消费）|

### 随重构消失（无需修复）

| 编号 | 原因 |
|------|------|
| L3 | PlayerHealth 重构后继承链不再含基类 Heal()，"重复基类逻辑"前提不存在 |
| L5 | EntityLocomotion 已删除，墙壁检测改为 `WallDetectionCollider.IsTouchingLayers(...)`，Gizmo 直接绘制同一 Collider 的 bounds，物理与可视共用同一数据 |
| H11 | StartThrust 已改走 `LocomotionComponent.ForceMove(thrustForce)`（PlayerThrust.cs:41），不再直接写 velocity；`ExecuteChain` 亦无 velocity 覆盖，"双重设置速度"前提不存在 |

---

## 建议修复优先级

1. **立即修复**: **N2（键位资产加载路径错误，Inspector 改键位静默失效）**
2. **尽快修复**: H7-H8、H10、H13-H18（逻辑/内存泄漏）
3. **计划修复**: M1-M3、M5、M7-M11、M13、M15-M21（设计改进）
4. **技术债务**: L1-L2、L4、L7-L12（代码质量）
