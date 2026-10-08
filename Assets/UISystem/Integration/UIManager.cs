using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace Managers.UI
{
    /// <summary>
    /// MOMS UI 宿主：构建 UIRoot 三层根 Canvas、按注册表实例化/缓存面板、保证唯一 EventSystem。
    /// 面板缓存只增不减（Unload/池化留二期）。
    /// </summary>
    public class UIManager : IManager
    {
        
        public string Name => "UIManager";
        private static readonly List<Type> _dependencies = new();   // 无依赖 → 拓扑第一批
        public List<Type> Dependencies => _dependencies;
        private static UIManager Resolve => GameManager.Instance?.GetManager<UIManager>();
        private const string ConfigPath = "data/ui/UIManagerConfig";
        private const string RootName = "UIRoot";
        // 显式列出要建的层（不遍历枚举——将来加 Overlay=200 预留层时不该自动建出空画布）
        private static readonly UILayer[] BuildLayers = {UILayer.HUD, UILayer.Panel, UILayer.Guide };
        private UIManagerConfig _config;
        private GameObject _root;
        private readonly Dictionary<UILayer, Transform> _layerRoots = new();
        private readonly Dictionary<string, UIPanel> _cache = new(); // 实例缓存，含已关闭面板
        private readonly Dictionary<string, UIManagerConfig.PanelEntry> _registry = new(); // 注册表，含已关闭面板


        //================================ IManager 接口 =================================

    
        /// <summary>
        /// 初始化（全程同步段，无等待）：
        /// ① Resources.Load 配置，失败 → CreateInstance 兜底（骨架照建，仅注册表为空）
        /// ② 建 UIRoot（按配置 DDOL）
        /// ③ 逐层建根 Canvas（HUD/Panel/Guide）
        /// ④ EnsureEventSystem
        /// ⑤ RebuildRegistry
        /// ⑥ 自动显示常驻面板
        /// ⑦ 哨兵日志（兼作 MOMS 扫描存在性检查）
        /// </summary>
        /// <returns></returns>
        public IEnumerator Initialize()
        {
            _config = Resources.Load<UIManagerConfig>(ConfigPath);
            if(_config == null)
            {
                Debug.LogWarning($"[UIManager] 未找到 {ConfigPath}，使用兜底空配置（面板注册表为空）");
                _config = ScriptableObject.CreateInstance<UIManagerConfig>(); //兜底空配置，避免后续空引用
            }
            _root = new GameObject(RootName);
            if(_config.dontDestroyOnLoad) UnityEngine.Object.DontDestroyOnLoad(_root);
            foreach(var layer in BuildLayers)
            {
                BuildLayerCanvas(layer, (int)layer);
            }
            EnsureEventSystem();
            RebuildRegistry();
            // 常驻面板：注册表里勾了 autoShow 的直接 Show（HUD 等）
            //（注册表条目必带 prefab——RebuildRegistry ① 已过滤空条目，这里不必再判）
            foreach(var entry in _registry.Values)
            {
                if(entry.autoShow)
                    ShowPanel(string.IsNullOrEmpty(entry.panelId) ? entry.prefab.PanelId : entry.panelId);
            }
            Debug.Log($"[UIManager] Initialized: {_registry.Count} panels registered, {_layerRoots.Count} layer roots built");
            yield break; // 全程同步，无等待——保证同帧先于对话/引导行为
        }


        /// <summary>
        /// 反初始化：销毁根节点、清缓存、清注册表、卸载配置
        /// ① 面板缓存只增不减（Unload/池化留二期），所以反初始化时清掉缓存
        /// ② 注册表只增不减（Unload/池化留二期），所以反初始化时清掉注册表 
        /// </summary>
        public void Deinitialize()
        {
            UnityEngine.Object.Destroy(_root);
            _layerRoots.Clear();
            _cache.Clear();
            _registry.Clear();
            _config = null;
        }

        //================================ 静态接口 =================================


        public static UIPanel Show(string panelId) {return Resolve == null ? null : Resolve.ShowPanel(panelId); }
        public static void Hide(string panelId) => Resolve?.HidePanel(panelId);

        
        /// <summary>
        /// 切换面板：若打开就关，若关闭就开
        /// </summary>
        /// <param name="panelId"></param>
        public static void Toggle(string panelId)
        {
            if(IsOpen(panelId)) Hide(panelId);
            else Show(panelId);
        }


        /// <summary>
        /// 查询面板是否打开：只查缓存，缓存没命中就返 false
        /// </summary>
        public static bool IsOpen(string panelId)
        {
            var m = Resolve;
            return m != null
                && m._cache.TryGetValue(panelId, out var p)
                && p != null            // Unity 对已 Destroy 对象重载了 == ：防缓存悬空引用
                && p.IsOpen;            // activeSelf，与 UIPanel.IsOpen 同一定义
        }


        /// <summary>
        /// 关闭所有「经 Show 打开、仍在缓存且 active」的面板。
        /// 直接看 panel.IsOpen，不按 PanelId 回查缓存——缓存键是注册表 id，
        /// 与组件 PanelId（可能走了回退）不保证相等，按后者查会漏关
        /// </summary>
        public static void CloseAllPanels()
        {
            var m = Resolve;
            if(m == null) return;
            foreach(var panel in m._cache.Values)
            {
                if(panel != null && panel.IsOpen) panel.Hide();
            }
        }


        /// <summary>
        /// 外部挂载入口（GuideManager 等用）。未知层 LogError 返 null
        /// </summary>
        public static Transform GetLayerRoot(UILayer layer)
        {
            if(Resolve == null) return null;
            if(Resolve._layerRoots.TryGetValue(layer,out var root)) return root;
            else
            {
                Debug.LogError($"UIManager.GetLayerRoot({layer}) failed: layer root not found.");
                return null;
            }
        }
        //================================ 内部实现 =================================


        /// <summary>
        /// 构建三层根 Canvas、按注册表实例化/缓存面板、保证唯一 EventSystem。
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private Transform BuildLayerCanvas(UILayer layer,int order)
        {
            var go = new GameObject($"{layer}Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().sortingOrder = order;
            go.transform.SetParent(_root.transform, false);
            SetupScaler(go.GetComponent<CanvasScaler>());
            _layerRoots[layer] = go.transform;
            return go.transform;
        }


        /// <summary>
        /// 设置 CanvasScaler：与对话画布一致，避免 UIManager 的面板和对话画布的缩放不一致
        /// </summary>
        /// <param name="scaler"></param>
        private void SetupScaler(CanvasScaler scaler)   
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _config.referenceResolution;
            scaler.matchWidthOrHeight = _config.matchWidthOrHeight;
        }


        /// <summary>
        /// 保证场景中存在唯一 EventSystem：已存在则复用（inactive 则警告），否则新建。
        /// 与对话插件 EnsureEventSystem 双向收敛到唯一实例
        /// </summary>
        private void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if(es != null)
            {
                if(!es.gameObject.activeInHierarchy)
                    Debug.LogWarning("[UIManager] 检测到已存在 EventSystem，但未激活（inactive 的不处理输入，请检查）");
                return;
            }
            var go = new GameObject("UIEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(_root.transform, false);
        }


        /// <summary>
        /// 重建注册表：清空旧表，按 config 逐条检查并填入新表
        /// ① 条目本身无效：没拖 prefab 的行直接跳过
        /// ② id 回退：config 没填 → 用 prefab 组件的 PanelId
        /// ③ 重复 id：先到先得，后到的报错跳过（避免静默覆盖让人查半天）
        /// ④ 填了 id 但和 prefab 组件上的 PanelId 不一致 → 只警告不拦截，键以 config 为准
        /// </summary>
        private void RebuildRegistry()
        {
            _registry.Clear();  // 幂等：重建语义，先清旧表
            if(_config ?. panels == null) return;   // 兜底 config 也可能没面板，直接空表
            foreach(var entry in _config.panels)
            {
                // ① 条目本身无效：没拖 prefab 的行直接跳过
                if(entry == null || entry.prefab == null){Debug.LogWarning($"UIManager.RebuildRegistry: null entry or prefab for panelId = {entry?.panelId}"); continue;}
                
                // ② id 回退：config 没填 → 用 prefab 组件的 PanelId
                //（UIPanel.PanelId 自带二级回退：组件没填就取 gameObject.name，这里一级表达式就兜完了）
                string id = entry.panelId;
                if(string.IsNullOrEmpty(id))
                { 
                    id = entry.prefab.PanelId ; 
                    Debug.LogWarning($"UIManager.RebuildRegistry: empty panelId in config, using prefab.PanelId={id}");
                }
                // ③ 重复 id：先到先得，后到的报错跳过（避免静默覆盖让人查半天）
                if(_registry.ContainsKey(id))
                {
                    Debug.LogWarning($"UIManager.RebuildRegistry: duplicate panelId={id} in config, skipping");
                    continue;
                }
                // ④ 填了 id 但和 prefab 组件上的 PanelId 不一致 → 只警告不拦截，键以 config 为准
                if(!string.IsNullOrEmpty(entry.panelId) && entry.panelId != entry.prefab.PanelId){Debug.LogWarning($"UIManager.RebuildRegistry: panelId mismatch in config vs prefab: config={entry.panelId}, prefab={entry.prefab.PanelId}");}
                _registry[id] = entry;
            }
        }


        /// <summary>
        /// 显示面板：先查缓存，缓存没命中再查注册表实例化
        /// </summary>
        /// <param name="panelId"></param>
        /// <returns></returns>
        private UIPanel ShowPanel(string panelId)
        {
            // ① 缓存命中：复用已有实例（面板只建一次，之后开/关只是 SetActive）
            if(_cache.TryGetValue(panelId, out var panel))
            {
                if(panel == null)       // Unity 对已 Destroy 的对象重载了 == ：这里判真
                {
                    _cache.Remove(panelId);             // 先清掉缓存，避免后续重复判真  
                }
                else
                {
                    panel.Show();
                    panel.transform.SetAsLastSibling(); // 保证置顶
                    return panel;
                }
            }
            // ② 注册表查 prefab（entry 可能是 default，LogError 不能解引用它）
            if(!_registry.TryGetValue(panelId, out var entry) || entry.prefab == null)
            {
                Debug.LogError($"[UI] 未注册的面板：「{panelId}」");
                return null;
            }
            // ③ 层根：layer 的事实源在 prefab 组件上，不实例化就能读到
            if(!_layerRoots.TryGetValue(entry.prefab.Layer, out var layerRoot))
            {
                Debug.LogError($"[UI] 面板「{panelId}」的目标层 {entry.prefab.Layer} 没有对应层根");
                return null;
            }
            // ④ 实例化到层根，并净化掉 "(Clone)" 后缀——
            //防组件 PanelId 留空时回退到 gameObject.name，把 "(Clone)" 带进缓存键
            var go = UnityEngine.Object.Instantiate(entry.prefab.gameObject, layerRoot);
            go.name = entry.prefab.name; // 去掉 "(Clone)"，方便查找
            panel = go.GetComponent<UIPanel>();
            if(panel == null)
            {
                Debug.LogError($"[UI] 面板「{panelId}」的 prefab 根上没有 UIPanel 组件");
                UnityEngine.Object.Destroy(go);
                return null;
            }
            // ⑤ 入缓存并打开
            _cache[panelId] = panel; // 缓存实例
            panel.Show();
            return panel;
        }


        /// <summary>
        /// 关闭面板：已开才 Hide（已关幂等静默）；未缓存/悬空 → LogWarning（多为拼写错）
        /// </summary>
        private void HidePanel(string panelId)
        {
            if(!_cache.TryGetValue(panelId, out var panel) || panel == null)
            {
                Debug.LogWarning($"[UI] HidePanel：未找到面板「{panelId}」");
                return;
            }
            if(panel.IsOpen) panel.Hide();
        }

    }
}