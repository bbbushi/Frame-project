using UnityEngine;
using UI;

namespace Managers.UI
{
    /// <summary>
    /// UIPanel 子类示范：生命周期日志 + Close() 供按钮 OnClick 绑定
    /// （Button 的 OnClick 拖本组件，函数选 TestPanel > Close）。
    /// </summary>
    public class TestPanel : UIPanel
    {
        protected override void OnShow() => Debug.Log($"[TestPanel] OnShow（{PanelId}）");
        protected override void OnHide() => Debug.Log($"[TestPanel] OnHide");

        /// 给 UI 按钮用的关闭入口
        public void Close() => UIManager.Hide(PanelId);
    }
}
