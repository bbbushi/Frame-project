using System;
using System.Collections.Generic;
using UnityEngine;
using Guide;
using System.Collections;
using Managers.UI;
using UI;
namespace Managers.Guide
{
    /// 引导系统宿主（MOMS 风格）。桥接核心引擎与本项目。
    /// 移植到其他项目时重写本类，核心层原样拷贝。
    
    ///<summary>
    /// 引导管理器:
    /// - 负责引导系统的初始化、更新、存档、事件转发
    /// - 负责引导实例的启动、停止、断点续传
    /// - 负责引导库的加载、引导进度的存储
    /// - 负责表现层的挂接（UGUI/NGUI/自定义）
    /// - 负责适配层谓词的注册（PredicateCondition.RegisterAll）
    /// - 负责扫描 autoStart 且未完成的引导 → 启动
    /// </summary>
    public class GuideManager : IManager, IUpdatable
    {
        public string Name => "GuideManager";
        // 依赖 InputManager：拓扑序保证同帧内先刷新输入缓存、后判定引导条件。
        // 此依赖不可删 —— 删了会出现按键晚一帧判定的隐性 bug（类注释写明）
        private static readonly List<Type> _dependencies = new()
        {
            typeof(InputManager),
            typeof(UIManager), // 依赖 UIManager 才能转发表现层事件
        };
        public List<Type> Dependencies => _dependencies;
        private static GuideManager Resolve => GameManager.Instance?.GetManager<GuideManager>();




        // ============================= 核心层组件 ================================        
        private GuideLibrary _library;
        private JsonProgressStore _store;
        private GuideProgressData _progress;
        private GuideEngine _engine;
        private IGuideView _view;
        //  ========================================================================




        //  =============================事件订阅=============================
        private void OnGuideStarted(GuideData guide)
        {
            _progress.runningGuideId = guide.guideId;       // 开跑即记录，崩溃也能断点续传
            _progress.runningStepIndex = 0;
            SaveProgress();
        }
        private void OnStepStarted(GuideStepContext ctx) => _view?.ShowStep(ctx);   //转发表现层
        private void OnStepCompleted(GuideStepContext ctx)
        {
            if(ctx == null) return;
            // 记「下一步」：已完成的这步不用重播
            _progress.runningGuideId = ctx.GuideId;
            _progress.runningStepIndex = ctx.StepIndex + 1;
            SaveProgress();             // 每步都存 —— 文件极小，崩溃后断点天然准确
        }
        private void OnGuideFinished(GuideData guide)
        {
            if(!_progress.completedGuideIds.Contains(guide.guideId)) _progress.completedGuideIds.Add(guide.guideId); // 完成记录 → autoStart 不再触发
            ClearRunning();
        }
        private void OnGuideAborted(GuideData guide) => ClearRunning();     // 中断 = 下次从头开始


        /// <summary>
        /// 清除当前运行的引导信息
        /// </summary>
        private void ClearRunning()
        {
            _view?.Hide(); // 隐藏表现层
            _progress.runningGuideId = "";
            _progress.runningStepIndex = 0;
            SaveProgress();
        }
        private void SaveProgress() => _store?.Save(_progress);
        //=====================================================================
        




        // ══════════ 静态门面（游戏侧只调这四个）══════════
        public static bool IsGuideCompleted(string guideId)
        {
            var m = Resolve;
            return m?._progress != null && m._progress.completedGuideIds.Contains(guideId);
        }
        public static void StartGuide(string guideId, bool resumeIfIncomplete = true) => Resolve?.StartGuideInstance(guideId, resumeIfIncomplete);
        public static void StopGuide(bool markCompleted = false) => Resolve?.StopGuideInstance(markCompleted);
        public static void ResetProgress()
        {
            if(Resolve == null) return;
            Resolve._engine?.Stop(false);       // 在跑的引导先停掉（会走 Aborted 清断点）
            Resolve._store?.Clear();            // 删存档文件
            Resolve._progress = new GuideProgressData();        // 内存进度归零
        }
        /// ══════════════════════════════════════════════════   
        

        //================================ 内部实现 =================================

        /// <summary>
        /// 启动一个引导实例:
        /// - 如果 resumeIfIncomplete 为 true，且存在未完成的引导，则从断点处继续
        /// - 如果 resumeIfIncomplete 为 false，则从头开始
        /// </summary>
        private void StartGuideInstance(string guideId, bool resumeIfIncomplete)
        {
            if(_engine == null || _library ==null) return;
            if (_engine.IsRunning)
            {
                Debug.LogWarning($"[Guide] 已有引导实例在跑，无法启动新引导 {guideId}");
                return; // 只跑一个引导实例
            } 
            var guide = _library.Find(guideId);
            if(guide == null)
            {
                Debug.LogWarning($"[Guide] 未找到引导 {guideId}");
                return;
            }
            if(IsGuideCompleted(guideId))
            {
                Debug.LogWarning($"[Guide] 引导 {guideId} 已完成，无法启动");
                return; // 已完成的不再触发
            }
            // 断点续传：存档里有这条引导的进行中记录 → 从记录的步骤恢复
            int startStepIndex = 0;
            if(resumeIfIncomplete 
            && _progress.runningGuideId == guideId
            && _progress.runningStepIndex < guide.steps.Count
            && _progress.runningStepIndex > 0)
            {
                startStepIndex = _progress.runningStepIndex;
                Debug.Log($"[Guide] 引导 {guideId} 断点续传，从步骤 {startStepIndex} 开始");
            }
            _engine.Start(guide, startStepIndex);
        }

        /// <summary>
        /// 停止当前引导实例：
        /// - markCompleted 为 true，则标记为已完成（下次不再触发 autoStart）
        /// - markCompleted 为 false，则标记为未完成（下次 autoStart 仍会触发）
        /// </summary>
        /// <param name="markCompleted"></param>
        /// <returns></returns>
        private void StopGuideInstance(bool markCompleted)
        {
            if (_engine == null) return;
            if (!_engine.IsRunning) return;                  // 没在跑，静默幂等
            _engine.Stop(markCompleted);
        }
        private IGuideView CreateUguiView()
        {
            var root = UIManager.GetLayerRoot(UILayer.Guide);
            if(root == null)
            {
                Debug.LogWarning("[Guide] 无法创建 UGUI 引导表现层：UIManager 未初始化或未建 Guide 层");
                return new NullGuideView();
            }
            var prefab = Resources.Load<GameObject>("data/ui/GuidePanel");
            if(prefab == null)
            {
                Debug.LogWarning("[Guide] 无法创建 UGUI 引导表现层：未找到 ui/GuidePanel.prefab");
                return new NullGuideView();
            }
            var go = GameObject.Instantiate(prefab, root);
            var view = go.GetComponent<IGuideView>();
            if(view == null)
            {
                Debug.LogError("[Guide] GuidePanel 根节点缺少 IGuideView 实现（应为 GuideViewUGUI），已降级 NullGuideView");
                GameObject.Destroy(go);     // 销毁残骸，防空横幅永久挡在 Guide 层
                return new NullGuideView();
            }
            go.SetActive(false); // 先隐藏，等引导开始时再 Show
            return view;
        }

        //================================ IManager 接口 =================================

        /// <summary>
        /// 初始化引导系统：
        /// 1. Resources.Load<GuideLibrary> → _library
        /// 2. JsonProgressStore.Load → _progress
        /// 3. GuideEngine 构造 → 订阅事件
        /// 4. NullGuideView 构造 → _view
        /// 5. 注册适配层谓词（PredicateCondition.RegisterAll）
        /// 6. 扫描 autoStart 且未完成的引导 → StartGuide
        /// 7. 哨兵日志："[Guide] 初始化完成，库中 N 条引导"（兼作 MOMS 扫描存在性检查）
        /// </summary>
        /// <returns></returns>
        public IEnumerator Initialize()
        {
            _library =  Resources.Load<GuideLibrary>("data/guide/GuideLibrary");
            if(_library == null) Debug.LogWarning("[Guide] 未找到 data/GuideLibrary.asset，引导系统空库运行");
            //    —— 找不到仅 LogWarning，降级为空库而非报死
            _store = new JsonProgressStore(); 
            _progress = _store.Load();

            _engine = new GuideEngine(); //订阅事件 → 转发 _view + 存档
            _engine.GuideStarted += OnGuideStarted;
            _engine.StepStarted += OnStepStarted;
            _engine.StepCompleted += OnStepCompleted;
            _engine.GuideFinished += OnGuideFinished;
            _engine.GuideAborted += OnGuideAborted;

            //表现层：有 GuidePanel 资产用 UGUI 版，缺资产/配置错降级 NullObject（冒烟测试不受影响）
            _view = CreateUguiView();

            // 5. 注册适配层谓词（PredicateCondition.RegisterAll）
            // 6. 扫描 autoStart 且未完成的引导 → StartGuide
            // 7. 哨兵日志："[Guide] 初始化完成，库中 N 条引导"（兼作 MOMS 扫描存在性检查）
            if(_library != null)
            {
                foreach(var guide in _library.guides)
                {
                    if(guide == null || !guide.autoStart) continue;
                    if(IsGuideCompleted(guide.guideId)) continue;
                    StartGuideInstance(guide.guideId, resumeIfIncomplete: true);
                }
            }
            Debug.Log($"[Guide] 初始化完成，库中 {_library?.guides.Count ?? 0} 条引导");
            yield break;
        }   
        public void OnUpdate(float deltaTime)
        {
            if(_engine == null) return;
            if(DialogueManager.IsAnyPlaying) return;// 对话期间暂停引导（防文案互顶/输入屏蔽空转）
            _engine.Tick(Time.unscaledDeltaTime);// timeScale=0 时 Auto 步骤仍推进
        }
        public void Deinitialize()
        {
            /* 退订事件、运行中引导存档 */
            if(_engine != null)
            {
                _engine.GuideStarted  -= OnGuideStarted;
                _engine.StepStarted   -= OnStepStarted;
                _engine.StepCompleted -= OnStepCompleted;
                _engine.GuideFinished -= OnGuideFinished;
                _engine.GuideAborted  -= OnGuideAborted;
            }
            PredicateCondition.Clear();
        }
        
    }
}