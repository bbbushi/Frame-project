// 穿刺光针 — 处决击杀特效（PierceEffect 驱动 _Progress 0→1）。程序化无贴图：
// 沿局部 +x 的一道菱形光束（中心宽两端尖），进度前段从中心向两端拉伸（刺入），
// 中段保持最亮（内芯过曝），后段整体淡出收窄。Additive 发光叠加。
Shader "BABEL/FX/Pierce"
{
    Properties
    {
        [HDR] _Color    ("Color", Color) = (1, 0.95, 0.7, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _Width    ("Width (of quad)", Range(0.05, 0.5)) = 0.30
        _Taper    ("Taper Sharpness", Range(0.3, 3)) = 1.2
        _Softness ("Edge Softness", Range(0.005, 0.1)) = 0.03
        _CoreBoost("Core Brightness", Range(1, 5)) = 2.5
        _StretchEnd ("Stretch End (progress)", Range(0.05, 0.6)) = 0.3
        _FadeStart  ("Fade Start (progress)", Range(0.2, 0.8)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One        // Additive 发光叠加
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Pierce"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Progress, _Width, _Taper, _Softness, _CoreBoost, _StretchEnd, _FadeStart;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float x = i.uv.x * 2.0 - 1.0;      // -1..1 沿穿刺方向
                float y = i.uv.y * 2.0 - 1.0;      // -1..1 宽度方向

                // 伸展段：光束长度从中心向两端拉开（easeOut）
                float stretch = saturate(_Progress / max(_StretchEnd, 1e-3));
                stretch = 1.0 - (1.0 - stretch) * (1.0 - stretch);
                if (abs(x) > stretch) return 0;    // 尚未伸展到的部分

                // 菱形轮廓：半宽沿长度向两端收尖
                float halfW = _Width * pow(saturate(1.0 - abs(x)), _Taper);
                float band = 1.0 - smoothstep(halfW - _Softness, halfW + _Softness, abs(y));

                // 消散段：整体淡出 + 宽度收窄
                float fade = 1.0 - saturate((_Progress - _FadeStart) / max(1.0 - _FadeStart, 1e-3));
                float shrink = lerp(1.0, 0.55, saturate((_Progress - _FadeStart) / max(1.0 - _FadeStart, 1e-3)));
                band *= smoothstep(0.0, 0.12, fade);            // 收窄平滑过渡，避免硬裁切
                halfW *= shrink;

                // 亮度分布：中心（刺入点）过曝的内芯 + 外层颜色光晕；沿长度向两端略衰减
                float along = saturate(1.0 - abs(x) * 0.45);
                float core = smoothstep(halfW * 0.45, 0.0, abs(y)) * _CoreBoost * along;
                half3 col = _Color.rgb * (band * along + core * band);

                return half4(col * fade, band * fade);
            }
            ENDHLSL
        }
    }
}
