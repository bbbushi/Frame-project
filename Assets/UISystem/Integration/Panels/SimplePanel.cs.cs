using UnityEngine;
using UI;

  namespace Managers.UI
  {
      /// <summary>
      /// 通用面板：没有内容逻辑的面板直接挂这个，不写子类（暂停菜单/死亡面板/确认框/提示框…）。
      /// 语义（modal/pause/layer/panelId）全在 prefab 字段上配；多个 prefab 共用本类毫无问题——
      /// UIManager 全程按 panelId 字符串寻址，不看组件类型。
      /// 只有面板需要"内容逻辑"（绑数据/动态列表/事件订阅，参照 HudPanel）时才值得新写子类。
      /// </summary>
      public class SimplePanel : UIPanel
      {
          protected override void OnShow() => Debug.Log($"[SimplePanel] OnShow（{PanelId}）");
          protected override void OnHide() => Debug.Log($"[SimplePanel] OnHide");

          /// 给"继续游戏"类按钮用的关闭入口（Button OnClick 拖本组件选 SimplePanel > Close）
          public void Close() => UIManager.Hide(PanelId);
      }
  }