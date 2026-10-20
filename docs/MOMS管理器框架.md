# MOMS 管理器框架 — 设计与工作逻辑

> 代码：`Assets/Script/GameManager.cs`、`Assets/Interface/Interface_Manager.cs`
> 定位：全项目一切系统管理器的宿主——注册、排序、初始化、每帧派发、销毁，一条龙。
> 前置阅读：《项目模块说明.md》§1.1

---

## 一、设计思路（为什么这么设计）

**1. Manager 是纯 C# 类，不是 MonoBehaviour。**
GameManager 是全场唯一的 MonoBehaviour 宿主（GameManager.cs:7），Manager 全部 `Activator.CreateInstance` 实例化（:60）。收益：Manager 不需要挂场景物体（配合"场景零摆放"）、可脱离 Unity 生命周期单独测试、创建时机完全受控。代价：没有 Update——由 IUpdatable 接口 + 宿主统一泵补回。

**2. 初始化顺序用拓扑排序显式解决，不靠手排列表。**
每个 Manager 用 `Dependencies`（`List<Type>`）声明依赖（Interface_Manager.cs:9），GameManager 建 DAG 后跑 Kahn 算法。被依赖者保证先初始化——例如 PlayerInputController 声明依赖 InputManager 后，它的 Initialize 里 `GameManager.Get<InputManager>()` 必然拿得到（PlayerInputController.cs:17/25）。新增依赖只改声明，不动初始化代码。

**3. 初始化走协程。**
`Initialize()` 返回 `IEnumerator`（Interface_Manager.cs），GameManager 逐个 `yield return` 泵（:78）。两个收益：Manager 内部可以异步加载资源（UIManager 建 Canvas、SFXManager 读配置）；保持"严格按拓扑序逐个完成"的语义。

**4. 故障隔离三层。**
① 创建期 try/catch：单个 Manager 实例化失败只 LogError，不拖垮全家（:58-66）；
② 初始化期 `InitializeManagerSafe` 手动 `MoveNext` + try/catch（:91-108）——协程内部抛的异常普通 await 接不住，必须手动泵才能兜住，实现"单个失败不影响其余"；
③ 循环依赖不死锁：排序结果数 ≠ 节点数 ⇒ 报错并**回退原始扫描顺序**（:161-166），系统照常启动只是顺序不保。

**5. 反射扫描 + 命名空间白名单。**
`ScanAllManagers`（:37-51）扫全部程序集中实现 IManager 的非抽象类，再按白名单过滤：
`Managers` / `Managers.*` / `InputComponent` / `InputComponent.*`。
目的是挡住 Editor 程序集和第三方包类型。**副作用成为本项目第一大坑**：命名空间写错 = 永不初始化 + 零报错（五期 GameFlowManager 曾因此整体失联）。

**6. 静态门面模式。**
每个 Manager 自己暴露静态入口（`TimeManager.GlobalTimeScale`、`GameFlowManager.StartLevel(...)`），内部统一走 `Resolve => GameManager.Instance?.GetManager<T>()`，判空静默降级。调用侧代码短，且环境未就绪时不炸。

**7. Update 单点派发。**
只有实现 `IUpdatable.OnUpdate(float dt)` 的 Manager 进 `updatableManagers` 列表（:83-84），GameManager.Update 每帧遍历泵（:183-188）。TimeManager 的冻结恢复/慢动作过渡/Timer 全靠这条泵驱动。⚠ 列表只在初始化时收集一次——**运行中途给某 Manager 补实现 IUpdatable 不会生效**。

**8. 销毁逆序。**
OnDestroy 按 initializationOrder **倒序** Deinitialize（:190-197），镜像初始化依赖（先用的后销）。

**9. 单例 + DDOL。**
Awake 里 Instance 已存在则 `Destroy(gameObject); return;`（自毁重复实例，所以每个场景摆 Manager.prefab 是安全的），否则 `Instance = this; DontDestroyOnLoad`（:20-26）——跨场景存活，转场时管理器群无缝延续。

**10. 对照组：ObjectPoolManager 刻意不走 MOMS**（ObjectPoolManager.cs:9 注释）。
池需要 GameObject 宿主挂池内实例，是 MonoBehaviour 单例——"什么进 MOMS"的边界案例：无状态服务进，需要场景载体的不进。

---

## 二、工作逻辑（怎么运转）

### 启动时序

```
GameManager.Awake (:18)
 ├─ Instance 已存在？→ Destroy(gameObject) 返回          [重复场景摆放自毁]
 ├─ Instance = this; DontDestroyOnLoad()
 ├─ managerTypes 为空 → ScanAllManagers()                [反射 + 命名空间白名单]
 └─ StartCoroutine(InitializeAll())

InitializeAll (:53)
 ├─ ① foreach type: Activator.CreateInstance → managers 字典   [try/catch 隔离]
 ├─ ② TopologicalSort() → initializationOrder
 │      Kahn (:141-159)：A 依赖 B ⇒ 边 B→A，inDegree[A]++
 │      入度 0 入队 → 出队进结果 → 邻居入度减，归零入队
 │      结果数 ≠ 节点数 ⇒ 循环依赖报错，回退原序 (:161-166)
 ├─ ③ foreach manager in initializationOrder:
 │      yield return InitializeManagerSafe(manager)      [逐个可跨帧；手动泵+try/catch]
 │      manager is IUpdatable ⇒ updatableManagers.Add
 └─ Debug.Log "Manager 初始化完成：N 个已加载"
```

### 每帧与销毁

```
Update (:183)：foreach updatableManagers → OnUpdate(Time.deltaTime)
OnDestroy (:190)：initializationOrder 逆序 → Deinitialize()
```

### 服务定位链

```
调用方 → XManager.静态门面 → Resolve => GameManager.Instance?.GetManager<T>()
       → GameManager.Get<T>() (:181) → managers[typeof(T)] 字典查找
       → 未注册：LogError + 返回 null（门面判空静默）
```

现存 Manager 一览（拓扑序大致）：
TimeManager / SFXManager / MusicManager（`Managers`）、UIManager / FloatingTextManager（`Managers.UI`）、
GameFlowManager（`Managers.GameFlow`）、GuideManager（`Managers.Guide`）、
InputManager / PlayerInputController（`InputComponent`）。

---

## 三、关键类型速查

| 类型 | 位置 | 职责 |
|---|---|---|
| GameManager | Assets/Script/GameManager.cs:7 | 宿主：扫描/实例化/拓扑排序/初始化/Update 泵/逆序销毁 |
| IManager | Assets/Interface/Interface_Manager.cs:5 | `Name`、`Dependencies`、`IEnumerator Initialize()`、`Deinitialize()` |
| IUpdatable | 同上 :14 | `OnUpdate(float deltaTime)` |
| PlayerInputController | Assets/Component/InputComponent/PlayerInputController.cs:14 | 输入→命令 Invoker，拓扑序消费 InputManager 的典型 |
| ObjectPoolManager | Assets/Pool System/ObjectPoolManager.cs:12 | 不走 MOMS 的对照案例（需 GameObject 载体） |

---

## 四、坑与约定

1. **命名空间白名单**（第一大坑）：新管理器 namespace 必须在白名单内，否则静默失联。
   写完新 Manager 去 Play 看 Console 的"N 个已加载"确认在列。
2. **`[SerializeField] List<Type> managerTypes` 是摆设**（:12）：System.Type 无法被 Unity 序列化，
   Inspector 路径恒空，运行时永远走自动扫描——别试图手排管理器列表。
3. **IUpdatable 只在初始化时收集**：中途补接口不生效；新 Manager 要每帧逻辑，初始时就该实现。
4. **纯 C# Manager 的 `[SerializeField]` 永远是 null**（Activator 创建不经 Unity 序列化），
   引用一律 `Resources.Load` 或配置 SO。
5. 门面判空是**静默**的——门面没反应时先查管理器是否被注册，别急着查业务逻辑。

## 五、与相邻模块的边界

- **实体侧不走 MOMS**：Entity 的组件刷新是每个实体自己的 FixedUpdate 手动链（见《实体与组件模型》），两层"手动调度"思路同源但互不依赖。
- **IUpdatable 的三大消费者**：TimeManager（时钟）、UIManager（Esc 轮询）、GuideManager（引导推进）——凡是"全局每帧心跳"都归这条泵。
