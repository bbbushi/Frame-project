# Frame Project

Unity 2D 平台动作游戏项目 —— 采用领域驱动模块化架构，具备完整的移动、二段跳、突刺、攻击、子弹时间 + 链式处决、血量管理、受击反馈等核心玩法系统。

## 环境要求

| 依赖 | 版本 |
|---|---|
| Unity 编辑器 | **2022.3.15f1c1** (LTS) |
| C# / .NET | Unity 内置 |
| 版本控制 | Plastic SCM |

## 快速开始

1. 用 **Unity Hub** 打开项目根目录
2. 等待 Unity 自动解析并下载依赖包
3. 打开 `File → Build Settings` 切换目标平台后构建
4. 在编辑器中直接点击 **Play** 运行

> 项目中无自动化 CI/CD 脚本与测试套件，所有构建、验证均通过 Unity 编辑器完成。

## 项目结构

```
Assets/
├── Entity.cs                      # Entity 抽象基类（所有角色），实现 iDamagable
├── Faction.cs                     # 阵营枚举 + 扩展方法（Contains/Hostile/GetHostileFaction）
├── Effect/                        # 血液粒子特效（生成器/粒子/示例）
├── Resources/data/                # ScriptableObject 配置资产（运行时 Resources.Load）
│   ├── PlayerCharacterData.asset  #   角色数据（最大血量/攻击伤害）
│   ├── PlayerController.asset     #   控制器参数（跳跃/突刺/子弹时间）
│   ├── PlayerInputData.asset      #   键位绑定
│   ├── SFXManagerConfig.asset     #   音效库 + 池参数
│   ├── HitBoxData.asset           #   碰撞箱参数
│   └── Displacement/              #   位移曲线（攻击前冲/突刺/受击击退）
└── Script/                        # 所有游戏代码，按领域分层
    ├── GameManager.cs             # 全局单例：Manager 生命周期调度（MOMS 容器）
    ├── Singleton.cs               # 泛型 MonoBehaviour 单例基类
    ├── Interface/                 # IManager / IUpdatable / IPoolable
    ├── Component/                 # Entity 组件层（挂 Entity 子物体）
    │   ├── EntityComponent.cs     #   组件基类（Owner/TimeScale/Init/Interrupt）
    │   ├── DetectionComponent.cs  #   物理 detection（地面/墙壁/平台穿透/攻击挂点）
    │   ├── LocomotionComponent.cs #   移动物理（位移/翻转/ForceMove 击退）
    │   ├── HealthManageComponent.cs # 血量（OnChanged/OnDied/OnRevived 事件）
    │   ├── DamageComponent.cs     #   受击反应（打断/击退/受击动画/血液粒子）
    │   ├── PlayerModuleControlComponent.cs # Player 模块宿主
    │   └── PlayerAnimatorComponent.cs      # 状态机宿主
    ├── Player/                    # Player 组装根 + 状态机
    │   ├── Player.cs              #   Player : Entity（组装根，Frame_Player 命名空间）
    │   ├── PlayerStateMachine.cs  #   轻量 FSM（含 TranState 过渡机制）
    │   └── PLAYERSTATE/           #   所有 PlayerState
    ├── Modules/                   # Player 领域模块（纯 C#，[Serializable]）
    │   ├── EntityModule.cs        #   模块基类（Owner/Bind）
    │   ├── PlayerModule.cs        #   玩家模块基类（player 强类型引用）
    │   ├── PlayerLocomotion.cs    #   跳跃管理（二段跳）+ 移动输入
    │   ├── PlayerCombat.cs        #   攻击/连击
    │   ├── PlayerThrust.cs        #   突刺（ForceMove + 重力归零）
    │   ├── PlayerBulletTime.cs    #   子弹时间/标记/链式处决
    │   └── PlayerHealth.cs        #   空壳（血量已下沉到 HealthManageComponent）
    ├── InputComponent/            # 输入系统
    │   ├── InputManager.cs        #   键位绑定 + 每帧状态缓存（MOMS Manager）
    │   ├── PlayerAction.cs        #   动作枚举（8 个值）
    │   ├── PlayerInputController.cs # 输入 → 命令映射，双模式（MOMS Manager）
    │   └── Commands/              #   命令模式（IPlayerCommand）
    ├── ActionSystem/              # 攻击与伤害系统
    │   ├── Hitboxes/              #   Hitbox 基类 + 近战/子弹/爆炸
    │   ├── Damage.cs              #   伤害数据结构
    │   ├── Displacement.cs        #   位移曲线配置
    │   └── ActionIgnoreMask.cs    #   动作忽略掩码（受击硬直互斥）
    ├── Config/                    # ScriptableObject 配置类定义
    ├── Time/                      # TimeManager（时间缩放）+ Timer
    ├── SFX/                       # SFXManager（音效池化）
    ├── Camera/                    # PlayerCameraComponent（跟随）+ CameraShaker（抖动）
    ├── Enemy/                     # 测试敌人（支持对象池/子弹时间标记）
    ├── pool/                      # ObjectPoolManager（敌人）+ ShadowPool（残影）
    └── Tools/                     # MathTools / LayerMaskPreset / FPSCounter 等
```

## 核心架构

项目遵循 **领域驱动模块化架构**，自下而上分四层：

### 1. Manager 层（MOMS 模式）

`GameManager` 是全局单例（`DontDestroyOnLoad`），负责所有 `IManager` 的生命周期管理：

- **自动扫描**：反射扫描 `Managers` / `InputComponent` 命名空间下所有 `IManager` 实现并实例化
- **依赖解析**：**拓扑排序（Kahn 算法）** 按 `IManager.Dependencies` 确定初始化顺序
- **异步初始化**：`Initialize()` 返回 `IEnumerator`，协程分帧加载，单点失败不影响其余
- **逐帧驱动**：实现 `IUpdatable` 的 Manager 每帧被驱动
- **安全销毁**：逆序调用 `Deinitialize()`
- **访问方式**：`GameManager.Get<InputManager>()`

| Manager | 职责 | 依赖 |
|---|---|---|
| `InputManager` | 键位绑定加载 + pressed/held/released 状态缓存，移动输入合成，全局输入开关 | 无 |
| `PlayerInputController` | 输入→命令映射；正常/子弹时间双模式 | `InputManager` |
| `TimeManager` | 全局时间缩放（帧冻结/慢动作/暂停/调试），驱动全局 Timer 列表 | 无 |
| `SFXManager` | 音效池化播放，脚步声表面映射 | 无 |

另有三个独立 MonoBehaviour 单例（不经 MOMS）：`ObjectPoolManager`（敌人对象池）、`ShadowPool`（残影池）、`BloodParticleGenerator`（血液粒子）。

### 2. Entity 组件层（所有角色共用）

`Entity`（[Assets/Entity.cs](Assets/Entity.cs)）是所有角色的抽象基类（Player、Enemy 均继承），实现 `iDamagable`。**`Awake()` 自动查找并初始化四个子组件** —— 任何 Entity 子类在子物体上挂组件即获得对应能力：

| 组件 | 职责 |
|---|---|
| `DetectionComponent` | 地面/墙壁/平台穿透检测，攻击挂点（attackSocket）|
| `LocomotionComponent` | 移动物理：`ApplyHorizontal`/`Flip`/`ForceMove`（Displacement 曲线驱动的强制位移，击退/前冲/突刺统一入口）|
| `HealthManageComponent` | 血量：TakeDamage/Heal/Revive + C# 事件（OnChanged/OnDied/OnRevived）|
| `DamageComponent` | 受击反应：打断 + 受击动画 + 沿冲击方向击退 + 动作忽略 + 血液粒子 |

Entity 还提供：阵营系统、无敌/格挡计时、动作忽略窗口（`AddIgnore`）、本地时间缩放（自动叠乘 `TimeManager.GlobalTimeScale`）、视线检测、`Interrupt()` 递归中断。

**完整受击管线**：`Hitbox` 检测目标 → `iDamagable.Hit(Damage)` → `Entity.Hit()`（格挡/无敌前置检查）→ `DamageComponent.Hit()`（打断 + 击退 + 血液粒子）。

### 3. Player 领域模块层

`Player`（命名空间 `Frame_Player`，注意是下划线）继承 `Entity`，是模块宿主和组装根（composition root）。五个 `[Serializable]` 纯 C# 模块由 `PlayerModuleControlComponent` 持有：

| 模块 | 职责 |
|---|---|
| `PlayerLocomotion` | 跳跃管理（二段跳 `maxJumps=2`）、`SetMoveInput`（含朝向翻转）|
| `PlayerCombat` | 攻击伤害、`HitDetect()` 生成 Hitbox（动画事件触发）、攻击前冲 |
| `PlayerThrust` | 突刺：`ForceMove` 曲线位移 + 重力归零协程 + 冷却 |
| `PlayerBulletTime` | 子弹时间（瞄准弧/标记敌人/链式处决：瞬移+贯穿+残影）|
| `PlayerHealth` | 空壳（血量已下沉到 Entity 层 `HealthManageComponent`）|

模块间通过 C# 事件通信（如 `healthManageComponent.OnDied → DeathState`），避免直接相互依赖。

### 4. 状态机层

轻量级 FSM（`PlayerStateMachine`），无 MonoBehaviour 依赖：

- 继承链：`PlayerState` → `PlayerGroundState`（Idle/Move）和 `PlayerAirState`（Jump/Air）
- 具体状态：Idle / Move / Jump / Air / Thrust / Attack / Execution / Death
- **过渡状态机制**：`ChangeState(state, animName)` 重载可动态创建 `PlayerTranState`（播放过渡动画后自动进入目标状态）；每个状态还可配置 `SetEnterState`/`SetExitState` 进入/退出子状态链
- Animator 用 bool 参数（Idle/Move/Jump/Thrust/Attack/Execution/Death）驱动状态表现；动画结束通过动画事件回调 `AnimEnd()`

**命令模式**：`PlayerInputController` 将输入映射为 `IPlayerCommand`（`CanExecute(Player)` → `Execute(Player)`），当前有 `JumpCommand`、`AttackCommand`、`ThrustCommand`。

**双模式输入**：

- 正常模式：移动 + 跳跃/突刺/攻击 + 右键进入子弹时间
- 子弹时间模式：移动/跳跃正常；鼠标移动控制瞄准角度；左键标记敌人；E 键链式处决；右键取消

### 配置层

ScriptableObject 配置资产（位于 `Assets/Resources/data/`），数据与逻辑分离：

| 配置 | 内容 |
|---|---|
| `PlayerCharacterData` | 最大血量、攻击伤害 |
| `PlayerControllerData` | 移动速度、跳跃力、突刺参数（力度/冷却/伤害）、攻击/突刺位移曲线、子弹时间参数 |
| `PlayerInputData` | 键位绑定列表（`List<ActionBinding>`）|
| `HitBoxConfig` | 伤害类型/冲击力/相机抖动/帧冻结 |
| `SFXManagerConfig` | 音效库 + AudioSource 池参数 + 脚步表面映射 |
| `Displacement` | 强制位移曲线（maxSpeed + length + speedCurve）|

## 默认键位

| 动作 | 按键 | 触发方式 |
|---|---|---|
| 左移 / 右移 | A / D | 按住 |
| 跳跃 | Space | 按下 |
| 突刺 | S | 按下 |
| 攻击 | 鼠标左键 | 按下 |
| 子弹时间（进入/取消）| 鼠标右键 | 按下 |
| 标记敌人（子弹时间中）| 鼠标左键 | 按下 |
| 链式处决（子弹时间中）| E | 按下 |

键位可通过 `PlayerInputData` ScriptableObject 在 Inspector 中自定义配置。

> ⚠️ 已知问题：`InputManager` 当前加载路径与资产实际位置不匹配，`.asset` 中的键位修改暂不生效（静默回退到硬编码默认键位，两者默认值恰好一致）。详见 [bug-list.md](bug-list.md) N2。

## 设计约定

- **通用能力放 Entity 组件层，玩家特有逻辑放 PlayerModule**：血量/移动物理/受击反应是所有角色共用的；跳跃、突刺、子弹时间只属于 Player
- **强制位移统一入口**：击退/攻击前冲/突刺一律走 `LocomotionComponent.ForceMove(Displacement, direction?)`
- **函数名**：C# 标准 PascalCase，不做中文化
- **单例分三种**：MOMS `IManager`（`GameManager.Get<T>()`）、`Player.Instance`、`Singleton<T>`（对象池等）
- **TimeManager 静态 API**：`TimeManager.SlowScale` / `FrameFreezeScale` 等，类似 `Time.timeScale`
- **模块间通信**：优先 C# 事件（如 `OnDied`），避免模块直接相互依赖
- **动画参数**：bool 参数控制状态，受击用 `SetTrigger("Hit")`，动画结束走动画事件回调

## 扩展新能力

为 Player 添加新能力（技能/动作）的标准流程（详见 `.claude/skills/add-player-ability/`，含代码模板与检查清单）：

1. 在 `Script/Modules/` 下创建新 `PlayerModule` 子类
2. 在 `Script/Player/PLAYERSTATE/` 下创建新 `PlayerState`
3. 在 `Script/InputComponent/Commands/` 下创建新 `IPlayerCommand` 实现
4. 在 `PlayerAction` 枚举中注册新动作
5. 在 `PlayerModuleControlComponent` 中注册模块（`Bind()` + `LoadConfig()`）
6. 在 `PlayerAnimatorComponent.Init()` 中创建状态实例
7. 在 `PlayerInputController.BuildCommands()` 中注册命令映射
8. 在 `PlayerControllerData` / `PlayerCharacterData` 中添加配置字段（按需）
9. 在 `PlayerInputData` 中配置键位绑定

若新能力是所有角色共用的（如新受击效果），优先加到 Entity 组件层而非 PlayerModule。

## 外部依赖

| 包 | 用途 |
|---|---|
| `com.unity.cinemachine` | 虚拟摄像机（PlayerCameraComponent）|
| `com.unity.feature.2d` | 2D 功能套件（Sprite、Tilemap、Animation、SpriteShape、Pixel Perfect）|
| `com.unity.textmeshpro` | 文本渲染 |
| `com.unity.purchasing` | Unity IAP 应用内购买 |
| `com.unity.ads` / `com.unity.analytics` | 广告与分析 |
| `com.unity.collab-proxy` | Plastic SCM 版本控制集成 |

## 附加文档

- [CLAUDE.md](CLAUDE.md) — 项目架构详细指南（面向 AI 编程助手，与代码同步维护）
- [bug-list.md](bug-list.md) — 已知问题清单（按严重度分级，含行号与修复记录）
- `.claude/skills/add-player-ability/` — Player 能力扩展技能（含代码模板与检查清单）

## 开发状态

- [x] 玩家基础移动与跳跃（含二段跳）
- [x] 突刺（Thrust）— Displacement 曲线位移 + 重力归零
- [x] 攻击（动画事件驱动 Hitbox 生成 + 攻击前冲）
- [x] 血量系统（受伤/治疗/复活/死亡，Entity 组件层）
- [x] 受击反馈（打断/击退/动作忽略/血液粒子）
- [x] 子弹时间 + 敌人标记 + 链式处决
- [x] Manager 生命周期管理（拓扑排序依赖注入）
- [x] 输入系统（可配置键位绑定 + 命令模式）
- [x] 时间管理器（帧冻结/慢动作/暂停 + 全局 Timer）
- [x] 音效管理（AudioSource 池化 + 脚步声表面映射）
- [x] 对象池（敌人 + 残影）
- [ ] 攻击连击完整逻辑
- [ ] 敌人 AI 系统
- [ ] UI/HUD 系统
- [ ] 关卡设计
