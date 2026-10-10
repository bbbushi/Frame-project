using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 直线追击：可见就贴脸，不可见追最后目击点（EnemyVision 的记忆），
    /// 到点站着等视野盒再扫到。三个出口：进攻击距离 / 记忆到期 / 出 leash。
    /// </summary>
    public class ChaseState : IEnemyState
    {
        readonly IEnemyBrain brain;
        float lostTimer;

        public ChaseState(IEnemyBrain brain) { this.brain = brain; }

        public void Enter()
        {
            lostTimer = 0f;
            brain.Owner.locomotionComponent.Velocity = brain.Cfg.chase.speedRatio;
            brain.SetAnimBool("Is Move", true);
        }

        public void Tick(float dt)
        {
            // 目标没了（死亡/销毁）→ 当作彻底丢失
            if (brain.Target == null)
            {
                brain.ForgetTarget();
                brain.TryEnterRoot();
                return;
            }

            var locomotion = brain.Owner.locomotionComponent;
            if (brain.HasLineOfSightNow)
            {
                lostTimer = 0f;
                FaceTo(brain.Target.transform.position);
                locomotion.ApplyHorizontal(locomotion.FacingDirection);
            }
            else
            {
                lostTimer += dt;
                // 追最后目击点；到了就停下干等（视野盒还在扫，扫到自动续追）
                if (brain.Owner.GetDistance(brain.LastKnownPosition) > 0.4f)
                {
                    FaceTo(brain.LastKnownPosition);
                    locomotion.ApplyHorizontal(locomotion.FacingDirection);
                }
                else
                {
                    locomotion.Stop();
                }
            }

            // 出口①：看得见且进攻击距离 → 出招
            if (brain.HasLineOfSightNow && brain.Owner.GetDistance(brain.Target) <= brain.Cfg.attack.range)
            {
                brain.TryEnter(EnemyStateId.Attack);
                return;
            }
            // 出口②：丢了超过记忆期 → 彻底放弃
            if (lostTimer > brain.Cfg.chase.memoryTime)
            {
                brain.ForgetTarget();
                brain.TryEnterRoot();
                return;
            }
            // 出口③：离家超过 leash → 放弃（防一路追出活动区）
            if (Vector2.Distance(brain.Owner.transform.position, brain.HomeAnchor) > brain.Cfg.chase.leashRange)
            {
                brain.ForgetTarget();
                brain.TryEnterRoot();
            }
        }

        public void Exit()
        {
            brain.Owner.locomotionComponent.Velocity = 1f;
            brain.SetAnimBool("Is Move", false);
        }

        void FaceTo(Vector3 position)
        {
            float dir = Mathf.Sign(position.x - brain.Owner.transform.position.x);
            if (dir != 0f) brain.Owner.locomotionComponent.SetFacing(dir);
        }
    }
}
