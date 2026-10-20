using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using InputComponent;
namespace Managers.UI
{
    /// <summary>
    /// MOMS UI 宿主：构建 UIRoot 三层根 Canvas、按注册表实例化/缓存面板、保证唯一 EventSystem。
    /// 面板缓存只增不减（Unload/池化留二期）。
    /// </summary>
    public class UIManager : IManager, IUpdatable
    {
        
        public string Name => "UIManager";
        private static readonly List<Type> _dependencies = new(){ typeof(InputManager) };
        public List<Type> Dependencies => _dependencies;
        private static UIManager Resolve => GameManager.Instance?.GetManager<UIManager>();
        private const string ConfigPath = "data/ui/UIManagerConfig";
        private const string RootName = "UIRoot";
        // 显式列出要建的层（不遍历枚举——纯常量清单，缺哪层一眼可见）
        private static readonly UILayer[] BuildLayers = {UILayer.HUD, UILayer.Panel, UILayer.Guide, UILayer.Overlay};
        private UIManagerConfig _config;
        private GameObject _root;
        /// <summary>
        /// 输入/暂停的唯一写入者：每次面板开合后全量重算（派生状态，§3.1）。
        /// 冒烟测试不打桩也能断言：决策结果先落这两个只读属性，再写向真闸门
        /// </summary>
        public bool DesiredInputEnabled { get; private set; } = true;
        public float DesiredPauseScale { get; private set; } = 1f;

        private readonly Dictionary<UILayer, Transform> _layerRoots = new();
        private readonly Dictionary<string, UIPanel> _cache = new(); // 实例缓存，含已关闭面板
        private readonly Dictionary<string, UIManagerConfig.PanelEntry> _registry = new(); // 注册表，含已关闭面板
        /// <summary>
        /// 导航栈：只收 modal≠None 的面板实例，用于管理面板的打开顺序和关闭逻辑。
        /// </summary>
        private readonly List<UIPanel> _stack = new(); // 导航栈：只收 modal≠None


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
            // DDOL 是 Play 概念——编辑器上下文（冒烟/编辑器工具）调用会抛 InvalidOperationException，非 Play 跳过
            if(_config.dontDestroyOnLoad && Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_root);
            foreach(var layer in BuildLayers)
            {
                BuildLayerCanvas(layer, (int)layer);
            }
            EnsureEventSystem();
            EnsureFadeVeil();
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
            _stack.Clear();
            _config = null;
        }


        //================================ IUpdatable =================================
        public void OnUpdate(float dt)// MOMS 每帧回调
        {
            var input = GameManager.Get<InputManager>();
            if (input == null || !input.IsUICancelPressed) return;
            // 过渡期 UI 不持有 Esc（五期 §五.5.7）：LoadingPanel 在栈里，Esc 会把它退掉=过渡中断。
            // 放 OnUpdate 不放 HandleCancel——后者是纯退栈语义（对齐四期决策 9 的分层理由）
            if (Managers.GameFlow.GameFlowManager.Current == Managers.GameFlow.FlowState.Loading) return;
            HandleCancel();
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
            m._stack.Clear();       // 全关=栈空；上面直接 Hide 不逐个出栈，末尾统一清
            m.ApplyStackState();    // 派生重算：输入/暂停自然全恢复（漏了这步=面板全关但世界还停着、输入还锁着）
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
        /// 全屏过渡黑幕（UIFade 的画布，五期方案 §五.5.5）：挂在 Overlay 层，常驻随 UIRoot 跨场景。
        /// ⚠ Image.raycastTarget 必须 false——alpha=0 的 Image 只要 raycastTarget=true 就会挡住全屏点击
        ///（CanvasGroup 不拦射线，Image 才拦），UGUI 高频隐形坑。
        /// </summary>
        private void EnsureFadeVeil()
        {
            var go = new GameObject("FadeVeil", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(_layerRoots[UILayer.Overlay], false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;          // 四边 stretch 拉满层画布
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;                    // 全透明起始，UIFade 渐变驱动
            group.blocksRaycasts = false;
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
                    RegisterOpen(panel);   // ⚠ 缓存命中分支同样要走收尾——面板只建一次，第二次开永远走这，
                    return panel;          // 漏了压栈=二次打开不进栈（Exclusive 二次打开=假暂停）
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
            RegisterOpen(panel);
            return panel;
        }


        /// <summary>
        /// 关闭面板：已开才 Hide（已关幂等静默）；
        /// 未缓存/悬空 → LogWarning（多为拼写错）;
        /// </summary>
        private void HidePanel(string panelId)
        {
            if(!_cache.TryGetValue(panelId, out var panel) || panel == null)
            {
                Debug.LogWarning($"[UI] HidePanel：未找到面板「{panelId}」");
                return;
            }
            if(panel.IsOpen) HidePanelDirect(panel);
        }
        /// <summary>
        /// 关闭实例的统一出口（HidePanel / HandleCancel 共用）——不走 panelId 查缓存：
        /// 栈里存的是实例，缓存键是注册表 id，两者可能不相等（CloseAllPanels 注释同款坑），按 id 反查会漏
        /// </summary>
        private void HidePanelDirect(UIPanel panel)
        {
            if(!panel.IsOpen) return;   // 已关幂等静默
            _stack.RemoveAll(p => p == panel);
            panel.Hide();
            ApplyStackState();
        }
        /// <summary>
        /// Show 的收尾（缓存命中/首次实例化两个出口共用）：交互面板压栈顶（重复开=置顶语义）+ 全量重算输入/暂停
        /// </summary>
        private void RegisterOpen(UIPanel panel)
        {
            if (panel.Modal != PanelModal.None) { _stack.RemoveAll(p => p == panel); _stack.Add(panel); }
            ApplyStackState();
        }
        /// <summary>
        /// Esc 语义 = 退栈顶（不是关选中项）；
        /// 栈空无事发生
        /// </summary>
        public static void HandleCancel()
        {
            var m = Resolve; if (m == null) return;
            if (m._stack.Count == 0) return;
            m.HidePanelDirect(m._stack[^1]);   // 直关实例：栈顶的 PanelId 未必等于缓存键
        }
        /// <summary>
        /// 根据当前栈顶的 Exclusive 面板状态，更新全局输入和暂停状态。
        /// </summary>
        private void ApplyStackState()
        {
            // 从栈顶往下找第一个 Exclusive（栈顶优先；
            // Popup 叠在 Exclusive 上时以最顶 Exclusive 为准，
            // 弹窗关闭后自动回到暂停状态——派生式的价值就在这）
            bool inputEnabled = true;
            float pauseScale = 1f;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Modal != PanelModal.Exclusive) continue;
                inputEnabled = false;
                pauseScale = _stack[i].Pause == PanelPause.World ? 0f : 1f;
                break;
            }
            DesiredInputEnabled = inputEnabled;   // 先落只读镜像（冒烟断言点）
            DesiredPauseScale  = pauseScale;
            var input = GameManager.Get<InputManager>();
            if (input != null) input.IsInputEnabled = inputEnabled;
            TimeManager.PauseScale = pauseScale;
        }

    }
}