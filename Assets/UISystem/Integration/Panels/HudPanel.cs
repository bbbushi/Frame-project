using UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Attributes;
using PlayerSystem;
namespace Managers.UI
{
    public class HudPanel : UIPanel
    {
       [SerializeField] private Image fill;// 血条填充图（Filled Horizontal）
       [SerializeField] private TMP_Text hpText;// 血量数值文本
       bool _bound; // 订阅状态标记（防重复订阅）
       protected override void OnShow()
        {
            TryBind();// 立刻试一次——Player 早就绪时就地绑定
        }
        void Update()
        {
            if(!_bound) TryBind();// Player 还没进场就每帧轻试一次（一次 null check，代价可忽略）
        }
        void TryBind()
        {
            var player = Player.Instance;
            if(player == null || player.healthManageComponent == null) return;// 时机未到，下帧再试
            player.healthManageComponent.Set.Changed += OnAttrChanged;
            _bound = true;
            RefreshBar();// 立刻刷新一次，避免进场时血条闪烁
        }
        void OnAttrChanged(AttributeChangedEventArgs e)
        {
            if(e.type != AttributeType.Health && e.type != AttributeType.MaxHealth) return;
            RefreshBar();
        }
        protected override void OnHide()
        {
            if(_bound && Player.Instance != null && Player.Instance.healthManageComponent != null)
                Player.Instance.healthManageComponent.Set.Changed -= OnAttrChanged;
            _bound = false; // 无条件重置：Player 已销毁时旧订阅随事件源失效，无需（也无法）退订，
                            // 但标记必须清——否则常驻 HUD 在 Player 重建后永远不再 TryBind
        }
        void RefreshBar()
        {
            var a = Player.Instance.healthManageComponent;
            fill.fillAmount = a.MaxHP > 0f ? a.CurrentHP / a.MaxHP : 0f;
            if(hpText != null) hpText.SetText("{0:0}/{1:0}", a.CurrentHP, a.MaxHP);
        }
       
    }
}