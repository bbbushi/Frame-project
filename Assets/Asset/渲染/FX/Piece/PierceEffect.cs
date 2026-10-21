using UnityEngine;
using UnityEngine.Rendering;

namespace Effects
{
    /// <summary>
    /// 穿刺光针特效 — 程序化 shader（BABEL/FX/Pierce），无贴图。
    /// 沿冲刺方向的一道菱形光束：Spawn 后自驱动 _Progress（刺入拉伸 → 保持 → 消散），duration 秒后自销毁。
    /// 默认尺寸长 3.6 × 宽 1.4 世界单位，足以罩住角色。
    /// </summary>
    public class PierceEffect : MonoBehaviour
    {
        private static Mesh s_quad;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MeshRenderer _renderer;
        private Material _mat;   // 每实例材质——不用 MPB（SRP Batcher 下 MPB 属性可能不生效）
        private float _duration, _elapsed;

        /// <summary>
        /// 在 position 生成一道穿刺光针，沿 dir 方向；duration 秒后自销毁。
        /// material 由调用方的 Inspector 引用传入（BABEL/FX/Pierce 材质），不按路径查找资源。
        /// </summary>
        public static PierceEffect Spawn(Material material, Vector3 position, Vector2 dir,
            float length = 3.6f, float width = 1.4f, float duration = 0.32f, Color? color = null)
        {
            if (material == null)
            {
                Debug.LogWarning("[PierceEffect] 未指定材质：请在 Player → PlayerModuleControlComponent → BulletTime 拖入 Pierce 材质");
                return null;
            }

            var go = new GameObject("PierceEffect");
            go.transform.position = position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;   // 光束沿局部 +x，旋转到冲刺方向
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(length, width, 1f);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuad();
            var mr = go.AddComponent<MeshRenderer>();
            var fx = go.AddComponent<PierceEffect>();
            fx._renderer = mr;
            fx._mat = new Material(material);
            mr.sharedMaterial = fx._mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingLayerName = "BulletTimeTop";
            mr.sortingOrder = 300;

            fx._duration = Mathf.Max(0.05f, duration);
            if (color.HasValue) fx._mat.SetColor(ColorId, color.Value);
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
            _mat.SetFloat(ProgressId, progress);
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
