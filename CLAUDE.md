# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目与环境

Unity **2022.3.15f1c1**（URP，装在 `/Volumes/Tool/Unity/2022.3.15f1c1`）的 2D 横版动作学习项目。目录结构与战斗数据流见根目录 `README.md`（已维护）；各系统设计方案在根目录《XX设计方案.md》（**只留本地不入 git**）。版本管理 git（remote: `bbbushi/Frame-project`）+ Plastic 并存；**force push 由用户本人执行**。

**协作模式**：新系统先出方案 md（根目录，对齐现有命名），用户手写代码/资产；小修与排障可直接改。用户中文交流，代码注释也用中文。

## 常用命令

无传统 build/lint——Unity 打开即编译。开发期主要用：

**离线全项目编译验证**（不开 Unity、Unity 占用时也可用；本机 Mono 的 csc）：

```bash
cd /Volumes/workspace/UnityProject/Project
R="/Volumes/Tool/Unity/2022.3.15f1c1/Unity.app/Contents"
: > /tmp/refs.rsp
echo "-r:\"$R/NetStandard/ref/2.1.0/netstandard.dll\"" >> /tmp/refs.rsp
for d in "$R/Managed/UnityEngine/"*.dll; do echo "-r:\"$d\"" >> /tmp/refs.rsp; done
for a in UnityEngine.UI Unity.TextMeshPro Cinemachine Unity.Timeline Dialogue Dialogue.Interaction; do
  echo "-r:\"Library/ScriptAssemblies/$a.dll\"" >> /tmp/refs.rsp; done
find Assets -name '*.cs' | sed 's/.*/"&"/' > /tmp/srcs.rsp
/Library/Frameworks/Mono.framework/Versions/Current/bin/csc -t:library -nologo -noconfig -nostdlib \
  -nowarn:CS0103,CS0649,CS0618,CS0108,CS0660,CS0661,CS0219,CS0169,CS0414 \
  @/tmp/refs.rsp @/tmp/srcs.rsp -out:/tmp/fullcheck.dll
```

坑：`-noconfig -nostdlib` 必须放命令行（进 rsp 会被忽略）；引擎目录只 glob `Managed/UnityEngine/`（含模块化 UnityEditor.*Module），**别挂 `Managed/UnityEditor.dll`**（CS0433 重复类型）；UGUI/TMP/Dialogue 是包程序集，在 `Library/ScriptAssemblies/`；CS0246 缺依赖就从那里补。Cinemachine 在本项目里叫 `Cinemachine.dll`（无 Unity 前缀）。

**测试** = 编辑器菜单冒烟（非 Play 可跑，static 方法可直接调）：`Attributes/Effect冒烟测试（纯逻辑）`、`Attributes/属性冒烟`、`Guide/引导冒烟`、`UI/校验注册表`。

**查 Unity Console**：读 `~/Library/Logs/Unity/Editor.log`（含堆栈行号，可与磁盘源码对质判断 stale 程序集；`Library/ScriptAssemblies/Assembly-CSharp.dll` 的 mtime 对比源文件 mtime）。

## 架构要点（读多个文件才能拼出的部分）

**MOMS 管理器框架**（`GameManager.cs`）：纯 C# 类实现 `IManager`，**namespace 必须是 `Managers` / `Managers.*` / `InputComponent.*` 才被反射扫描注册**（写错 = 永远不初始化，无报错）；`Dependencies` 声明依赖，Kahn 拓扑排序初始化。访问走静态门面：`private static X Resolve => GameManager.Instance?.GetManager<X>();`。`IUpdatable` 只在初始化时收集，中途补接口不生效。范本：`SFXManager`。

**场景零摆放 + 资源约定**：场景里只有挂 GameManager 的 prefab，一切运行时装配。纯 C# 管理器的 `[SerializeField]` 永远是 null → 引用一律 `Resources.Load` 或配置 SO；所有配置 SO 在 `Resources/data/`。无项目级 asmdef（全在 Assembly-CSharp，`Editor/` 子目录进 Assembly-CSharp-Editor）；对话插件 com.otus.dialogue 是独立程序集（Dialogue.dll）。

**GAS 属性/效果管线**（`Assets/Attribute/Core/`，参考 UE GAS，已完成一期+二期、A 层未动）：
- 一切数值修改走 `AttributeSet.Modify → Apply` 单管线：clamp → 判变（没变不发事件）→ `Changed`/静态总线 `AnyChanged` 事件，参数带 old/new/delta/owner/`ChangeContext{source, reason}`。表现层（飘字/血条）**只订阅事件**，白名单过滤 reason（飘字只认 Damage/Heal）。
- `GameplayEffect` SO：Instant / Duration+Period 双通道（`modifiers` 持续修饰到期按落地 delta 精确回退；`periodModifiers` 每跳即时不回退）。Additive-only。
- **效果时钟驱动链**：`Entity.FixedUpdate → healthManageComponent.RefreshFixedUpdate → TickEffects(FixedFrameInterval)`。dt 必须是帧间隔（`FixedFrameInterval` = fixedDeltaTime×LocalTimeScale），**不是 `TimeScale`**（那是倍率，传错 = 时钟快 ~50 倍）。
- 死亡语义：`IsDead` 后负增量被拒；Heal 正增量放行（"药水复活"不清效果），真复活走 `Revive`（清效果+回满）。

**Entity / 组件**：`Entity`（Player/Enemy 共基类）在 `FixedUpdate` 里**手动逐个**调用组件的 `RefreshFixedUpdate`（detection/locomotion/damage/healthManage）——**给组件加新 Refresh 逻辑必须同时记得在 Entity/Player 里接线**，漏了 = 静默不运行（飘字/毒两案皆此）。敌人池化复用：`OnSpawnFromPool → Revive`。

**UI 分层**（`UISystem`）：`UIManager.BuildLayerCanvas` 建三层 ScreenSpaceOverlay Canvas——HUD(sort 10) / Panel(20) / Guide(40)，对话插件自建 100；统一 CanvasScaler 1280×720 match 0.5。面板注册表 `Resources/data/ui/UIManagerConfig`。

## 已知坑（排障优先查）

- **MenuItem 回调（编辑器上下文）里 Instantiate 的 UGUI 对象"活着但不渲染"**——Awake/Update 照跑、CanvasRenderer 不出几何体、零警告。调试菜单要出可渲染对象必须延迟一帧（协程 `yield return null` 后再执行，见 `UIDebugMenu.ShowNextFrame`）。纯数据操作（改属性/挂效果）不受影响。
- **TMP 思源字体**（`SourceHanSansSC-Medium SDF`，对话包）：烘焙字符表不全 + 源字体是 CFF OTF（运行时动态栅格化静默产出空白字形）→ 未烘焙字符隐形零警告。烘焙字符集原料在根目录 `思源SDF烘焙字符集.txt`。DamageNumber 飘字已定稿用 LiberationSans SDF（纯数字够用）。
- 排障手法：Editor.log 堆栈对质 + 离线编译 + 纯逻辑冒烟（手喂 dt）三层验证；纯 C# 系统可复制进 /tmp 写 UnityEngine 垫片离线复现。
