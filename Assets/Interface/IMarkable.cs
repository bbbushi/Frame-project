/// <summary>
/// 可被子弹时间标记的实体 — 实现此接口的对象才允许被鼠标点击标记，
/// 且在瞄准扇形内时保持原色（不暗幕压暗，由 BulletTimeStage 切换渲染排序实现）。
/// </summary>
public interface IMarkable
{
    bool CanBeMarked { get; }
}
