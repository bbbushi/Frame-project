using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;          // LayoutGroup / LayoutUtility 在这

namespace Managers.UI
{
    /// <summary>
    /// 流式换行布局：子元素按 preferredWidth 依次排，排不下换行，行高取行内最高。
    /// UGUI 官方布局件没有 Flow——本类从 LayoutGroup 裸写，
    /// 管线与官方 HorizontalOrVerticalLayoutGroup 同构（CalcAlongAxis/SetChildrenAlongAxis 两段式）。
    /// 刻意简化：childAlignment 固定 UpperLeft（从上往下、从左往右）；spacing 水平垂直共用；
    /// 不做 childControlWidth 开关（恒为"读子级 preferred、写子级尺寸"）——差异去对照阅读里认知
    /// </summary>
    [AddComponentMenu("Layout/Flow Layout Group")]
    public class FlowLayout : LayoutGroup
    {
        [Tooltip("元素水平间距 & 行间距（简化共用一个值）")]
        [SerializeField] float m_Spacing = 10f;
        public float spacing
        {
            get{ return m_Spacing; }
            set{ SetProperty(ref m_Spacing, value); } // 基类工具：值变了才 SetDirty
        }
        // ── 两段式之间的数据通道：水平段装箱，垂直段消费 ──

        // m_Slots 存储每个子元素的槽位信息，包括 x、y、width、height
        // 每个子级一个槽位：x/y 是容器局部坐标（y 向上为正、首行 y=0 往下递减），width/height 为分配尺寸
        readonly List<Rect> m_Slots = new List<Rect>();
        float m_TotalHeight;    // 装箱结果总高（垂直段上报用）


        // ═══ 水平段 ═══
        /// <summary>
        /// 计算水平方向的布局输入，包括最小宽度、首选宽度和弹性宽度。
        /// 最小宽度取最宽单元素，首选宽度取全部一行，弹性宽度为 0。
        /// 其中oneRow 为全部一行所需的宽度，widest 为最宽单元素的宽度。
        /// SetLayoutInputForAxis 方法用于设置水平方向的布局输入。
        /// </summary>
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();  //rectChildren 的收集在这
            //保守协商：preferred = 全部一行；min = 最宽单元素；flexible = 0（不参与剩余空间瓜分）
            float oneRow = padding.horizontal;
            float widest = 0f;
            for(int i = 0; i < rectChildren.Count; i++)
            {
                float w = LayoutUtility.GetPreferredWidth(rectChildren[i]);
                oneRow += w + (i > 0 ? m_Spacing : 0f);
                widest = Mathf.Max(widest, w);
            }
            SetLayoutInputForAxis(widest + padding.horizontal, oneRow, 0f, 0);
        }
        // ═══ 垂直段：上报用水平段的真实装箱结果（还没装箱过就保守单行）═══
        /// <summary>
        /// 计算垂直方向的布局输入，包括最小高度、首选高度和弹性高度。
        /// 最小高度取行内最高单元素，首选高度取实际装箱总高，弹性高度为 0。
        /// </summary>
        public override void CalculateLayoutInputVertical()
        {
            float h;
            if (m_Slots.Count > 0)
            {
                h = m_TotalHeight;   // 水平段已装箱：行数定了，总高就是真实值
            }
            else
            {
                // 还没装箱过：保守按"单行"估——最高子级的高度 + 上下 padding
                // （rectChildren 为空时循环零次，h = padding.vertical，空容器高度 = 纯 padding，也自洽）
                float tallest = 0f;
                for (int i = 0; i < rectChildren.Count; i++)
                    tallest = Mathf.Max(tallest, LayoutUtility.GetPreferredHeight(rectChildren[i]));
                h = tallest + padding.vertical;
            }
            SetLayoutInputForAxis(h, h, 0f, 1);
        }
        // ═══ 水平布置：装箱（真正的行结构在这算——rect.width 已是终值）═══
        /// <summary>
        /// 设置水平方向的布局，包括每个子元素的 x 坐标和宽度。
        /// CalculateLayoutInputHorizontal()计算布局输入，该方法根据输入设置实际布局。
        /// PackRows() 方法负责将子元素按行装箱数据，更新 m_Slots 和 m_TotalHeight。
        /// 最终通过 SetChildAlongAxis() 方法将每个子元素的 x 坐标和宽度设置到布局中。
        /// </summary>
        public override void SetLayoutHorizontal()
        {
            m_Tracker.Clear();                          // 基类规则：一轮一清
            Packrows();

            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 0, m_Slots[i].x + padding.left, m_Slots[i].width);
        }
        public override void SetLayoutVertical()
        {
            // pos 语义（LayoutGroup 源码注释原文 "The position from the left side or top"）：
            // 垂直轴 pos = 块顶距父【顶】的向下偏移——anchor 被钉在 Vector2.up（父左上角），
            // 内部写 anchoredPosition.y = -pos - size*(1-pivot.y)，pivot 项就是实测 anchoredY 偏差的来源。
            // m_Slots.y = -(行顶偏移 + h)，翻回顶部偏移：pos = padding.top + 行顶偏移 = padding.top - m_Slots.y - h。
            // 核对（padding.top 10）：首行块 10-(-40)-40=10 → 块顶距父顶 10；换行块 10-(-90)-40=60 → 第二行 ✓
            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 1,
                    padding.top - m_Slots[i].y - m_Slots[i].height, m_Slots[i].height);
        }
        /// <summary>
        /// 装箱逻辑：
        /// 先清空槽位信息
        /// 获取每个子元素的槽位信息（行列坐标、宽度和高度）
        /// 按行装箱，更新 m_Slots 和 m_TotalHeight
        /// （只存数据，不处理实际布局，实际布局由 SetLayoutHorizontal/SetLayoutVertical 处理）
        /// </summary>
        void Packrows()
        {
            m_Slots.Clear();
            m_TotalHeight = 0f;
            // 可用宽度 = 总宽度 - 左右 padding
            float avail = rectTransform.rect.width - padding.horizontal;
            float x = 0f, y = 0f, rowHeight = 0f;
            for(int i = 0; i < rectChildren.Count; i++)
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