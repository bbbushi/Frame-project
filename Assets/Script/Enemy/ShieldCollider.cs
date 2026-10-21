using UnityEngine;

namespace enemy
{
    /// <summary>
    /// 盾牌碰撞盒 — 挂在盾兵的盾牌子物体上（SpriteRenderer + Collider2D + 本组件）。
    /// 子弹时间标记射线打到它即被阻挡（无法标记其身后的敌人）；
    /// 鼠标点击命中它时同样视为被盾格挡（无法标记盾兵正面）。
    /// 敌人转身（localScale 翻转）时本碰撞盒随父变换自动翻到另一侧，无需额外朝向判断。
    /// 建议 Collider2D 勾选 Is Trigger：参与射线/点检测，但不参与实体碰撞。
    /// </summary>
    public class ShieldCollider : MonoBehaviour { }
}
