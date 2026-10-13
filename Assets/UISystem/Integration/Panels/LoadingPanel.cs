using TMPro;
using UI;
using UnityEngine;

namespace Managers.UI
{
    /// <summary>
    /// 过渡面板（Exclusive+World）：全屏黑底+百分比，开=输入灭/世界停（四期派生自动，本类零闸门代码）。
    /// 进度由 GameFlowManager.Routine 每帧静态转发（ReportProgress），面板没开时静默忽略。
    /// </summary>
    public class LoadingPanel : UIPanel
    {
        [SerializeField] private TMP_Text progressText;   // 可空：没拖就不显示百分比
        public static LoadingPanel Active { get; private set; }   // ReportProgress 的转发目标

        protected override void OnShow()  { Active = this; ReportProgress(0f); }
        protected override void OnHide()  { if (Active == this) Active = null; }
        public static void ReportProgress(float p)
        {
            if (Active != null && Active.progressText != null)
                Active.progressText.SetText("{0:0}%", p * 100f);
        }
    }
}
