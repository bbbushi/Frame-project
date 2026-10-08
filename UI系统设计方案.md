# UI 系统设计方案（第一期：窗口框架 + 引导表现层）

> 参照《unity三大方向_提炼.md》方向一（UGUI 组件化布局 + 动态 Prefab 生成），落地为本项目的 UI 基建。
> 分工：**本文档给全量规格，代码由你手写；Prefab/资产由你搭建**。写完任意文件发我审查即可。

---

## 一、目标与范围

| 本期做 | 本期不做（留二期） |
|---|---|
| UIManager 进 MOMS：UIRoot 三层 Canvas、EventSystem 保证、面板注册表/缓存 | HUD 血条（数据源已备好，见附录） |
| UIPanel 面板基类 + Show/Hide/Toggle/CloseAll 门面 | 伤害飘字、消息列表 |
| GuideViewUGUI：补上 GuideSystem 预留的 IGuideView 实现 | 挖孔高亮/气泡/手指（TargetKey 字段已留位） |
| 修复 GuideManager `_view.Hide()` 无调用点的缺口 | 面板转场动画、模态/互斥、多语言 |
| TestPanel 示范面板（验证框架用，可选） | FadeManager 改造（只留路标） |

**方向一对照表**（学习路线与本项目的对应关系）：

| 方向一原项 | 本期处置 |
|---|---|
| CanvasScaler 多分辨率适配 | ✓ 三层画布统一 1280x720 / match 0.5（与对话画布一致） |
| LayoutGroup"网页化"布局 | ✓ GuidePanel 就是示范：VerticalLayoutGroup + ContentSizeFitter 的流式自适应 |
| 自定义 LayoutElement（margin/padding） | 二期：VLGroup 自带 Padding 已够用 |
| ScrollRect 消息列表动态生成 | 二期（对话插件 Backlog 已有先例可抄） |
| 运行时动态生成 Prefab | ✓ 框架留位：任何系统 `GetLayerRoot(层)` 自行实例化——GuidePanel 走的就是这条路 |
| EditorWindow 场景生成 Prefab 工具 | 与 UI 无关，另行安排 |

---

## 二、现状事实（设计依据，已逐条核实）

1. **项目自身零 UI**：场景（1.unity 等）无 Canvas / EventSystem / CanvasScaler。唯一在运行的 UI 是对话插件运行时自举的。
2. **对话画布参数**（`DialogueUI.cs`，外部包）：ScreenSpaceOverlay、**sortingOrder=100**、ScaleWithScreenSize **1280x720 / match 0.5**；其 EventSystem 叫 `DialogueEventSystem`（StandaloneInputModule，挂在 DDOL 的 DialogueCanvas 下），`EnsureEventSystem` 先 `FindFirstObjectByType<EventSystem>()` 查到即复用（DialogueUI.cs:594-603）。
3. **可复用中文字体**：`Assets/Dialogue/Text/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Medium SDF`（TMP SDF，对话样式在用）。**不需要新生成字集图集**，Prefab 里手动拖这个 Font Asset 即可。TMP Essentials 已导入（tmp 3.0.6）。
4. **输入**：旧 Input Manager（activeInputHandler=0）→ UI 用 StandaloneInputModule。
5. **MOMS 惯例**：纯 C# 管理器类，namespace 必须是 `Managers` 或 `Managers.*`（GameManager.cs:44-48 反射扫描 + Activator.CreateInstance + 依赖拓扑排序）；范本 SFXManager（Resources.Load 配置 + new GameObject 根 + DDOL + Deinitialize 销毁）；静态门面范式 `private static XXXManager Resolve => GameManager.Instance?.GetManager<XXXManager>();`。
6. **Resources 惯例**：SO 配置在 `Resources/data/`；UI 资源目录不存在（本期新建 `Resources/ui/`）。**纯 C# 管理器的 [SerializeField] 永远无法赋值**（FadeManager.fadeCanvasPrefab 恒 null 的教训）→ 一切引用走 Resources.Load 或配置 SO。
7. **场景零摆放是惯例**：UIRoot 由 UIManager 在 Initialize 时 `new GameObject` 构建，不进场景、不进 prefab。
8. **GuideSystem 接缝**：`IGuideView { ShowStep(GuideStepContext); Hide(); }`；`GuideManager.cs:181-182` 现为 `_view = new NullGuideView()`（注释写明将来替换）；`GuideStepContext` 字段：GuideId / StepId / Text / TargetKey / ConditionText / StepIndex / StepCount。**缺口：`_view.Hide()` 全项目无调用点**（GuideFinished/GuideAborted 处理器都没碰 _view）——本期必修。
9. **无 asmdef**：全项目一个 Assembly-CSharp，UI 模块与 GuideSystem 类型互相可见。

---

## 三、总体架构

### 3.1 目录与命名空间

```
Assets/Script/UISystem/
├── Core/                        # namespace UI —— 可移植，零游戏依赖
│   ├── UILayer.cs
│   └── UIPanel.cs
├── Integration/                 # namespace Managers.UI —— MOMS 自动扫描注册（StartsWith("Managers.")）
│   ├── UIManagerConfig.cs
│   ├── UIManager.cs
│   ├── GuideViewUGUI.cs
│   └── TestPanel.cs
└── Editor/                      # 只在编辑器编译
    └── UIDebugMenu.cs
```

与 GuideSystem 同一分层哲学：Core 是纯 UGUI 抽象（可拷去任何项目），Integration 承接 MOMS 与游戏系统。**没有 asmdef**，照旧全在一个程序集里。

### 3.2 Canvas 结构与排序契约（运行时构建后的最终形态）

```
UIRoot (普通 GameObject，DDOL，什么都不挂)
├─ UIEventSystem          (EventSystem + StandaloneInputModule)
├─ HUDCanvas              (Canvas overlay, sortingOrder=10, CanvasScaler, GraphicRaycaster)
├─ PanelCanvas            (Canvas overlay, sortingOrder=20, CanvasScaler, GraphicRaycaster)
│    └─ [缓存的面板实例，如 TestPanel]
└─ GuideCanvas            (Canvas overlay, sortingOrder=40, CanvasScaler, GraphicRaycaster)
     └─ GuidePanel        (GuideManager 实例化，默认 inactive)

DialogueCanvas (对话插件自建, sortingOrder=100, DDOL)   ← 全局压过上面所有层
（200 预留给 Fade/Loading，不建实体）
```

- **三层是兄弟根 Canvas，不是嵌套**：Overlay 模式下根 Canvas 按 sortingOrder 全局排序，与对话画布的关系一眼可见；每层独立合批、独立 GraphicRaycaster 本就省不掉；单层可整体 SetActive（将来截图模式关 HUD）。
- **CanvasScaler 必须与 Canvas 同物体**；UIRoot 容器本体什么都不挂。
- 排序契约以注释固化：`HUD(10) < Panel(20) < Guide(40) < Dialogue(100，外部包硬编码) < 预留(200)`。

### 3.3 MOMS 接入要点

- `UIManager : IManager`（**不实现 IUpdatable**——本期无逐帧逻辑。注意 GameManager.cs:83-85 只在初始化时收集 IUpdatable，中途补接口不会生效，将来加 toast/淡出队列时一起处理）。
- `Dependencies = 空`：拓扑第一批初始化，尽早可用；GuideManager 依赖它，Kahn 排序保证 UIManager 的 Guide 层根先于引导表现层存在。
- Manager.prefab 没有手工 managerTypes 列表（已核实）→ `Managers.UI` 命名空间被自动扫描，**无需手工注册**。⚠️ 若将来有人手工填了 managerTypes，必须记得加 UIManager。
- **Initialize 全程在协程同步段完成**（不 yield 等待）→ 同帧内先于对话懒加载单例与任何引导行为，EventSystem 去重双向安全。

---

## 四、逐文件规格

> 骨架给出签名与关键实现；「// ①②③」是行为步骤，照抄即可。命名/注释风格对齐 GuideSystem。

### 4.1 `Core/UILayer.cs`

```csharp
namespace UI
{
    /// UI 层级，数值 = ScreenSpaceOverlay 的 sortingOrder。
    /// 对话画布=100（外部包硬编码，不入枚举防 GetLayerRoot 歧义）；200 预留 Fade/Loading。
    public enum UILayer
    {
        HUD = 10,
        Panel = 20,
        Guide = 40,
    }
}
```

### 4.2 `Core/UIPanel.cs`

```csharp
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 面板抽象基类：Show/Hide 只切 active，视觉表现交给 OnShow/OnHide。
    /// layer 挂在组件上（单一事实源跟 prefab 走），UIManager 不实例化即可读到。
    /// ⚠️ 面板实例被 UIManager 缓存且 UIRoot DDOL：面板不得持有场景对象引用，
    /// 跨场景数据一律走 Manager/事件，否则切场景悬空。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        [Tooltip("面板唯一 id。留空则用 gameObject.name（Instantiate 会带 (Clone)，UIManager 已做净化）")]
        [SerializeField] private string panelId;

        [Tooltip("本面板挂在哪一层")]
        [SerializeField] private UILayer layer = UILayer.Panel;

        public string PanelId => string.IsNullOrEmpty(panelId) ? gameObject.name : panelId;
        public UILayer Layer => layer;
        public bool IsOpen => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow();
        }

        public void Hide()
        {
            OnHide();                        // 先回调再关（OnHide 里可能要读自身状态）
            gameObject.SetActive(false);
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}
```

### 4.3 `Integration/UIManagerConfig.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UI;

namespace Managers.UI
{
    [CreateAssetMenu(menuName = "MOMS/UIManagerConfig", fileName = "UIManagerConfig")]
    public class UIManagerConfig : ScriptableObject
    {
        [Header("缩放（与对话画布一致）")]
        public Vector2 referenceResolution = new(1280, 720);   // 字段初始化器 = 缺资产时的兜底默认值
        [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;

        [Header("生存期")]
        public bool dontDestroyOnLoad = true;

        [Header("面板注册表（layer 归 UIPanel 组件管，此处不放，防两处漂移）")]
        public List<PanelEntry> panels = new();

        [Serializable]
        public class PanelEntry
        {
            public string panelId;
            public UIPanel prefab;
        }
    }
}
```

**为什么缺资产不能学 SFXManager 直接 `yield break`**：SFX 挂了只是没声音；UI 骨架挂了，引导表现层跟着挂。降级策略 = `Resources.Load` 失败 → `ScriptableObject.CreateInstance<UIManagerConfig>()` 兜底（字段初始化器给出 1280x720/0.5 默认值）+ LogWarning，只是面板注册表为空，骨架照建。

### 4.4 `Integration/UIManager.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UI;

namespace Managers.UI
{
    /// <summary>
    /// MOMS UI 宿主：构建 UIRoot 三层根 Canvas、按注册表实例化/缓存面板、保证唯一 EventSystem。
    /// 面板缓存只增不减（Unload/池化留二期）。
    /// </summary>
    public class UIManager : IManager
    {
        public string Name => "UIManager";
        private static readonly List<Type> _dependencies = new();   // 无依赖 → 拓扑第一批
        public List<Type> Dependencies => _dependencies;
        private static UIManager Resolve => GameManager.Instance?.GetManager<UIManager>();

        private const string ConfigPath = "data/UIManagerConfig";
        private const string RootName = "UIRoot";
        // 显式列出要建的层（不遍历枚举——将来加 Overlay=200 预留层时不该自动建出空画布）
        private static readonly UILayer[] BuiltLayers = { UILayer.HUD, UILayer.Panel, UILayer.Guide };

        private UIManagerConfig _config;
        private GameObject _root;
        private readonly Dictionary<UILayer, Transform> _layerRoots = new();
        private readonly Dictionary<string, UIPanel> _cache = new();   // 实例缓存，含已关闭面板
        private readonly Dictionary<string, UIManagerConfig.PanelEntry> _registry = new();

        // ---------------- IManager ----------------

        public IEnumerator Initialize()
        {
            // ① 配置：Load 失败 → CreateInstance 兜底 + LogWarning（不能 yield break，理由见 4.3）
            // ② BuildRoot：new GameObject(RootName)；config.dontDestroyOnLoad → DontDestroyOnLoad
            // ③ foreach BuiltLayers → BuildLayerCanvas(layer, (int)layer)
            // ④ EnsureEventSystem()
            // ⑤ RebuildRegistry()
            // ⑥ 哨兵日志："[UI] 初始化完成：N 层画布 / 面板注册表 M 条"
            yield break;      // 全程同步，无 await——保证同帧先于对话/引导
        }

        public void Deinitialize()
        {
            // 学 SFXManager：UnityEngine.Object.Destroy(_root)；清空 _layerRoots/_cache/_registry；_config = null
        }

        // ---------------- 静态门面（业务侧只调这些） ----------------

        /// 打开面板。已开 → 置顶（SetAsLastSibling）。返回面板实例（方便业务直接拿组件），失败返回 null
        public static UIPanel Show(string panelId) { var m = Resolve; return m == null ? null : m.ShowPanel(panelId); }

        /// 关闭面板（实例留缓存）。未注册/未开 → LogWarning（防拼写错，开发期友好）
        public static void Hide(string panelId) => Resolve?.HidePanel(panelId);

        public static void Toggle(string panelId)
        {
            // IsOpen(panelId) ? Hide : Show
        }

        public static bool IsOpen(string panelId)
        {
            // 缓存命中且 activeSelf → true；其余 false
        }

        /// 关闭所有「经 Show 打开、仍在缓存里且 active」的面板。GuidePanel 由 GuideManager 自管，不受影响
        public static void CloseAllPanels()
        {
            // foreach _cache.Values → p.IsOpen → p.Hide()
        }

        /// 外部挂载入口（GuideManager 等用）。未知层 LogError 返 null
        public static Transform GetLayerRoot(UILayer layer)
        {
            // Resolve null → null；_layerRoots.TryGetValue(layer) 未命中 → LogError + null
        }

        // ---------------- 实例实现 ----------------

        private Transform BuildLayerCanvas(UILayer layer, int order)
        {
            // new GameObject($"{layer}Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            // canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            // 父 = _root；SetupScaler(scaler)；存入 _layerRoots[layer]，返回 transform
        }

        private void SetupScaler(CanvasScaler scaler)
        {
            // uiScaleMode = ScaleWithScreenSize；referenceResolution / matchWidthOrHeight 从 _config 读
        }

        private void EnsureEventSystem()
        {
            // ① FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) 命中 →
            //    若 inactive：LogWarning（inactive 的不处理输入，宁可警告别静默），仍然复用不动它
            // ② 未命中 → new GameObject("UIEventSystem", typeof(EventSystem), typeof(StandaloneInputModule))，父 = _root
            // 命名与 DialogueEventSystem 区分，出问题好排查
        }

        private void RebuildRegistry()
        {
            // foreach _config.panels：
            //  entry == null || entry.prefab == null → LogError continue
            //  entry.panelId 空 → 回退 prefab.GetComponent<UIPanel>()?.PanelId → 再退 prefab.name
            //  重复 id → LogError skip
            //  entry.panelId != prefab 组件 PanelId → LogWarning（注册表键以 config 为准）
        }

        private UIPanel ShowPanel(string panelId)
        {
            // ① 缓存命中：
            //    实例已被外部销毁（Unity 假值）→ 出缓存，继续走新建
            //    否则 panel.Show() + transform.SetAsLastSibling()，返回
            // ② 未命中：_registry 查 entry，查不到 / prefab 空 → LogError + null
            // ③ 层：entry.prefab.Layer（不实例化就能读）→ GetLayerRoot 实例版；null → LogError + null
            // ④ Instantiate(prefab, layerRoot)；go.name = prefab.name（去掉 "(Clone)"，
            //    防 PanelId 空回退时带 (Clone) 污染缓存键）
            // ⑤ GetComponent<UIPanel>()（prefab 根必有，防御判空）→ _cache[id] = panel → panel.Show() → 返回
        }

        private void HidePanel(string panelId)
        {
            // 缓存命中且 IsOpen → panel.Hide()；缓存未命中 → LogWarning
        }
    }
}
```

### 4.5 `Integration/GuideViewUGUI.cs`

```csharp
using Guide;
using TMPro;
using UnityEngine;

namespace Managers.UI
{
    /// <summary>
    /// 引导表现层（底部横幅）。实现 GuideSystem 的 IGuideView。
    /// Prefab 契约：根节点挂本组件；子节点 GuideText(必需) / ProgressText(可选) / ConditionText(可选)。
    /// 所有节点 Raycast Target = false（横幅不许吃射线）。
    /// </summary>
    public class GuideViewUGUI : MonoBehaviour, IGuideView
    {
        [SerializeField] private TextMeshProUGUI guideText;      // 主文案 ctx.Text
        [SerializeField] private TextMeshProUGUI progressText;   // "第 X/Y 步"
        [SerializeField] private TextMeshProUGUI conditionText;  // ctx.ConditionText，空则清空

        public void ShowStep(GuideStepContext ctx)
        {
            if (ctx == null) return;
            gameObject.SetActive(true);
            if (guideText)      guideText.text = ctx.Text;
            if (progressText)   progressText.text = $"第 {ctx.StepIndex + 1}/{ctx.StepCount} 步";
            if (conditionText)  conditionText.text = ctx.ConditionText ?? "";
        }

        public void Hide() => gameObject.SetActive(false);

        private void Reset() => AutoBind();      // 编辑器：首次添加组件时自动接线

        [ContextMenu("自动接线")]
        private void AutoBind()
        {
            // 按契约节点名补序列化引用（已有则不覆盖）：
            // guideText      ??= transform.Find("GuideText")?.GetComponent<TextMeshProUGUI>();
            // progressText   ??= transform.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
            // conditionText  ??= transform.Find("ConditionText")?.GetComponent<TextMeshProUGUI>();
        }
    }
}
```

> 设计说明：放 `Managers.UI` 而非 `UI`——它依赖 `Guide.IGuideView` + TMP + UIManager，是纯 Integration；`UI` 命名空间留给零游戏依赖的可移植层。

### 4.6 `Integration/TestPanel.cs`（可选，建议写——面板框架需要一个验证载体）

```csharp
using UnityEngine;
using UI;

namespace Managers.UI
{
    /// UIPanel 子类示范：生命周期日志 + Close() 供按钮 OnClick 绑定
    /// （Button 的 OnClick 拖本组件，函数选 TestPanel > Close）。
    public class TestPanel : UIPanel
    {
        protected override void OnShow() => Debug.Log($"[TestPanel] OnShow（{PanelId}）");
        protected override void OnHide() => Debug.Log($"[TestPanel] OnHide");

        /// 给 UI 按钮用的关闭入口
        public void Close() => UIManager.Hide(PanelId);
    }
}
```

### 4.7 `Editor/UIDebugMenu.cs`

无命名空间（对齐 GuideDebugMenu，Editor 目录已保证只在编辑器编译）。

```csharp
using UnityEditor;
using UnityEngine;
using Managers.UI;

public static class UIDebugMenu
{
    [MenuItem("UI/打开面板/TestPanel")]
    public static void ShowTestPanel() { /* EnsurePlaying 通过后 UIManager.Show("TestPanel") */ }

    [MenuItem("UI/关闭面板/TestPanel")]
    public static void HideTestPanel() { /* EnsurePlaying 通过后 UIManager.Hide("TestPanel") */ }

    [MenuItem("UI/关闭全部面板")]
    public static void CloseAll() { /* EnsurePlaying 通过后 UIManager.CloseAllPanels() */ }

    [MenuItem("UI/校验注册表")]
    public static void ValidateConfig()
    {
        // 非 Play 可用：Resources.Load<UIManagerConfig>("data/UIManagerConfig")
        // 逐条检查：prefab 非空 / 根节点挂 UIPanel / id 非空 / id 唯一 / entry.panelId 与组件 PanelId 一致
        // 全部 LogError/Warning 逐条列出，无问题则 Log("[UI校验] 注册表 N 条，全部通过 ✓")
    }

    private static bool EnsurePlaying()
    {
        // !Application.isPlaying → LogWarning("进 Play Mode 后再用") + return false
    }
}
```

---

## 五、GuideManager 三处修改（已核实行号）

文件头补两个 using（`UIManager` 在 `Managers.UI`，`UILayer` 在 `UI`，都是兄弟/外部命名空间，不写 using 找不到）：

```csharp
using Managers.UI;
using UI;
```

### ① 依赖声明（`GuideManager.cs:26-29`）

```csharp
private static readonly List<Type> _dependencies = new()
{
    typeof(InputManager),
    typeof(UIManager)       // 拓扑序保证 Initialize 时 Guide 层根已存在
};
```

### ② 表现层挂接（`GuideManager.cs:181-182`，替换 `_view = new NullGuideView();`）

```csharp
/// 表现层：实例化 ui/GuidePanel 到 Guide 层。契约 = 根节点上存在实现 IGuideView 的组件。
/// 任一环节失败 → 降级 NullGuideView（表现层永远不能拖死引导逻辑）。
private IGuideView CreateUguiView()
{
    var root = GameManager.Instance?.GetManager<UIManager>()?.GetLayerRoot(UILayer.Guide);
    if (root == null)
    {
        Debug.LogWarning("[Guide] Guide 层不可用，引导表现层降级 NullGuideView");
        return new NullGuideView();
    }
    var prefab = Resources.Load<GameObject>("ui/GuidePanel");
    if (prefab == null)
    {
        Debug.LogWarning("[Guide] 未找到 Resources/ui/GuidePanel.prefab，引导表现层降级 NullGuideView");
        return new NullGuideView();
    }
    var go = GameObject.Instantiate(prefab, root);
    var view = go.GetComponent<IGuideView>();
    if (view == null)
    {
        Debug.LogError("[Guide] GuidePanel 根节点缺少 IGuideView 实现（应为 GuideViewUGUI），已降级 NullGuideView");
        UnityEngine.Object.Destroy(go);      // 销毁残骸，防空横幅挡视线
        return new NullGuideView();
    }
    go.SetActive(false);                     // 同帧关闭，等第一条 StepStarted 再亮
    return view;
}
```

然后 `Initialize()` 中改为：

```csharp
//表现层：有 GuidePanel 资产用 UGUI 版，否则 NullObject（冒烟测试不受影响）
_view = CreateUguiView();
```

> ⚠️ 注意 `Object.Destroy` 必须写全名 `UnityEngine.Object.Destroy`——本文件同时 `using System;` + `using UnityEngine;`，裸 `Object` 会 CS0104 二义性。`Instantiate` 用 `GameObject.Instantiate` 同理。

> 调用点在 autoStart 扫描（:187）**之前**，自动触发的引导第一帧就有 View 可用——顺序别挪。

### ③ Hide 缺口修复（`ClearRunning()`，`GuideManager.cs:74`）

```csharp
private void ClearRunning()
{
    _view?.Hide();               // 表现层收尾：完成/中止两条路都经过这里
    _progress.runningGuideId = "";
    _progress.runningStepIndex = 0;
    SaveProgress();
}
```

一处覆盖两个事件（OnGuideFinished:66 / OnGuideAborted:68 都走 ClearRunning）；`ResetProgress()` 走 `_engine.Stop(false)` → Aborted → 同样覆盖。

---

## 六、资产搭建步骤（Inspector 操作级）

### 6.1 `UIManagerConfig.asset`

1. Project 窗口进 `Assets/Resources/data`，右键 **Create > MOMS > UIManagerConfig**，保持默认名。
2. Inspector 确认：Reference Resolution=(1280, 720)、Match=0.5、Dont Destroy On Load=✓。
3. Panels 本期先不填（TestPanel 建好后再回填，见 6.3 第 6 步）。

### 6.2 `GuidePanel.prefab`（用临时场景当工作台，防污染主场景）

1. File > New Scene > Empty，**不保存**，只当工作台。
2. Hierarchy 右键 **GameObject > UI > Image**——Unity 自动生成 Canvas + EventSystem + Image。把自动生成的 Image 改名 `GuidePanel`（这个临时 Canvas 只用来提供 UI 坐标系）。
3. 选中 GuidePanel，Rect Transform 左上 Anchor Presets 方格，**按住 Shift+Alt 点 bottom-stretch**（anchorMin(0,0)、anchorMax(1,0)、pivot(0.5,0)）；Pos Y=16；Height=0；Width 保持 stretch。
4. Add Component 三个：`GuideViewUGUI`、`Vertical Layout Group`、`Content Size Fitter`。
5. Image：颜色改黑、A=165，**取消勾选 Raycast Target**。
6. Vertical Layout Group：Padding L/R=24、T/B=12；Spacing=4；**Child Control Width/Height=勾（不选 Force）**；Child Force Expand W/H=不勾。Content Size Fitter：**Vertical Fit=Preferred Size**（Horizontal 不约束，宽度由 stretch 锚给）。
7. 右键 GuidePanel > **UI > Text - TextMeshPro** 三次，依次改名 `ProgressText` / `GuideText` / `ConditionText`：

   | 节点 | 字号 | 颜色 | 对齐 | 必需 |
   |---|---|---|---|---|
   | ProgressText | 18 | 白 85% | Left / Middle | 可选 |
   | GuideText | 26 | 白 | Left / Middle，Word Wrapping 开 | **必需** |
   | ConditionText | 18 | 灰 60% | Left / Middle | 可选 |

   每个 TMP：**Font Asset 槽拖入 `SourceHanSansSC-Medium SDF`**（不换的话中文全是空白块）；**取消勾选 Raycast Target**；删掉默认占位文本 "New Text"。
8. GuidePanel 根的 GuideViewUGUI 组件标题上右键 > **自动接线**，确认三个槽已连上对应子节点。
9. 把 GuidePanel 从 Hierarchy 拖到 Project 的 `Assets/Resources/ui`（先建 `ui` 文件夹）生成 prefab。
10. **清理（重要）**：Hierarchy 删除自动生成的 Canvas 和 EventSystem（连根删），场景**不保存**。回主场景。
    > 为什么：若场景里存了一个 EventSystem，UIManager 会复用它；切场景后它被卸载，而对话插件只在构建时检查一次 → 之后所有 UI 点击全死。

### 6.3 `TestPanel.prefab`（可选，同一临时场景流程）

1. UI > Image 改名 `TestPanel`；Anchor Presets 点 **center**（Shift+Alt）；Width=420、Height=260；Image 色深灰，**Raycast Target 保留勾选**（面板要吃按钮点击）。
2. Add Component > `TestPanel`；Inspector 填 Panel Id=`TestPanel`、Layer=`Panel`。
3. 子节点 `Title`（UI > Text - TextMeshPro，文本"测试面板"，换 SourceHanSansSC 字体，居中）；子节点 `CloseButton`（UI > Button - TextMeshPro，文本"关闭"，换字体）。
4. CloseButton 的 Button 组件 OnClick() 点 **+**，把 Hierarchy 里的 TestPanel 拖进对象槽，函数选 **TestPanel > Close()**。
5. 拖到 `Assets/Resources/ui/panels`（新建文件夹）成 prefab；删场景临时对象与自动 Canvas/EventSystem；不保存。
6. 回 `UIManagerConfig.asset`：Panels +，Element0：Panel Id=`TestPanel`，Prefab=拖入 TestPanel.prefab。

> **ConditionText 空文案时横幅变矮是特性**（VerticalLayoutGroup + Preferred Height：无附加信息不占位），不是 bug。

---

## 七、实施顺序（每步「写完即编译」，编译过了再做自测）

| 步 | 内容 | 自测 |
|---|---|---|
| 1 | UILayer.cs、UIPanel.cs（纯定义无行为） | 编译通过 |
| 2 | UIManagerConfig.cs → 建资产（6.1） | 菜单能创建资产、字段齐全 |
| 3 | UIManager.cs（门面 API 一起写完） | Play：Hierarchy 出现 UIRoot（DDOL 标记）、UIEventSystem、三 Canvas；Inspector 核对 sortingOrder=10/20/40、scaler=1280x720/0.5；Console 无 "Multiple EventSystem" 告警；Hierarchy 搜索 `t:EventSystem` 全场唯一 |
| 4 | TestPanel.cs + prefab + config 注册 + UIDebugMenu.cs | Play：UI/打开面板/TestPanel → 实例出现在 PanelCanvas 下且在兄弟之上；Toggle/Hide 正常；关闭后 active=false 但对象仍在（缓存）；UI/校验注册表全绿 |
| 5 | GuideViewUGUI.cs + GuidePanel.prefab（6.2） | 非关键步骤，随步 6 一起验 |
| 6 | GuideManager 三处改动（§五） | Play：Guide/重置进度 → Guide/开始基础引导 → GuidePanel 出现在 GuideCanvas 下显示"第 1/N 步"；跑完或 Guide/停止引导后横幅消失（**重点回归 Hide 缺口**） |
| 7 | 端到端清单（§八） | 全过 |

## 八、端到端验证清单

1. **UIRoot 结构目检**：Play 后 UIRoot/DDOL + 三 Canvas + UIEventSystem 齐备、参数正确；退出 Play 无报错，再进重建正常。
2. **EventSystem 唯一性**：初始 1 个；走近 NPC 触发对话后**仍 1 个**（对话插件复用了 UIEventSystem），对话文本与按钮可点。
3. **排序目检**：TestPanel 正常显示；引导横幅叠在 TestPanel 之上；播放对话时 DialogueCanvas 盖住横幅与面板；对话结束后引导横幅恢复可见。
4. **引导全流程显隐**：开始 → 文案/进度更新 → 完整跑完横幅消失且 autoStart 不再触发；中途 Guide/停止引导（Aborted 路径）横幅也消失；重置后可复触发。
5. **CloseAll 与引导互不干扰**：引导显示中执行 UI/关闭全部面板，横幅**不**消失（它不经 UIManager.Show）。
6. **分辨率自适应**：Game 视图 1280x720 与 1920x1080 各一遍，横幅宽度贴屏、字不糊、位置不变。
7. **场景干净**：场景文件无 Canvas/EventSystem 混入（§6.2 第 10 步的清理是否执行）。
8. **降级路径冒烟**（验完还原）：临时把 GuidePanel 改名 → Play 应 LogWarning「未找到 ui/GuidePanel」并走 NullGuideView 日志流，引导逻辑照常推进。

---

## 九、关键设计决策记录（为什么这么设计）

| 决策 | 理由 |
|---|---|
| 三层兄弟根 Canvas，不是单 Canvas + 嵌套子排序 | Overlay 下根 Canvas 按 sortingOrder 全局排序，与对话画布关系直白；嵌套要 overrideSorting 才参与全局排序，语义绕、排查贵；每层独立 GraphicRaycaster/合批本就省不掉 |
| EventSystem 检查用 FindFirstObjectByType(Inactive.Include) | 与对话插件 EnsureEventSystem（DialogueUI.cs:594-603）双向收敛到唯一实例；inactive 命中要警告（不处理输入，静默复用会误判）。UIManager 同步段初始化必先于对话懒加载，反向也安全 |
| GuideManager 加载用 `Resources.Load<GameObject>` + `GetComponent<IGuideView>()` | 引导系统不引用 UI 具体类型，降级 NullGuideView 三连——表现层永远不能拖死引导逻辑；GuideSystem 对 UI 的依赖只剩 UILayer 枚举与 GetLayerRoot |
| layer 放 UIPanel 组件，不放 config | 单一事实源跟 prefab 走（谁建面板谁看得见）；UIManager 不实例化即可 `prefab.GetComponent<UIPanel>().Layer`；放 config 会两处漂移 |
| UIManagerConfig 缺失走 CreateInstance 兜底而非 yield break | UI 骨架挂了引导表现层一起挂；字段初始化器天然提供默认值，损失只是空面板注册表 |
| 本期不做 IUpdatable | 无逐帧逻辑；且 GameManager 只在初始化时收集 IUpdatable（GameManager.cs:83-85），中途补接口不生效 |
| 面板缓存 SetActive 不 Destroy | 简单够用；Unload/池化留二期，接项目 `Script/pool/` 即可 |

## 十、附录：遗留与二期路标

1. **排序契约**（注释固化）：HUD 10 < Panel 20 < Guide 40 < DialogueCanvas 100（外部包硬编码）< 预留 200（Fade/Loading）。若将来对话插件开放 sortingOrder 配置，同步本注释。
2. **对话期间横幅被盖**：GuideManager.OnUpdate 在 IsAnyPlaying 时不 Tick，旧横幅被对话底板盖住——接受。嫌丑再给 GuideViewUGUI 订阅 DialogueManager 状态收起，GuideManager 不动。
3. **HUD 二期**：`HealthManageComponent` 的 `OnChanged/OnDied` 事件已备好（0 订阅者），HUD 面板 Show 到 HUD 层订阅即可，框架零改动。⚠️ 伤害管线现状不扣血（`DamageComponent.Hit` 未调 TakeDamage），HUD 前需先接通。
4. **伤害飘字二期**：挂点 `Hitbox.Hit` → `HitResult`（Hitbox.cs:114-141），生成即 `GetLayerRoot(HUD)` 实例化，正是"运行时动态生成 Prefab"的用武之地。
5. **面板 Unload/池化**：UIManager 加 `Unload(panelId)` + UIPanel 加 `virtual OnRecycle()`。
6. **挖孔高亮**：做成 GuideView 的装饰层（TargetKey 字段已留），GuideManager 不动。
7. **输入迁移**：切 Input System 包时 EventSystem 需换 InputSystemUIInputModule，破坏性变更记迁移清单。
8. **FadeManager 修复路标**：`fadeCanvasPrefab` 恒 null（纯 C# 管理器序列化字段无赋值来源，淡入淡出一直空转）——改 Resources.Load + 挂到预留层（届时 UILayer 加 Overlay=200）。
9. **顺带发现**：`InputManager.cs:48` 加载 `"PlayerInputData"` 但资产实际在 `data/` 下，路径不匹配会静默落入硬编码默认键位——与本期无关，记录待修。
