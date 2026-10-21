using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Managers;
using PlayerSystem;

namespace Effects
{
    /// <summary>
    /// 子弹时间暗角 RendererFeature — 全屏径向渐变：玩家附近保持原亮度，向外平滑压暗偏蓝。
    /// 强度直接映射 TimeManager.SlowScale 实时值（1→bulletTimeScale 的平滑过程即暗幕淡入/淡出），
    /// 不自持任何状态：暂停、死亡重载、玩家销毁时随 SlowScale 恢复自动消退。
    /// 强度为 0 时跳过绘制（MainMenu / 非子弹时间零开销）；只对 Game 相机生效。
    /// 挂载位置：RendererData（2D雾气.asset）的 m_RendererFeatures。
    /// </summary>
    public class BulletTimeVignetteFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material material;
            [Range(0f, 1f)] public float radius = 0.35f;       // 玩家亮区半径（视口空间，y 轴尺度）
            [Range(0f, 1f)] public float softness = 0.45f;     // 亮区外渐变带宽
            [Range(0f, 1f)] public float maxIntensity = 0.65f; // 完全进入子弹时间时的压暗强度
            public Color dimColor = new Color(0.30f, 0.32f, 0.45f); // 暗区色调倍率（偏蓝）

            [Header("瞄准扇形（全局变暗之上，扇形内浅暗稍亮）")]
            [Range(5f, 180f)] public float arcHalfAngleDeg = 60f;   // 扇形半角（全角 = 2 倍，旧实现全角 120）
            public float arcRadius = 10f;                           // 扇形半径（世界单位，对齐旧 markRange）
            [Range(0f, 0.3f)] public float arcEdgeSoftness = 0.06f; // 弧缘柔边（视口单位）
            [Range(0f, 1f)] public float arcReveal = 0.4f;          // 扇形内暗角减弱比例（0.4 = 扇形内保留六成暗角，浅暗）
            [Range(1f, 1.5f)] public float arcBoost = 1.05f;        // 扇形内轻微提亮（1 = 只靠暗角对比）
        }

        public Settings settings = new Settings();
        private VignettePass _pass;

        public override void Create()
        {
            _pass = new VignettePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null) return;
            // SceneView / 预览相机不画，避免编辑器画面被调参效果污染
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }

        private class VignettePass : ScriptableRenderPass
        {
            private static readonly int IntensityId = Shader.PropertyToID("_BulletTimeIntensity");
            private static readonly int CenterRadiusId = Shader.PropertyToID("_BulletTimeCenterRadius");
            private static readonly int DimColorId = Shader.PropertyToID("_BulletTimeDimColor");
            private static readonly int ArcId = Shader.PropertyToID("_BulletTimeArc");
            private static readonly int ArcFxId = Shader.PropertyToID("_BulletTimeArcFx");

            private readonly Settings _s;
            private Material _mat;
            private Player _player;

            public VignettePass(Settings s)
            {
                _s = s;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_mat == null) _mat = _s.material;
                if (_mat == null) return;

                // 懒缓存 Player； destroyed 引用经 Unity 重载的 == 视为 null，自动重查
                if (_player == null) _player = Player.Instance;

                // 慢动作因子 → 暗幕强度：1 时为 0，bulletTimeScale 时为 maxIntensity。
                // SlowScale 的 0.15s/0.3s MoveTowards 即暗幕的淡入淡出，视觉与慢动作天然同步
                float target = 0.2f;
                if (_player != null && _player.PlayerConfig != null)
                    target = _player.PlayerConfig.bulletTimeScale;
                float intensity = Mathf.InverseLerp(1f, target, TimeManager.SlowScale) * _s.maxIntensity;
                if (intensity <= 0.001f) return;

                var camera = renderingData.cameraData.camera;
                var centerRadius = new Vector4(0.5f, 0.5f, _s.radius, _s.softness);
                if (_player != null)
                {
                    Vector3 vp = camera.WorldToViewportPoint(_player.transform.position);
                    centerRadius.x = Mathf.Clamp01(vp.x);
                    centerRadius.y = Mathf.Clamp01(vp.y);
                }

                _mat.SetFloat(IntensityId, intensity);
                _mat.SetVector(CenterRadiusId, centerRadius);
                _mat.SetColor(DimColorId, _s.dimColor);

                // ── 瞄准扇形（随子弹时间同步淡入淡出：arcStrength = 暗角强度归一）──
                float arcStrength = intensity / Mathf.Max(_s.maxIntensity, 1e-4f);
                // 瞄准方向：鼠标的世界位置相对玩家（正交相机 xy 与 z 无关，同 BloodExample 惯例）
                Vector2 aimDir = Vector2.right;
                if (_player != null)
                {
                    Vector3 mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
                    Vector2 toMouse = (Vector2)mouseWorld - (Vector2)_player.transform.position;
                    if (toMouse.sqrMagnitude > 1e-6f) aimDir = toMouse.normalized; // 鼠标贴脸时保持右向，避免抖动
                }
                // 半径世界单位 → 视口比例（正交相机 y 半高 = orthographicSize）
                float radiusVp = _s.arcRadius / Mathf.Max(2f * camera.orthographicSize, 0.01f);

                _mat.SetVector(ArcId, new Vector4(
                    Mathf.Cos(_s.arcHalfAngleDeg * Mathf.Deg2Rad), radiusVp,
                    _s.arcEdgeSoftness, _s.arcReveal * arcStrength));
                _mat.SetVector(ArcFxId, new Vector4(aimDir.x, aimDir.y,
                    Mathf.Lerp(1f, _s.arcBoost, arcStrength), arcStrength));

                CommandBuffer cmd = CommandBufferPool.Get("BulletTimeVignette");
                // self-blit：URP 14 在 BeforePostProcessing 事件下的官方示例写法
                Blitter.BlitCameraTexture(cmd,
                    renderingData.cameraData.renderer.cameraColorTargetHandle,
                    renderingData.cameraData.renderer.cameraColorTargetHandle,
                    _mat, 0);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
