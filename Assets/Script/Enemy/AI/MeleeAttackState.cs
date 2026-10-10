using ActComponents;
using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 近战三段：前摇 → 出招 → 冷却，timer 分段推进。
    /// 出招判定用 timer 不依赖动画事件——FSM 正确性不挂在动画上（PlayerState 同款结论）。
    /// </summary>
    public class MeleeAttackState : IEnemyState
    {
        enum Phase { Windup, Cooldown }

        readonly IEnemyBrain brain;
        float timer;
        Phase phase;

        public MeleeAttackState(IEnemyBrain brain) { this.brain = brain; }

        public void Enter()
        {
            if (brain.Target == null) { brain.TryEnterRoot(); return; }

            timer = 0f;
            phase = Phase.Windup;
            var locomotion = brain.Owner.locomotionComponent;
            locomotion.Stop();
            FaceTo(brain.Target.transform.position);
            brain.SetAnimTrigger("attack");
        }

        public void Tick(float dt)
        {
            timer += dt;

            if (phase == Phase.Windup && timer >= brain.Cfg.attack.windup)
            {
                Swing();
                timer = 0f;
                phase = Phase.Cooldown;
            }
            else if (phase == Phase.Cooldown && timer >= brain.Cfg.attack.cooldown)
            {
                // 冷却完：还在射程就再来一轮（重走 Enter 的朝向/前摇），脱离了就回追
                if (brain.Target != null && brain.HasLineOfSightNow
                    && brain.Owner.GetDistance(brain.Target) <= brain.Cfg.attack.range)
                {
                    Enter();
                }
                else
                {
                    brain.TryEnter(EnemyStateId.Chase);
                }
            }
        }

        public void Exit() { }

        /// <summary>出招一击：在攻击挂点处生成命中判定，伤害走 GAS 的 AttackPower</summary>
        void Swing()
        {
            Entity owner = brain.Owner;
            if (owner.detection.hitboxPrefab == null) return;   // prefab 没配判定框就不出招，不报错
            Hitbox.GenerateHitbox(owner.detection.hitboxPrefab, owner, null,
                owner.healthManageComponent.AttackPower,
                owner.detection.GetAttackSocket().position);
        }

        void FaceTo(Vector3 position)
        {
            float dir = Mathf.Sign(position.x - brain.Owner.transform.position.x);
            if (dir != 0f) brain.Owner.locomotionComponent.SetFacing(dir);
        }
    }
}
