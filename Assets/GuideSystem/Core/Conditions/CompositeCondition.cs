using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Guide
{
    [Serializable]
    public class CompositeCondition : IGuideCondition
    {
        public enum Mode {Any , All}
        public Mode mode = Mode.All;
        [SubclassSelector, SerializeReference] public List<IGuideCondition> conditions = new();
        //IsSatisfied 按 mode 聚合；Describe 聚合子描述。嵌套枚举在 SerializeReference 下正常
        public bool IsSatisfied(GuideConditionContext context) =>
        mode == Mode.All ? conditions.All(c => c != null && c.IsSatisfied(context)) 
        : conditions.Any(c => c != null && c.IsSatisfied(context));

        public string Describe() =>
    $"{(mode == Mode.All ? "全部" : "任一")}满足: [{string.Join(", ", conditions.Select(c => c?.Describe() ?? "null"))}]";
    }
}