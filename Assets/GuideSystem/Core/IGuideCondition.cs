namespace Guide
{
    
    /// 实现类必须是 [Serializable] 普通类（供 SerializeReference 序列化）
    
    /// <summary>
    /// 引导步骤的完成条件。每帧轮询 IsSatisfied，返回 true 即完成本步。
    /// </summary>
    public interface IGuideCondition
    {
        bool IsSatisfied(GuideConditionContext context); // 每帧轮询，true 即完成本步
        string Describe();  // 人类可读描述（日志 / 将来 UI 副文案）
    }
}