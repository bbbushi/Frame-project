using System;
using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 移动模块 — 管理水平移动、跳跃、地面/墙壁检测。
    /// 数据（速度、跳跃力）+ 逻辑（移动/跳跃方法）+ 物理检测 全在这里。
    /// </summary>
    [Serializable]
    public class PlayerLocomotion : PlayerModule
    {
        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private int maxJumps = 2;

        [Header("碰撞检测")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask wallLayer;
        [SerializeField] public Vector2 groundCheckOffset = Vector2.down * 0.5f;
        [SerializeField] public Vector2 groundCheckSize = new(0.8f, 0.1f);
        [SerializeField] public Vector2 wallCheckSize = new(0.1f, 0.8f);


        // 运行时状态
        public int RemainingJumps { get; private set; } = 2;
        public float HorizontalInput { get; set; }
        public float FacingDirection { get; set; } = 1f;
        public float MoveSpeed => moveSpeed;
        public float JumpForce => jumpForce;
        public bool IsGrounded { get; private set; }
        public bool IsTouchingWall { get; private set; }
        public bool CanJump => RemainingJumps > 0 && !player.Health.IsDead;

        /// <summary>从 ScriptableObject 加载配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            moveSpeed = cfg.moveSpeed;
            jumpForce = cfg.jumpforce;
        }

        // ═══════════════════════════════════════════════════
        //  移动方法
        // ═══════════════════════════════════════════════════
        public void ApplyHorizontal(float direction)        
            => player.rb.velocity = new Vector2(direction * moveSpeed, player.rb.velocity.y);
        

        public void ApplyJump()
        {
            player.rb.velocity = new Vector2(player.rb.velocity.x, jumpForce);
            ConsumeJump();
        }

        public void Stop()
            => player.rb.velocity = new Vector2(0f, player.rb.velocity.y);

        public void ZeroVelocity()
            => player.rb.velocity = Vector2.zero;

        public void SetVelocity(float x, float y)
            => player.rb.velocity = new Vector2(x, y);

        // ═══════════════════════════════════════════════════
        //  跳跃次数管理
        // ═══════════════════════════════════════════════════
        public void ResetJumps() => RemainingJumps = maxJumps;
        public void ConsumeJump() => RemainingJumps = Mathf.Max(0, RemainingJumps - 1);

        // ═══════════════════════════════════════════════════
        //  物理检测（每帧在 Player.Update 中调用）
        // ═══════════════════════════════════════════════════
        public void UpdatePhysics()
        {
            Vector2 pos = player.transform.position;

            IsGrounded = Physics2D.OverlapBox(pos + groundCheckOffset, groundCheckSize, 0f, groundLayer);
            IsTouchingWall = Physics2D.OverlapBox(
                pos + Vector2.right * FacingDirection * 0.2f,
                wallCheckSize, 0f, wallLayer);
        }

        // ═══════════════════════════════════════════════════
        //  输入入口（由 PlayerInputController 调用）
        // ═══════════════════════════════════════════════════
        /// <summary>统一处理水平输入与朝向翻转，避免外部直接操作 FacingDirection</summary>
        public void SetMoveInput(float input)
        {
            HorizontalInput = input;

            // 有输入且方向与当前朝向相反时翻转
            if (input != 0f && input * FacingDirection < 0f)
                Flip();
        }

        // ═══════════════════════════════════════════════════
        //  视觉
        // ═══════════════════════════════════════════════════
        public void Flip()
        {
            FacingDirection = -FacingDirection;
            float yAngle = FacingDirection == 1f ? 0f : 180f;
            player.transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
            Debug.Log($"Player flipped. New facing direction: {FacingDirection}");
        }

        // ═══════════════════════════════════════════════════
        //  编辑器 / 运行时可视化
        // ═══════════════════════════════════════════════════
        public void DrawGizmos(Player p)
        {
            Vector2 pos = p.transform.position;

            // 地面检测区域
            var groundColor = IsGrounded ? Color.green : Color.yellow;
            DebugDrawBox(pos + groundCheckOffset, groundCheckSize, groundColor);

            // 墙壁检测区域
            var wallColor = IsTouchingWall ? Color.red : Color.cyan;
            var wallOffset = Vector2.right * FacingDirection * 0.5f;
            DebugDrawBox(pos + wallOffset, wallCheckSize, wallColor);
        }

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
    }
}
