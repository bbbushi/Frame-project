using System;
using UnityEngine;
using Config;
using ActComponents;
using Components;
using PlayerSystem;
namespace Modules
{
    /// <summary>
    /// 移动模块 — 管理水平移动、跳跃、地面/墙壁检测。
    /// 数据（速度、跳跃力）+ 逻辑（移动/跳跃方法）+ 物理检测 全在这里。
    /// </summary>
    [Serializable]
    public class PlayerLocomotion : PlayerModule
    {
        [Header("移动参数")]
        [SerializeField] protected float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private int maxJumps = 2;
        // 转身过零容差：地面反向输入时，水平速度符号追上输入方向（input·vx ≥ 容差）才真正翻转
        private const float TurnVelocityTolerance = -0.01f;


        // 运行时状态
        public int RemainingJumps { get; private set; } = 2;     
        public float JumpForce => jumpForce;
        // 动作期间禁跳（互斥模型约定：动作持锁时移动层在 None，跳跃不得将其拉出）
        public bool CanJump => !Players.AnimatorComponent.IsActionBusy
                   && !Players.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Jump)
                   && RemainingJumps > 0;

        
        public float HorizontalInput { get; set; }
        public float MoveSpeed => moveSpeed;
        /// <summary>从 ScriptableObject 加载配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            moveSpeed = cfg.moveSpeed;
            jumpForce = cfg.jumpforce;
        }
        public void ApplyJump()
        {
            if(!CanJump) return;
            // 先设速度再请求转 Air（上升/下落动画由 Animator 按 yvelocity 自动流转）
            Players.rb.velocity = new Vector2(Players.rb.velocity.x, jumpForce * Players.locomotionComponent.Velocity);
            Players.AnimatorComponent.MotionMachine.ChangeState(MotionStateId.Air);
            ConsumeJump();
        }
            
        // ═══════════════════════════════════════════════════
        //  跳跃次数管理
        // ═══════════════════════════════════════════════════
        public void ResetJumps() => RemainingJumps = maxJumps;
        public void ConsumeJump() => RemainingJumps = Mathf.Max(0, RemainingJumps - 1);
    
        /// <summary>
        /// 统一处理水平输入与朝向翻转（每帧由 PlayerInputController 调用，正常/子弹时间两种模式均走此）。
        /// 转身是移动层内的物理过渡，不是动作锁：反向输入由 ApplyHorizontal 刹车分支渐变减速，
        /// 速度过零的那一帧翻转并播 Turnflip 涟漪（FX 事件层，同 Hit/Landing 模式）；
        /// 急停表现由 GroundMove 树按 xvelocity 天然呈现，转身期间跳跃/攻击/突刺不被锁。
        /// 空中瞬时转身（平台惯例：空中操控响应优先）；受击硬直期间（AddIgnore(All) 含 Move 位）不处理。
        /// </summary>
        public void SetMoveInput(float input)
        {
            HorizontalInput = input;

            if (input == 0f) return;
            LocomotionComponent loco = Players.locomotionComponent;
            if (input * loco.FacingDirection >= 0f) return;   // 同向或静止朝向，无需处理
            if (Players.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Move)) return;  // 受击硬直让位

            // 空中：瞬时转身，不播动画
            if (!loco.IsGrounded)
            {
                loco.Flip();
                return;
            }

            // 地面：速度过零（或本就静止）才翻转——转身时序由物理驱动；静止反向时条件立即成立
            if (input * Players.rb.velocity.x >= TurnVelocityTolerance)
            {
                loco.Flip();
                Players.anim?.SetTrigger("flip");
            }
        }
    }
}
