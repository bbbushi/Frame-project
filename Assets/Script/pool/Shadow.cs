using System.Collections;
using System.Collections.Generic;
using PlayerSystem;
using UnityEngine;

public class Shadow : MonoBehaviour
{
    private Transform PlayerTrans;
    private SpriteRenderer thissprite;
    private SpriteRenderer playerSprite;
    private Color shadowColor;
    [Header("时间控制参数")] public float activeTime = 0.5f; // 影子存在的时间
    public float activeStart; // 开始显示时间
    [Header("不透明度设置")] private float alpha = 1f; // 影子初始不透明度 
    public float alphaset;
    public float alphamultiplier = 2f; // 不透明度衰减速度

    private void OnEnable()
    {
        // 当影子被激活时，初始化其状态
        PlayerTrans = Player.Instance.transform; // 获取玩家的 Transform
        thissprite = GetComponent<SpriteRenderer>();
        playerSprite = Player.Instance.GetComponentInChildren<SpriteRenderer>();
        thissprite.sprite = playerSprite.sprite;
        transform.position = PlayerTrans.position;
        transform.localScale = PlayerTrans.localScale;
        transform.rotation = PlayerTrans.rotation;
        alpha = alphaset;
        activeStart = Time.time;
    }

    void Update()
    {
        // 逐渐降低影子的透明度
        alpha -= alphamultiplier * Time.deltaTime;

        thissprite.color = new Color(0.6f, 1f, 1f, alpha); // 设置影子的颜色和透明度
        if (Time.time - activeStart >= activeTime)
        {
            ShadowPool.Instance.ReturnPool(this.gameObject); // 影子存在时间结束，返回对象池
        }
    }
}