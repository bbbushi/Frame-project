using System;
using Guide;
using Managers;
namespace Managers.Guide
{
    /// 步骤期间接管全局输入开关，离开时恢复进入前的值
    [Serializable]
    public class SetInputEnabledCommand : IGuideStepCommand
    {
        public bool enabledOnStep = false;   // 演出步骤填 false 锁输入
        public void Execute(GuideCommandContext ctx)
        {
            ctx.Scratch["prev"] = GameManager.Get<InputManager>().IsInputEnabled;
            GameManager.Get<InputManager>().IsInputEnabled = enabledOnStep;
        }
        public void Revert(GuideCommandContext ctx)
            => GameManager.Get<InputManager>().IsInputEnabled =
               ctx.Scratch.TryGetValue("prev", out var v) && v is bool b && b;
    }
}
