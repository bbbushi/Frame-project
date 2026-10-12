using Managers.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 布局冒烟测试（纯逻辑，不进 Play Mode）：
/// 内存搭 RectTransform 层级（容器挂 FlowLayout + 三块 LayoutElement），
/// LayoutRebuilder.ForceRebuildLayoutImmediate 立刻驱动四段管线，断言直排 / 换行 / 总高上报。
/// MenuItem 里 Instantiate 的 UGUI 对象"活着但不渲染"的坑只影响过帧渲染——
/// 这里算完即毁、不过帧，纯计算不受影响。
/// 数字依据（块 preferred 100×40，spacing 10，padding 四边 10）：
///   容器宽 350 → avail 330 ≥ 320（3×100+2×10）→ 单行直排，x = 10/120/230；
///   容器宽 250 → avail 230，第三块 220+100=320 > 230 → 换行：
///     行顶 y = -(40+10) = -50，元素自身再降 h → anchoredY = -90；
///   两行总高 preferredHeight = 10+40+10+40+10 = 110（上下 padding + 行1 + spacing + 行2）。
/// </summary>
public static class LayoutSmokeTest
{
    private static int _failed;

    [MenuItem("UI/布局冒烟（纯逻辑）")]
    public static void SmokeTest()
    {
        _failed = 0;

        // ── 搭台：无 Canvas 也能算——ForceRebuild 只走 RectTransform/布局管线，不依赖渲染 ──
        var container = new GameObject("FlowBox").AddComponent<RectTransform>();
        try
        {
            container.sizeDelta = new Vector2(350f, 300f);   // 先给"直排放得下"的宽度

            var flow = container.gameObject.AddComponent<FlowLayout>();
            flow.padding = new RectOffset(10, 10, 10, 10);
            flow.spacing = 10f;

            var children = new RectTransform[3];
            for (int i = 0; i < children.Length; i++)
            {
                children[i] = new GameObject($"Block{i}").AddComponent<RectTransform>();
                children[i].SetParent(container, false);
                var le = children[i].gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 100f;   // 裸 RectTransform 无内建 preferred，
                le.preferredHeight = 40f;   // 不挂 LayoutElement 则 GetPreferred 返回 0，全叠原点
            }

            // ── 用例一：350 宽直排（320 ≤ avail 330）──
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);   // 不等渲染帧，立刻跑四段管线
            // 读回用 offsetMin/offsetMax（锚点框偏移，pivot 无关）——别用 anchoredPosition：
            // 它读的是 pivot 中心位置，pivot 默认 (0.5,0.5) 会整体偏半宽/半高（实测 x 全 +50 的来源）
            Assert(Eq(children[0].offsetMin.x, 10f) && Eq(children[1].offsetMin.x, 120f)
                && Eq(children[2].offsetMin.x, 230f),
                $"用例一 直排 offsetMin.x=10/120/230（实际 {children[0].offsetMin.x}/{children[1].offsetMin.x}/{children[2].offsetMin.x}）");
            // 垂直读「块顶距父顶」= -offsetMax.y（SetChildAlongAxis 把子锚点钉在 Vector2.up=父左上角，
            // offset 全相对那个锚点；父顶上方为正距离就取负号）
            float top0 = -children[0].offsetMax.y;
            Assert(Eq(top0, 10f) && Eq(-children[1].offsetMax.y, 10f)
                && Eq(-children[2].offsetMax.y, 10f),
                $"用例一 单行三块顶边距容器顶全 10（实际 {top0}/...）");
            Assert(Eq(children[0].sizeDelta.x, 100f) && Eq(children[0].sizeDelta.y, 40f),
                $"用例一 尺寸驱动 sizeDelta=100×40（实际 {children[0].sizeDelta}）");   // SetChildAlongAxis 带 size 重载："读 preferred 写 size"
            Assert(Eq(((ILayoutElement)flow).preferredWidth, 340f),
                $"用例一 preferredWidth=340（全部一行 320+padding 20，实际 {((ILayoutElement)flow).preferredWidth}）");

            // ── 用例二：压到 250 宽 → 第三块换行 ──
            container.sizeDelta = new Vector2(250f, 300f);   // 改尺寸本身已自动 SetDirty（编辑器下消费时机不定），
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);   // Force 立即兑现，结果确定
            Assert(Eq(-children[2].offsetMax.y, 60f),
                $"用例二 换行块顶边距顶=60（第一行10+行高40+spacing10，实际 {-children[2].offsetMax.y}）");
            Assert(Eq(-children[0].offsetMax.y, 10f) && Eq(-children[1].offsetMax.y, 10f)
                && Eq(children[0].offsetMin.x, 10f) && Eq(children[1].offsetMin.x, 120f),
                "用例二 首行两块位置不变");

            // ── 用例三：两行总高上报（父级 ContentSizeFitter 靠它把容器高度包住内容）──
            Assert(Eq(((ILayoutElement)flow).preferredHeight, 110f),
                $"用例三 preferredHeight=110 = 10+40+10+40+10（实际 {((ILayoutElement)flow).preferredHeight}）");
        }
        finally
        {
            Object.DestroyImmediate(container.gameObject);   // 断言失败也清场，不留对象
        }

        if (_failed == 0) Debug.Log("[布局冒烟] 直排/换行/总高 全部通过 ✓");
        else Debug.LogError($"[布局冒烟] {_failed} 项失败，见上方红字");
    }

    // ── 辅助（对齐 AttributeSmokeTest 风格）──
    private static bool Eq(float a, float b) => Mathf.Approximately(a, b);

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[布局冒烟] 失败：{label}"); }
    }
}
