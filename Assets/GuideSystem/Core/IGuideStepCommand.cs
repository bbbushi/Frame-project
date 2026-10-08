using System.Collections.Generic;

namespace Guide
{
    /// <summary>
    /// 命令执行上下文。Scratch 供同一步骤内 Execute↔Revert 传递运行时状态。
    /// 注意：命令实例属于 SO 资产，运行时严禁写命令自身字段（Editor 下会持久污染资产），
    /// 一切运行时状态放 Scratch
    /// </summary>
    public class GuideCommandContext
    {
        public GuideStepContext Step;// 步骤信息快照
        public Dictionary<string, object> Scratch = new();// Execute 存、Revert 取
    }
    /// <summary>
    /// 步骤进入时执行的命令。步骤因任何原因离开（完成/超时中止/Stop/引导结束）时，
    /// 引擎按逆序调用 Revert。无需回滚的命令（如播音效）Revert 留空即可
    /// </summary>
    public interface IGuideStepCommand
    {
        void Execute(GuideCommandContext context);
        void Revert(GuideCommandContext context);
    }
}