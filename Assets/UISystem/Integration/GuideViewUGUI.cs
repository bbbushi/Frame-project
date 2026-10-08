using UnityEngine;
using Guide;
using TMPro;
namespace Managers.UI
{

    public class GuideViewUGUI : MonoBehaviour, IGuideView
    {
        [SerializeField] private TextMeshProUGUI guideText; // 主文案 ctx.Text
        [SerializeField] private TextMeshProUGUI progressText; // "第 X/Y 步"
        [SerializeField] private TextMeshProUGUI conditionText; // ctx.ConditionText，空则清空
        public void ShowStep(GuideStepContext ctx)
        {
            if(ctx == null) return;
            gameObject.SetActive(true);
            if(guideText) guideText.text = ctx.Text;
            if(progressText) progressText.text = $"第 {ctx.StepIndex + 1}/{ctx.StepCount} 步";
            if(conditionText) conditionText.text = ctx.ConditionText ?? "";
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }
        private void Reset() => AutoBind();// 编辑器：首次添加组件时自动接线
        [ContextMenu("自动接线")]
        private void AutoBind()
        {
            guideText ??= transform.Find("GuideText")?.GetComponent<TextMeshProUGUI>();
            progressText ??= transform.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
            conditionText ??= transform.Find("ConditionText")?.GetComponent<TextMeshProUGUI>();
        }
    }
}