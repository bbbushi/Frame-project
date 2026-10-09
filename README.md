# Frame-project

Unity 2D 横版动作游戏（学习项目）。围绕一套自研管理器框架 **MOMS** 逐步搭出：战斗管线 → 新手引导 → UI 框架 → 属性系统（参考 UE GAS 思路改造中）。

> 环境：Unity **2022.3.15f1c1**（URP 渲染），克隆后用该版本打开即可。

## 顶层结构

| 目录 | 内容 |
|---|---|
| `Assets/Script/` | 游戏骨架：`GameManager.cs`（MOMS 入口）、`Entity.cs`（实体基类）、`Player/`（玩家 + 状态机）、`Enemy/` |
| `Assets/Component/` | 实体组件：`HealthManageComponent`（属性宿主）、`DamageComponent`（受击结算）、`PlayerAnimatorComponent`、移动/检测/输入等 |
| `Assets/Modules/` | 玩家功能模块：`PlayerCombat`、`PlayerBulletTime`（子弹时间）、`PlayerLocomotion`、`PlayerThrust` |
| `Assets/ActionSystem/` | 攻击侧：`Hitboxes`（打击盒）、`Damage`、`HitBoxConfig`、击退位移 |
| `Assets/Attribute/` | **GAS 一期**：`AttributeSet` 属性系统（Base/Current 分离、结构化变化事件、静态总线），替代散落的血量字段 |
| `Assets/UISystem/` | UI 框架：三层 Canvas（HUD/Panel/Guide）+ 面板生命周期 + 玩家血条 `HudPanel` + 伤害飘字 `FloatingTextManager` |
| `Assets/GuideSystem/` | 新手引导：引擎/步骤/命令回滚纯逻辑层 + UGUI 表现层，`Guide/` 菜单可跑纯逻辑冒烟测试 |
| `Assets/Pool System/` | 对象池（`ObjectPoolManager`，敌人回收复用走这条链） |
| `Assets/Time System/` | 时间缩放（配合子弹时间） |
| `Assets/SFX&Music System/` | 音效/音乐管理器、脚步声表面配置 |
| `Assets/Effect/` | 受击表现：血粒子等 |
| `Assets/Config/` | 实体配置 SO（`EntityCharacterConfig`：血量/攻击力等数据源） |
| `Assets/SavingSystem/` | 存档 |

## 核心约定

- **MOMS 管理器框架**：管理器实现 `IManager`（放 `Managers` / `Managers.*` 命名空间即被自动扫描注册），`Dependencies` 声明依赖、Kahn 拓扑排序决定初始化顺序，静态门面 `X.Resolve` 全局访问。纯 C# 管理器不进场景——本项目**场景零摆放**，一切由 `GameManager` 启动时装配。
- **战斗数据流**：`Hitbox` 结算命中 → `Entity.Hit` 做格挡/无敌分流 → `DamageComponent` 播受击表现 → 数值落地走 `HealthManageComponent.ApplyDamage`（内部即 `AttributeSet` 管线：clamp → 判变 → 发事件）。血条/飘字**订阅**属性变化事件（`Changed` / 静态总线 `AnyChanged`），不与战斗逻辑互相调用。
- **配置单向**：数值源头是 `Resources/data/` 下的 SO，实体 `Init` 时灌入属性集，运行时只读属性、不回写配置。

## 版本管理

- 本地用 **Plastic SCM**，远端同步到 GitHub，两者并存（`.plastic/` 已被 git 忽略，互不干扰）。
- 设计/学习文档（根目录各 `*.md`）只留本地，不入 git。

## 调试菜单（Unity 顶部菜单栏）

- `Guide/` — 引导冒烟测试（纯逻辑，非 Play 可跑）、开始/重置引导
- `Attributes/` — 属性系统冒烟测试（纯逻辑，非 Play 可跑）
- `UI/调试/` — 玩家受伤/治疗/测试飘字
