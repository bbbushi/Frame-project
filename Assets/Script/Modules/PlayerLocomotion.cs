using System;
using UnityEngine;
using Config;
using Frame_Player;
using Modules;
namespace Modules_Player
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

        // 运行时状态
        public int RemainingJumps { get; private set; } = 2;     
        public float JumpForce => jumpForce;
        public bool CanJump => RemainingJumps > 0 && !player.healthManageComponent.IsDead;

        // 运行时状态
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
            player.rb.velocity = new Vector2(player.rb.velocity.x, jumpForce * player.locomotionComponent.Velocity);
            ConsumeJump();
        }
            
        // ═══════════════════════════════════════════════════
        //  跳跃次数管理
        // ═══════════════════════════════════════════════════
        public void ResetJumps() => RemainingJumps = maxJumps;
        public void ConsumeJump() => RemainingJumps = Mathf.Max(0, RemainingJumps - 1);
    
        /// <summary>统一处理水平输入与朝向翻转，避免外部直接操作 FacingDirection</summary>
        public void SetMoveInput(float input)
        {
          
            HorizontalInput = input;
            // Debug.Log($"HorizontalInput set to {input}");
            // 有输入且方向与当前朝向相反时翻转
            if (input != 0f && input * Owner.locomotionComponent.FacingDirection < 0f)
                Owner.locomotionComponent.Flip();
        
        }    
    }
}
