Shader "Graphix/Toon"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _BaseMap("Base Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Toggle] _SKINNING ("Enable Vertex Skinning", Float) = 0

        [Toggle] _INSTANCED_BASECOLOR ("Enable Base Color Instancing", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _SKINNING_ON

            #pragma shader_feature_local _INSTANCED_BASECOLOR_ON

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma instancing_options nolightprobe

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;

                #if defined(_SKINNING_ON)
                uint4 indices : BLENDINDICES;
                float4 weights : BLENDWEIGHTS;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 positionHCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            CBUFFER_START(UnityPerMaterial)
            #if !defined(_INSTANCED_BASECOLOR_ON)
                half4 _BaseColor;
            #endif
                half _Smoothness;
                half _DissolveScale;
                half _Dissolve;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
            #if defined(_INSTANCED_BASECOLOR_ON)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            #endif
            #if defined(_SKINNING_ON)
                UNITY_DEFINE_INSTANCED_PROP(float, _JointOffset)
            #endif
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            #if defined(_SKINNING_ON)
            TEXTURE2D(_JointMap);
            float4 _JointMap_TexelSize;
            #endif

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            half4 CalculateSurface(half4 color, float2 uv)
            {
                half4 noise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, uv * _DissolveScale);
				clip(noise.r - UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Dissolve));
                return color;
            }

            #include "Packages/graphix/Shaders/Includes/Toon.hlsl"
            #include "Packages/graphix/Shaders/Includes/Pass.hlsl"
            ENDHLSL
        }
    }
}
