// 子弹时间暗角 — 全屏径向渐变压暗（RendererFeature 用，勿挂材质到物体）。
// 参数全部由 BulletTimeVignetteFeature 每帧写入材质实例，不在 Properties 暴露，
// 避免材质序列化值覆盖运行时传入值。
Shader "Hidden/BulletTimeVignette"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "BulletTimeVignettePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // 顺序照官方 Hidden/Universal Render Pipeline/Blit：
            // URP Core.hlsl 提供 TEXTURE2D_X 等 XR 宏（Blit.hlsl 依赖），core Blit.hlsl 提供 Vert/Varyings/_BlitTexture
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BulletTimeIntensity;      // 0 = 无效果（提前跳过不画），1 = 全强度
            float4 _BulletTimeCenterRadius;  // xy = 玩家视口坐标(0-1)，z = 亮区半径，w = 柔边宽度
            float4 _BulletTimeDimColor;      // xyz = 暗区色调倍率（乘法压暗）
            float4 _BulletTimeArc;           // x = cos(半角)，y = 扇形半径(视口)，z = 扇形柔边，w = 扇形内暗角移除比例
            float4 _BulletTimeArcFx;         // xy = 朝向(视口空间)，z = 扇形内提亮倍率，w = 扇形总强度(0~1，淡入淡出)

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // 宽高比修正：x 拉到与 y 同尺度，保证亮区是圆不是椭圆
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = input.texcoord;
                p.x *= aspect;
                float2 c = _BulletTimeCenterRadius.xy;
                c.x *= aspect;

                float d = distance(p, c);
                float vign = smoothstep(_BulletTimeCenterRadius.z,
                                        _BulletTimeCenterRadius.z + _BulletTimeCenterRadius.w, d);

                // ── 瞄准扇形：朝向 ±(半角) 、半径内 ──
                float arc = 0.0;
                float2 arcDir = _BulletTimeArcFx.xy;
                if (dot(arcDir, arcDir) > 0.25)   // 朝向有效（单位向量）
                {
                    float2 toPix = (p - c) / max(d, 1e-5);
                    // step(edge, x)：x ≥ edge 为 1 → 夹角余弦 ≥ cos(半角) = 在扇形角度内
                    float inAngle = step(_BulletTimeArc.x, dot(normalize(arcDir), toPix));
                    float radial = 1.0 - smoothstep(_BulletTimeArc.y,
                                                    _BulletTimeArc.y + _BulletTimeArc.z, d);
                    arc = inAngle * radial * _BulletTimeArcFx.w;
                }

                // 扇形更亮的来源：扇形内不吃暗角（周围暗把它衬出来）+ 轻微提亮
                vign *= lerp(1.0, 1.0 - _BulletTimeArc.w, arc);
                col.rgb *= lerp(1.0, _BulletTimeArcFx.z, arc);

                col.rgb *= lerp(half3(1.0, 1.0, 1.0), _BulletTimeDimColor.rgb,
                                vign * _BulletTimeIntensity);
                return col;
            }
            ENDHLSL
        }
    }
}
