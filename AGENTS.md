# AGENTS.md

Unity **2022.3.15f1c1**（URP）2D 横版动作学习项目。与用户**中文交流，代码注释也用中文**。

**协作模式**：新系统先出方案 md（放根目录、对齐现有命名），用户手写代码/资产；小修与排障可直接改。**force push 由用户本人执行**。git（remote: `bbbushi/Frame-project`）与 Plastic SCM 并存。根目录各设计方案 md、`docs/`、`项目模块说明.md` 只留本地，不入 git。

## 必读文档（改对应系统前先读）

- `README.md` — 顶层目录结构与核心约定
- `项目模块说明.md` — 各模块是什么/在哪/怎么工作（面向人的总览）
- `docs/` — 分模块深度文档：MOMS管理器框架 / 实体与组件模型 / 打击盒与动作系统 / GAS属性系统 / 玩家系统 / 敌人AI / UI框架 / 引导系统 / 游戏流程系统 / 支撑服务
- `CLAUDE.md` — 与本文件同源，含离线编译完整脚本

## 常用命令

无传统 build/lint——Unity 打开即编译。

- **离线全项目编译验证**（不开 Unity、Unity 被占用时也可用）：本机 Mono csc 全量编 `Assets/**/*.cs`。完整脚本见 `CLAUDE.md`。坑：`-noconfig -nostdlib` 必须放命令行（进 rsp 被忽略）；引擎目录只 glob `Managed/UnityEngine/`，**别挂 `Managed/UnityEditor.dll`**（CS0433 重复类型）；缺依赖（CS0246）去 `Library/ScriptAssemblies/` 补包程序集 dll。
- **测试 = 编辑器菜单冒烟**（非 Play 可跑，static 方法可直接调）：`Attributes/Effect冒烟测试（纯逻辑）`、`Attributes/冒烟测试（纯逻辑）`、`Guide/冒烟测试（纯逻辑）`、`UI/校验注册表`、`UI/导航冒烟（纯逻辑）`、`UI/布局冒烟（纯逻辑）`、`Flow/校验场景注册表`、`Flow/流程冒烟（纯逻辑）`。
- **查 Unity Console**：读 `~/Library/Logs/Unity/Editor.log`（含堆栈行号；与磁盘源码对质可判断 stale 程序集——对比 `Library/ScriptAssemblies/Assembly-CSharp.dll` 与源文件 mtime）。

## 架构铁律

- **MOMS 管理器框架**（`Assets/Script/GameManager.cs`）：纯 C# 类实现 `IManager` 即被反射扫描注册，**namespace 必须是 `Managers` / `Managers.*` / `InputComponent.*`**——写错 = 永不初始化且无报错（本项目第一大坑）。依赖走 `Dependencies`（Kahn 拓扑排序），访问走静态门面 `private static X Resolve => GameManager.Instance?.GetManager<X>();`。范本：`SFXManager`。`IUpdatable` 只在初始化时收集，中途补接口不生效。
- **场景零摆放**：场景里只有挂 GameManager 的 prefab，一切运行时装配。纯 C# 管理器的 `[SerializeField]` 永远是 null → 引用一律 `Resources.Load`；全部配置 SO 在 `Assets/Resources/data/`（运行时只读，不回写配置）。无项目级 asmdef（全在 Assembly-CSharp，`Editor/` 子目录进 Assembly-CSharp-Editor）；对话包 `com.otus.dialogue` 是独立程序集（Dialogue.dll）。
- **Entity.FixedUpdate 手动接线**：`Entity` 在 `FixedUpdate` 里逐个调用组件 `RefreshFixedUpdate`（detection/locomotion/damage/healthManage）——给组件加新 Refresh 逻辑必须同时在 Entity/Player 里接线，漏了 = 静默不运行。
- **GAS 属性管线**（`Assets/Attribute/Core/`，参考 UE GAS）：一切数值修改走 `AttributeSet.Modify → Apply` 单管线：clamp → 判变（没变不发事件）→ `Changed`/静态总线 `AnyChanged` 事件，参数带 old/new/delta/owner/`ChangeContext{source, reason}`。表现层（血条/飘字）**只订阅事件**、按 reason 白名单过滤，不与战斗逻辑互调。效果时钟 dt 必须用 `FixedFrameInterval`（fixedDeltaTime×LocalTimeScale），**不是 `TimeScale`**（那是倍率，传错 = 时钟快 ~50 倍）。死亡语义：`IsDead` 后负增量被拒；真复活走 `Revive`（清效果+回满）。
- **UI 分层**（`Assets/UISystem/`）：ScreenSpaceOverlay Canvas 四层 `UILayer`——HUD(10)/Panel(20)/Guide(40)/Overlay(200)，对话插件自建 100；统一 CanvasScaler 1280×720 match 0.5。面板注册表 `Resources/data/ui/UIManagerConfig`。转场黑幕用 `Assets/Script/Tools/UIFade.cs`（旧的 FadeManager 已删）。
- **战斗数据流**：`Hitbox` 结算命中 → `Entity.Hit` 格挡/无敌分流 → `DamageComponent` 播受击表现 → 数值落地走 `HealthManageComponent.ApplyDamage`（内部即 AttributeSet 管线）。敌人池化复用：`OnSpawnFromPool → Revive`。

## 已知坑（排障优先查）

- **MenuItem 回调（编辑器上下文）里 Instantiate 的 UGUI 对象"活着但不渲染"**——Awake/Update 照跑、CanvasRenderer 不出几何体、零警告。调试菜单要出可渲染对象必须延迟一帧（协程 `yield return null` 后再执行）。纯数据操作（改属性/挂效果）不受影响。
- **TMP 思源字体**（`SourceHanSansSC-Medium SDF`）：烘焙字符表不全 + 源字体 CFF OTF 运行时动态栅格化静默产出空白字形 → 未烘焙字符隐形零警告。DamageNumber 飘字已定稿用 LiberationSans SDF（纯数字够用）。
- 排障三层验证：Editor.log 堆栈对质 + 离线编译 + 纯逻辑冒烟（手喂 dt）；纯 C# 系统可复制进 /tmp 写 UnityEngine 垫片离线复现。
