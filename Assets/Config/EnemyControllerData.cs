using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Config
{
    [CreateAssetMenu(fileName = "NewEnemyControllerData", menuName = "Game/Enemy Controller Data")]
    public class EnemyControllerData : EntityControllerConfig
    {
        // 敌人暂无专属控制参数（跳跃/子弹时间是玩家侧 PlayerControllerData 的事）；
        // 将来敌人要独立手感参数（如受击位移倍率）加在这里，不动基类
    }
}
