namespace Guide
{
    /// 将来的挖孔高亮/气泡/手指实现此接口
    public interface IGuideView
    {
        void ShowStep(GuideStepContext context);// 展示/切换步骤
        void Hide();// 引导结束/中断
    }
}
    