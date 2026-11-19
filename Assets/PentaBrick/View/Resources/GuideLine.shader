Shader "PentaBrick/GuideLine"
{
    // 높이 상한선 같은 안내선: 조명 없는 반투명 단색. Resources에 있어 빌드에 항상 포함된다.
    Properties { _BaseColor ("Color", Color) = (0.55, 0.85, 0.95, 0.3) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 Frag(Varyings i) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
