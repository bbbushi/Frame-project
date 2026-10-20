namespace UI
{
    /// <summary>
    /// UI 层级，数值 = ScreenSpaceOverlay 的 sortingOrder。
    /// HUD: 玩家界面（血条/小地图/快捷栏/提示等），不独占输入，不暂停世界。
    /// Panel: 交互面板（设置/确认框/提示框/背包等），不独占输入，可暂停世界。
    /// Guide: 引导层（新手引导/剧情对话/教程等），不独占输入，可暂停世界。
    /// Overlay: 过渡层（UIFade 黑幕等全屏视觉，盖过对话，必要时连对话都遮）
    /// </summary>
    public enum UILayer
    {
        HUD = 10,
        Panel = 20,
        Guide = 40,
        /// <summary>过渡层：UIFade 黑幕等全屏视觉（盖过对话，必要时连对话都遮）</summary>
        Overlay = 200,
    }
}