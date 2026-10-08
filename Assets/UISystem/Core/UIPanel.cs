using UnityEngine;
namespace UI
{
    /// <summary>
    /// 面板抽象基类：Show/Hide 只切 active，视觉表现交给 OnShow/OnHide。
    /// layer 挂在组件上（单一事实源跟 prefab 走），UIManager 不实例化即可读到。
    /// ⚠️ 面板实例被 UIManager 缓存且 UIRoot DDOL：面板不得持有场景对象引用，
    /// 跨场景数据一律走 Manager/事件，否则切场景悬空。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        [Tooltip("面板唯一 id。留空则用 gameObject.name（Instantiate 会带 (Clone)，UIManager 已做净化）")]
        [SerializeField] private string panelId;
        [Tooltip("本面板挂在哪一层")]
        [SerializeField] private UILayer layer = UILayer.Panel;
        public string PanelId => string.IsNullOrEmpty(panelId) ? gameObject.name : panelId;
        public UILayer Layer => layer;      
        public bool IsOpen => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow();
        }
        public void Hide()
        {
            OnHide();// 先回调再关（OnHide 里可能要读自身状态）
            gameObject.SetActive(false);
        }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}