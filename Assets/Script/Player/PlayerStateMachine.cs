using System;
using UnityEngine;
using State_Player;

namespace Frame_Player
{
    /// <summary>
    /// 玩家状态机 — 管理状态切换。
    /// 轻量级 FSM，不直接依赖任何模块。
    /// </summary>
    public class PlayerStateMachine
    {
        public PlayerState CurrentState { get; private set; }
        public void ReEnter()
        {
            CurrentState?.Exit();
            CurrentState?.Enter();
        }

        public void Initialize(PlayerState startState)
        {
            if (startState == null)
            {
                Debug.LogError("[PlayerStateMachine] Initialize 收到 null 初始状态，状态机未初始化");
                return;
            }
            CurrentState = startState;
            CurrentState.Enter();
        }

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == newState) return;
            if (newState == null)
            {
                Debug.LogWarning("[PlayerStateMachine] ChangeState 收到 null 目标状态，已忽略");
                return;
            }

            // ── 构建子状态链（纯数据，不执行）──
            PlayerState firstState = newState;   // 最终要进入的状态（从后往前串联）

            // 2. newState 的进入子状态：enterState → 目标状态
            //    跳过「当前已处于该 enterState」的情况，防止子状态自引用死循环
            if (newState.enterState != null && newState.enterState != CurrentState)
            {
                newState.enterState.SetNextState(firstState);
                firstState = newState.enterState;
            }

            // 1. currentState 的离开子状态：exitState → (enterState → 目标状态)
            //    同理跳过自引用（防御性）；CurrentState 为 null（Initialize 之前）时跳过
            if (CurrentState != null && CurrentState.exitState != null && CurrentState.exitState != CurrentState)
            {
                CurrentState.exitState.SetNextState(firstState);
                firstState = CurrentState.exitState;
            }

            // ── 执行切换（只执行一次 Exit + Enter）──
            CurrentState?.Exit();
            CurrentState = firstState;
            CurrentState.Enter();
        }
        public void ChangeState(PlayerState newState, String AnimName)
        {
            if (CurrentState == newState) return;
            if (newState == null)
            {
                Debug.LogWarning("[PlayerStateMachine] ChangeState(动画重载) 收到 null 目标状态，已忽略");
                return;
            }
            // CurrentState 可能为 null（Initialize 之前），退化为取目标状态的 player
            Player player = CurrentState != null ? CurrentState.GetPlayer() : newState.GetPlayer();
            PlayerTranState tranState = new PlayerTranState(player, this ,AnimName , newState);
            CurrentState?.Exit();
            CurrentState = tranState;
            CurrentState.Enter();
        }
        
    }
}
