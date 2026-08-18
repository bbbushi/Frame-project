# Frame Project

Unity 2D 平台动作游戏项目 —— 采用领域驱动模块化架构，具备完整的玩家移动、跳跃、突刺、攻击连击、血量管理等核心玩法系统。

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

> 项目中无自动化 CI/CD 脚本，所有构建通过 Unity 编辑器完成。

## 项目结构

```
Assets/
├── MANAGER/                    # Manager 层 — 全局服务管理
│   └── managers/
│       └── TimeManager.cs      # 时间缩放管理器（冻结帧、子弹时间、暂停）
├── GameManager.cs              # 全局单例，Manager 生命周期调度
├── Interface/                  # 核心接口定义
│   ├── Interface_Manager.cs    # IManager / IUpdatable 接口
│   └── IEvent.cs              # 事件标记接口（预留）
├── InputComponent/             # 输入系统
│   ├── InputManager.cs         # 输入管理器（键位绑定 + 状态缓存）
│   ├── PlayerAction.cs         # 玩家动作枚举
│   ├── PlayerInputController.cs# 输入 → 命令映射（Manager）
│   └── Commands/               # 命令模式
│       ├── IPlayerCommand.cs   # 命令接口
│       ├── JumpCommand.cs      # 跳跃命令
│       ├── AttackCommand.cs    # 攻击命令
│       └── ThrustCommand.cs    # 突刺命令
├── Player_DomainModules/       # Player 领域模块层
│   ├── Player.cs               # Player 宿主（组装根）
│   ├── PlayerStateMachine.cs   # 轻量级 FSM 状态机
│   └── PLAYERSTATE/
│       ├── PlayerState.cs      # 状态基类
│       ├── PlayerGroundState.cs# 地面状态基类
│       ├── PlayerIdleState.cs  # 待机状态
│       ├── PlayerMoveState.cs  # 移动状态
│       ├── PlayerJumpState.cs  # 跳跃状态
│       ├── PlayerAirState.cs   # 空中状态
│       ├── PlayerAttackState.cs# 攻击状态
│       ├── PlayerThrustState.cs# 突刺状态
│       ├── PlayerDeathState.cs # 死亡状态
│       └── Modules/            # 领域模块
│           ├── PlayerModule.cs     # 模块基类
│           ├── PlayerHealth.cs     # 血量模块
│           ├── PlayerLocomotion.cs # 移动/跳跃/物理检测
│           ├── PlayerCombat.cs     # 战斗/连击模块
│           └── PlayerThrust.cs     # 突刺模块
└── Config/DATA/                # ScriptableObject 配置
    ├── PlayerCharacterData.cs  # 角色数据配置
    ├── PlayerControllerData.cs # 控制器参数配置
    └── PlayerInputData.cs      # 键位绑定配置
```

## 核心架构

项目遵循 **领域驱动模块化架构**，通过以下三层组织代码：

### 1. Manager 层

`GameManager` 是全局单例（`DontDestroyOnLoad`），负责所有 `IManager` 的生命周期管理：

- **自动扫描**：通过反射扫描所有实现 `IManager` 的类
- **依赖解析**：使用 **拓扑排序（Kahn 算法）** 按 `IManager.Dependencies` 解析初始化顺序
- **逐帧驱动**：实现 `IUpdatable` 的 Manager 在每帧 `Update` 中驱动
- **安全销毁**：销毁时逆序调用 `Deinitialize()`
- **访问方式**：`GameManager.Get<InputManager>()` 或静态 `GameManager.Get<T>()`

| Manager | 职责 |
|---|---|
| `InputManager` | 键位绑定加载/查询（`IsPressed` / `IsHeld` / `IsReleased`），移动输入合成 |
| `PlayerInputController` | 输入→命令映射，依赖 `InputManager`，每帧驱动命令执行 |
| `TimeManager` | 全局时间缩放，支持冻结帧、慢动作（子弹时间）、暂停、调试倍率 |

### 2. Player 领域模块层

`Player`（`Frame.Player` 命名空间）是模块宿主和组装根（composition root）：

- 缓存 Unity 组件引用（Rigidbody2D、Animator、Collider2D、SpriteRenderer），模块和状态可直接访问
- 在 `Awake()` 中通过 `Bind(this)` 注入模块，从 ScriptableObject 加载配置
- 每帧按固定顺序驱动：物理检测 → 冷却更新 → 状态机 Update

**领域模块**（继承 `PlayerModule` 基类）：

| 模块 | 职责 |
|---|---|
| `PlayerHealth` | 血量管理、受伤、治疗、复活，通过 C# event 对外通知（`OnDied`、`OnChanged`、`OnRevived`） |
| `PlayerLocomotion` | 水平移动、跳跃（支持二段跳）、地面/墙壁 OverlapBox 检测、朝向翻转 |
| `PlayerCombat` | 攻击伤害、连击窗口、攻击位移（连击逻辑部分处于早期开发） |
| `PlayerThrust` | 突刺力度/持续/冷却计时、重力归零协程、方向控制 |

**状态机**：轻量级 FSM，无 MonoBehaviour 依赖：
- 继承链：`PlayerState` → `PlayerGroundState`（Idle / Move）和 `PlayerAirState`（Jump）
- 状态通过 `player.xxx` 直接访问所有模块
- 状态切换通过 `PlayerStateMachine.ChangeState()` 驱动，Enter/Exit 时控制 Animator 的 bool 参数

**状态转换图**：

```
                    +──→ DeathState (Health.OnDied 事件触发)
                    |
  [IdleState] ←────+────→ [MoveState]    (有/无水平输入)
       |            |          |
       |  (不在地面)           (不在地面)
       v                       v
  [JumpState] ──────→ [AirState] ──→ [IdleState] (落地 + vy≈0)
       |                  |
       | (vy ≤ 0)         | (IsTouchingWall → WallSlide, 待实现)
       v                  |
  [AirState]              |
                          |
  [ThrustState] (从 Move/Idle/Air 通过 ThrustCommand 触发)
       |
       +──→ IdleState (突刺结束后在地面)
       +──→ AirState  (突刺结束后在空中)

  [AttackState] (从任意 GroundState 通过 AttackCommand 触发)
       |
       +──→ IdleState (动画结束回调)

  [DeathState] (从任意状态通过 Health.OnDied 事件触发)
       |
       +──→ 场景重载 (1.5s 后)
```

**命令模式**：`PlayerInputController` 将输入事件映射为命令：
- `IPlayerCommand` 接口：`CanExecute(Player)` → `Execute(Player)`
- 命令直接读取模块状态做前置判断，然后触发状态机转换
- 当前命令：`JumpCommand`、`AttackCommand`、`ThrustCommand`

### 3. 配置层

ScriptableObject 配置资产，数据与逻辑分离：

| 配置 | 内容 |
|---|---|
| `PlayerCharacterData` | 最大血量、攻击伤害 |
| `PlayerControllerData` | 移动速度、跳跃力、突刺参数（力度/冷却/持续/伤害） |
| `PlayerInputData` | 键位绑定列表（`List<ActionBinding>`），通过 `Resources.Load` 加载 |

## 默认键位

| 动作 | 按键 |
|---|---|
| 左移 | A |
| 右移 | D |
| 跳跃 | Space |
| 突刺 | S |
| 攻击 | 鼠标左键 |

键位可通过 `PlayerInputData` ScriptableObject 在 Inspector 中自定义配置。

## 设计约定

- **函数名**：使用 C# 标准 PascalCase，不做中文化
- **ScriptableObject 配置**：通过 `CreateAssetMenu` 生成，存放于 `Assets/Config/DATA/data/`
- **动画参数**：Animator 使用 bool 参数（如 "Idle"、"Move"、"Jump"）控制状态切换
- **Player 单例入口**：外部通过 `Player.instance` 访问，避免重复查找
- **模块间通信**：优先使用 C# event（如 `Health.OnDied`），避免模块间直接相互依赖
- **命名空间**：Player 相关代码在 `Frame.Player`，Manager 在 `Managers`

## 扩展新能力

为 Player 添加新能力（技能/动作）的标准流程：

1. 在 `Modules/` 下创建新 `PlayerModule` 子类
2. 在 `PLAYERSTATE/` 下创建新 `PlayerState`
3. 在 `Commands/` 下创建新 `IPlayerCommand` 实现
4. 在 `PlayerAction` 枚举中注册新动作
5. 在 `Player.cs` 中注册模块和状态实例
6. 在 `PlayerInputController.BuildCommands()` 中注册命令映射
7. 在 `PlayerControllerData` / `PlayerCharacterData` 中添加配置字段（按需）
8. 在 `PlayerInputData` 中配置键位绑定

## 外部依赖

| 包 | 用途 |
|---|---|
| `cn.unity.uos.launcher` | 中国 Unity 服务启动器（Git URL） |
| `com.unity.purchasing` | Unity IAP 应用内购买 |
| `com.unity.ads` | Unity Ads 广告 |
| `com.unity.analytics` | Unity Analytics 分析 |
| `com.unity.feature.2d` | 2D 功能套件（Sprite、Tilemap、Animation、SpriteShape、Pixel Perfect） |
| `com.unity.textmeshpro` | TextMeshPro 文本渲染 |
| `com.unity.collab-proxy` | Plastic SCM 版本控制集成 |

## 附加文档

- [CLAUDE.md](CLAUDE.md) — 项目架构详细指南（面向 AI 编程助手）
- `.claude/skills/add-player-ability/` — Player 能力扩展技能（含代码模板与检查清单）

## 开发状态

- [x] 玩家基础移动与跳跃（含二段跳）
- [x] 突刺（Thrust）— 短距位移 + 伤害
- [x] 攻击命令框架
- [x] 血量系统（受伤/治疗/复活/死亡）
- [x] Manager 生命周期管理（拓扑排序依赖注入）
- [x] 输入系统（可配置键位绑定）
- [x] 时间管理器（冻结帧/慢动作/暂停）
- [ ] 攻击连击完整逻辑
- [ ] 敌人 AI 系统
- [ ] UI/HUD 系统
- [ ] 关卡设计
