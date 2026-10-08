using System;
using Guide;
using System.Collections.Generic;
using UnityEngine;  
namespace Managers.Guide
{   
    ///<summary>
    /// "移动到某位置""血量低于 X"等无法用枚举表达的判定。
    /// 谓词在 GuideManager.Initialize 中注册
    ///</summary>
    [Serializable]
    public class PredicateCondition : IGuideCondition
    {
        public string predicateKey;
        private static readonly Dictionary<string, Func<GuideConditionContext, bool>> Registry = new();
        public static void Register(string key,Func<GuideConditionContext,bool> predicate)
        => Registry[key] = predicate;

        // 未注册 key：LogError + false（防引导死锁）
        public bool IsSatisfied(GuideConditionContext context)
        {
            if(Registry.TryGetValue(predicateKey, out var predicate))
            {
                return predicate(context);
            }
            else
            {
                Debug.LogError($"PredicateCondition 未注册 key: {predicateKey}");
                return false;
            }
        } 
        public string Describe() => $"自定义条件 {predicateKey}";
        public static void Clear() => Registry.Clear();
    }
    // 游戏侧用法：PredicateCondition.Register("player-landed", ctx => 玩家已落地);
}