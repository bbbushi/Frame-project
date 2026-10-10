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
            float h = m_Slots.Count > 0 ? m_TotalHeight : m_TotalHeight + padding.vertical;
            SetLayoutInputForAxis(h, h, 0f, 1);
        }
    }
}