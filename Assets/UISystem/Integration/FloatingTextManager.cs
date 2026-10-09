using UnityEngine;
using UI;
using System;
using System.Collections.Generic;
using System.Collections;
using Attributes;
namespace Managers.UI
{
    public class FloatingTextManager : IManager
    {
        public string Name => "FloatingTextManager";
        private static readonly List<Type> _dependencies = new() // 拓扑序：层根必须先建好
        {
            typeof(UIManager), // 飘字要挂 HUD 层根下，拓扑序保证层根先建好
        };
        public List<Type> Dependencies => _dependencies;
        private static FloatingTextManager Resolve => GameManager.Instance?.GetManager<FloatingTextManager>();
        const string PrefabPath = "data/ui/DamageNumber";
        GameObject _prefab;
        RectTransform _hudCanvasRect;        // HUD 层 Canvas 的 RectTransform，坐标转换用
        bool _prefabWarned;     // prefab 缺失只警告一次，防打斗中刷屏
        bool _camWarned;        // MainCamera 缺失只警告一次（同上）

        void OnAnyAttrChanged(AttributeChangedEventArgs e)
        {
            if(e.context.reason != ChangeReason.Damage && e.context.reason != ChangeReason.Heal) return;  // Init/Revive/Debug 不飘
            if(e.type != AttributeType.Health || e.owner == null) return;
            string text = Mathf.Abs(e.delta).ToString("0");          // 显示实际落地值（有防御后=减免后的数，GAS 语义的改进）
            Color color = e.delta < 0f ? Color.white : Color.green;  // 伤害白/治疗绿（顺手完成旧路标项）
            Show(text, e.owner.ChestPosition, color);
        }

        //============================= IManager =============================

        public IEnumerator Initialize()
        {
            _prefab = Resources.Load<GameObject>(PrefabPath);
            _hudCanvasRect = UIManager.GetLayerRoot(UILayer.HUD)?.GetComponent<RectTransform>();
            AttributeSet.AnyChanged += OnAnyAttrChanged;   // using Attributes;
            Debug.Log($"[FloatingText] Initialized（prefab {( _prefab != null ? "✓" : "✗" )}）");
            yield break;

        }
        public void Deinitialize()
        {
            AttributeSet.AnyChanged -= OnAnyAttrChanged;   // 与订阅对称
        }    // 纯 C# 管理器，运行时产物 DamageNumber 挂在 UIRoot 下随根销毁，无额外清理


        //============================= 静态接口 =============================

        /// <summary>
        /// 在 HUD 层显示飘字（DamageNumber），世界坐标 → HUD 层局部坐标
        /// - 由 DamageComponent.Hit 统一调用，受击侧统一出口。
        /// - Blocked/Miss 在 Entity.Hit 就被分流，不会到这——天然只飘真实命中
        /// - 由 FloatingTextManager 统一管理，prefab 缺失只警告一次，防打斗中刷屏
        /// </summary>
        /// <param name="text"></param>
        /// <param name="worldPos"></param>
        /// <param name="color"></param>
        public static void Show(string text, Vector3 worldPos, Color color)
        {
            var m = Resolve;  if(m == null) return;     // 引导/游戏侧永不为表现层崩
            if(m._prefab == null || m._hudCanvasRect == null)
            {
                if(!m._prefabWarned){ Debug.LogWarning("[FloatingText] prefab 或 HUD 层缺失，飘字跳过"); m._prefabWarned = true; }
                return;
            }
            var go = UnityEngine.Object.Instantiate(m._prefab, m._hudCanvasRect);
            go.name = m._prefab.name;       // 去 (Clone)，与 UIManager.ShowPanel 同规
            var rect = go.GetComponent<RectTransform>();
            // ── 世界→屏幕→Canvas 局部，两步各走各的相机语义，不能混 ──
            // 第一步：游戏世界坐标 → 屏幕像素，必须经 MainCamera 投影。
            //         注意不能写 RectTransformUtility.WorldToScreenPoint(null, worldPos)——
            //         null 相机版把输入当屏幕坐标直接截 xy（Overlay 世界空间 UI 专用），
            //         世界坐标喂进去 screenPos=(3.2,1.5) 这种值，落点就是屏幕左下角
            var cam = Camera.main;
            if(cam == null)
            {
                if(!m._camWarned) { Debug.LogWarning("[FloatingText] 场景没有 MainCamera，飘字跳过"); m._camWarned = true; }
                UnityEngine.Object.Destroy(go);
                return;
            }
            Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
            // 第二步：屏幕像素 → Canvas 局部坐标。Overlay 画布渲染不经相机，这里传 null 才是对的；
            //         内部已除掉 CanvasScaler 的缩放，localPos 直接赋 anchoredPosition
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(m._hudCanvasRect, screenPos, null, out Vector2 localPos))
            {
                rect.anchoredPosition = localPos;
            }
            go.GetComponent<DamageNumber>()?.Init(text, color);  // 立刻初始化，避免 Start 延迟

        }

    }
    
}