using System;
using UnityEngine;
using System.Collections.Generic;
namespace Guide
{
    [Serializable]
    public class AutoCompleteCondition : IGuideCondition
    {
        public float duration = 1f;
        public bool IsSatisfied(GuideConditionContext context) => context.ElapsedSeconds >= duration;
        public string Describe() => duration <= 0f ? "自动完成，无时间限制" : $"自动完成，持续时间：{duration}秒";
    }
    
}
