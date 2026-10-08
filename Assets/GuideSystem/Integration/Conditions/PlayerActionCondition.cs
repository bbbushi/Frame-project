using System;
using Guide;
using InputComponent;
using Config;
namespace Managers.Guide
{
    /// <summary>
    /// 判定玩家执行某 PlayerAction。引用项目枚举 —— 只存在于适配层
    /// </summary>
    [Serializable]
    public class PlayerActionCondition : IGuideCondition
    {
        public PlayerAction action;
        public InputTriggerType phase = InputTriggerType.Down;
        public bool IsSatisfied(GuideConditionContext context)
        {
            var input = GameManager.Get<InputManager>();
            return phase switch
            {
                InputTriggerType.Down => input.IsPressed(action),
                InputTriggerType.Hold => input.IsHeld(action),
                InputTriggerType.Up   => input.IsReleased(action),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public string Describe() => $"玩家执行 {action} ({phase})";

    }
}
