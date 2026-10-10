using ActComponents;
using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 远程三段：与 MeleeAttackState 同构，仅出招一行不同（发弹 vs 挥击）。
    /// 刻意不合并成一个类加分支——各 40 行直读优于类内分支，新攻击类型 = 新积木（开闭原则）。
    /// </summary>
    public class RangedAttackState : IEnemyState
    {
        enum Phase { Windup, Cooldown }

        readonly IEnemyBrain brain;
        float timer;
        Phase phase;

        public RangedAttackState(IEnemyBrain brain) { this.brain = brain; }

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
                Shoot();
                timer = 0f;
                phase = Phase.Cooldown;
            }
            else if (phase == Phase.Cooldown && timer >= brain.Cfg.attack.cooldown)
            {
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

        /// <summary>发一弹：bulletPrefab 用配置里的远程弹（HitboxBullet prefab）；初速/伤害在弹的 prefab 上配</summary>
        void Shoot()
        {
            GameObject bulletPrefab = brain.Cfg.attack.bulletPrefab;
            if (bulletPrefab == null) return;   // 远程弹还没建——不出招不报错，配好 asset 即生效
            Entity owner = brain.Owner;
            Hitbox.GenerateHitbox(bulletPrefab, owner, null,
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
