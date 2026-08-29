using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerSystem;
using ActComponents;

namespace State_Player
{
    /// <summary>
    /// 动作层状态基类 — 动作与移动正交：动作层不应用移动输入（移动层负责），
    /// 只负责调度模块并在完成条件满足时 Request(None)。需要超时兜底的子类自行检查 stateTimer。
    /// </summary>
    public abstract class ActionState : PlayerState
    {
        protected ActionState(Player player, string animBoolName) : base(player, animBoolName) { }
    }

    /// <summary>
    /// 动作层空状态 — 未在做任何动作，也是所有动作完成后的回归状态（animStateName=null，静默跳过姿态切换）。
    /// 互斥模型下姿态交还不再由本状态负责：宿主的转换联动会随即恢复移动层，
    /// Ground/Air 各自的 Enter → CrossFade 天然完成姿态交还。
    /// </summary>
    public class PlayerNoneState : ActionState
    {
        public PlayerNoneState(Player player, string animStateName) : base(player, animStateName) { }
    }

    /// <summary>
    /// 攻击状态 — 动画事件 AttackTrigger 出判定、AnimEnd 收招退出。
    /// 攻击期间通过 AddIgnore(All) 锁移动/跳跃/突刺，退出后再追加一段硬直（attackRecovery）。
    /// </summary>
    public class PlayerAttackState : ActionState
    {
        public PlayerAttackState(Player player, string animBoolName) : base(player, animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            Config.PlayerControllerData cfg = player.PlayerConfig;
            float ignoreDuration = cfg != null ? cfg.attackIgnoreDuration : 0.3f;

            player.locomotionComponent.ZeroVelocity();
            player.actionIgnoreComponent.AddIgnore(ignoreDuration, ActionIgnoreTag.All);
            // 攻击起手：前冲位移 + 记录攻击时间（战斗模块）
            player.ModuleControlComponent.Combat.BeginAttack();
        }

        public override void Exit()
        {
            base.Exit();
            Config.PlayerControllerData cfg = player.PlayerConfig;
            player.BusyFor(cfg != null ? cfg.attackRecovery : 0.3f);
            // 中止残留的攻击前冲：attackForce.length 若长于攻击动画，曲线尾部的近零速度
            // 会在 RefreshFixedUpdate 中每物理帧覆写 rb.velocity.x，表现为"跑步动画但不位移"
            player.locomotionComponent.Interrupt();
        }

        public override void Update()
        {
            base.Update();
            player.locomotionComponent.ZeroVelocity();

            if (AnimEndTrigger)
            {
                Debug.Log("Attack animation ended, returning to None state.");
                player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.None);
            }

                
        }
    }

    /// <summary>
    /// 突刺状态 — 位移核心由 PlayerThrust.ThrustCore() 驱动（ForceMove + 重力归零），
    /// 冷却占用在命令入口 StartThrust 中完成；突刺结束回 None，落地/离地由移动层自行跟踪。
    /// </summary>
    public class PlayerThrustState : ActionState
    {
        public PlayerThrustState(Player player, string animBoolName) : base(player, animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            player.ModuleControlComponent.Thrust.ThrustCore();
        }

        public override void Update()
        {
            base.Update();

            if (!player.ModuleControlComponent.Thrust.IsThrusting)
                player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.None);
        }
    }

    /// <summary>
    /// 处决状态 — 链式突刺由 PlayerBulletTime.BeginExecution 协程驱动（内部复用 ThrustCore，不切状态），
    /// 状态机全程持有 Execution；链结束后回 None。
    /// </summary>
    public class PlayerExecutionState : ActionState
    {
        public PlayerExecutionState(Player player, string animBoolName) : base(player, animBoolName) { }

        public override void Enter()
        {
            base.Enter();
            player.ModuleControlComponent.BulletTime.BeginExecution();
        }

        public override void Update()
        {
            base.Update();

            if (!player.ModuleControlComponent.BulletTime.IsExecuting)
                player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.None);
        }
    }

    /// <summary>
    /// 死亡状态 — 终态（守卫表禁止转出）：停止移动、零重力，短延迟后重载场景。
    /// </summary>
    public class PlayerDeathState : ActionState
    {
        private float deathAnimationDuration = 1.5f;
        private bool hasGameOverTriggered;

        public PlayerDeathState(Player player, string animBoolName) : base(player, animBoolName) { }

        public override void Enter()
        {
            base.Enter(); // animStateName=null：暂无死亡剪辑，静默跳过姿态切换（保留当前画面）
            hasGameOverTriggered = false;

            Config.PlayerControllerData cfg = player.PlayerConfig;
            if (cfg != null) deathAnimationDuration = cfg.deathAnimationDuration;

            player.locomotionComponent.Stop();
            player.rb.gravityScale = 0f;
        }

        public override void Update()
        {
            if (stateTimer > 0f)
                stateTimer -= Time.deltaTime;
            else if (!hasGameOverTriggered)
            {
                hasGameOverTriggered = true;
                player.StartCoroutine(HandleGameOver());
            }
        }

        private IEnumerator HandleGameOver()
        {
            yield return new WaitForSeconds(deathAnimationDuration);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
