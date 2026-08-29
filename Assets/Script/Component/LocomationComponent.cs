using UnityEngine;
using ActComponents;
using PlayerSystem;
using Config;
namespace Components
{
    public class LocomotionComponent : EntityComponent
    {
        [Header("移动参数")]
        [SerializeField] protected float moveSpeed;
        // 掉头刹车率（m/s²）：反向输入时速度渐变过零的减速率；0 = 瞬时掉头
        [SerializeField] protected float deceleration = 40f;

        public float Velocity {get; set;} = 1f;
        protected Displacement currentDisplacement;
        protected float displacementTimer;
        //位移方向（与 FacingDirection 解耦：受击击退沿冲击方向，面朝可面向攻击方）
        protected float displacementDirection = 1f;
        public bool IsDisplacing => currentDisplacement != null;
        // 运行时状态
        public bool IsGrounded => Owner.detection.IsOnGround;
        public bool IsTouchingWall => Owner.detection.IsFacingWall;
        public bool IsTouchingPlatform => Owner.detection.isTouchingPlatform;
        // 运行时状态
        public float FacingDirection { get; set; } = 1f;
        public float MoveSpeed => moveSpeed;
        /// <summary>从 ScriptableObject 加载配置</summary>
        
        public override void Init()
        {
            base.Init();
            loadConfig(Owner.controllerData);
        }
        private void loadConfig(EntityControllerConfig cfg)
        {
            if (cfg == null) return;
            moveSpeed = cfg.moveSpeed;
            deceleration = cfg.deceleration;
        }
        public virtual void ApplyHorizontal(float direction)
        {
            float vx = Owner.rb.velocity.x;
            float target = direction * moveSpeed * Velocity;
            // 掉头窗口：目标速度与当前速度反向 → 按 deceleration 渐变（刹车过零再渐变到反向），不瞬间掉头；
            // 翻转时机由上层按速度归零触发（PlayerLocomotion.SetMoveInput）
            if (direction != 0f && direction * vx < 0f && deceleration > 0f)
                vx = Mathf.MoveTowards(vx, target, deceleration * FrameInterval);
            else
                vx = target;
            Owner.rb.velocity = new Vector2(vx, Owner.rb.velocity.y);
        }
        
        
        public virtual void Stop()
            => Owner.rb.velocity = new Vector2(0f, Owner.rb.velocity.y);

        public virtual void ZeroVelocity()
            => Owner.rb.velocity = Vector2.zero;

        public virtual void SetVelocity(float x, float y)
            => Owner.rb.velocity = new Vector2(x * Velocity, y * Velocity);

        // ═══════════════════════════════════════════════════
        //  强制位移（击退等外力，由 Displacement 曲线驱动）
        // ═══════════════════════════════════════════════════
        

        /// <summary>
        /// 施加强制位移，在 length 时间内按 speedCurve 衰减速度。
        /// direction 为 null 时沿当前 FacingDirection（攻击前冲、突刺等自身位移）；
        /// 受击击退时传入冲击方向，使击退方向与面朝方向解耦。
        /// 新位移会覆盖进行中的位移；Interrupt() 可立即中止。
        /// </summary>
        public virtual void ForceMove(Displacement force, float? direction = null)
        {
            if (force == null || force.length <= 0f) return;
            currentDisplacement = force;
            displacementTimer = 0f;
            displacementDirection = direction ?? FacingDirection;
        }

        

        public override void RefreshFixedUpdate()
        {
            if (currentDisplacement == null) return;

            displacementTimer += FixedFrameInterval;

            // 位移结束：水平速度清零，交还控制权
            if (displacementTimer >= currentDisplacement.length)
            {
                currentDisplacement = null;
                Stop();
                return;
            }

            // 曲线时间归一化到 [0,1]，速度 = maxSpeed × 曲线值，方向沿 ForceMove 指定的位移方向
            float t = displacementTimer / currentDisplacement.length;
            float speed = currentDisplacement.maxSpeed * currentDisplacement.speedCurve.Evaluate(t);
            Owner.rb.velocity = new Vector2(displacementDirection * speed, Owner.rb.velocity.y);
        }

        public override void Interrupt()
        {
            // 被打断时立即中止位移（DamageComponent.Hit 中先 Interrupt 再 ForceMove，顺序不冲突）
            currentDisplacement = null;
        }

        // ═══════════════════════════════════════════════════
        //  物理检测（每帧在 Owner.Update 中调用）
        // ═══════════════════════════════════════════════════
        public virtual void UpdatePhysics()
        {
            Vector2 pos = Owner.transform.position;
        }
        
        /// <summary>统一处理水平输入与朝向翻转，避免外部直接操作 FacingDirection</summary>

        /// <summary>统一处理水平输入与朝向翻转，避免外部直接操作 FacingDirection</summary>

        // ═══════════════════════════════════════════════════
        //  视觉
        // ═══════════════════════════════════════════════════
        /// <summary>直接设置朝向（±1）并同步翻转视觉；仅设置 FacingDirection 属性不会翻转模型</summary>
        public virtual void SetFacing(float direction)
        {
            
            FacingDirection = direction >= 0f ? 1f : -1f;
            Owner.transform.localScale = new Vector3(FacingDirection, 1f, 1f);

        }

        public virtual void Flip()
            => SetFacing(-FacingDirection);

        // ═══════════════════════════════════════════════════
        //  编辑器 / 运行时可视化
        // ═══════════════════════════════════════════════════
        #if UNITY_EDITOR
        private static void DebugDrawBox(Vector2 center, Vector2 size, Color color)
        {
            var half = size * 0.5f;
            var topLeft     = center + new Vector2(-half.x,  half.y);
            var topRight    = center + new Vector2( half.x,  half.y);
            var bottomLeft  = center + new Vector2(-half.x, -half.y);
            var bottomRight = center + new Vector2( half.x, -half.y);

            Debug.DrawLine(topLeft,     topRight,    color);
            Debug.DrawLine(topRight,    bottomRight, color);
            Debug.DrawLine(bottomRight, bottomLeft,  color);
            Debug.DrawLine(bottomLeft,  topLeft,     color);
        }
        #endif
    


       
        
    }
}