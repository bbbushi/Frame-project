using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Effect
{
    /// <summary>
    /// 血液粒子 — 由 BloodParticleGenerator 生成。
    /// </summary>
    public class BloodParticle : MonoBehaviour
    {
        static bool generatorMissingWarned; // 整个会话只警告一次，避免大量粒子命中时刷屏

        public Sprite[] sprites;
        public Color startColor;
        public Color endColor;

        public Vector2 velocity;
        public float existTime = 0.75f;

        SpriteRenderer renderer;
        float t = 0;

        void Start()
        {
            renderer = GetComponent<SpriteRenderer>();
        }
        // Update is called once per frame
        void Update()
        {
            t += Time.deltaTime;

            //根据t选择对应的图片
            int spriteIndex = Mathf.Clamp((int)(t * sprites.Length / existTime), 0, sprites.Length - 1);
            renderer.sprite = sprites[spriteIndex];
            //根据t选择对应的颜色
            Color color = Color.Lerp(startColor, endColor, Mathf.Clamp01(t / existTime));
            renderer.color = color;

            //模拟重力，保持粒子始终朝向运动方向
            velocity -= new Vector2(0, 10 * Time.deltaTime);
            float angle = Mathf.Atan2(velocity.y, velocity.x);
            transform.position += (Vector3)velocity * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, 0, angle * 180 / Mathf.PI);

            //用射线检测是否碰撞到地形
            RaycastHit2D raycastHit = Physics2D.Raycast(transform.position, velocity.normalized, velocity.magnitude * (1.5f * Time.deltaTime),
                LayerMask.GetMask("Ground", "Wall", "Platform"));
            if (raycastHit)
            {
                // 生成器不在场景时跳过血液生成，但粒子仍必须自毁，避免残留
                if (BloodParticleGenerator.Instance != null)
                    BloodParticleGenerator.Instance.GenerateBloodOnWall(raycastHit.point, raycastHit.normal);
                else if (!generatorMissingWarned)
                {
                    generatorMissingWarned = true;
                    Debug.LogWarning("[BloodParticle] 场景中未找到 BloodParticleGenerator，跳过墙壁血液生成");
                }
                Destroy(gameObject);
            }

            //超时则自毁
            if (t >= existTime + 0.2f)
            {
                Destroy(gameObject);
            }
        }

    }
}
