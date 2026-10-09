using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Config
{
    public class EntityCharacterConfig : ScriptableObject
    {
        // 这里可以添加一些通用的配置属性
        public float maxHealth;             // 最大生命值
        public float attackDamage;          // 平A伤害数值
        public float defense;
        public bool immortal;             // 不倒翁（沙包）：血量下限锁 1，打死不掉，专测打击手感
    }
}