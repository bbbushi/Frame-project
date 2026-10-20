# UI 框架 — 设计与工作逻辑

> 代码：`Assets/UISystem/`（Core/UILayer·UIPanel、Integration/UIManager·UIManagerConfig·Panels/·FloatingTextManager·DamageNumber）+ `Assets/Script/Tools/UIFade.cs`
> 定位：四期成果——分层 Canvas + 面板注册表 + 模态栈 + 派生闸门；表现层订阅者（飘字/血条）也归这层。
> 前置阅读：《MOMS管理器框架》（UIManager 初始化时机、IUpdatable 轮询）

---

## 一、设计思路（为什么这么设计）

**1. 纯代码建 UI 骨架（场景零摆放的 UI 版）。**
UIManager.Initialize 同步段建 UIRoot + 四层 Canvas + 唯一 EventSystem + FadeVeil 黑幕，全部由 `Resources/data/ui/UIManagerConfig` SO 驱动；配置缺失时 `CreateInstance` 兜底（骨架照建、注册表为空）——"冒烟测试不打桩可跑"。

**2. 面板缓存只增不减（池化留二期）。**
面板只实例化一次，之后开/关只是 SetActive。三张表分离：`_cache`（实例）+ `_registry`（配置）+ `_stack`（modal≠None 的导航栈）。

**3. 派生状态闸门（"唯一写入者"模式）。**
输入开关与暂停缩放不是各面板自己写，而是每次面板开合后 `ApplyStackState` **全量重算**：先落两个只读镜像 `DesiredInputEnabled`/`DesiredPauseScale`（冒烟断言点），再单向写向 `InputManager.IsInputEnabled` 和 `TimeManager.PauseScale`。UIManager 是这两个全局闸门的唯一写入者（例外：引导命令也写输入开关，见坑 4）。

**4. 事实源跟 prefab 走。**
layer/modal/pause/panelId 全挂 prefab 的 UIPanel 组件字段，`PanelEntry` 刻意不放（防两处漂移，UIManagerConfig.cs:16）。UIManager 不实例化就能读 prefab 的 Layer 决定挂载层。

**5. 模态语义：Exclusive 独占闸门，Popup 叠其上。**
`PanelModal{None, Popup, Exclusive}`：Exclusive=独占（暂停/加载/死亡），Popup=叠在 Exclusive 上的子页（设置等）。栈顶往下找**第一个 Exclusive** 决定闸门——弹窗关闭后自动回到暂停状态，这就是"派生式"的价值。

**6. 面板与场景 UI 的分界心法：随场景生死的归场景，活得比场景久的归面板。**
UIRoot DDOL → 面板跨场景存活；UIPanel 类注释红线：**面板不得持有场景对象引用**，跨场景数据一律走 Manager/事件（UIPanel.cs:22-51）。

**7. UGUI OnClick 只能绑实例方法 → 静态门面须转发器。**
面板按钮先 `HideSelf` 再调门面——防 DDOL 残留。

---

## 二、工作逻辑（怎么运转）

### Initialize 全流程（UIManager.cs:58-85，全程同步——保证同帧先于对话/引导）

```
① Load data/ui/UIManagerConfig（null → CreateInstance 兜底）
② new GameObject("UIRoot")；DDOL：Application.isPlaying 检查是编辑器护栏
   [编辑器冒烟调 DDOL 会抛 InvalidOperationException]
③ 逐层 BuildLayerCanvas——显式常量清单 {HUD, Panel, Guide, Overlay}，不遍历枚举（缺哪层一眼可见）
   每层 ScreenSpaceOverlay Canvas，sortingOrder=枚举值（HUD=10/Panel=20/Guide=40/Overlay=200）
   CanvasScaler 1280×720 match 0.5（刻意与对话画布参数一致）
④ EnsureEventSystem：找到（含 inactive）则复用，否则新建——与对话插件双向收敛到唯一实例
⑤ EnsureFadeVeil：Overlay 层下建黑幕（alpha=0 起步）
   [img.raycastTarget=false——alpha=0 的 Image 只要 raycastTarget=true 就挡全屏点击；
    CanvasGroup 不拦射线、Image 才拦]
⑥ RebuildRegistry（幂等）：空条目跳过；panelId 回退 prefab.PanelId（再回退 GO.name）；
   重复 id 先到先得后到报错；config 与 prefab 的 id 不一致只警告、键以 config 为准
⑦ autoShow 扫描：勾了的直接 ShowPanel（HUD、Loading 常驻）
```

### ShowPanel 链路（:294-339）

```
① 缓存命中 → 判 Unity fake-null（已 Destroy 则移出缓存）；活实例 →
   panel.Show() + SetAsLastSibling()（置顶）+ RegisterOpen(panel)
   [注释强调：缓存命中也必须走收尾——漏了压栈 = 二次打开不进栈 = Exclusive 假暂停]
② 未命中 → 查注册表 → 层根（读 prefab.Layer，不实例化即可）→ Instantiate
③ 净化 Clone 后缀：go.name = entry.prefab.name（防 PanelId 留空时 "(Clone)" 进缓存键）
④ 根上没 UIPanel → LogError + Destroy 残骸；否则 _cache[id]=panel → Show → RegisterOpen
```

### 栈与闸门

```
RegisterOpen：modal≠None → _stack.RemoveAll(p=>p==panel) + Add   [重复开=置顶语义] → ApplyStackState
出栈在 HidePanelDirect：_stack.RemoveAll + panel.Hide() + ApplyStackState
  [统一出口按实例操作，不走 panelId 反查缓存——栈存实例、缓存键是注册表 id，两者可能不等]
ApplyStackState (:387-406)：
  for i = 栈顶 → 0：找到第一个 Modal==Exclusive → inputEnabled=false;
                   pauseScale = (Pause==World ? 0 : 1) → break
  DesiredInputEnabled/DesiredPauseScale（只读镜像，冒烟断言点）
  → InputManager.IsInputEnabled = inputEnabled; TimeManager.PauseScale = pauseScale
```

### Esc 轮询（OnUpdate :105-113 + HandleCancel :378-383）

```
OnUpdate：InputManager.IsUICancelPressed？
  └─ Loading 护栏：GameFlowManager.Current==Loading → return
     [LoadingPanel 在栈里时 Esc 会把它退掉 = 过渡中断；放 OnUpdate 不放 HandleCancel——
      后者是纯退栈语义]
  └─ HandleCancel：栈空无事；否则 HidePanelDirect(栈顶)——Esc=退栈顶实例，不是关"选中项"
对话屏蔽不在 UIManager，在 InputManager 侧（对话期 uiCancelPressed=false）
```

### 表现层订阅者（纯事件，零逻辑耦合）

- **飘字 FloatingTextManager**：订静态总线 `AttributeSet.AnyChanged`（Init/Deinit 对称加减）；白名单 `reason∈{Damage,Heal}` 且 `type==Health`；显示 `|delta|`（防御后落地值），伤害白/治疗绿。坐标两步投影：`cam.WorldToScreenPoint` → `RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, …, null, …)`（Overlay 画布传 null 才对）。**没有对象池**——Instantiate + DamageNumber 自毁；Update 全 `unscaledDeltaTime`（不被 timeScale 拖慢），随机 ±20px 防连击数字叠死；prefab/相机缺失只警告一次（防打斗刷屏）。
- **血条 HudPanel**：订实例事件 `Player.healthManageComponent.Set.Changed`（与飘字的静态总线相对）；OnShow 立即 TryBind，失败则 Update 每帧轻试；OnHide 退订 + **无条件重置 _bound**（Player 已销毁时旧订阅无法退订，但标记不清 = 常驻 HUD 在 Player 重建后永远不再 TryBind）；RefreshBar 立即执行防进场闪烁。

### UIFade（画面过渡原语，Assets/Script/Tools/UIFade.cs）

静态门面零状态业务；黑幕本体 FadeVeil 归 UIManager 的 Overlay 层持有、随 UIRoot 跨场景，本类只做渐变原语——**编排（何时黑、多快）归消费者**（Flow/死亡表现/传送各自 yield 组合）。
- token 并发护栏：`int token = ++_token`，每帧不等则 yield break——新渐变顶旧渐变（死亡切场景两层动画不打架）；
- 时钟一律 `Time.unscaledDeltaTime`——过渡期 PauseScale 常为 0（LoadingPanel 是 Exclusive+World），用 deltaTime 永久定格第一帧 = 黑屏死锁零报错；
- `blocksRaycasts` 跟随 alpha（>0.001 才挡）——漏复位 = 黑幕隐形但吃掉全部点击；
- 懒找 Veil：黑幕永远 active（视觉走 alpha 不走 SetActive），按名 Find 可靠。

---

## 三、关键类型速查

| 类型 | 位置 | 职责 |
|---|---|---|
| UIManager | UISystem/Integration/UIManager.cs:15 | 四层 Canvas/EventSystem/FadeVeil 构建、三表管理、派生闸门唯一写入者 |
| UILayer | Core/UILayer.cs:10 | 枚举值=sortingOrder（HUD10/Panel20/Guide40/Overlay200） |
| UIPanel | Core/UIPanel.cs:22 | Show/Hide（先 OnHide 再 SetActive(false)）+ OnShow/OnHide 钩子；PanelId 两级回退 |
| PanelModal / PanelPause | Core/UIPanel.cs:11 / :15 | None/Popup/Exclusive；None/World |
| UIManagerConfig / PanelEntry | Integration/UIManagerConfig.cs:8 / :24 | 缩放与生存期配置 + 注册表条目（刻意无 layer/modal） |
| SimplePanel | Integration/Panels/SimplePanel.cs:12 | 无逻辑通用面板 + Close()（暂停/死亡/确认框零代码） |
| LoadingPanel | Integration/Panels/LoadingPanel.cs:11 | Exclusive+World——打开即自动锁输入停世界，本类零闸门代码；静态 Active+ReportProgress |
| HudPanel | Integration/Panels/HudPanel.cs:9 | 血条：实例事件订阅+每帧轻试绑定 |
| FloatingTextManager / DamageNumber | Integration/ | 飘字订阅方 / 单条飘字自毁 |
| UIFade | Script/Tools/UIFade.cs:16 | 渐变静态原语：token 护栏/unscaled/懒找 Veil |

---

## 四、坑与约定

1. **面板不持场景对象引用**（UIRoot DDOL，场景换了引用全悬空）——跨场景数据走 Manager/事件。
2. **Hide 顺序是先 OnHide 再 SetActive(false)**——OnHide 里可能还要读自身状态；订阅退订放 OnHide（HudPanel 模式）。
3. 新面板接入 = 建 prefab（UIPanel 三属性填好）+ 注册表加条目，零代码；有逻辑才写 UIPanel 子类。
4. **已知讨论点——输入开关双写入者**：UIManager（栈派生）与 GuideManager.SetInputEnabledCommand（步骤接管，Scratch 存旧值回滚）都写 `InputManager.IsInputEnabled`。引导锁输入期间开 Exclusive 面板、关面板时 ApplyStackState 会按栈态覆盖引导的锁定值。目前不冲突（引导期不开面板），扩展前先想清楚优先级。
5. Esc 语义 = 退栈顶；Loading 护栏在 OnUpdate 里，别挪进 HandleCancel。
6. 飘字与血条订阅不同事件（静态总线 vs 实例事件）是刻意的：飘字全局一份订一次；血条绑的是"当前 Player"实例。
7. 黑幕/渐变一律 unscaled 时钟；任何 alpha>0 的 Image 记得管 raycastTarget。

## 五、与相邻模块的边界

- 上游《GAS属性系统》：飘字/血条只订阅 Changed/AnyChanged，白名单过滤 reason——逻辑→表现的唯一通道。
- 上游《游戏流程系统》：转场借用本框架的派生闸门（LoadingPanel Exclusive+World）+ Esc 护栏读 FlowState。
- 下游《引导系统》：GuideViewUGUI 挂 Guide 层；引导的输入接管是闸门的第二写入者（见坑 4）。
