using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private GameObject canmara;
    [SerializeField] private float parallaxEffectx;

    private float xposition;
    private float length;
    void Start()
    {
        canmara = GameObject.Find("Main Camera");

        // 背景-天气这类无 SpriteRenderer 的层（纯粒子/装饰）：视差对它无意义，停用组件而不是每帧刷异常
        var sr = GetComponent<SpriteRenderer>();
        if(sr == null)
        {
            Debug.LogWarning($"[视差] {gameObject.name} 缺 SpriteRenderer，视差已停用");
            enabled = false;
            return;
        }
        length = sr.bounds.size.x;
        xposition = transform.position.x; 
    }
    void Update()
    {
        float tempx = canmara.transform.position.x * (1 - parallaxEffectx);
        float distancex = canmara.transform.position.x * parallaxEffectx;
        transform.position = new Vector3(xposition + tempx , transform.position.y);
        if(distancex > xposition + length)
        {
            xposition += length;
        }
        else if(distancex < xposition - length)
        {
            xposition -= length;
        }
        
        
    }
}
