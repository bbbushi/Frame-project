using UnityEngine;
using System.Collections.Generic;
namespace Guide
{
    /// <summary>
    /// 引导库: 存储所有可用的引导数据。
    /// </summary>
    [CreateAssetMenu(fileName = "GuideLibrary", menuName = "Data/Guide/GuideLibrary")]
    public class GuideLibrary : ScriptableObject
    {
        public List<GuideData> guides = new();
        public GuideData Find(string guideId) => guides.Find(g => g.guideId == guideId);
    }


    /// <summary>
    /// 条件判定上下文：刻意极简，只给时间与步骤信息，不携带任何游戏服务引用。
    /// 适配层条件需要 InputManager 时自己 GameManager.Get<T>()，context 不做扩展点
    /// </summary>
    public class GuideConditionContext
    {
        public GuideData Guide;
        public GuideStep Step;
        public float ElapsedSeconds;
        public float DeltaTime;
    }
}
