using System;
using System.Collections.Generic;
namespace Guide
{
    /// 刻意平坦：JsonUtility 不支持 Dictionary 与多态 —— 扩展此结构时的红线，类头注明
    
    /// <summary>
    /// 引导进度数据：
    /// 包含已完成的引导 ID 列表（string）、当前进行中的引导 ID（string）和当前步骤索引（int）。
    /// </summary>
    [Serializable]
    public class GuideProgressData
    {
        public List<string> completedGuideIds = new();
        public string runningGuideId = "";// 进行中引导（断点续传）
        public int runningStepIndex = 0;
    }
    public interface IGuideProgressStore
    {
        GuideProgressData Load();
        void Save(GuideProgressData data);
        void Clear();
    }
    /// 可移植默认实现：PlayerPrefs（key="Guide.Progress"，JsonUtility 存字符串）
}