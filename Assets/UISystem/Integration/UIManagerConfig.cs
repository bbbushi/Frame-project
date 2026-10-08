using UnityEngine;
using System.Collections.Generic;
using UI;
using System;
namespace Managers.UI
{
    [CreateAssetMenu(menuName = "MOMS/UIManagerConfig", fileName = "UIManagerConfig")]
    public class UIManagerConfig : ScriptableObject
    {
        [Header ("缩放（与对话画布一致）")]
        public Vector2 referenceResolution = new Vector2(1280, 720);// 字段初始化器 = 缺资产时的兜底默认值
        [Range(0f ,1f)] public float matchWidthOrHeight = 0.5f;
        [Header("生存期")]
        public bool dontDestroyOnLoad = true;

        [Header("面板注册表"),Tooltip("layer 归 UIPanel 组件管，此处不放，防两处漂移")]
        public List<PanelEntry> panels = new();

        /// <summary>
        /// 面板注册表条目
        /// 仅用于编辑器配置，运行时不使用
        /// </summary>
        [Serializable]
        public class PanelEntry
        {
            public string panelId;
            public UIPanel prefab;
            [Tooltip("勾选后 UIManager 初始化完成即自动 Show（常驻面板：HUD、后期 Loading 屏等）")]
            public bool autoShow;
        }

    }
    

}