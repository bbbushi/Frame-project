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

        length = GetComponent<SpriteRenderer>().bounds.size.x;
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
