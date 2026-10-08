using UnityEngine;
using System.Collections.Generic;

namespace Guide
{
    /// <summary>
    /// 引导超时动作枚举：空、完成步骤、中止引导
    /// </summary>
    public enum GuideTimeOutAction{
        None,
        CompleteStep,
        AbortGuide
    }

    /// <summary>
    /// 引导步骤数据结构。每个步骤包含：
    /// - 文案（text）
    /// - 高亮目标（targetKey）
    /// - 完成条件（completeCondition）
    /// - 超时设置（timeoutSeconds + timeoutAction）
    /// - 下一步去向（nextStepId）
    /// - 进入时执行的命令（onEnterCommands）
    /// </summary>
    [System.Serializable]
    public class GuideStep
    {
        /// nextStepId 的哨兵值：当前步骤完成后提前结束引导
        public const string EndStepId = "__end__";

        [Tooltip("步骤唯一 ID（引导内唯一即可，如 step_move）")]
        public string stepId;
        [TextArea(2,4)] public string text;//步骤文本
        public string targetKey;//高亮目标键
        [SubclassSelector, SerializeReference, Tooltip("完成判断条件")]
        public IGuideCondition completeCondition;

        [Min(0)] public float timeoutSeconds;
        public GuideTimeOutAction timeoutAction = GuideTimeOutAction.CompleteStep;
        [Tooltip("下一步去向。留空 = 顺序推进（index+1）；填某步骤 stepId = 显式跳转（分支/跳步/回环）；填 __end__ = 提前结束引导")]
        public string nextStepId;
        [Tooltip("进入本步骤时执行的命令（锁输入/播音效等逻辑副作用），步骤离开时引擎自动逆序回滚")]
        [SubclassSelector, SerializeReference] public List<IGuideStepCommand> onEnterCommands = new();
    }

    /// <summary>
    /// 引导步骤的运行时上下文。每帧轮询 IsSatisfied，返回 true 即完成本步。
    /// 存储了步骤的全部信息快照（文案/高亮目标/条件描述/索引/总数），供表现层 UI 使用。
    /// 运行时状态（如计时器）放在 GuideCommandContext.Scratch 中。
    /// </summary>
    public class GuideStepContext
    {
        public string GuideId,StepId;
        public string Text; // 文案
        public string TargetKey;// 可选高亮目标
        public string ConditionText; // 条件 Describe()，调试/副文案用
        public int StepIndex,StepCount;
    }
}