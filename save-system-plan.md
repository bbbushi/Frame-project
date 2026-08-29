# 存档功能 — 最小框架方案

> 状态：方案已获批，**代码尚未实现**。本文档是实现的依据。
> 范围：SaveManager + 数据模型 + 文件读写 API（最小框架），不接任何游戏流程。
> 后续阶段：存档点实体（参考空洞骑士长椅）→ 死亡重载 + 读档恢复 → UI。

## 背景与需求

项目目前**没有任何存档/持久化代码**（PlayerPrefs/JsonUtility/System.IO 全项目零命中），游戏状态全部存于场景与内存，进程退出即丢。已确认的需求：

1. **最终形态**：存档点实体（参考空洞骑士长椅：坐下 = 设重生点 + 保存 + 回血），死亡后重载场景并从存档恢复位置和血量
2. **首期范围**：仅最小框架 —— SaveManager + 数据模型 + 文件读写 API，**不接任何游戏流程**（不做存档点实体、不改死亡流程、不做 UI）
3. 现状约束：只有 1 个场景 `Assets/Scenes/1.unity`；可存数据极少（血量/位置/场景名，无收集品与技能系统）；伤害管线目前从未扣血（死亡实际不可达），但数据模型需为最终形态设计好

关键现状（来自代码探索）：

- MOMS Manager 模式是天然扩展点：`GameManager` 反射扫描 `Managers`/`InputComponent` 命名空间下的 `IManager` 自动实例化，**零注册成本**
- `HealthManageComponent.SetHP()` 的注释原文即"用于检查点恢复"——恢复血量的入口已预留
- 既有 bug：Build Settings 无启用场景，`GetActiveScene().buildIndex` 为 -1，死亡重载 `LoadScene(-1)` 会抛异常 → 数据模型**用场景名而非 buildIndex**（存档框架不修此 bug，见风险区）
- [TimeManager.cs](Assets/Script/Time/TimeManager.cs) 是静态 API + IManager 的参考模板

## A. 参考来源说明

| 参考游戏/来源 | 具体机制 | 学到/借鉴的设计 | 来源 |
|---|---|---|---|
| 空洞骑士 | 长椅（Bench）：坐下**自动**保存游戏 + 设为死亡/退出后的重生点 + 回满血 + 更新地图 + 重生已击败敌人（当前房间除外）；部分长椅需付 Geo 解锁。死亡回到最近长椅并掉落 Geo | **借鉴思路**：存档点 = 重生点 + 回血 + 快照捕获三合一，交互即保存（无确认弹窗）；数据模型按"在存档点捕获玩家快照"设计（位置/血量/朝向/场景），嵌套结构为未来"敌人重生/地图/收集品"子块留位 | [Hollow Knight Wiki – Bench](https://hollowknight.wiki/w/Bench_(Hollow_Knight))（官方 Wiki，已通读全文）、[Reddit r/HollowKnight](https://www.reddit.com/r/HollowKnight/comments/1nzgsef/can_i_just_use_the_quit_to_main_menu_option_to/)（退出保存行为交叉验证） |
| Sirlin《Saving the Day: Save Systems in Games》(Game Developer) | save point（玩家主动存档）与 invisible checkpoint（隐形死亡回退点）是**两个独立系统**；"玩家应能随时停止游戏而不丢失有意义的进度"；存档便利与游戏难度是假两难（Gears of War 自动检查点 + 高难度并存） | **借鉴思路**：区分"进度持久化"与"死亡恢复"两层数据职责；为未来"退出自动保存"留数据依据；反面教材（Dead Rising 单槽限制、NSMB 刻意扣留存档权）不学 | [Game Developer 文章](https://www.gamedeveloper.com/design/saving-the-day-save-systems-in-games)（全文已读） |
| Unity 社区存档技术实践 | 存档 DTO 内嵌 version 字段 + 载入时顺序迁移（v1→v2→…）；写临时文件再替换（防崩溃半写）；保留 .bak 备份；解析失败回退备份而非覆盖 | **直接移植**（技术模式）：SaveManager 的版本检查 / .tmp+.bak 写入链 / 损坏降级逻辑 | [Safe Writes, Backups, Version Migration 指南](https://uhiyama-lab.com/en/notes/unity/unity-save-data-versioning-migration/)、[Reddit r/Unity3D 崩溃写盘讨论](https://www.reddit.com/r/Unity3D/comments/1m02xf2/how_to_prevent_save_corruption_when_the_game/)、[Unity Discussions 版本字段讨论](https://discussions.unity.com/t/how-do-you-handle-saved-data-structure-change/870079)、[Stack Overflow 迁移模式](https://stackoverflow.com/questions/39757427/unity-c-sharp-savegame-migration) |
| 死亡细胞（检索补充） | 关卡入口自动存档、房间级检查点 | 仅作为"自动检查点"备选形态的认知，本期不采用（已选存档点实体） | [r/metroidvania 存档系统偏好讨论](https://www.reddit.com/r/metroidvania/comments/hv8lkr/what_save_system_do_you_guys_prefer/) |

**基于技术常识、未经逐条搜索确认的部分**（均为 Unity 官方文档可查的公知事实）：

- `JsonUtility` 支持 `[Serializable]` 嵌套类 / `Vector3` / `List<T>`，不支持 `Dictionary` 与多态字段
- `Application.persistentDataPath` 是各平台持久化目录的标准位置
- `JsonUtility.FromJson` 对非法 JSON 抛 `ArgumentException`（读取需 try/catch）

## B. 与现有架构的契合分析

**放哪一层**：通用系统 → **MOMS Manager 层**（非 EntityComponent / 非 PlayerModule）。存档是全局服务，与具体角色无关，与 `TimeManager`/`SFXManager` 同级。

**遵循的既有约定**：

- 命名空间 `Managers`（GameManager 反射扫描过滤器只认 `Managers`/`Managers.*`/`InputComponent`，放错命名空间不会被实例化）
- 静态 API 外壳 + 私有实例逻辑，内部经 `Resolve => GameManager.Instance?.GetManager<SaveManager>()` 找实例，未就绪时安全降级（完全仿 [TimeManager.cs](Assets/Script/Time/TimeManager.cs) 的 Resolve 模式）
- 实现 `IManager`，**不实现** `IUpdatable`（存档是事件驱动，非逐帧系统）；`Dependencies` 为空
- 数据与逻辑分离：`GameSaveData` 是纯 `[Serializable]` 数据类，不含任何 Unity 生命周期依赖

**与现有机制的接口关系**：

- `HealthManageComponent.SetHP()`（[HealthManageComponent.cs](Assets/Script/Component/HealthManageComponent.cs)）——恢复血量的现成入口，注释已预留此用途
- `Player.Instance` 懒查找、`Entity.rb` 缓存、`LocomotionComponent.SetFacing` —— 快照捕获/恢复直接复用
- 与 BulletTime/处决/Thrust 无互斥关系（本期不接流程，无接触点）

**改动范围**：3 个新建文件 + 1 处文档更新，**零现有代码逻辑改动**。

## C. 分步实现计划

### 文件清单

| 文件 | 新建/修改 | 说明 |
|---|---|---|
| `Assets/Script/Save/SaveManager.cs` | 新建 | 命名空间 `Managers`。IManager 实现 + 静态 API：`Save(data, slot=0)` / `Load(slot=0):GameSaveData`（无档/损坏返回 null）/ `HasSave(slot=0)` / `DeleteSave(slot=0)` / `SaveFilePath(slot)`。写入链：写 `.tmp` → 旧主文件复制为 `.bak` → 覆盖主文件；`Save()` 内部统一补 `version = kCurrentVersion` 与 `savedAtUnixTime`。读取链：主文件解析失败 → 试 `.bak` → 仍失败 LogWarning 返回 null（**不删除损坏文件**，下次 Save 自然覆盖）。版本检查：`data.version > kCurrentVersion` 拒载；`<` 走 `Migrate()`（当前空实现占位，未来挂 v1→v2 链）。附静态事件 `OnSaved`/`OnLoaded`（未来 UI 提示用，本期无人订阅） |
| `Assets/Script/Save/GameSaveData.cs` | 新建 | 命名空间 `Managers`。`[Serializable]` 纯数据类：`version:int`、`savedAtUnixTime:long`、`sceneName:string`、嵌套 `PlayerSaveData{ currentHP:float, maxHP:float, position:Vector3, facingRight:bool }`。便捷方法：`CaptureFromPlayer(Player)`（静态工厂：抓血量/位置/朝向/场景名，朝向读 `localScale.x`，实现时对照 `LocomotionComponent` 确认）、`ApplyToPlayer(Player)`（`SetHP` + 位置 + 朝向 + `rb.velocity` 清零；`maxHP` 仅存档备未来，不回写）。嵌套结构便于未来各系统扩展各自子块 |
| `Assets/Script/Tools/SaveDebuger.cs` | 新建 | 编辑器验证脚手架（`#if UNITY_EDITOR` 包裹整类，打包剔除，风格仿 [FPSCounter.cs](Assets/Script/Tools/FPSCounter.cs)，命名空间与其一致）。`Update` 监听 **F5 保存 / F9 读取**：保存 = `CaptureFromPlayer(Player.Instance)` → `Save` → Console 打印路径与 JSON；读取 = `Load` → `ApplyToPlayer` → 打印恢复日志。可加 `OnGUI` 左上角显示按键提示。需手动挂到场景 `Manager` 对象上 |
| `CLAUDE.md` | 修改 | MOMS Manager 表加 `SaveManager` 行；命名空间速查表 `Managers` 内容补 SaveManager/GameSaveData（README 已声明过时，不动） |

### 参数表

| 参数 | 类型/默认值 | 所在类 | 说明 |
|---|---|---|---|
| `kCurrentVersion` | `const int = 1` | SaveManager | 当前存档格式版本，迁移链的锚点 |
| 存档文件名 | `"save_{slot}.json"`，slot 默认 0 | SaveManager | `Application.persistentDataPath` 下；slot 参数预留多槽，本期固定单槽 |
| `.bak` / `.tmp` 后缀 | `".bak"` / `".tmp"` | SaveManager | 备份与原子写中转 |
| `prettyPrint` | `true` | SaveManager | JSON 带缩进，便于手动查看调试（文件很小，无性能顾虑） |
| `PlayerSaveData.maxHP` | 存而不读 | GameSaveData | 为未来"最大血量升级"预留，本期 Apply 不回写 |

### 无状态机改动

本期不涉及 FSM（不接死亡流程、无新状态）。未来接入后的目标流转（仅供后续参考）：坐存档点 → `CaptureFromPlayer` + `Save`；死亡 `HandleGameOver`（[ActionStates.cs](Assets/Script/Player/PLAYERSTATE/ActionStates.cs)）→ 重载场景（**改用场景名**）→ 场景加载后钩子 `Load` + `ApplyToPlayer`。

## D. 风险与权衡

- **JsonUtility 限制**：不支持 Dictionary/多态 → 数据模型约束为基本类型 + `List<T>` + `[Serializable]` 嵌套类；未来需要字典时用 `List<键值对>` 模拟（收集品 ID 集合等）。项目无 Newtonsoft.Json，不为存档引入新依赖
- **原子性是"尽力而为"**：`.tmp → .bak → 覆盖` 链在极端时序下仍有小窗口，Windows 的 `File.Replace` 跨平台行为不一，单机原型不值得引入平台特定 API；损坏降级链（主档→备份→当无档）已兜底最坏情况
- **既有 bug 不在本期范围**：`buildIndex = -1` 的死亡重载异常（Build Settings 无启用场景）依旧存在；数据模型已用 `sceneName` 规避存档侧，死亡流程接入时需一并把场景加入 Build Settings 或改 `LoadScene(name)`
- **"仅框架不接流程"的验证空窗**：若无可执行入口，框架写了也无法验证 → 用 `SaveDebuger` 编辑器脚手架解决（F5/F9 全链路走通），脚手架随打包自动剔除
- **替代方案取舍**：① PlayerPrefs 存储——免文件管理但无结构化版本迁移、编辑器清理麻烦，弃；② 引入 Newtonsoft.Json——序列化能力更强但为一个几十字节的存档引入整包依赖，过度设计，弃；③ 存档数据也做成 ScriptableObject——SO 是编辑器期资产，运行时写入不会持久化到磁盘，概念性错误，弃

## 验证（Unity 编辑器手动流程）

1. 打开项目等待编译，将 `SaveDebuger` 挂到场景 `Manager` 对象上
2. Play → 移动 Player / 改变朝向 → 按 **F5** → Console 打印保存路径与 JSON；到 `persistentDataPath` 打开 `save_0.json` 核对内容
3. 再移动到别处 → 按 **F9** → Player 位置/朝向/血量恢复为保存时值，Console 打印恢复日志
4. 手动把 JSON 改坏（删几个字符）→ F9 → Console 出现降级警告，游戏不崩（若有 .bak 则从备份恢复）
5. 把 JSON 的 version 改成 99 → F9 → 拒载警告，返回 null 不崩溃
6. 用 `SaveManager.DeleteSave()`（可临时加键到调试脚本，如 F8）删除后确认 `HasSave == false`
