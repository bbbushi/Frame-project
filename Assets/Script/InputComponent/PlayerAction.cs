namespace InputComponent
{
    /// <summary>
    /// 玩家动作枚举，用于 InputManager 键位绑定和命令映射
    /// </summary>
    public enum PlayerAction
    {
        MoveLeft,
        MoveRight,
        Jump,
        Thrust,        // 突刺（整合了原 Dash + 短距位移）
        Attack,        // 普通攻击（鼠标左键）
        BulletTime,    // 子弹时间（鼠标右键，切换进入/取消）
        Execution,     // 处决（E 键，子弹时间模式下触发链式处决）
        Mark,          // 标记（鼠标左键，子弹时间模式下标记敌人）
    }
}
