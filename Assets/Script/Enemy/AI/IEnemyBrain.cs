using Config;
using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 积木对宿主的全部需求（依赖倒置：积木只认这个接口，不认 EnemyAIComponent 具体类）。
    /// EnemyAIComponent 实现它；将来友军/NPC 宿主实现同一接口即可复用整个积木库。
    /// </summary>
    public interface IEnemyBrain
    {
        Entity Owner { get; }                  // 移动/朝向/距离/攻击挂点从这拿

        // 感知缓存（EnemyVision 产出）
        EnemyAIConfig Cfg { get; }
        Entity Target { get; }
        Vector3 LastKnownPosition { get; }
        bool HasLineOfSightNow { get; }
        Vector3 HomeAnchor { get; }            // leash 基准

        // 转换与记忆
        bool TryEnter(EnemyStateId id);        // 未注册槽位静默失败，积木走自己的 fallback
        void TryEnterRoot();
        void ForgetTarget();

        // 动画收口（换表现方案只改宿主实现，积木不动）
        void SetAnimBool(string name, bool value);
        void SetAnimTrigger(string name);
    }
}
