namespace Attributes
{
    /// <summary>
    /// 变化原因：表现层据此分色/过滤（飘字只认 Damage/Heal）
    /// </summary>
    public enum ChangeReason{Init, Damage, Heal, Revive, Debug, Set }
    /// <summary>
    /// 变化上下文：谁改的、为什么改。二期升级为 GameplayEffectSpec 引用。
    /// </summary>
    public struct ChangeContext
    {
        public Entity source;   // 伤害来源等（可为 null：环境/调试）
        public ChangeReason reason;
        public ChangeContext(ChangeReason reason, Entity source = null)
        {
            this.reason = reason;
            this.source = source;
        } 
    }
    /// <summary>
    /// 属性变化事件参数——比旧 OnChanged(current,max) 多出的正是
    /// 二期飘字当初拿不到的东西：old/new/delta/来源/原因
    /// </summary>
    public struct AttributeChangedEventArgs
    {
        public Entity owner;
        public AttributeType type;
        public float oldValue, newValue, delta;
        public ChangeContext context;
    }
}