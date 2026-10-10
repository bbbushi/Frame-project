# UI 三期设计方案（自定义布局组件：FlowLayout 流式换行布局）

> 分工延续一二期：本文档给出全部设计与成员级骨架，代码由你手写、资产由你手建、验证由你自跑。
> 本期是**三大方向考核差距项**（交互式 EditorWindow 另立一期），目标不是"多一个组件"，
> 而是把 UGUI 布局系统的底层管线打穿——写完你应当能解释"改一个 spacing，屏幕上的按钮为什么、
> 经过哪几步、在渲染前的哪个时机动了"。

---

## 一、目标与范围

**一句话**：从 `LayoutGroup` 裸写一个 UGUI 官方没有的 `FlowLayout`（子元素按自身宽度依次排、排不下自动换行、行高取行内最高——背包/标签云/成就列表的通用布局）。

| 本期做 | 本期不做（留作进阶/另期） |
|---|---|
| `FlowLayout.cs` 从 LayoutGroup 裸写（~120 行，递进两步：直排→换行） | childControlWidth / childForceExpand 开关矩阵（对照阅读里认知差异即可） |
| 尺寸协商（min/preferred 上报父级，配合 ContentSizeFitter） | childAlignment 九宫对齐（固定 UpperLeft，`GetStartOffset` 留白） |
| `UI/布局冒烟` 纯逻辑菜单（非 Play 可跑的断言冒烟，项目传统） | 交互式 EditorWindow（三大方向另一差距项） |
| 对照阅读官方 ugui 包源码三处（写完后的收尾动作） | 实战接入（背包等真实面板——等有真实需求再挂） |

---

## 二、现状事实（设计依据）

1. **官方缺口**：`Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Layout/` 下 14 个文件，Horizontal/Vertical/Grid 三兄弟齐全，**没有 Flow**——这是真实缺口不是玩具练习（NGUI/UIToolkit 都有，UGUI 一直靠社区插件补）。
2. **已有使用基础**：GuidePanel 已在用官方 LayoutGroup + ContentSizeFitter（一期成果）——你是布局系统的**使用者**，本期变成**实现者**。
3. **基类给的东西**（`LayoutGroup.cs` 已核实）：`rectChildren`（自动收集直系、active、非 `ILayoutIgnorer` 的子级）、`m_Tracker`（驱动属性锁）、`padding`/`childAlignment` 序列化、`SetDirty()`（标记重建）、`SetLayoutInputForAxis(min, pref, flex, axis)`（上报尺寸）、`SetChildAlongAxis(rect, axis, pos[, size])`（写子级位置并自动记驱动锁）。
4. **冒烟传统**：`Attributes/效果冒烟（纯逻辑）` 等菜单证明"非 Play 纯计算断言"在本项目可行；⚠ MenuItem 里 Instantiate UGUI **不渲染**的老坑（UIDebugMenu.ShowNextFrame）——本冒烟只做计算+断言+当场销毁，不过帧，不受此坑影响。

---

## 三、布局系统管线（本期核心知识，先读懂再动手）

### 3.1 两段式——为什么是四个方法而不是两个

布局不是"一次算完"，引擎分两个 pass，**水平先于垂直**：

```
CalculateLayoutInputHorizontal()   ①收集 rectChildren ②上报本容器 min/preferred 宽
        ↓ （布局系统拿所有上报值协商：父容器决定你的最终 rect.width）
SetLayoutHorizontal()              用真实 rect.width 算行结构、写每个子级的 x/宽度
        ↓
CalculateLayoutInputVertical()     上报本容器 min/preferred 高（此时可用水平段算好的行结构）
        ↓
SetLayoutVertical()                写每个子级的 y/高度
```

水平先行的原因：垂直布局常常依赖水平结果（FlowLayout 的行数 = 宽度决定），反过来几乎不成立。
**两段之间的数据通道**：`SetLayoutHorizontal` 里算好的行结构存字段，`SetLayoutVertical` 消费——这是合法且官方同款的做法（`HorizontalOrVerticalLayoutGroup` 的 `CalcAlongAxis`/`SetChildrenAlongAxis` 同构）。

### 3.2 尺寸协商——min / preferred / flexible 三级

子元素怎么"告诉"父级自己要多大：实现 `ILayoutElement`（LayoutElement 组件、TMP_Text、Image 都内建实现）。
读别人尺寸的唯一正道：`LayoutUtility.GetPreferredWidth(rect)`（内部找该物体上 priority 最高的 ILayoutElement）。
⚠ **裸 Image 没挂 LayoutElement 时 GetPreferredWidth 返回 0**——测试色块要么挂 LayoutElement 填值，要么用 TMP 文本（有内建 preferred），否则全叠原点，别懵。

### 3.3 驱动锁——"Inspector 变灰"的来历

`SetChildAlongAxis` 内部 `m_Tracker.Add(this, child, DrivenTransformProperties.Anchors | ...)`：
布局系统接管子级的锚点/位置/尺寸后，**Inspector 对应字段灰锁、手拖会被下一轮布局覆盖**——这是 by design。
每轮 `SetLayoutHorizontal` 开头 `m_Tracker.Clear()`（基类规则：一轮一清）。

### 3.4 dirty 链——改 spacing 为什么立即生效

```
你改 spacing（SetProperty → SetDirty）
  → LayoutRebuilder.MarkLayoutForRebuild(rectTransform)
  → CanvasUpdateRegistry 排队，本帧渲染前 Rebuild
  → 依次调回四个方法 → 子级 RectTrans 全部更新 → 才轮到渲染
```
基类已接好的回调：`OnEnable/OnDisable/OnTransformChildrenChanged`（增删子级）/`OnRectTransformDimensionsChange`（容器自身尺寸变）都自动 SetDirty。**你唯一要操心的是"布局结果影响自身 preferred 高"时的再标记**（见 §4 骨架注释）。

### 3.5 FlowLayout 的鸡生蛋（本组件唯一真难点）

行结构依赖容器宽度；容器宽度若又依赖 preferred（父级挂 ContentSizeFitter）就循环了。
**标准解**（NGUI/社区 FlowLayout 同款）：
- `CalculateLayoutInputHorizontal` 上报**保守值**：preferred = 全部子级排一行的总宽（"我最理想这么宽"）
- 真实行结构**只在 `SetLayoutHorizontal` 里算**——那时 `rectTransform.rect.width` 是父级协商后的终值，装箱以此为准
- 行数变了总高就变 → 垂直段 `SetLayoutInputForAxis` 上报真实总高 → 父级 fitter 下轮把容器高度包住内容

---

## 四、FlowLayout 成员级骨架（手写照抄级）

`Assets/UISystem/Layout/FlowLayout.cs`，namespace `Managers.UI`（对齐 UIManager）：

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;          // LayoutGroup / LayoutUtility 在这

namespace Managers.UI
{
    /// <summary>流式换行布局：子元素按 preferredWidth 依次排，排不下换行，行高取行内最高。
    /// UGUI 官方布局件没有 Flow——本类从 LayoutGroup 裸写，
    /// 管线与官方 HorizontalOrVerticalLayoutGroup 同构（CalcAlongAxis/SetChildrenAlongAxis 两段式）。
    /// 刻意简化：childAlignment 固定 UpperLeft（从上往下、从左往右）；spacing 水平垂直共用；
    /// 不做 childControlWidth 开关（恒为"读子级 preferred、写子级尺寸"）——差异去对照阅读里认知</summary>
    [AddComponentMenu("Layout/Flow Layout Group")]
    public class FlowLayout : LayoutGroup
    {
        [Tooltip("元素水平间距 & 行间距（简化共用一个值）")]
        [SerializeField] float m_Spacing = 10f;
        public float spacing
        {
            get { return m_Spacing; }
            set { SetProperty(ref m_Spacing, value); }   // 基类工具：值变了才 SetDirty
        }

        // ── 两段式之间的数据通道：水平段装箱，垂直段消费 ──
        // 每个子级一个槽位：x/y 是容器局部坐标（y 向上为正、首行 y=0 往下递减），width/height 为分配尺寸
        readonly List<Rect> m_Slots = new List<Rect>();
        float m_TotalHeight;    // 装箱结果总高（垂直段上报用）

        // ═══ 水平段 ═══
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();      // ← 别漏：rectChildren 的收集在这
            // 保守协商：preferred = 全部一行；min = 最宽单元素；flexible = 0（不参与剩余空间瓜分）
            float oneRow = padding.horizontal;
            float widest = 0f;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                float w = LayoutUtility.GetPreferredWidth(rectChildren[i]);
                oneRow += w + (i > 0 ? m_Spacing : 0f);
                widest = Mathf.Max(widest, w);
            }
            SetLayoutInputForAxis(widest + padding.horizontal, oneRow, 0f, 0);
        }

        // ═══ 垂直段：上报用水平段的真实装箱结果（还没装箱过就保守单行）═══
        public override void CalculateLayoutInputVertical()
        {
            float h = m_Slots.Count > 0 ? m_TotalHeight : 行内最高单元素 + padding.vertical;
            SetLayoutInputForAxis(h, h, 0f, 1);
        }

        // ═══ 水平布置：装箱（真正的行结构在这算——rect.width 已是终值）═══
        public override void SetLayoutHorizontal()
        {
            m_Tracker.Clear();                          // 基类规则：一轮一清
            PackRows();

            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 0, m_Slots[i].x + padding.left, m_Slots[i].width);
        }

        // ═══ 垂直布置：消费水平段的槽位 ═══
        public override void SetLayoutVertical()
        {
            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 1, m_Slots[i].y, m_Slots[i].height);
        }

        /// <summary>装箱：固定宽度逐个放，放不下换行。first-fit 简单贪心，不回头、不重排</summary>
        void PackRows()
        {
            m_Slots.Clear();
            m_TotalHeight = 0f;
            float avail = rectTransform.rect.width - padding.horizontal;
            float x = 0f, y = 0f, rowHeight = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                float w = LayoutUtility.GetPreferredWidth(rectChildren[i]);
                float h = LayoutUtility.GetPreferredHeight(rectChildren[i]);

                // x > 0 才换行：首元素再宽也硬放（防"永远放不进任何行"死循环）
                if (x > 0f && x + w > avail)
                {
                    x = 0f;
                    y -= rowHeight + m_Spacing;         // UGUI 局部系 y 向上为正：下一行 = y 减
                    rowHeight = 0f;
                }

                m_Slots.Add(new Rect(x, y - h, w, h));  // 槽位顶边贴行顶：y-h 为底
                x += w + m_Spacing;
                rowHeight = Mathf.Max(rowHeight, h);
            }
            if (rectChildren.Count > 0)
                m_TotalHeight = -y + rowHeight + padding.vertical;   // y 是负数往下长，总高取反
        }
    }
}
```

**骨架之外唯一可能要补的一行**（先不加，验证清单第 5 条不过再加）：
容器宽度变化 → 基类 `OnRectTransformDimensionsChange` 已 SetDirty → 全链自动重跑，理论上**不需要**额外代码；
但若出现"行数变了、父级 ContentSizeFitter 高度没跟上"，在 `SetLayoutVertical` 末尾对自身 `LayoutRebuilder.SetLayoutVerticalFor` …… 先别记这个，大概率用不上，出了再查。

---

## 五、冒烟菜单（并进 UIDebugMenu.cs，~40 行）

```csharp
[MenuItem("UI/布局冒烟（纯逻辑）")]
public static void LayoutSmoke()
{
    // 内存搭台：Canvas → FlowLayout 容器(width 300) → 三块 LayoutElement(preferred 100×40)
    // 全程不进 Play、不留对象（算完断言完立即 DestroyImmediate——MenuItem UGUI 渲染坑只影响
    // "过帧渲染的对象"，纯计算+当场销毁不受影响）
    //
    // 断言①（直排）：ForceRebuildLayoutImmediate 后三块 x 依次 10 / 120 / 230（padding 10 + spacing 10）
    // 断言②（换行）：容器压到 width 250 → rebuild → 第三块 y = -(40+10)（换到第二行）
    //                且第一二块 y 不变
    // 断言③（总高上报）：容器 preferredHeight == 10+40+10+40+10 = 110（padding.vertical 10）
    // 三条全过 Debug.Log("[布局冒烟] ✓ 直排/换行/总高 全过")，任何一条不过 LogError 并停
}
```

要点：`LayoutRebuilder.ForceRebuildLayoutImmediate(rect)` 是"不等渲染帧、现在就算"的测试用钥匙；
断言从 `child.anchoredPosition` 和 `(container as ILayoutElement).preferredHeight` 读回。

---

## 六、工作台目检步骤（临时场景流程，一二期惯例）

1. 临时空场景 → UI → Image 改名 `FlowBox`（带出的 Canvas/EventSystem 留着，目检完整场景丢弃不保存）
2. FlowBox：RectTransform 宽 500 高 300；Add Component → **Layout → Flow Layout Group**（写完代码才会有）
   - Spacing 摆 10；Padding 四边 10
3. 造 6 个子块：UI → Text(TMP)，FontSize 随意、文本给长短不一的内容（TMP 的 preferred 由文本长度定——天然不等宽测试集）
   ⚠ 别忘了 Font Asset 换 SourceHanSansSC（GuideText 教训）；或者更省：Image + LayoutElement(preferredWidth 手填 60/120/80 乱序)
4. **目检清单**：
   - 单行放得下的从左往右排；排不下的自动掉到第二行
   - 行内块顶边对齐、行高 = 行内最高块
   - 拖 FlowBox 的 width（Editor 里直接拖）→ 排布**实时**重算（dirty 链活的证明）
   - 点任一子块：Inspector 里 anchoredPosition / anchorMinMax **灰锁**（驱动锁活的证明）
   - FlowBox 再挂 ContentSizeFitter(Vertical Fit=Preferred) → 容器高度自动包住两行内容（协商链活的证明）
   - 同台摆一个官方 HorizontalLayoutGroup 对照：它不换行只压缩/溢出——差异亲手摸到

---

## 七、实施顺序（写完即编译，过了再下一步）

1. **空骨架**：四方法 + base 调用 + spacing 属性（编译过即可，行为无变化）
2. **直排版**：PackRows 写成"永不换行"（删掉 if 那三行）→ 工作台目检 = 自制 HorizontalLayoutGroup
3. **换行版**：加回换行 if → 目检窄容器换行、行高对齐
4. **冒烟菜单**：三条断言全绿
5. **ContentSizeFitter 联动**目检（§六.4）
6. **对照阅读**（§八）——读不懂的地方回自己代码找对应

---

## 八、对照阅读（收尾动作，UGUI 包源码在 PackageCache）

写完自己的 FlowLayout，带着三个问题读官方实现（各 ~15 分钟）：

| 读什么 | 带着的问题 |
|---|---|
| `LayoutGroup.cs` 的 `SetChildAlongAxis` 重载 + `DrivenRectTransformTracker` | 我 Inspector 里看到的灰锁，官方怎么记的？驱动标记清了没清会怎样？ |
| `HorizontalOrVerticalLayoutGroup.cs` 的 `CalcAlongAxis`/`SetChildrenAlongAxis` | 官方的 childControlWidth/childForceExpand 开关矩阵比我多了什么能力、付了什么复杂度代价？（对照你"恒读 preferred 写尺寸"的简化） |
| `LayoutRebuilder.cs` 的 `Rebuild` | 我调 ForceRebuild 时引擎实际按什么顺序调那四个方法？为什么水平先于垂直？（§3.1 的引擎侧证据） |

读罢在 FlowLayout.cs 头部注释补一行你自己的结论（一句话即可）——这行注释就是本期的毕业证。

---

## 九、验证清单（端到端）

1. 编译：离线 csc 全项目零错误（FlowLayout 用 UnityEngine.UI，CLAUDE.md 命令的 refs 已含 UGUI 包 dll，不用改）
2. 冒烟菜单三条断言全绿（非 Play 可跑）
3. 工作台目检：换行/行高/实时重排/灰锁/fitter 联动五项全过
4. 对照项：HorizontalLayoutGroup 同场景行为差异能口头说出一条
5. 回归：GuidePanel 引导横幅、HUD 血条、对话面板排版不受影响（没动任何官方件，理论零风险，跑一遍防意外）
6. **毕业证**：能不看文档讲出"改 spacing 到屏幕上按钮移动"的完整链路（SetDirty → MarkLayoutForRebuild → CanvasUpdateRegistry → 四方法 → 渲染前）

---

## 十、设计决策记录

1. **从 LayoutGroup 继承而非裸 UIBehaviour 实现 ILayoutController**：rectChildren 收集/tracker/padding/SetDirty 基类全给了，重造这些无教学价值；裸接口版的理解收益放对照阅读里拿。
2. **preferred = 全部一行（保守协商）**：解 §3.5 鸡生蛋的标准姿势——上报值只是"理想态"，行结构永远以 SetLayoutHorizontal 的真实 rect 为准。
3. **行结构缓存在字段、两段间传递**：官方同构做法（CalcAlongAxis 预算、SetChildrenAlongAxis 消费），不是投机取巧。
4. **不做 childControlWidth 开关矩阵**：官方那套是通用性代价（四种组合×两轴），教学版固定一种语义，差异认知留给对照阅读——"知道少了什么"比"全都有但不知道为什么"值钱。
5. **alignment 固定 UpperLeft**：九宫对齐是 GetStartOffset 的排列组合题，跟管线无关，砍掉聚焦。
6. **装箱用 first-fit 贪心**：不回头不重排——背包布局界同类取舍（NGUI 同款），最优装箱是算法题不是 UI 管线题。
7. **首元素强放防死循环**：`x > 0` 才判换行——单元素比容器还宽时宁可溢出也不无限换行。

---

## 十一、遗留与展望

- 实战接入：背包/成就/标签面板出现时直接挂（零代码需求已满足）
- childControlWidth 矩阵 / alignment 九宫 / 最优装箱：真实需求出现再做
- 交互式 EditorWindow（三大方向另一差距项）：候选载体——面板预览器或 AI 参数实时调节器，另立一期
