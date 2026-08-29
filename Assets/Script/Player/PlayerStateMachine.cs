using System;
using System.Collections.Generic;
using UnityEngine;
using State_Player;

namespace PlayerSystem
{
    /// <summary>移动层状态 — 回答"人在哪"（地面/空中），由物理事实驱动，持续运行。
    /// Idle/Move 是纯动画差异（xInput 直写 bool），不构成独立状态</summary>
    public enum MotionStateId { None,Ground, Air }

    /// <summary>动作层状态 — 回答"人在干什么"，持锁期间抑制移动层输入</summary>
    public enum ActionStateId { None, Attack, Thrust, Execution, Death }

    /// <summary>
    /// 玩家状态机 — 泛型 FSM，motion / action 两层共用。
    /// 转换统一走 Request()：守卫裁决 → Exit → Enter。
    /// 守卫三层 API（均为拒绝式，未声明拒绝的一律允许）：
    ///   ForbidAllFrom(id)      — 终态声明（如 Death），拒绝一切转出
    ///   ForbidAllBetween(...)  — 互斥组声明（如各动作状态），组内互转全拒
    ///   Forbid(from, to)       — 单点例外
    /// 转换进行中（Exit/Enter 内）拒绝并发的 Request，杜绝嵌套切状态。
    /// Transitioned 事件在转换完成后触发，供宿主做跨机联动（motion/action 互斥）。
    /// 打开 EnableTransitionLog 可输出每次转换与拒绝原因。
    /// </summary>
    public class PlayerStateMachine<TId> where TId : struct
    {
        readonly string machineName;
        readonly Dictionary<TId, PlayerState> states = new Dictionary<TId, PlayerState>();
        readonly HashSet<(TId from, TId to)> forbidden = new HashSet<(TId, TId)>();
        readonly HashSet<TId> terminalStates = new HashSet<TId>();
        readonly List<TId[]> mutexGroups = new List<TId[]>();
        readonly TId idleId;

        bool transitioning;

        public PlayerState Current { get; private set; }
        public TId CurrentId { get; private set; }

        /// <summary>当前处于"忙碌"状态（非空闲锚点即忙碌；空闲锚点 = 构造第二参数，两层均为 None）</summary>
        public bool IsBusy => !EqualityComparer<TId>.Default.Equals(CurrentId, idleId);

        /// <summary>真实转换完成后触发（from, to）；自转换与守卫拒绝不触发。
        /// 在 transitioning 复位之后触发，订阅者可安全发起后续转换（跨机联动用）</summary>
        public event Action<TId, TId> Transitioned;

        // public bool EnableTransitionLog { get; set; }

        public PlayerStateMachine(string machineName, TId idleId = default)
        {
            this.machineName = machineName;
            this.idleId = idleId;
        }

        public void Register(TId id, PlayerState state)
        {
            if (state == null)
            {
                Debug.LogError($"[{machineName}] Register 收到 null 状态（{id}），已忽略");
                return;
            }
            states[id] = state;
        }

        /// <summary>声明非法转换（单点例外用；结构性规则请用下面两个 API）</summary>
        public void Forbid(TId from, TId to) => forbidden.Add((from, to));

        /// <summary>终态声明：from 不允许转出到任何状态（后续注册的新状态也自动被覆盖）</summary>
        public void ForbidAllFrom(TId from) => terminalStates.Add(from);

        /// <summary>互斥组声明：组内任意两状态互转均被拒绝；组员与组外状态（如 None、Death）互转不受影响</summary>
        public void ForbidAllBetween(params TId[] group)
        {
            if (group == null || group.Length < 2) return;
            mutexGroups.Add(group);
        }

        public void Initialize(TId start)
        {
            if (!states.TryGetValue(start, out PlayerState state))
            {
                Debug.LogError($"[{machineName}] Initialize 找不到起始状态 {start}，状态机未初始化");
                return;
            }
            Current = state;
            CurrentId = start;
            // if (EnableTransitionLog) Debug.Log($"[{machineName}] Initialize → {start}");
            Current.Enter();
        }

        /// <summary>
        /// 请求切换状态。返回是否切换成功（自转换静默返回 true，守卫拒绝返回 false）。
        /// 一切状态切换必须走这里，不要直接改 Current。
        /// </summary>
        public bool ChangeState(TId id)
        {
            
            if (transitioning)
            {
                Debug.LogWarning($"[{machineName}] 转换进行中拒绝了并发的 Request({id}) —— 状态的 Exit/Enter 内不允许再切状态");
                return false;
            }
            
            if (EqualityComparer<TId>.Default.Equals(CurrentId, id)) return true;
            if (!states.TryGetValue(id, out PlayerState next))
            {
                Debug.LogError($"[{machineName}] Request 的目标状态 {id} 未注册，已忽略");
                return false;
            }
            string denyReason = DenyReason(CurrentId, id);
            if (denyReason != null)
            {
                // if (EnableTransitionLog)
                //    Debug.LogWarning($"[{machineName}] 拒绝转换 {CurrentId} → {id}（{denyReason}）");
                return false;
            }

            // if (EnableTransitionLog) Debug.Log($"[{machineName}] {CurrentId} → {id}");

            PlayerState previous = Current;
            TId previousId = CurrentId;
            transitioning = true;
            try
            {
                previous?.Exit();
                Current = next;
                CurrentId = id;
                Current.Enter();
                Transitioned?.Invoke(previousId, id);
            }
            finally
            {
                transitioning = false;
            }
            // 转换后事件：transitioning 已复位，订阅者可安全发起后续转换（跨机联动）
            
            return true;
        }

        public void Update() => Current?.Update();

        /// <summary>守卫裁决：返回拒绝原因（null 表示允许）。结构规则（终态/互斥组）优先于逐对规则。</summary>
        string DenyReason(TId from, TId to)
        {
            if (terminalStates.Contains(from))
                return $"{from} 是终态";
            foreach (TId[] group in mutexGroups)
                if (ContainsId(group, from) && ContainsId(group, to))
                    return $"{from} 与 {to} 同属互斥组";
            if (forbidden.Contains((from, to)))
                return "逐对守卫规则";
            return null;
        }

        static bool ContainsId(TId[] array, TId value)
        {
            foreach (TId item in array)
                if (EqualityComparer<TId>.Default.Equals(item, value))
                    return true;
            return false;
        }
    }
}
