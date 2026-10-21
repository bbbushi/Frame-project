using UnityEngine;
using UnityEngine.Rendering;

namespace Effects
{
    /// <summary>
    /// 锁链（纯视觉，无物理）— 程序化链环 shader，连接两个端点。
    /// SetEnds 每帧由调用方更新头尾（瞄准链跟鼠标，标记链跟玩家/敌人位置）；
    /// Spawn 后自驱动 _Progress 从头向尾生长一次。
    /// </summary>
    public class ChainLink : MonoBehaviour
    {
        private static Mesh s_quad;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int WorldLenId = Shader.PropertyToID("_WorldLen");
        private static readonly int LinkLenId = Shader.PropertyToID("_LinkLen");

        private MeshRenderer _renderer;
        private Material _mat;   // 每链材质实例——不用 MPB（SRP Batcher 下 MPB 属性可能不生效）
        private readonly float _growTime = 0.22f;
        private float _elapsed;
        private bool _grown;

        public Vector3 Head => transform.position;

        /// <summary>生成锁链：head = 链头（玩家/上一个标记点），tail = 链尾（新标记的敌人）</summary>
        public static ChainLink Spawn(Material material, Vector3 head, Vector3 tail,
            float thickness = 0.25f, float linkLength = 0.5f)
        {
            if (material == null)
            {
                Debug.LogWarning("[ChainLink] 未指定材质：请在 Player → PlayerModuleControlComponent → BulletTime 拖入 Chain 材质");
                return null;
            }

            var go = new GameObject("ChainLink");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetQuad();
            var mr = go.AddComponent<MeshRenderer>();
            var link = go.AddComponent<ChainLink>();
            link._renderer = mr;
            link._mat = new Material(material);
            mr.sharedMaterial = link._mat;
            link._mat.SetFloat(LinkLenId, linkLength);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingLayerName = "BulletTimeTop";
            mr.sortingOrder = 280;
            link.SetEnds(head, tail);
            return link;
        }

        /// <summary>更新头尾端点（长度/朝向随之变化，链环密度按世界长度平铺）</summary>
        public void SetEnds(Vector3 head, Vector3 tail)
        {
            Vector2 d = (Vector2)tail - (Vector2)head;
            float len = Mathf.Max(d.magnitude, 0.01f);
            // Quad 锚点在中心：位置取两端中点，缩放取两端距离，才能精确覆盖 head → tail
            transform.position = ((Vector2)head + (Vector2)tail) * 0.5f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(len, Mathf.Max(transform.localScale.y, 0.05f), 1f);
            _mat.SetFloat(WorldLenId, len);
        }

        private void Update()
        {
            if (_grown) return;
            _elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(_elapsed / _growTime);
            _mat.SetFloat(ProgressId, p);
            if (p >= 1f) _grown = true;
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
