using System;
using System.Collections.Generic;
using System.Collections;
using Managers;

namespace Frame.Player
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
                _player = Player.instance;
                if (_player == null) return;
            }

            // 同步水平输入与朝向到移动模块（朝向翻转由模块内部处理）
            _player.Locomotion.SetMoveInput(_input.MoveInput);

            // 输入映射到命令
            TryExecute(PlayerAction.Jump, _input.IsPressed(PlayerAction.Jump));
            TryExecute(PlayerAction.Thrust, _input.IsPressed(PlayerAction.Thrust));
            TryExecute(PlayerAction.Attack, _input.IsPressed(PlayerAction.Attack));
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
