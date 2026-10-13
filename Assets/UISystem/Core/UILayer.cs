namespace UI
{
    /// <summary>
    /// UI 层级，数值 = ScreenSpaceOverlay 的 sortingOrder。
    /// 对话画布=100（外部包硬编码，不入枚举防 GetLayerRoot 歧义）。
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