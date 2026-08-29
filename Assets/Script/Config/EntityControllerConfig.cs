using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Config
{
    public class EntityControllerConfig : ScriptableObject
    {
        // 这里可以添加一些通用的配置属性
        public float moveSpeed = 5f; // 移速
        [Tooltip("掉头刹车率（m/s²）。反向输入时速度渐变过零的减速率；0 = 瞬时掉头（保留旧手感）")]
        public float deceleration = 40f;
    }
}