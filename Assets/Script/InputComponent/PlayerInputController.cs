using System;
using System.Collections.Generic;
using System.Collections;
using Managers;
using UnityEngine;
using PlayerSystem;
using Commands;
using State_Player;
namespace InputComponent
{
    /// <summary>
    /// 玩家输入控制器 (Invoker) — 将 InputManager 的输入映射为 IPlayerCommand 并执行。
    /// 适配领域模块架构：命令直接通过 Player 的模块访问判断和执行。
    /// </summary>
    public class PlayerInputController : IManager, IUpdatable
    {
        public string Name => "PlayerInputController";
        public List<Type> Dependencies => new() { typeof(InputManager) };

        private InputManager _input;
        private Player _player;
        private readonly Dictionary<PlayerAction, IPlayerCommand> _commands = new();

        public IEnumerator Initialize()
        {
            _input = GameManager.Get<InputManager>();
            BuildCommands();
            yield break;
        }

        public void Deinitialize()
        {
            _commands.Clear();
            _player = null;
            _input = null;
        }

        public void OnUpdate(float deltaTime)
        {
            if (_input == null) return;

            // 懒加载 Player 引用
            if (_player == null)
            {
                _player = Player.Instance;
                if (_player == null) return;
            }

            // ── 子弹时间模式：输入全部重定向 ──
            if (_player.ModuleControlComponent.BulletTime.IsActive)
            {
                HandleBulletTimeInputs();
                return;
            }

            // ── 正常模式 ──
            // 同步水平输入与朝向到移动模块（朝向翻转由模块内部处理）
            _player.ModuleControlComponent.Locomotion.SetMoveInput(_input.MoveInput);

            // 右键按下 → 进入子弹时间（不切状态，只设 IsActive + 改 timeScale）
            if (_input.IsPressed(PlayerAction.BulletTime))
            {
                _player.ModuleControlComponent.BulletTime.EnterBulletTime();
                return;
            }

            // 输入映射到命令
            TryExecute(PlayerAction.Jump, _input.IsPressed(PlayerAction.Jump));
            TryExecute(PlayerAction.Thrust, _input.IsPressed(PlayerAction.Thrust));
            TryExecute(PlayerAction.Attack, _input.IsPressed(PlayerAction.Attack));
        }

        // ═══════════════════════════════════════════════════
        //  子弹时间输入处理
        // ═══════════════════════════════════════════════════
        private void HandleBulletTimeInputs()
        {
            var bt = _player.ModuleControlComponent.BulletTime;

            // 移动输入（人物可正常操作，timeScale 0.2 使动作自然变慢）
            _player.ModuleControlComponent.Locomotion.SetMoveInput(_input.MoveInput);

            // 跳跃
            if (_input.IsPressed(PlayerAction.Jump))
                TryExecute(PlayerAction.Jump, true);

            // 鼠标移动 → 更新瞄准角度（限制在 120° 扇形内）
            float mouseDelta = Input.GetAxis("Mouse X");
            bt.AimAngle += mouseDelta * 3f; // 灵敏度
            float halfArc = bt.AimArcAngle * 0.5f;
            bt.AimAngle = Mathf.Clamp(bt.AimAngle, -halfArc, halfArc);

            // 右键按下 → 取消子弹时间
            if (_input.IsPressed(PlayerAction.BulletTime))
            {
                bt.CancelBulletTime();
                return;
            }

            // E 键 → 进入处决状态（统一走 InputManager）
            if (_input.IsPressed(PlayerAction.Execution) && bt.MarkCount > 0 && !bt.IsExecuting)
            {
                _player.AnimatorComponent.ActionMachine.ChangeState(ActionStateId.Execution);
                return;
            }

            // 左键点击 → 标记敌人（统一走 InputManager）
            if (_input.IsPressed(PlayerAction.Mark))
            {
                TryMarkEnemyUnderMouse();
            }
        }

        /// <summary>鼠标射线检测敌人并尝试标记</summary>
        private void TryMarkEnemyUnderMouse()
        {
            var bt = _player.ModuleControlComponent.BulletTime;
            if (!bt.CanMarkMore) return;

            Vector3 mousePos = Camera.main != null ? Camera.main.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
            mousePos.z = 0f;

            var hits = Physics2D.OverlapCircleAll(mousePos, 0.3f);
            foreach (var h in hits)
            {
                if (((1 << h.gameObject.layer) & bt.EnemyLayer.value) != 0)
                {
                    if (bt.TryMark(h.transform))
                        return;
                }
            }
        }

        private void BuildCommands()
        {
            _commands[PlayerAction.Jump] = new JumpCommand();
            _commands[PlayerAction.Thrust] = new ThrustCommand();
            _commands[PlayerAction.Attack] = new AttackCommand();
        }

        private void TryExecute(PlayerAction action, bool triggered)
        {
            if (!triggered) return;
            if (_commands.TryGetValue(action, out var cmd) && cmd.CanExecute(_player))
                cmd.Execute(_player);
        }
    }
}
