using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "NewPlayerCharacterData", menuName = "Game/Player Character Data")]
public class PlayerCharacterData : ScriptableObject
{
    public float maxHealth;             // 最大生命值
    public float attackDamage;          // 平A伤害数值
    
}
