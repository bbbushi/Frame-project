namespace enemy.ai
{
    /// <summary>站桩哨兵：原地待机，玩家进前向视野即转追击（Sentry 的根状态）。</summary>
    public class IdleState : IEnemyState
    {
        readonly IEnemyBrain brain;

        public IdleState(IEnemyBrain brain) { this.brain = brain; }

        public void Enter()
        {
            brain.Owner.locomotionComponent.Stop();
            brain.SetAnimBool("Is Move", false);
        }

        public void Tick(float dt)
        {
            if (brain.Target != null && brain.HasLineOfSightNow)
                brain.TryEnter(EnemyStateId.Chase);
        }

        public void Exit() { }
    }
}
