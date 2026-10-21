// 斩击弧光 — 处决击杀特效（SlashEffect 驱动 _Progress 0→1，勿挂到常规物体）。
// 程序化绘制无贴图：UV 极坐标画一条弧带，进度前段划出弧光、头部提亮、尾部拖影，后段整体消散。
Shader "BABEL/FX/SlashArc"
{
    Properties
    {
        [HDR] _Color     ("Color", Color) = (1, 0.92, 0.6, 1)
        _Progress  ("Progress", Range(0, 1)) = 0
        _Radius    ("Arc Radius", Range(0.1, 0.5)) = 0.36
        _Thickness ("Thickness", Range(0.01, 0.3)) = 0.1
        _ArcSpan   ("Arc Span (turns)", Range(0.1, 0.9)) = 0.42
        _Softness  ("Edge Softness", Range(0.001, 0.1)) = 0.02
        _HeadBoost ("Head Brightness", Range(1, 4)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One        // Additive 发光叠加
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SlashArc"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Progress, _Radius, _Thickness, _ArcSpan, _Softness, _HeadBoost;
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
                float2 p = i.uv - 0.5;
                float r = length(p) * 2.0;                        // 0 中心 → 1 边缘
                float a = atan2(p.y, p.x) / (2.0 * PI) + 0.5;     // 角度归一化 0..1（0.5 = +x 方向）

                // 弧带揭示：起点固定在弧的一端，头部随进度向另一端划
                float start = 0.5 - _ArcSpan * 0.5;               // 弧居中于 +x 方向
                float headPos = saturate(_Progress * 1.4);        // 前 ~70% 进度划完整条弧
                float t = (a - start) / max(_ArcSpan, 1e-4);      // 弧上参数位置：0 起点 … 1 终点
                float inSpan = step(0.0, t) * step(t, headPos);   // 已被划过的部分

                // 两端收尖：厚度沿弧呈正弦分布（中间厚两端细）
                float taper = sin(saturate(t) * PI);
                float thick = _Thickness * (0.3 + 0.7 * taper);
                float band = 1.0 - smoothstep(thick * 0.5 - _Softness,
                                              thick * 0.5 + _Softness, abs(r - _Radius));

                // 头部后 60% 弧长渐亮（拖影），头部最亮
                float trail = saturate((headPos - t) / 0.6);
                float headGlow = 1.0 + (_HeadBoost - 1.0) * saturate(1.0 - abs(t - headPos) * 6.0);

                // 进度后段整体消散
                float fade = 1.0 - saturate((_Progress - 0.6) / 0.4);

                float alpha = band * inSpan * trail * fade;
                half3 col = _Color.rgb * alpha * headGlow;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
