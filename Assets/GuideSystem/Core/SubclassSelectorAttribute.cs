using UnityEngine;

namespace Guide
{
    /// <summary>
    /// 给 [SerializeReference] 字段加这个标记，Inspector 会出现"选择具体类型"的下拉框。
    /// Unity 2022.3 默认没有这个 UI（点 + 只会添加 null 元素）。
    /// 绘制器实现见 Editor/SubclassSelectorDrawer.cs。
    /// </summary>
    public class SubclassSelectorAttribute : PropertyAttribute { }
}
