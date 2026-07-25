using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "NewPlayerControllerData", menuName = "Game/Player Controller Data")]
public class PlayerControllerData : ScriptableObject
{
    public float moveSpeed = 5f; // 移速
    public float jumpforce; // 跳跃强度
    public float dashforce;        // [废弃] 旧冲刺强度，保留向后兼容
    public float dashcooldown;     // [废弃] 旧冲刺冷却，保留向后兼容
    public Vector2[] attackMovement; // [废弃] 旧攻击位移，保留向后兼容
    public float thrustForce;     // 突刺位移力度
    public float thrustDamage;    // 突刺伤害
    public float thrustCooldown;  // 突刺冷却
    public float thrustDuration;  // 突刺持续时间
}