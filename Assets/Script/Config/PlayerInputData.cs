using System.Collections.Generic;
using UnityEngine;
using InputComponent;
namespace Config{
    /// <summary>
    /// 输入触发方式
    /// </summary>
    public enum InputTriggerType
    {
        Down,  // GetKeyDown
        Hold,  // GetKey
        Up     // GetKeyUp
    }

    /// <summary>
    /// 单个动作的键位绑定
    /// </summary>
    [System.Serializable]
    public class ActionBinding
    {
        public PlayerAction action;
        public KeyCode key;
        public InputTriggerType trigger;
    }

    /// <summary>
    /// 玩家输入配置 ScriptableObject，在 Inspector 中调整键位
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerInputData", menuName = "Data/PlayerInputData")]
    public class PlayerInputData : ScriptableObject
    {
        public List<ActionBinding> bindings = new()
        {
            new() { action = PlayerAction.MoveLeft,   key = KeyCode.A,          trigger = InputTriggerType.Hold },
            new() { action = PlayerAction.MoveRight,  key = KeyCode.D,          trigger = InputTriggerType.Hold },
            new() { action = PlayerAction.Jump,       key = KeyCode.Space,      trigger = InputTriggerType.Down },
            new() { action = PlayerAction.Thrust,     key = KeyCode.S,          trigger = InputTriggerType.Down },
            new() { action = PlayerAction.Attack,     key = KeyCode.Mouse0,     trigger = InputTriggerType.Down },
            new() { action = PlayerAction.BulletTime, key = KeyCode.Mouse1,     trigger = InputTriggerType.Down },
            new() { action = PlayerAction.Execution,  key = KeyCode.E,          trigger = InputTriggerType.Down },
            new() { action = PlayerAction.Mark,       key = KeyCode.Mouse0,     trigger = InputTriggerType.Down },
        };
    }
}