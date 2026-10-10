using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 走停巡逻：随机方向走一段 → 随机停一会 → 换向再来。撞墙/悬崖立即掉头；
    /// 玩家进视野随时转追击（走/停两段都先查目标再干自己的活）。
    /// </summary>
    public class PatrolState : IEnemyState
    {
        readonly IEnemyBrain brain;
        float timer;
        float phaseTime;   // 当前段（走或停）的总时长，Enter 时随机好
        bool walking;

        public PatrolState(IEnemyBrain brain) { this.brain = brain; }

        public void Enter() => BeginWalk();

        public void Tick(float dt)
        {
            if (brain.Target != null && brain.HasLineOfSightNow)
            {
                brain.TryEnter(EnemyStateId.Chase);
                return;
            }

            timer += dt;
            var locomotion = brain.Owner.locomotionComponent;

            if (walking)
            {
                locomotion.ApplyHorizontal(locomotion.FacingDirection);

                // 撞墙，或再走一步会悬空 → 掉头（前探实现归 DetectionComponent.IsGroundAhead）
                if (brain.Owner.detection.IsFacingWall || !brain.Owner.detection.IsGroundAhead())
                    locomotion.SetFacing(-locomotion.FacingDirection);

                if (timer >= phaseTime) BeginWait();
            }
            else if (timer >= phaseTime)
            {
                BeginWalk();
            }
        }

        public void Exit()
        {
            brain.Owner.locomotionComponent.Velocity = 1f;   // 还回公共的速度倍率
            brain.SetAnimBool("Is Move", false);
        }

        void BeginWalk()
        {
            var locomotion = brain.Owner.locomotionComponent;
            locomotion.SetFacing(Random.value < 0.5f ? -1f : 1f);
            locomotion.Velocity = brain.Cfg.patrol.speedRatio;
            phaseTime = Random.Range(brain.Cfg.patrol.moveTimeRange.x, brain.Cfg.patrol.moveTimeRange.y);
            timer = 0f;
            walking = true;
            brain.SetAnimBool("Is Move", true);
        }

        void BeginWait()
        {
            brain.Owner.locomotionComponent.Stop();
            phaseTime = Random.Range(brain.Cfg.patrol.waitTimeRange.x, brain.Cfg.patrol.waitTimeRange.y);
            timer = 0f;
            walking = false;
            brain.SetAnimBool("Is Move", false);
        }
    }
}
