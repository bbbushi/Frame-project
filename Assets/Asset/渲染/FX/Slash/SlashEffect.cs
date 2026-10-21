using UnityEngine;
using UnityEngine.Rendering;

namespace Effects
{
    /// <summary>
    /// 斩击弧光特效 — 程序化 shader（BABEL/FX/SlashArc），无贴图。
    /// Spawn 后自驱动 _Progress 0→1（划出弧光 → 消散），duration 秒后自销毁。
    /// </summary>
    public class SlashEffect : MonoBehaviour
    {
        private static Material s_material;
        private static Mesh s_quad;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _duration, _elapsed;

        /// <summary>在 position 生成一道斩击弧光，弧的朝向沿 dir；duration 秒后自销毁</summary>
        public static SlashEffect Spawn(Vector3 position, Vector2 dir, float size = 2.4f, float duration = 0.28f, Color? color = null)
        {
            var mat = GetMaterial();
            if (mat == null) return null;

            var go = new GameObject("SlashEffect");
            go.transform.position = position;
            // 弧在 shader 中居中朝 +x，整体旋转到冲刺方向
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = Vector3.one * size;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingLayerName = "BulletTimeTop";
            mr.sortingOrder = 300;

            var fx = go.AddComponent<SlashEffect>();
            fx._renderer = mr;
            fx._duration = Mathf.Max(0.05f, duration);
            fx._mpb = new MaterialPropertyBlock();
            if (color.HasValue) fx._mpb.SetColor(ColorId, color.Value);
            fx.Apply(0f);
            return fx;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = _elapsed / _duration;
            if (progress >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(progress);
        }

        private void Apply(float progress)
        {
            _mpb.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_mpb);
        }

        private static Material GetMaterial()
        {
            if (s_material == null) s_material = Resources.Load<Material>("FX/SlashArc");
            if (s_material == null) Debug.LogWarning("[SlashEffect] 未找到 Resources/FX/SlashArc 材质");
            return s_material;
        }

        private static Mesh GetQuad()
        {
            if (s_quad != null) return s_quad;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            s_quad = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return s_quad;
        }
    }
}
