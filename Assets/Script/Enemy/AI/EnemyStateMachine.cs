using System;
using System.Collections.Generic;

namespace enemy.ai
{
    /// <summary>
    /// 敌人专用状态机：注册表 + 终态守卫 + 防重入。
    /// 刻意不复用玩家 PlayerStateMachine——玩家的三层守卫/互斥组这里用不上，
    /// 40 行从头读到尾优于跨系统耦合（可读性 &gt; DRY）。
    /// </summary>
    public class EnemyStateMachine
    {
        readonly Dictionary<EnemyStateId, IEnemyState> states = new Dictionary<EnemyStateId, IEnemyState>();
        readonly List<EnemyStateId> terminalStates = new List<EnemyStateId>();   // 只会放 Death
        bool transitioning;   // Exit/Enter 期间拒绝并发切换（Exit 里再切状态是常见翻车点）

        public EnemyStateId CurrentId { get; private set; }
        public IEnemyState Current { get; private set; }
        /// <summary>(from, to)，转换完成后触发（调试日志挂这里）</summary>
        public event Action<EnemyStateId, EnemyStateId> Transitioned;

        public void Register(EnemyStateId id, IEnemyState state) => states[id] = state;
        public void MarkTerminal(EnemyStateId id) => terminalStates.Add(id);

        /// <summary>直接置位并进入。初始化与池化重置用——Death 是终态转不出，重置只能走这</summary>
        public void Initialize(EnemyStateId start)
        {
            transitioning = false;
            CurrentId = start;
            Current = states[start];
            Current.Enter();
        }

        /// <summary>请求切换。自转静默 true；未注册/终态/转换中 → false（调用方走自己的 fallback）</summary>
        public bool ChangeState(EnemyStateId id)
        {
            if (transitioning) return false;
            if (id == CurrentId) return true;
            if (!states.ContainsKey(id) || terminalStates.Contains(CurrentId)) return false;

            var from = CurrentId;
            transitioning = true;
            try { Current.Exit(); CurrentId = id; Current = states[id]; Current.Enter(); }
            finally { transitioning = false; }
            Transitioned?.Invoke(from, id);
            return true;
        }

        /// <summary>只推进当前积木，不做任何决策——转换判定是积木自己的事</summary>
        public void Tick(float dt) => Current?.Tick(dt);
    }
}
