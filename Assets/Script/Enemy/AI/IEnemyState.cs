namespace enemy.ai
{
    /// <summary>
    /// 状态积木契约。实现约定：构造注入 IEnemyBrain，自持计时器（纯 float，不走 Timer），
    /// Tick 的 dt 是物理帧间隔，由宿主喂入——显式传参优于隐式取时。
    /// </summary>
    public interface IEnemyState
    {
        void Enter();
        void Tick(float dt);
        void Exit();
    }
}
