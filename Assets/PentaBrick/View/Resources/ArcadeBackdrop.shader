Shader "PentaBrick/ArcadeBackdrop"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float d = saturate(length((i.uv - 0.5) * float2(1.1, 1.0)) * 1.5);
                float3 center = float3(0.117647, 0.160784, 0.231373);
                float3 edge = float3(0.058824, 0.090196, 0.164706);
                float3 col = lerp(center, edge, smoothstep(0, 1, d));
                // Linear shader output gives the requested #1e293b -> #0f172a display colors.
                #ifndef UNITY_COLORSPACE_GAMMA
                col = SRGBToLinear(col);
                #endif
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
