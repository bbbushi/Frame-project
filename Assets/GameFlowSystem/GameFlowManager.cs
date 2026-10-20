using System;
using System.Collections.Generic;
using UnityEngine;
using Managers.UI;
using UnityEngine.SceneManagement;
using System.Collections;
/// <summary>
/// 游戏流程状态: 主菜单、加载、进行中、游戏结束
/// </summary>
namespace Managers.GameFlow   // ⚠ MOMS 反射扫描白名单只认 Managers/Managers.*——命名空间必须在白名单内才会被注册（写错=永不初始化且无报错）
{
    public enum FlowState { MainMenu, Loading, Playing, GameOver }
    public class GameFlowManager : IManager
    {
        public string Name => "GameFlowManager";
        private static readonly List<Type> _dependencies = new() { typeof(UIManager) };  // 过渡要 Show 面板
        public List<Type> Dependencies => _dependencies;

        /// <summary>
        /// 流程状态事实源（冒烟断言点；Loading 期 UI 据此跳过 Esc）
        /// </summary>
        public FlowState State { get; private set; } = FlowState.MainMenu;
        private SceneRegistry _registry;
        private string _currentScene;
        private static GameFlowManager Resolve => GameManager.Instance?.GetManager<GameFlowManager>();

        //========================== IManager ==========================

        public IEnumerator Initialize()
        {
            _registry = Resources.Load<SceneRegistry>("data/Scene/SceneRegistry");
            if (_registry == null) Debug.LogWarning("[Flow] 缺 SceneRegistry，场景切换不可用（用菜单生成/手工建）");
            _currentScene = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += OnSceneLoaded;    // ⚠ Deinitialize 必须对称退订
            yield break;
        }
        public void Deinitialize() => SceneManager.sceneLoaded -= OnSceneLoaded;

        //========================== 静态门面（全项目唯一合法入口；内部各自校验当前状态合法才动作） ==========================

        /// <summary>开始一个新关卡（须在 SceneRegistry 里，拼写错误在入口报而不是等 LoadSceneAsync 返 null）</summary>
        public static void StartLevel(string sceneId)
        {
            var m = Resolve; if (m == null) return;               // 修复：原 if(Resolve == null) 判反了——非 null 时整体静默不做事，null 时反而进去 NRE
            if (m.FindEntry(sceneId) == null)
            {
                Debug.LogWarning($"[Flow] StartLevel({sceneId}) 无效：SceneRegistry 没有这个场景");
                return;
            }
            if (sceneId == m._currentScene)
            {
                // §四转换表：Playing→同关卡重复 Start 拒绝——重开语义由 ReloadCurrent 承担
                Debug.LogWarning("[Flow] 目标即当前场景，重开请用 ReloadCurrent()");
                return;
            }
            m.TransitionTo(sceneId);
        }

        /// <summary>切换到主菜单（按注册表 isMainMenu 标记找，不硬编码场景名）</summary>
        public static void ToMainMenu()
        {
            var m = Resolve; if (m == null) return;
            var main = m.FindMainMenu();
            if (main == null) { Debug.LogWarning("[Flow] SceneRegistry 里没有 isMainMenu=true 的条目"); return; }
            m.TransitionTo(main.sceneId);
        }

        /// <summary>重新加载当前场景（死亡/重开时用）；主菜单没有"重开"语义，拒绝</summary>
        public static void ReloadCurrent()
        {
            var m = Resolve; if (m == null) return;
            if (m.State != FlowState.Playing && m.State != FlowState.GameOver) return;
            m.TransitionTo(m._currentScene);
        }

        /// <summary>游戏结束：纯状态位（非场景！DeathPanel 的展示归死亡表现挂账，方案 §九.3）。
        /// 修复：原实现 TransitionTo("GameOver")——那场景不存在，且转场后 Routine 会把 State 落回 Playing，
        /// GameOver 状态根本活不过一次转场</summary>
        public static void GameOver()
        {
            var m = Resolve; if (m == null) return;
            if (m.State != FlowState.Playing) return;
            m.State = FlowState.GameOver;
        }

        public static FlowState Current => Resolve?.State ?? FlowState.MainMenu;   // UI 护栏读这个

        //========================== 内部实现 ==========================

        /// <summary>转场唯一入口：Loading 中拒绝并发转场（两条 Routine 同时 LoadSceneAsync = 乱套）</summary>
        private void TransitionTo(string sceneId)
        {
            if (State == FlowState.Loading)
            {
                Debug.LogWarning($"[Flow] 转场进行中，拒绝并发转场（{sceneId}）");
                return;
            }
            GameManager.Instance.StartCoroutine(Routine(sceneId));
        }

        /// <summary>
        /// 场景切换协程：显示 LoadingPanel（Exclusive+World，四期派生自动锁输入/停世界），
        /// 异步加载场景，LoadingPanel 显示进度，场景加载完成后落点校准（sceneLoaded 里）
        /// </summary>
        private IEnumerator Routine(string sceneId)
        {
            var prevState = State;
            State = FlowState.Loading;
            UIManager.Show("LoadingPanel");                  // 修复：原 Show("Loading")/Hide("LoadingPanel") 键名不一致——Hide 永远找不到，黑幕关不掉=输入锁死
            var op = SceneManager.LoadSceneAsync(sceneId);
            if (op == null)
            {
                // 场景不在 Build Settings（Build 列表手工维护——"注册表有但 Build 没有"是这个错的典型形态，方案 §九.8）
                Debug.LogError($"[Flow] LoadSceneAsync({sceneId}) 返回 null：场景多半不在 Build Settings 列表");
                UIManager.Hide("LoadingPanel");
                State = prevState;                           // 回退转场前状态，不留 Loading 僵尸态（卡死所有后续转场）
                yield break;
            }
            while (!op.isDone)                               // allowSceneActivation=true 时 isDone 才是真完成（progress 会停在 0.9）
            {
                LoadingPanel.ReportProgress(op.progress);    // 静态转发，面板没开就忽略
                yield return null;
            }
            // sceneLoaded 已触发：落点校准完成；此时新场景 Awake 全部跑完
            UIManager.Hide("LoadingPanel");
            State = IsMainMenu(_currentScene) ? FlowState.MainMenu : FlowState.Playing;   // Hide 之后落定（Esc 护栏靠 Loading 期成立）
        }

        /// <summary>
        /// 场景加载完成时调用
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _currentScene = scene.name;
            FlowSpawn.Calibrate();   //落点校准（静态工具：找 SpawnPoint → 挪玩家）
        }

        // ── 注册表查询 ──
        private SceneRegistry.SceneEntry FindEntry(string sceneId)
            => _registry?.scenes?.Find(e => e != null && e.sceneId == sceneId);
        private SceneRegistry.SceneEntry FindMainMenu()
            => _registry?.scenes?.Find(e => e != null && e.isMainMenu);
        private bool IsMainMenu(string sceneId)
            => _registry?.scenes?.Exists(e => e != null && e.sceneId == sceneId && e.isMainMenu) ?? false;
    }
}
