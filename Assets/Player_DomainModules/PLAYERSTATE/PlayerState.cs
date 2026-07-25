using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 状态基类 — 所有玩家状态的抽象。
    /// 状态只需一个 player 引用，通过 player.Health / player.Locomotion / player.Combat / player.Thrust 访问一切。
    /// 不区分"数据层"和"组件层" — 每个领域模块自己封装自己的数据和操作。
    /// </summary>
    public class PlayerState
    {
        protected Player player;
        protected PlayerStateMachine stateMachine;
        protected string animBoolName;
        protected float Xinput;
        protected float stateTimer;
        protected bool AnimEndTrigger;

        public PlayerState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
        {
            player = _player;
            stateMachine = _stateMachine;
            animBoolName = _animBoolName;
        }

        public virtual void Enter()
        {
            player.anim.SetBool(animBoolName, true);
            AnimEndTrigger = false;
            stateTimer = 0f;
        }

        public virtual void Update()
        {
            if (!player.Thrust.IsThrusting)
            {
                Xinput = player.Locomotion.HorizontalInput;
                player.Locomotion.ApplyHorizontal(Xinput);
            }
            player.anim.SetFloat("yvelocity", player.rb.velocity.y);

            if (stateTimer > 0f)
                stateTimer -= Time.deltaTime;
        }

        public virtual void Exit()
        {
            player.anim.SetBool(animBoolName, false);
        }

        public virtual void Trigger()
        {
            AnimEndTrigger = true;
        }
    }
}
