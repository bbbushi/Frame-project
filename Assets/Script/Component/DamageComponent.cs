using UnityEngine;
using ActComponents;
using Effect;
using UnityEditor.Experimental.GraphView;
namespace Components
{
    public class DamageComponent : EntityComponent
    {
        public Displacement hitRepel;
        private bool bloodEffectEnabled = false;
        private int tick = 0;
        private Vector2 direction;
        public virtual HitResult Hit(Damage damage)
        {
            Debug.Log($"Entity {Owner.name} received damage: {damage.damage}, impact type: {damage.impact}");
            switch (damage.impact)
            {
                case ImpactType.None:
                    break;
                default:
                    //打断所有行动
                    Owner.Interrupt();
                    Owner.anim.SetTrigger("Hit");
                    //获取冲击方向（Parallel：攻击推动方向；Centrifugal：由爆心指向受击者）
                    direction = damage.impulseVector;
                    if (damage.impulseType == ImpulseType.Centrifugal)
                        direction = Owner.ChestPosition - damage.impulseVector;

                    //面向攻击方（攻击来源在冲击方向的反侧），沿冲击方向击退
                    float knockDir = direction.x >= 0f ? 1f : -1f;
                    Owner.locomotionComponent.SetFacing(-knockDir);
                    Owner.locomotionComponent.ForceMove(hitRepel, knockDir);
                    
                    
                    //播放动画，设置动作忽略
                    
                    Owner.actionIgnoreComponent.AddIgnore(hitRepel.length, ActionIgnoreTag.All);
                    if (BloodParticleGenerator.Instance != null)
                    {
                        tick = 0;
                        bloodEffectEnabled = true;
                        for (int i = 0; i < 3; i++)
                            BloodParticleGenerator.Instance.GenerateBloodOnBackground(Owner.ChestPosition + direction);
                    }
                    
                    break;
            }

            return new HitResult(damage.damage, HitResultType.Hit);
        }

        public virtual void Dead()
        {

        }
        public override void RefreshFixedUpdate()
        {
            if (bloodEffectEnabled && BloodParticleGenerator.Instance != null)
            {
                tick++;
                if (tick % 3 == 0 && tick < 50 && BloodParticleGenerator.Instance != null)
                {
                    BloodParticleGenerator.Instance.GenerateBloodParticle(Owner.ChestPosition + direction,
                        new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 3f)));
                }

            }
        }
    } 
}