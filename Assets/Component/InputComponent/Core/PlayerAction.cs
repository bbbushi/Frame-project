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
        BulletTime,    // 子弹时间开关（鼠标右键）
        Execution,     // 处决链触发（E 键，子弹时间中有标记时生效）
    }
}
