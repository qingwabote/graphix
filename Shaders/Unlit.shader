Shader "Graphix/Unlit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle(_BASEMAP_ON)] _BASEMAP ("Use Base Map", Float) = 0
        _BaseMap("Base Map", 2D) = "white" {}

        // Blend One Zero with BlendOp Add == Blend Off "When we see Blend One Zero with BlendOp Add, we turn blending off." https://discussions.unity.com/t/is-there-extra-performance-cost-for-blend-one-zero/851542/2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend][_DstBlend]
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _BASEMAP_ON

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                #if defined(_BASEMAP_ON)
                float2 texcoord : TEXCOORD0;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                #if defined(_BASEMAP_ON)
                float2 uv : TEXCOORD0;
                #endif
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            #if defined(_BASEMAP_ON)
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            #endif

            Varyings vert (Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                Varyings output = (Varyings)0;
                #if defined(_BASEMAP_ON)
                output.uv = input.texcoord;
                #endif
                output.positionHCS = mul(GetWorldToHClipMatrix(), mul(GetObjectToWorldMatrix(), input.positionOS));
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                half4 color = _BaseColor;
                #if defined(_BASEMAP_ON)
                color *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
