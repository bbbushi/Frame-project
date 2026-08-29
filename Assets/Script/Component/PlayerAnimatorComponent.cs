using State_Player;
using PlayerSystem;
using UnityEngine;
namespace Components
{
    /// <summary>
    /// 状态机宿主 — 装配 motion / action 两台状态机（互斥单活模型）：
    /// 任意时刻只有一台机有实际状态 —— 动作层进入 Attack/Thrust/Execution/Death 时移动层让位进 None；
    /// 动作层回 None 时按物理事实恢复 Ground/Air。跨机联动集中在 HandleActionTransitioned（订阅 Transitioned 事件），
    /// 状态类不感知跨机事务。动作互斥由守卫表声明。
    /// 动画三分法：Base 层姿态由状态 Enter 的 CrossFade 直控（animStateName = Animator 状态名，本组件注册表即姿态映射）；
    /// 连续量混合走 Blend Tree（GroundMove=xvelocity 树 / Jumping=yvelocity 树，参数由本组件每帧统一刷新）；
    /// 瞬时涟漪（Hit/Landing/flip trigger）归 FX 事件层，trigger 进 → exit-time 回 Empty，自治归位。
    /// 详见 state-machine.md。
    /// </summary>
    public class PlayerAnimatorComponent : PlayerComponent
    {
        public PlayerStateMachine<MotionStateId> MotionMachine { get; private set; }
        public PlayerStateMachine<ActionStateId> ActionMachine { get; private set; }

        /// <summary>动作层忙碌（攻击/突刺/处决/死亡中）— 移动层据此抑制输入</summary>
        public bool IsActionBusy => ActionMachine != null && ActionMachine.IsBusy;

        /// <summary>Base 层姿态切换的 CrossFade 混合时长（秒）</summary>
        public float CrossFadeDuration => crossFadeDuration;

        [Tooltip("Base 层姿态 CrossFade 混合时长（秒）")]
        [SerializeField] private float crossFadeDuration = 0.12f;

        // FX 涟漪层权重轮询：Empty 态权重 0 完全让位 Base 层，涟漪状态接管输出
        private int fxLayerIndex = -1;
        private static readonly int FxEmptyHash = Animator.StringToHash("Empty");

        // [Header("调试")]
        // [Tooltip("打印两台状态机的每次转换与守卫拒绝")]
        // [SerializeField] private bool enableTransitionLog;

        public override void Init()
        {
            MotionMachine = new PlayerStateMachine<MotionStateId>("Motion");
            ActionMachine = new PlayerStateMachine<ActionStateId>("Action", ActionStateId.None);

            // ── 移动层状态（animStateName = Base 层 Animator 状态名；
            //    GroundMove 是 Idle1/run 的 xvelocity 混合树，Jumping 是 yvelocity 混合树；
            //    None 是动作层持锁期间的让位锚点，不碰姿态不应用输入）──
            MotionMachine.Register(MotionStateId.None,   new PlayerMotionNoneState(Owner));
            MotionMachine.Register(MotionStateId.Ground, new PlayerGroundState(Owner, "GroundMove"));
            MotionMachine.Register(MotionStateId.Air,     new PlayerAirState(Owner, "Jumping"));

            // ── 动作层状态（null = 暂无对应剪辑，静默跳过 CrossFade；连击时 Attack 可按 ComboCount 选名）──
            ActionMachine.Register(ActionStateId.None,      new PlayerNoneState(Owner, null));
            ActionMachine.Register(ActionStateId.Attack,    new PlayerAttackState(Owner, "attack"));
            ActionMachine.Register(ActionStateId.Thrust,    new PlayerThrustState(Owner, "Dash"));
            ActionMachine.Register(ActionStateId.Execution, new PlayerExecutionState(Owner, null));
            ActionMachine.Register(ActionStateId.Death,     new PlayerDeathState(Owner, null));

            // ── 动作层守卫规则（结构性声明；未声明拒绝的一律允许；移动层物理驱动、不设禁令）──
            // 死亡是终态（任意状态 → Death 仍默认允许，即死亡可打断一切）
            ActionMachine.ForbidAllFrom(ActionStateId.Death);
            // 动作互斥组：组内互转拒绝，进出均经 None（None→动作进入、动作→None 完成）
            ActionMachine.ForbidAllBetween(ActionStateId.Attack, ActionStateId.Thrust, ActionStateId.Execution);

            // if (enableTransitionLog)
            // {
            //     MotionMachine.EnableTransitionLog = true;
            //     ActionMachine.EnableTransitionLog = true;
            // }

            // 死亡联动 + 互斥联动（具名方法订阅，OnDestroy 退订）
            ActionMachine.Transitioned += StateMachineTransitioned;
            if (Owner.healthManageComponent != null)
                Owner.healthManageComponent.OnDied += HandleOnDied;
            //获取FX层索引，便于轮询权重
            if (Owner.anim != null)
                fxLayerIndex = Owner.anim.GetLayerIndex("FX");
        }

        public void InitializeStateMachine()
        {
            MotionMachine.Initialize(MotionStateId.Ground);
            ActionMachine.Initialize(ActionStateId.None);
        }

        public override void RefreshUpdate()
        {
            // Blend Tree 连续量与状态机解耦：无论哪层在活动（含移动层让位进 None 期间）都持续刷新，避免参数冻结
            if (Owner.anim != null)
            {
                Owner.anim.SetFloat("yvelocity", Owner.rb.velocity.y);
                Owner.anim.SetFloat("xvelocity", Owner.rb.velocity.x);
            }

            // 先移动后动作：动作完成当帧即可恢复移动
            MotionMachine?.Update();
            ActionMachine?.Update();

            // FX 涟漪层权重轮询（层权重不影响 FX 层对 trigger 的响应，只影响输出混合）
            if (fxLayerIndex >= 0 && Owner.anim != null)
            {
                var fxInfo = Owner.anim.GetCurrentAnimatorStateInfo(fxLayerIndex);
                Owner.anim.SetLayerWeight(fxLayerIndex, fxInfo.shortNameHash == FxEmptyHash ? 0f : 1f);
            }
        }

        /// <summary>动画结束事件（动画剪辑 AnimEnd 事件回调）— 推进当前动作状态</summary>
        public void AnimEnd() => ActionMachine?.Current?.EndTrigger();

        /// <summary>攻击判定事件（attack 剪辑 AttackTrigger 事件回调）</summary>
        public void AttackTrigger() => Owner.ModuleControlComponent.Combat.HitDetect();

        private void HandleOnDied() => ActionMachine.ChangeState(ActionStateId.Death);

        /// <summary>
        /// 动作层转换联动（互斥模型核心）：进动作 → 移动层让位进 None；动作结束 → 按物理事实恢复移动层。
        /// 动作中死亡走"进动作"分支（motion 已是 None，自转换静默）；Death 是终态永不回 None，motion 永不恢复。
        /// 恢复到 Ground 时 Enter 会重置跳跃次数并 CrossFade 交还姿态 —— 无需额外交还逻辑。
        /// </summary>
        private void StateMachineTransitioned(ActionStateId from, ActionStateId to)
        {
            if (to == ActionStateId.None)
            {
                bool grounded = Owner.locomotionComponent.IsGrounded && Owner.rb.velocity.y <= 0.01f;                
                MotionMachine.ChangeState(grounded ? MotionStateId.Ground : MotionStateId.Air);
            }
            if(from == ActionStateId.None)
            {
                MotionMachine.ChangeState(MotionStateId.None);
            }
        }

        private void OnDestroy()
        {
            if (ActionMachine != null)
                ActionMachine.Transitioned -= StateMachineTransitioned;
            if (Owner != null && Owner.healthManageComponent != null)
                Owner.healthManageComponent.OnDied -= HandleOnDied;
        }
    }
}
