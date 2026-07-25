using System;
using UnityEngine;

namespace Frame.Player
{
    /// <summary>
    /// 战斗模块 — 管理攻击、连击计数、攻击位移。
    /// </summary>
    [Serializable]
    public class PlayerCombat : PlayerModule
    {
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float comboWindow = 0.5f;
        [SerializeField] private Vector2[] attackMovements;

        public float AttackDamage => attackDamage;
        public int ComboCount { get; private set; }
        public float LastAttackTime { get; private set; }
        public bool IsBusy { get; set; }

        /// <summary>加载战斗配置</summary>
        public void LoadConfig(PlayerCharacterData charCfg, PlayerControllerData ctrlCfg)
        {
            if (charCfg != null)
                attackDamage = charCfg.attackDamage;
            if (ctrlCfg != null)
                attackMovements = ctrlCfg.attackMovement ?? new Vector2[0];
        }

        /// <summary>执行一次连击，返回当前 combo 序号</summary>
        public void ExecuteCombo()
        {
            LastAttackTime = Time.time;
            Debug.Log($"Executing combo. Current LastAttackTime: {LastAttackTime}, Current Time: {Time.time}");
        }
    //     public int ExecuteCombo()
    //     {
    //         if (ComboCount >= 3 || Time.time >= LastAttackTime + comboWindow)
    //             ComboCount = 0;
    //         ComboCount++;
    //         LastAttackTime = Time.time;

    //         player.anim.SetInteger("ComboCounter", ComboCount);

    //         if (attackMovements != null && attackMovements.Length > 0)
    //         {
    //             int idx = Mathf.Clamp(ComboCount - 1, 0, attackMovements.Length - 1);
    //             float dir = player.Locomotion.FacingDirection;
    //             player.rb.velocity = new Vector2(
    //                 attackMovements[idx].x * dir,
    //                 attackMovements[idx].y);
    //         }

    //         return ComboCount;
    //     }

    //     /// <summary>获取指定连击的攻击位移</summary>
    //     public Vector2 GetAttackDisplacement(int comboIndex)
    //     {
    //         if (attackMovements == null || attackMovements.Length == 0)
    //             return Vector2.zero;
    //         int idx = Mathf.Clamp(comboIndex, 0, attackMovements.Length - 1);
    //         return attackMovements[idx];
    //     }
    }
}
