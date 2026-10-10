namespace enemy.ai
{
    /// <summary>
    /// 状态槽位。Attack 槽按 config.attack.type 装近战或远程积木（装配点全项目唯一：EnemyAIComponent.Init）。
    /// </summary>
    public enum EnemyStateId { Idle, Patrol, Chase, Attack, Death }
}
