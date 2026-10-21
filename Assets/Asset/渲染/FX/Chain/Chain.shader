// 锁链 — 子弹时间瞄准/标记链（ChainLink 驱动 _Progress 与 _WorldLen）。程序化无贴图：
// 沿链长平铺一节节椭圆链环，奇偶环上下错位制造互扣感；_Progress 控制从头向尾的生长。
Shader "BABEL/FX/Chain"
{
    Properties
    {
        [HDR] _Color    ("Color", Color) = (0.85, 0.8, 0.65, 1)
        _Progress ("Progress", Range(0, 1)) = 1
        _LinkLen  ("Link Length (world)", Float) = 0.5
        _Thickness("Thickness (of quad uv)", Range(0.05, 1)) = 0.6
        _Offset   ("Link Offset", Range(0, 0.4)) = 0.12
        _Softness ("Edge Softness", Range(0.01, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One        // Additive 发光
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Chain"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Progress, _WorldLen, _LinkLen, _Thickness, _Offset, _Softness;
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
                // 生长：从头（uv.x=0）向尾延伸
                if (i.uv.x > _Progress) return 0;

                // 链环平铺：_WorldLen 由 C# 每帧传入（localScale.x），决定环密度
                float links = max(_WorldLen / _LinkLen, 1.0);
                float u = i.uv.x * links;
                float linkIdx = floor(u);
                float f = frac(u);                          // 环内 0..1
                float odd = fmod(linkIdx, 2.0);

                // 椭圆链环：奇偶环上下错位（互扣感）
                float cy = (odd * 2.0 - 1.0) * _Offset;
                float lx = (f - 0.5) * 2.0;                 // 环内长轴 -1..1
                float ly = (i.uv.y * 2.0 - 1.0) - cy;       // 横向相对环心
                float ell = (lx * lx) / (0.55 * 0.55) + (ly * ly) / (0.85 * 0.85);
                // 椭圆环带：等值线 1 附近为实体，向内向外都衰减
                float ring = 1.0 - smoothstep(1.0 - _Softness, 1.0 + _Softness, abs(ell - 0.8));

                // 横向裁切：链厚（quad 边缘渐隐）
                float clipY = 1.0 - smoothstep(_Thickness, 1.0, abs(i.uv.y * 2.0 - 1.0));

                float alpha = ring * clipY;
                // 链环描边提亮：环边缘（ell 接近外圈）更亮
                float rim = smoothstep(0.55, 0.8, ell) * (1.0 - smoothstep(0.8, 0.95, ell));
                half3 col = _Color.rgb * (0.7 + rim * 0.8);

                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
