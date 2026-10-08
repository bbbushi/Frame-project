# UI 系统二期设计方案（战斗反馈闭环：伤害管线 + 玩家 HUD + 伤害飘字）

> 分工延续一期：本文档给出全部设计与成员级骨架，代码由你手写、资产由你手建、验证由你自跑。
> 所有 `文件:行号` 引用已对当前代码核实（2026-09-28）。

---

## 一、目标与范围

**一句话**：把「打一下」变成看得见的反馈——血条会动、头顶飘数字、敌人打得死。

| 本期做 | 本期不做（留三期） |
|---|---|
| 接通伤害管线（DamageComponent.Hit → TakeDamage） | 敌人头顶血条（世界坐标跟随 UI，独立一期） |
| 接通敌人死亡（OnDied → 已有的 Die()，一行订阅） | 玩家死亡表现（重生/结算画面，战斗逻辑范畴） |
| HudPanel：玩家血条（Image fillAmount，订阅 OnChanged） | Blocked/Miss 飘字（数据在 Entity.Hit 分流处，见 §八.5） |
| FloatingTextManager + DamageNumber：世界坐标伤害飘字 | 飘字池化（量级不到，见 §八.4） |
| PanelEntry.autoShow：常驻面板自动 Show | 暴击差异化字号/颜色（先留 color 参数，够了） |
| UIDebugMenu 三项调试（受伤/治疗/测试飘字） | 治疗绿色飘字（Heal 接飘字一行事，想做顺手加） |

---

## 二、现状事实（设计依据，已逐条核实）

### 2.1 伤害管线断点——「两根断线」

现状调用链（全部核实）：

```
Hitbox.Hit(iDamagable target, remainDamage)        Hitbox.cs:114-141
  → 构造 Damage（含 damage 数值、origin、冲击向量）
  → target.Hit(damage)                              = Entity.Hit
      Entity.Hit(Damage)                            Entity.cs:180
      → IsBlock → return HitResult(0, Blocked)      ← 格挡分流：不进下段
      → IsInvincible → return HitResult(0, Miss)    ← 无敌分流：不进下段
      → damageComponent.Hit(damage)                 DamageComponent.cs:13
          击退 ✓ 受击动画 ✓ 血粒子 ✓
          扣血 ✗ ← 断线①：从不调 TakeDamage
          return HitResult(damage.damage, Hit)
```

- **断线①（扣血）**：`DamageComponent.Hit`（DamageComponent.cs:13-50）做完了全部受击表现，唯独没调 `Owner.healthManageComponent.TakeDamage(damage.damage)`。`Entity.healthManageComponent` 缓存在 Entity.cs:21，`Entity.Start` 里已 `Init()`——接入点现成。
- **断线②（死亡）**：`Enemy.Die()`（Enemy.cs:123-131）完整实现了死亡回收（`RemTimer.Destroy()` → `ObjectPoolManager.Release` 回池，非池对象回退 Destroy），**但全项目零调用**。整条复用链都通了：`Die()` → `OnReturnToPool()`（Enemy.cs:142，清标记/停协程/清物理）→ 下次 `OnSpawnFromPool()`（Enemy.cs:133，**里面已有 `Revive()` 回满血**）。只差 `OnDied` 事件到 `Die()` 的订阅。

### 2.2 血量组件（数据源，已备好）

`HealthManageComponent`（Assets/Script/Component/HealthManageComponent.cs）：
- `OnChanged(float current, float max)` / `OnDied` / `OnRevived` 三个事件，**当前 0 订阅者**
- `TakeDamage(float)`：`IsDead` 时直接 return——血到 0 只触发一次 OnDied，不会重复
- `SetHP(float)`：测试用治疗入口
- 坑①：cs:36-37 有两行被注释的重复旧代码（与 38-39 生效版重复），本期顺手删
- 坑②：`Init()` 里 `LoadConfig(Owner.characterData)`，`maxHP` 来自 `EntityCharacterConfig.maxHealth`——若实体没配 characterData，maxHP 用序列化默认 100，不影响本期

### 2.3 组件挂载确认（prefab 级）

Player.prefab 与 Enemy.prefab **都挂着** HealthManageComponent 和 DamageComponent（guid 比对已核实）。接入代码仍保留 null 防御——防将来手建实体忘挂时静默 NRE。

### 2.4 Player 访问点

`Player.Instance` 单例（Player.cs:15-19，`FindObjectOfType` 兜底）。**但 Player 的进场方式不可靠**（场景摆放/生成顺序未定，BABEL2 场景里没有 Player）——所以 HudPanel 的订阅必须做成**延迟绑定**（§4.5 TryBind 模式），不赌初始化顺序。

### 2.5 飘字数据源与挂点

- `HitResult`（Hitbox.cs:203-217）：`damageReceived` + `hitResultType`（Miss/Hit/Stucked/Blocked/Counter）
- 挂点选 `DamageComponent.Hit`（受击侧）而非 `Entity.Hit`：Blocked/Miss 在 Entity.Hit 就被分流返回，**天然不进** DamageComponent.Hit——「只飘真实命中」的过滤零成本
- 位置用 `Owner.ChestPosition`（Entity.cs:82，`transform.position + (0, 0.5f, 0)`）

### 2.6 UI 框架现状（一期成果，直接复用）

- `UIManager.GetLayerRoot(UILayer.HUD)` 已可用——HUD 层根 Canvas 就是血条和飘字的共同父级
- `UIPanel` 生命周期（Show/Hide/OnShow/OnHide）就是 HudPanel 的基类
- `PanelEntry`（UIManagerConfig.cs:24-28）目前只有 `panelId + prefab` 两字段
- 先例：`DamageComponent.Hit` 里已直接调 `BloodParticleGenerator.Instance.GenerateBloodOnBackground(...)`（DamageComponent.cs:43）——**游戏组件直接调全局门面做表现**在本项目是既有风格，飘字走 `FloatingText.Show(...)` 静态门面同款
- 时间先例：`GuideManager.OnUpdate` 用 `Time.unscaledDeltaTime`——UI 反馈不受 timeScale/子弹时间影响是项目惯例，飘字动画沿用

---

## 三、总体架构

### 3.1 数据流（本期接通后的完整闭环）

```
                    ┌─ 断线①接入：TakeDamage ──→ HealthManageComponent
                    │                              ├─ OnChanged ──→ HudPanel（血条 fillAmount）
Hitbox 结算命中 ────┤                              └─ OnDied ──→ Enemy.Die()（断线②，一行订阅）
（攻击侧）          │
                    └─ 断线①同点：FloatingText.Show(数值, 胸口坐标)
                                                   └─→ HUD 层 Canvas 上 Instantiate DamageNumber
                                                        （世界→屏幕坐标一次性定位，上浮淡出自毁）
```

### 3.2 新增/改动文件总览

```
新增（3 个，都在 Assets/UISystem/，namespace Managers.UI）：
├── Integration/HudPanel.cs          UIPanel 子类：血条，TryBind 延迟订阅 Player
├── Integration/FloatingTextManager.cs  IManager：飘字门面 FloatingText.Show(text, worldPos, color)
└── Integration/DamageNumber.cs      MonoBehaviour：飘字本体，自驱动画（prefab 根组件）

改动（5 个，每个都小）：
├── Script/Component/DamageComponent.cs     Hit() return 前接入扣血 + 飘字（~6 行）
├── Script/Enemy/Enemy.cs                   OnEnable/OnDisable 订退 OnDied → Die()（~8 行）
├── UISystem/Integration/UIManagerConfig.cs PanelEntry 加 autoShow 字段（1 行）
├── UISystem/Integration/UIManager.cs       Initialize 尾部 autoShow 遍历 Show（~5 行）
├── UISystem/Editor/UIDebugMenu.cs          加三个调试菜单（~25 行）
└── Script/Component/HealthManageComponent.cs  删 cs:36-37 注释残留（清洁项，可选）

新建资产（2 个 prefab + 1 处登记）：
├── Resources/data/ui/HudPanel.prefab
├── Resources/data/ui/DamageNumber.prefab
└── UIManagerConfig.asset 登记 HudPanel（panelId="HUD", autoShow=true）
```

### 3.3 MOMS 接入要点

- `FloatingTextManager : IManager`，`Dependencies = { typeof(UIManager) }`——拓扑序保证层根先建好
- **不加 IUpdatable**：飘字动画由 DamageNumber 自身 `Update` 驱动，管理器只管生成（延续 UIManager「无逐帧逻辑」的设计）
- HudPanel 不是管理器，是走注册表的普通面板（autoShow=true 让它开场自动 Show）

---

## 四、逐文件规格

### 4.1 `DamageComponent.cs`——断线①接入（本期核心改动，~6 行）

位置：`Hit()` 方法内，`switch` 结束后、`return new HitResult(...)`（DamageComponent.cs:49）之前：

```csharp
// ===== 二期接入：数值结算（断线①）=====
// 扣血：表现（击退/动画/血粒子）归本组件，数值落地归血量组件
if (Owner.healthManageComponent != null)
    Owner.healthManageComponent.TakeDamage(damage.damage);
else
    Debug.LogWarning($"[Damage] {Owner.name} 未挂 HealthManageComponent，伤害未扣血");

// 飘字：受击侧统一出口。Blocked/Miss 在 Entity.Hit 就被分流，不会到这——天然只飘真实命中
FloatingText.Show(damage.damage.ToString("0"), Owner.ChestPosition, Color.white);
```

**为什么放这个位置**（写代码时把注释带上）：
- `switch` 之后 → `ImpactType.None`（无冲击表现）也扣血也飘字：None = 纯数值伤害，语义正确
- `return` 之前 → 所有能走到这里的路径统一结算，一处接入全覆盖
- 飘字位置 = 受击者胸口，不是攻击者——多段命中/范围技时字跟着受击方走

### 4.2 `Enemy.cs`——断线②接入（一行订阅，~8 行）

Enemy 现在没有 OnEnable/OnDisable（已核实），直接加：

```csharp
// 池化对象的事件订阅标准位：每次从池中取出（OnEnable）都重新订，回池（OnDisable）退订——
// 放 Start/Awake 的话，池化复用第二只起就收不到事件了
private void OnEnable()
{
    if (healthManageComponent != null) healthManageComponent.OnDied += Die;
}
private void OnDisable()
{
    if (healthManageComponent != null) healthManageComponent.OnDied -= Die;
}
```

`Die()` 已存在（Enemy.cs:123），不用动。接通后的完整链（全部现成代码）：
`HP→0` → `OnDied` → `Die()` → `Release()` 回池 → `OnReturnToPool()` 清状态 → 下次取出 `OnSpawnFromPool()` → `Revive()` 回满血 → `OnChanged` 触发 →（若将来敌人有血条）自动刷新。

**Player 侧本期不订 OnDied**：玩家血空后 HUD 归零但人还在（TakeDamage 的 IsDead return 保证不再掉血）。死亡表现是战斗逻辑，等三期。测试兜底用 §4.8 的「治疗回满」菜单。

### 4.3 `UIManagerConfig.cs`——PanelEntry 加 autoShow（1 行）

PanelEntry（UIManagerConfig.cs:24-28）加一个字段：

```csharp
[Serializable]
public class PanelEntry
{
    public string panelId;
    public UIPanel prefab;

    [Tooltip("勾选后 UIManager 初始化完成即自动 Show（常驻面板：HUD、后期 Loading 屏等）")]
    public bool autoShow;
}
```

### 4.4 `UIManager.cs`——Initialize 尾部 autoShow 遍历（~5 行）

位置：`Initialize()` 里 `RebuildRegistry();` 之后、哨兵日志之前：

```csharp
// 常驻面板：注册表里勾了 autoShow 的直接 Show（HUD 等）
foreach (var entry in _registry.Values)
    if (entry.autoShow) ShowPanel(entry.prefab != null && string.IsNullOrEmpty(entry.panelId)
        ? entry.prefab.PanelId : entry.panelId);
```

> 注：`RebuildRegistry` 有「config 的 panelId 为空时回退 prefab.PanelId」的逻辑（UIManager.cs:210-215），这里取 id 时同样要走回退，避免键对不上。更省事的写法——HudPanel 的 prefab 组件上直接填 `panelId = "HUD"`、config 里也填 `"HUD"`，两边一致就没有回退问题（推荐，prefab 搭建步骤里已这么写）。

安全性：`ShowPanel` 只写 `_cache` 不动 `_registry`，遍历 `_registry.Values` 时调用是安全的。

### 4.5 `HudPanel.cs`（新文件，UIPanel 子类）

```
namespace Managers.UI
├── [SerializeField] Image fill          // 血条填充图（Filled Horizontal）
├── [SerializeField] TMP_Text hpText     // 可选："87/100" 数字显示
├── bool _bound                          // 订阅状态标记（防重复订阅）
│
├── protected override void OnShow()
│     TryBind();                         // 立刻试一次——Player 早就绪时就地绑定
│
├── void Update()
│     if(!_bound) TryBind();             // Player 还没进场就每帧轻试一次（一次 null check，代价可忽略）
│
├── void TryBind()
│     var player = Player.Instance;
│     if(player == null || player.healthManageComponent == null) return;   // 时机未到，下帧再试
│     player.healthManageComponent.OnChanged += OnHpChanged;
│     _bound = true;
│     OnHpChanged(player.healthManageComponent.CurrentHP, player.healthManageComponent.MaxHP);
│     // ↑ 订阅完立刻手动刷一次：OnChanged 只在变化时来，进场瞬间要把血条刷到真实值
│
├── void OnHpChanged(float current, float max)
│     fill.fillAmount = max > 0f ? current / max : 0f;
│     if(hpText != null) hpText.SetText("{0:0}/{1:0}", current, max);
│
├── protected override void OnHide()
│     if(_bound && Player.Instance != null && Player.Instance.healthManageComponent != null)
│         Player.Instance.healthManageComponent.OnChanged -= OnHpChanged;
│     _bound = false;
│
└── （文件头记得 using PlayerSystem; —— Player 在 PlayerSystem 命名空间里；
     Entity 本身在根命名空间，但 HudPanel 只碰 Player，不需要引 Entity）
```

**设计要点写进注释**：
- **TryBind 延迟绑定**（§2.4）：不赌 Player 和 UIManager 谁先初始化。HUD 常驻开着，未绑定时每帧一次 null check 无感
- 未绑定时血条显示 prefab 初始状态（建议 prefab 里 fillAmount 就摆 1，满血观感）
- `Player.Instance` 中途销毁（换场景）时 `Update` 里 TryBind 用的是 `?.` 语义天然安全；HUD 本身 DDOL，下个场景 Player 出现会自动重新绑上

### 4.6 `FloatingTextManager.cs`（新文件，IManager）

```
namespace Managers.UI
├── Name => "FloatingTextManager"
├── Dependencies = { typeof(UIManager) }        // 拓扑序：层根必须先建好
├── static FloatingTextManager Resolve => GameManager.Instance?.GetManager<FloatingTextManager>()
├── const string PrefabPath = "data/ui/DamageNumber"
├── GameObject _prefab
├── RectTransform _hudCanvasRect               // HUD 层 Canvas 的 RectTransform，坐标转换用
├── bool _prefabWarned                          // prefab 缺失只警告一次，防打斗中刷屏
│
├── IEnumerator Initialize()
│     _prefab = Resources.Load<GameObject>(PrefabPath);
│     _hudCanvasRect = UIManager.GetLayerRoot(UILayer.HUD)?.GetComponent<RectTransform>();
│     哨兵日志："[FloatingText] Initialized（prefab ✓/✗）"
│     yield break;
│
├── Deinitialize()  // 纯 C# 管理器，运行时产物 DamageNumber 挂在 UIRoot 下随根销毁，无额外清理
│
├── ── 静态门面（DamageComponent/调试菜单只调这个）──
├── public static void Show(string text, Vector3 worldPos, Color color)
│     var m = Resolve;  if(m == null) return;                    // 引导/游戏侧永不为表现层崩
│     if(m._prefab == null || m._hudCanvasRect == null)
│         if(!m._prefabWarned){ LogWarning("[FloatingText] prefab 或 HUD 层缺失，飘字跳过"); m._prefabWarned = true; }
│         return;
│     var go = UnityEngine.Object.Instantiate(m._prefab, m._hudCanvasRect);
│     go.name = m._prefab.name;                                  // 去 (Clone)，与 UIManager.ShowPanel 同规
│     var rect = go.GetComponent<RectTransform>();
│     // ── 世界→屏幕→Canvas 局部，两步各走各的相机语义（⚠️ 实施中踩过坑后修正）──
│     // 第一步：游戏世界坐标 → 屏幕像素，必须 Camera.main.WorldToScreenPoint 投影。
│     //         不能用 RectTransformUtility.WorldToScreenPoint(null, pos)——null 相机版
│     //         把输入当屏幕坐标直接截 xy（Overlay 世界空间 UI 专用），世界坐标喂进去落在左下角
│     Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
│     // 第二步：屏幕像素 → Canvas 局部。Overlay 画布不经相机，这里传 null 才对；
│     //         内部已除 CanvasScaler 缩放，localPos 直接赋 anchoredPosition
│     if(RectTransformUtility.ScreenPointToLocalPointInRectangle(m._hudCanvasRect, screenPos, null, out var localPos))
│         rect.anchoredPosition = localPos;
│     go.GetComponent<DamageNumber>()?.Init(text, color);
│
└── （Instantiate 出的飘字挂 HUD 层根下——和血条同层，血条在角落、飘字在角色头顶，不冲突）
```

**坐标链是本期唯一的新硬知识**，原理（写进代码注释；⚠️ 初版方案此处写错过，实施时飘字落左下角后修正——以下为修正版）：
1. 第一步 `Camera.main.WorldToScreenPoint(worldPos)`：**游戏世界坐标 → 屏幕像素**，必须经相机投影。反面教材：`RectTransformUtility.WorldToScreenPoint(null, pos)` 的 null 相机重载语义是"输入已是屏幕坐标，直接截 xy"（Overlay 世界空间 UI 专用）——把游戏世界坐标喂进去，(3.2, 1.5) 这种值在屏幕像素空间就是左下角
2. 第二步 `ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out localPos)`：屏幕像素 → Canvas 局部坐标。Overlay 画布渲染不经相机，**这一步传 null 才是对的**；它内部已除掉 CanvasScaler 的缩放，得到的 localPos 直接就是 `anchoredPosition` 要的值
3. 飘字**出生点固定、不跟随**——出生后角色跑了字也不追，这是刻意设计（命中瞬间的位置快照），省掉每帧跟随的整条管线

### 4.7 `DamageNumber.cs`（新文件，prefab 根组件）

```
namespace Managers.UI
├── TMP_Text _text                       // Awake 里 GetComponent 缓存
├── const float Lifetime = 0.8f          // 总寿命
├── const float RiseSpeed = 60f          // 上浮速度（参考分辨率 720 空间的像素/秒）
├── const float FadeStart = 0.4f         // 从寿命的哪个点开始淡出
├── float _age; Color _baseColor;
│
├── public void Init(string text, Color color)
│     _text.text = text;  _baseColor = color;
│     // 随机水平偏移 ±20px：连击多段命中时数字不叠死
│     var pos = GetComponent<RectTransform>().anchoredPosition;
│     pos.x += Random.Range(-20f, 20f);
│     GetComponent<RectTransform>().anchoredPosition = pos;
│
└── void Update()
      _age += Time.unscaledDeltaTime;    // 项目惯例：UI 反馈不被 timeScale/子弹时间拖慢
      transform.localPosition += Vector3.up * (RiseSpeed * Time.unscaledDeltaTime);
      var a = _age < FadeStart ? 1f : 1f - (_age - FadeStart) / (Lifetime - FadeStart);
      _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
      if(_age >= Lifetime) Destroy(gameObject);   // 自毁：管理器不用管生命周期
```

（`RiseSpeed` 等参数做成 `const` 够用；想 Inspector 调参就改 `[SerializeField]`，第一次飘出来感觉不对时再改不迟。）

### 4.8 `UIDebugMenu.cs`——加三个调试项（~25 行）

```
[MenuItem("UI/调试/玩家受伤10点")]     // 验证 HUD：直调 TakeDamage——故意不走 DamageComponent，
    Player.Instance?.healthManageComponent?.TakeDamage(10f);   // 所以【不飘字】，这正是数据源分离的验证点
[MenuItem("UI/调试/玩家治疗回满")]     // 测死亡边界后拉回来反复测
    Player.Instance?.healthManageComponent?.SetHP(float.MaxValue);
[MenuItem("UI/调试/测试飘字")]         // 不用打怪就能验证坐标链
    if(Player.Instance != null)
        FloatingText.Show("999", Player.Instance.ChestPosition, Color.yellow);
    else Debug.LogWarning("[UI] 场景里没有 Player");
（三个都套一层 EnsurePlaying()，与既有菜单一致）
```

> 「玩家受伤10点」的菜单项名建议带后缀注明「无飘字」：`UI/调试/玩家受伤10点（直调，无飘字）`——省得将来自己困惑。

### 4.9 `HealthManageComponent.cs`——清洁项（可选，30 秒）

删掉 cs:36-37 两行注释残留（与 38-39 生效代码完全重复的旧版本）。

---

## 五、资产搭建步骤（Inspector 操作级，延续一期临时场景工作台流程）

### 5.1 `Resources/data/ui/HudPanel.prefab`

临时空场景当工作台（防污染主场景），全建完拖 prefab、删工作台对象、**不保存场景**。

1. Hierarchy 右键 → UI → Image，改名 `HudPanel`
   -（会自动带出 Canvas + EventSystem， prefab 建完**删掉这组 Canvas/EventSystem**，HudPanel 拖进 Resources 前必须脱离）
2. 选 HudPanel 根：
   - RectTransform：Anchor preset 选 **top-left**；Width=300、Height=28；Pos X=170（=20+150，宽的一半）、Pos Y=−34（=−20−14，高的一半）——锚在左上角时 position 是相对锚点的中心偏移
   - Image（这个当底框）：Color 黑 A=180；**Raycast Target 关**
3. 右键 HudPanel → UI → Image，改名 `Fill`：
   - Anchor preset **stretch-stretch**（四向拉满父级），Left/Right/Top/Bottom 全 0
   - Image → Image Type: **Filled**；Fill Method: Horizontal；Fill Origin: Left；Fill Amount: 1
   - Color 红（或你喜欢的血条色）；**Raycast Target 关**
4.（可选）右键 HudPanel → UI → Text (TMP)，改名 `HpText`：
   - Anchor **top-left**，Pos 调到血条右侧或内部；Font Asset 换 **SourceHanSansSC-Medium SDF**（别忘，GuideText 的教训）；FontSize 18；Alignment 居中；**Raycast Target 关**；占位文本删掉
5. 根节点 Add Component → **HudPanel**（脚本）：
   - Panel Id 填 `HUD`；Layer 选 **HUD**
   - Fill 拖进 fill 槽；HpText 拖进 hpText 槽（可选）
6. HudPanel 拖进 `Resources/data/ui/` 成 prefab → 删场景里的 Canvas + EventSystem + 原物体 → 不保存场景

### 5.2 `Resources/data/ui/DamageNumber.prefab`

1. 临时场景 → UI → Text (TMP)，改名 `DamageNumber`（同样会带 Canvas/EventSystem，最后删）
2. RectTransform：Width=200、Height=50（给数字留量，居中对齐）
3. TextMeshProUGUI 组件：
   - Font Asset：**SourceHanSansSC-Medium SDF**（必换——数字虽是 ASCII，LiberationSans 也能显示，但统一字体省心且伤害词缀将来有中文）
   - FontSize 28；Alignment 中中；**Raycast Target 关**；占位文本删掉
4. Add Component → **DamageNumber**（脚本）
5. 拖进 `Resources/data/ui/` → 删 Canvas/EventSystem → 不保存场景

### 5.3 `UIManagerConfig.asset` 登记（Assets/Resources/data/ui/UIManagerConfig.asset)

panels 列表加一条：
- Panel Id：`HUD`
- Prefab：拖 HudPanel.prefab
- **Auto Show：勾**（本期的点睛字段——开场自动显示，从此不用手动 Show）

> 一期遗留：如果 panels 里还留着那条 panelId=`Guide`、prefab 空的旧条目（控制台曾警告过的），这次一并删掉。

---

## 六、实施顺序（每步「写完即编译」，过了再做自测）

1. **清洁 + 配置层**：HealthManageComponent 删注释残留 → PanelEntry 加 autoShow → UIManager 加 autoShow 遍历
   （编译过即可，行为无变化——还没有面板勾 autoShow）
2. **DamageNumber.cs + FloatingTextManager.cs**：纯新增，不接线
   Play 自测：`UI/调试/测试飘字`（先做 5.2 的 prefab）→ 场景有 Player 时头顶飘"999"金字，0.8 秒上浮淡出消失
   ——这一步单独验证**坐标链**这条最险的路，打怪接线前先确认它通
3. **HudPanel.cs + 5.1 prefab + 5.3 登记**：
   Play 自测：进 Play 后 HUD 自动出现（autoShow 生效）→ `UI/调试/玩家受伤10点` 血条降、数字变 → `UI/调试/玩家治疗回满` 回满
4. **DamageComponent 接线**（§4.1 两段）：
   Play 自测：真打怪——敌人头顶飘伤害数字（白），玩家被打血条掉
5. **Enemy 接线**（§4.2）：
   Play 自测：把一只敌人打空血 → 它消失（回池/销毁）→ 若是池化刷的怪，再刷出来的满血
6. **UIDebugMenu 三项**（其实第 2、3 步就要用，建议第 2 步时先写好菜单再测）
7. **端到端走 §七 全清单**

> 推荐实际顺序微调：**先写 UIDebugMenu 三项（第 6 步提到最前）**，因为第 2、3 步自测全靠它们。

---

## 七、端到端验证清单

1. **autoShow**：进 Play 后 HUD 血条自动出现，无需任何手动操作；`[UIManager] Initialized: 1 panels registered`（或含 TestPanel 则 2）
2. **血条刷新**：`UI/调试/玩家受伤10点` ×N → fillAmount 平滑对应下降、HpText 数字同步；治疗回满 → 回 1
3. **数据源分离验证**（教学点）：受伤菜单**只动血条不飘字**（直调 TakeDamage 不经 DamageComponent）；打怪**又飘字又掉敌人血**——两条路径互不干扰
4. **飘字坐标**：打怪时数字出现在**敌人胸口偏上**而非屏幕中心/左下角（左下角 = 坐标链断了，数字直接用 anchoredPosition 零值）；连击多段时数字有随机水平散布
5. **飘字生命周期**：0.8 秒后数字自毁，Hierarchy 里 DamageNumber 不堆积（连打 30 秒后看一眼）
6. **敌人死亡**：打空一只敌人 → 消失（控制台无红字）；池化复用的下一只满血（若有刷怪）
7. **引导回归**（防一期成果被改坏）：Guide/重置进度 → 开始基础引导 → 横幅照常显隐；HUD 血条与引导横幅**同在**（都挂 HUD/Guide 层，不互斥）
8. **对话回归**：靠近 NPC 按 E 开对话 → 对话照常、EventSystem 仍唯一
9. **排序目检**：HUD 血条（10）永远在最底 → 引导横幅（40）盖住血条区 → 对话（100）盖住一切
10. **降级冒烟**：临时把 `DamageNumber` prefab 改名 → 打怪**不崩不刷屏**（警告只出一次）→ 飘字静默跳过、扣血照常 → 改回
11. **双分辨率**：1280×720 与 1920×1080 下血条位置/大小正常（CanvasScaler 适配），飘字位置跟手（坐标链含缩放换算的证据）

---

## 八、设计决策记录

1. **飘字挂 DamageComponent（受击侧）而非 Hitbox（攻击侧）**：Blocked/Miss 在 Entity.Hit 被分流，天然过滤；且未来掉落伤害、环境伤害等非 Hitbox 来源自动复用同一出口。
2. **死亡接 OnDied → 已有 Die()，只加订阅**：Die/回池/Revive 链全是现成代码，本期不写任何新战斗逻辑，只把事件和既有实现接上——和一期的「Hide 缺口」同性质：补管线，不加机制。
3. **TryBind 延迟绑定**：Player 进场方式不可靠（§2.4），任何「谁先初始化」的假设都是隐性时序炸弹；HUD 常驻、每帧一次 null check 的轮询成本为零。
4. **飘字不池化**：单机动作游戏每秒命中个位数，Instantiate/Destroy 无感；三期接 `Script/pool/` 时连同面板池化一起做（一期路标如此）。
5. **Blocked/Miss 不飘字**：数据在 Entity.Hit 的分流 return 里（HitResult(0, Blocked)），要飘得把挂点提到 Entity.Hit——等要做「格挡！」提示字时再说，届时 FloatingText 门面原样可用。
6. **飘字出生点快照、不跟随**：命中瞬间的位置定格，省每帧 WorldToScreenPoint 跟随管线；观感上「伤害数字留在被打的位置」也是主流做法。
7. **unscaledDeltaTime 驱动动画**：项目有子弹时间（TimeManager/PlayerBulletTime），UI 反馈被拖慢会显得卡 bug；与 GuideManager.Tick 同规。
8. **autoShow 而非硬编码 HUD**：将来 Loading 屏、结算面板等常驻 UI 走同一机制，UIManager 不需要知道「哪些面板重要」。
9. **飘字挂 HUD 层（10）而非新开层**：与血条同层同 Canvas，合批更优；伤害数字被引导横幅/对话盖住是正确层级（引导期间你别看伤害数字）。

---

## 九、遗留与三期路标

1. **敌人头顶血条**：受击显示/脱战隐藏的世界坐标跟随 UI——「每帧跟随 + Billboard」新机制，独立一期做。
2. **玩家死亡表现**：OnDied 订阅位已在（HudPanel 模式可抄），重生/结算画面属战斗逻辑。
3. **Blocked/Miss/暴击差异化飘字**：挂点上提 Entity.Hit + HitResultType 分色；color 参数已留。
4. **飘字+面板池化**：接 `Script/pool/` 的 ObjectPoolManager（Enemy 的 IPoolable 是现成范本）。
5. **FadeManager 修复 + Overlay=200 层**（二期问卷里被让位的那项）：UILayer 加枚举 + FadeManager 改 Resources.Load。
6. **InputManager 路径错配**（一期发现的旧账）：`InputManager.cs:48` 加载 `"data/Playerdata/PlayerInputData"` 与实际资产路径对不上会静默落默认键位——本期仍未动。
7. **挖孔高亮**：GuideView 装饰层，TargetKey 已留。
