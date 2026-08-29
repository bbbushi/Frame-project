using PlayerSystem;
using UnityEngine;

namespace State_Player
{
    /// <summary>
    /// 状态基类 — 所有玩家状态的抽象（移动层 MotionState / 动作层 ActionState 的共同祖先）。
    /// 状态只持有 player 引用，通过 player.ModuleControlComponent.* 访问模块，
    /// 通过 player.AnimatorComponent.MotionMachine / ActionMachine 发起转换（一律走 Request，不要直接改 Current）。
    /// 动画采用三分法：Base 层姿态由 Enter 的 CrossFade 直控（animStateName 即 Animator 状态名，null 静默跳过）；
    /// 连续量走 Blend Tree（xvelocity/yvelocity），瞬时涟漪（Hit/Landing/flip）归 FX 事件层，均不在状态类里操作。
    /// </summary>
    public abstract class PlayerState
    {
        protected readonly Player player;
        // Base 层 Animator 状态名（BlendTree 状态名或离散状态名）；null = 该状态无对应动画（如 Execution/Death 暂缺剪辑）
        protected readonly string animStateName;
        // 进入状态时重置的计时器（纯 float，不走 Timer —— Timer 构造会注册进 TimeManager.Timers 且需显式销毁）
        protected float stateTimer;
        protected float xInput;
        // 动画事件 AnimEnd() 置位。仅作表现推进（如攻击收招），FSM 的正确性不得依赖它
        protected bool AnimEndTrigger;

        protected PlayerState(Player player, string animStateName)
        {
            this.player = player;
            this.animStateName = animStateName;
        }

        public virtual void Enter()
        {
            FadeToAnimState();
            AnimEndTrigger = false;
            // 默认状态持续时长，可在子类 Enter 中覆盖
            stateTimer = 0.1f;
        }

        /// <summary>
        /// 姿态直控：CrossFade 到 animStateName，混合时长由状态机宿主统一配置。
        /// 移动层重写此方法在动作持锁期间让位（攻击中离地时不抢掉动作动画）。
        /// </summary>
        protected virtual void FadeToAnimState()
        {
            if (player.anim != null && !string.IsNullOrEmpty(animStateName))
                player.anim.CrossFade(animStateName, player.AnimatorComponent.CrossFadeDuration);
                
        }

        public virtual void Update() { }

        // 无需清参数——C# 每次进入状态都显式指定目标姿态，不存在残留 bool
        public virtual void Exit() { }

        /// <summary>动画结束事件回调（PlayerAnimatorComponent.AnimEnd → 动作层当前状态）</summary>
        public virtual void EndTrigger() => AnimEndTrigger = true;
    }
}
