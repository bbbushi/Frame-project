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
