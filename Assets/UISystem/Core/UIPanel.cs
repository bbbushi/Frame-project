using UnityEngine;
namespace UI
{
    // 新增两个枚举（放 UIPanel 同文件或 Core 下新文件，namespace UI）
    /// <summary>
    /// 面板模态类型：None=普通，Popup=弹窗，Exclusive=独占
    /// 普通面板：不独占输入，不暂停世界（通常用于 HUD 或装饰性面板））
    /// 弹窗面板：不独占输入，可暂停世界（通常用于临时提示或次要交互面板如消息提示、次要设置））
    /// 独占面板：独占输入，可暂停世界 （通常用于重要交互面板如设置、确认对话框）
    /// </summary>
    public enum PanelModal {None, Popup, Exclusive}
    /// <summary>
    /// 面板暂停类型：None=不暂停，World=打开期间世界暂停(PauseScale=0)
    /// </summary>
    public enum PanelPause { None, World }
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
        // UIPanel 类内新增字段（对齐 layer 的"事实源跟 prefab 走"哲学，不入 PanelEntry）：
        [Tooltip("None=装饰/HUD不进导航栈；Popup=交互面板；Exclusive=独占(夺玩家输入)")]
        [SerializeField] private PanelModal modal = PanelModal.Popup;
        [Tooltip("World=打开期间世界暂停(PauseScale=0)，Exclusive 面板常用")]
        [SerializeField] private PanelPause pause = PanelPause.None;
        public PanelModal Modal => modal;
        public PanelPause Pause => pause;
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