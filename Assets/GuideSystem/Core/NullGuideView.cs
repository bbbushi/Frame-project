using UnityEngine;
namespace Guide
{
    /// NullObject：本期默认，仅打日志便于验证事件流
    public class NullGuideView : IGuideView
    {
        public void ShowStep(GuideStepContext context) => 
        Debug.Log($"[Guide] 步骤 {context.StepIndex + 1}/{context.StepCount}：{context.Text}（条件：{context.ConditionText}）");

        public void Hide(){}
    }
}