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
                col.rgb *= lerp(half3(1.0, 1.0, 1.0), _BulletTimeDimColor.rgb,
                                vign * _BulletTimeIntensity);
                return col;
            }
            ENDHLSL
        }
    }
}
