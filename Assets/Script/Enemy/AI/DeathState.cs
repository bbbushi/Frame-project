namespace enemy.ai
{
    /// <summary>
    /// 终态（MarkTerminal 转不出）：停移动、播倒地，然后交还控制权——
    /// 池回收由 Enemy 订阅 OnDied 的 Die() 负责，状态只管停表现。
    /// </summary>
    public class DeathState : IEnemyState
    {
        readonly IEnemyBrain brain;

        public DeathState(IEnemyBrain brain) { this.brain = brain; }

        public void Enter()
        {
            brain.Owner.locomotionComponent.Stop();
            brain.SetAnimBool("Is Move", false);
            brain.SetAnimTrigger("Dead");
        }

        public void Tick(float dt) { }

        public void Exit() { }
    }
}
