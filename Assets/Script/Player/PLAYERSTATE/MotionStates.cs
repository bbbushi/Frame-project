using UnityEngine;
using PlayerSystem;
using ActComponents;

namespace State_Player
{
    /// <summary>
    /// 移动层状态基类 — 互斥模型下仅在动作层空闲时活动（动作层持锁期间移动层整体让位进 None）。
    /// 职责：应用水平输入（受击硬直/刹车待转时跳过）；Blend Tree 连续量（xvelocity/yvelocity）由宿主统一刷新。
    /// </summary>
    public abstract class MotionState : PlayerState
    {
        protected MotionState(Player player, string animStateName) : base(player, animStateName) { }

        public override void Update()
        {
            // 输入始终刷新，避免受击硬直期间残留旧值影响状态转换判断
            xInput = player.ModuleControlComponent.Locomotion.HorizontalInput;

            // 受击硬直/刹车待转期间不应用移动输入（动作期间移动层在 None，不会跑到这里）
            if (!player.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Move))
                player.locomotionComponent.ApplyHorizontal(xInput);
        }
    }

    /// <summary>
    /// 移动层空状态 — 动作层持锁期间的让位锚点：不应用输入、不碰姿态（animStateName=null）。
    /// 恢复由宿主的动作层转换联动触发（动作回 None 时按物理事实转 Ground/Air），本状态不自愈。
    /// </summary>
    public class PlayerMotionNoneState : MotionState
    {
        public PlayerMotionNoneState(Player player) : base(player, null) { }

        // 必须重写为空：基类 Update 会应用移动输入，互斥让位期间不得驱动
        public override void Update() { }
    }

    /// <summary>
    /// 地面状态 — Idle/run 只是 xvelocity 混合树（GroundMove）两端的姿态差异，不构成独立逻辑状态；
    /// C# 只在离地/落地这条物理边上做状态转换，帧内 Idle↔run 的表现完全由树的连续混合接管。
    /// </summary>
    public class PlayerGroundState : MotionState
    {
        public PlayerGroundState(Player player, string animStateName) : base(player, animStateName) { }

        public override void Enter()
        {
            base.Enter();
            player.ModuleControlComponent.Locomotion.ResetJumps();
        }

        public override void Update()
        {
            base.Update();

            // 离地自动转 Air（跳跃起跳、走动跨出平台边缘等）。
            // 真实离地 = 不在地面 且 垂直速度非零：不查 IsGrounded 时，落地残余速度会误判离地 →
            // 立即又判落地 → Landing trigger 双发 → 落地涟漪播两遍
            if (!player.locomotionComponent.IsGrounded && Mathf.Abs(player.rb.velocity.y) > 0.01f)
                player.AnimatorComponent.MotionMachine.ChangeState(MotionStateId.Air);
        }
    }

    /// <summary>
    /// 空中状态 — 覆盖整个滞空期间（Jumping 为 yvelocity 混合树，上升/apex/下落由物理连续驱动）。
    /// 落地是物理判定，不依赖任何动画事件；落地涟漪通过 Landing trigger 交给 FX 事件层。
    /// </summary>
    public class PlayerAirState : MotionState
    {
        public PlayerAirState(Player player, string animStateName) : base(player, animStateName) { }

        public override void Update()
        {
            base.Update();

            // 检测墙壁（WallSlide 预留）
            // if (player.locomotionComponent.IsTouchingWall) ...

            // 落地：在地面且不在上升即可（允许微小向下速度）
            if (player.locomotionComponent.IsGrounded && player.rb.velocity.y <= 0.01f)
            {
                // 落地涟漪归 FX 事件层（trigger 进 → exit-time 自治回 Empty），FSM 照常切回 Ground
                if (player.anim != null)
                    player.anim.SetTrigger("Landing");
                player.AnimatorComponent.MotionMachine.ChangeState(MotionStateId.Ground);
            }
        }
    }
}
