using System.Collections.Generic;
using UnityEngine;
using enemy;
using Modules;

namespace PlayerSystem
{
    /// <summary>
    /// 子弹时间舞台 — 场景层实现"全局深黑 + 鼠标向扇形浅黑 + 对象高亮"：
    /// 一张程序生成的暗幕纹理（扇形区域内浅、区域外深）挂在玩家头顶最上层，
    /// 玩家/扇形内/已标记的敌人通过提升 sortingOrder 画在暗幕之上（原色高亮）。
    /// 由 PlayerBulletTime 进入子弹时间时创建、退出时销毁（沿用旧 CreateAimArc 生命周期模式）。
    /// </summary>
    public class BulletTimeStage : MonoBehaviour
    {
        private const int LiftOrder = 100;          // 高亮提升量（保留对象间相对排序）
        private const int VeilOrder = 50;           // 暗幕自身 order（同层内低于高亮对象）
        private const string TopLayerName = "BulletTimeTop";
        private const int TexSize = 512;

        [Header("扇形（Inspector 可调）")]
        public float arcHalfAngleDeg = 30f;
        public float arcRadius = 10f;
        [Range(0f, 1f)] public float darkAlpha = 0.95f;   // 扇形外暗度
        [Range(0f, 1f)] public float arcAlpha = 0.48f;    // 扇形内暗度（浅黑）

        private readonly HashSet<Enemy> _lifted = new();
        // 高亮 = 切到 BulletTimeTop 层（跨层时 sortingOrder 无效），字典记录原层与 order 用于恢复
        private readonly Dictionary<SpriteRenderer, (string layer, int order)> _liftedRenderers = new();
        private SpriteRenderer _veil;
        private PlayerBulletTime _owner;
        private Transform _player;
        private Camera _cam;
        private Vector2 _aimDir = Vector2.up;

        /// <summary>工厂：由 PlayerBulletTime 在进入子弹时间时调用</summary>
        public static BulletTimeStage Create(Player player, PlayerBulletTime owner)
        {
            // 不挂玩家子物体：玩家转身是 localScale.x = -1 翻转，挂子物体会把扇形方向镜像反；
            // 位置/旋转由 LateUpdate 每帧自管，销毁走 PlayerBulletTime.Exit + Player.OnDestroy 兜底
            var go = new GameObject("BulletTimeStage");
            var stage = go.AddComponent<BulletTimeStage>();
            stage._owner = owner;
            stage._player = player.transform;
            stage._cam = Camera.main;
            stage.BuildVeil();
            if (player.spriteRenderer != null)
                stage.Lift(player.spriteRenderer);   // 玩家始终高亮
            return stage;
        }

        /// <summary>生成暗幕纹理与 SpriteRenderer（尺寸覆盖屏幕对角 + 扇形余量）</summary>
        private void BuildVeil()
        {
            float os = _cam != null ? _cam.orthographicSize : 5f;
            float aspect = _cam != null ? _cam.aspect : 1.77f;
            float screenW = 2f * os * aspect, screenH = 2f * os;
            float worldSize = Mathf.Sqrt(screenW * screenW + screenH * screenH) + 2f * arcRadius;

            var tex = new Texture2D(TexSize, TexSize, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[TexSize * TexSize];
            float halfRad = arcHalfAngleDeg * Mathf.Deg2Rad;
            float c = TexSize * 0.5f;
            // 扇形半径（像素）与柔边：弧缘约 6% 半径渐变
            float rPx = arcRadius / worldSize * TexSize;
            float softPx = Mathf.Max(rPx * 0.06f, 2f);
            float angSoft = 3f * Mathf.Deg2Rad;

            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float dx = x - c, dy = y - c;               // 纹理 +y 向上 = 扇形朝向
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 距离：扇形内浅、外深，径向柔边
                float radial = Mathf.Clamp01((rPx + softPx - dist) / (2f * softPx));
                // 角度：与 +y 夹角 ≤ 半角，角度柔边约 3°
                float ang = Mathf.Atan2(dx, dy);
                float angular = Mathf.Clamp01((halfRad - Mathf.Abs(ang)) / angSoft);
                float inArc = Mathf.Min(radial, angular);
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Lerp(darkAlpha, arcAlpha, inArc));
                pixels[y * TexSize + x] = new Color32(0, 0, 0, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize),
                new Vector2(0.5f, 0.5f), TexSize / worldSize);
            sprite.name = "BulletTimeVeil";

            _veil = gameObject.AddComponent<SpriteRenderer>();
            _veil.sprite = sprite;
            _veil.sortingLayerName = TopLayerName;
            _veil.sortingOrder = VeilOrder;
        }

        private void LateUpdate()
        {
            // Play 中改代码触发 domain reload 后，本对象的非序列化引用（_player/_owner）会丢失，
            // 留下显示着暗幕却不再工作的残骸 —— 自毁，由下一次 EnterBulletTime 重建
            if (_player == null || _owner == null)
            {
                Destroy(gameObject);
                return;
            }

            // 跟随玩家 + 旋转指向鼠标（纹理扇形朝 +y）
            transform.position = _player.position;
            if (_cam != null)
            {
                Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 toMouse = (Vector2)mouseWorld - (Vector2)_player.position;
                if (toMouse.sqrMagnitude > 1e-6f) _aimDir = toMouse.normalized;

                _owner?.RefreshChains(mouseWorld);   // 锁链：瞄准链尾随鼠标、标记链跟随对象位置
            }
            float angle = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            RefreshEnemyHighlights();
        }

        /// <summary>
        /// 敌人切序：在扇形内 → 切到 BulletTimeTop 层（原色高亮）；
        /// 离开范围（含已标记的）→ 标记与高亮一并清除；
        /// 滞回防边缘抖动（未提升按 98% 半径判定进入，已提升按 102% 判定仍在）
        /// </summary>
        private void RefreshEnemyHighlights()
        {
            _owner?.PruneDeadMarks();   // 被普通攻击打死/回池的已标记敌人：释放名额
            var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            HashSet<Enemy> seen = null;
            foreach (var e in enemies)
            {
                if (e == null || e.spriteRenderer == null) continue;
                seen ??= new HashSet<Enemy>();
                seen.Add(e);
                bool wasLifted = _lifted.Contains(e);
                float scale = wasLifted ? 1.02f : 0.98f;
                bool marked = _owner != null && _owner.IsMarked(e.transform);

                // 已标记：只看距离不看角度（鼠标转向去标下一个不会掉标记），跑出半径才清标记+编号；
                // 未标记：按扇形（角度+距离）高亮
                bool shouldLift;
                if (marked)
                {
                    shouldLift = InRange(e.transform.position, scale);
                    if (!shouldLift) _owner.Unmark(e.transform);
                }
                else
                {
                    shouldLift = InArc(e.transform.position, scale);
                }

                if (shouldLift && !wasLifted)
                {
                    Lift(e.spriteRenderer);
                    _lifted.Add(e);
                }
                else if (!shouldLift && wasLifted)
                {
                    Drop(e.spriteRenderer);
                    _lifted.Remove(e);
                }
            }

            // 池化/销毁清理：不再被扫描到的已提升敌人（回池或销毁），恢复其渲染排序
            _lifted.RemoveWhere(e2 =>
            {
                if (e2 == null) return true;
                if (seen != null && seen.Contains(e2)) return false;
                if (e2.spriteRenderer != null) Drop(e2.spriteRenderer);
                return true;
            });
        }

        private void Lift(SpriteRenderer sr)
        {
            if (sr == null || _liftedRenderers.ContainsKey(sr)) return;
            _liftedRenderers[sr] = (sr.sortingLayerName, sr.sortingOrder);
            sr.sortingLayerName = TopLayerName;
            sr.sortingOrder = LiftOrder + sr.sortingOrder;   // 保留对象间相对排序
        }

        private void Drop(SpriteRenderer sr)
        {
            if (sr == null || !_liftedRenderers.TryGetValue(sr, out var orig)) return;
            sr.sortingLayerName = orig.layer;
            sr.sortingOrder = orig.order;
            _liftedRenderers.Remove(sr);
        }

        /// <summary>点是否在瞄准扇形内（scale：1 = 标准判定，1.02/0.98 = 滞回判定）</summary>
        public bool InArc(Vector2 pos, float scale = 1f)
        {
            Vector2 to = pos - (Vector2)transform.position;
            if (to.magnitude > arcRadius * scale) return false;
            if (to.sqrMagnitude < 1e-8f) return true;
            return Vector2.Angle(_aimDir, to.normalized) <= arcHalfAngleDeg * scale;
        }

        /// <summary>点是否在扇形半径内（忽略角度；已标记敌人的保留判定）</summary>
        public bool InRange(Vector2 pos, float scale = 1f)
            => (pos - (Vector2)transform.position).magnitude <= arcRadius * scale;

        public Vector2 AimDir => _aimDir;

        /// <summary>暗幕退役：恢复全部提升对象（含玩家）的原层原序并清空记录，供处决链开始时调用</summary>
        public void RestoreAll()
        {
            foreach (var sr in new List<SpriteRenderer>(_liftedRenderers.Keys))
                Drop(sr);
            _lifted.Clear();
        }

        private void OnDestroy()
        {
            // 玩家高亮恢复（引用可能已随同销毁，判空）
            if (_player != null && _player.TryGetComponent<Entity>(out var entity)
                && entity.spriteRenderer != null)
                Drop(entity.spriteRenderer);
        }
    }
}
