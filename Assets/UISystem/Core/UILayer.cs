namespace UI
{
    /// <summary>
    /// UI 层级，数值 = ScreenSpaceOverlay 的 sortingOrder。
    /// 对话画布=100（外部包硬编码，不入枚举防 GetLayerRoot 歧义）；200 预留 Fade/Loading。
    /// </summary>
    public enum UILayer
    {
        HUD = 10,
        Panel = 20,
        Guide = 40,
    }
}