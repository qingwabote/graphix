Shader "Graphix/Phong"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle] _BASEMAP ("Enable Base Map", Float) = 0
        _BaseMap("Base Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Toggle] _SKINNING ("Enable Vertex Skinning", Float) = 0

        [Toggle] _INSTANCED_BASECOLOR ("Enable Base Color Instancing", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _BASEMAP_ON
            #pragma shader_feature_local _SKINNING_ON

            #pragma shader_feature_local _INSTANCED_BASECOLOR_ON

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma instancing_options nolightprobe

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(_SKINNING_ON)
            #include "Packages/graphix/Shaders/Includes/Skinning.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                #if defined(_BASEMAP_ON)
                float2 texcoord : TEXCOORD0;
                #endif

                #if defined(_SKINNING_ON)
                uint4 indices : BLENDINDICES;
                float4 weights : BLENDWEIGHTS;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                #if defined(_BASEMAP_ON)
                float2 uv : TEXCOORD0;
                #endif
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
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
            #if defined(_INSTANCED_BASECOLOR_ON)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            #endif
            #if defined(_SKINNING_ON)
                UNITY_DEFINE_INSTANCED_PROP(float, _JointOffset)
            #endif
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            #if defined(_BASEMAP_ON)
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            #endif

            #if defined(_SKINNING_ON)
            TEXTURE2D(_JointMap);
            float4 _JointMap_TexelSize;
            #endif

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                #if defined(_SKINNING_ON)
                // exclude from editor
                if (_JointMap_TexelSize.z > 0) {
                    SkinningDeform(positionOS, input.normalOS, input.indices, input.weights, _JointMap, _JointMap_TexelSize.z, UNITY_ACCESS_INSTANCED_PROP(PerInstance, _JointOffset));
                }
                #endif
                float3 positionWS = TransformObjectToWorld(positionOS);

                #if defined(_BASEMAP_ON)
                output.uv = input.texcoord;
                #endif
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Light light = GetMainLight();

                half3 diffuse = LightingLambert(light.color, light.direction, input.normalWS);

                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 specular = LightingSpecular(light.color, light.direction, input.normalWS, viewDir, half4(0.5, 0.5, 0.5, 1.0), exp2(10 * _Smoothness + 1));

                #if defined(_INSTANCED_BASECOLOR_ON)
                    half4 color = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                #else
                    half4 color = _BaseColor;
                #endif
                #if defined(_BASEMAP_ON)
                color *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #endif
                // https://discussions.unity.com/t/get-ambient-color-in-custom-shader/814307/3
                return color * half4(diffuse + specular + half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 1.0);
            }
            ENDHLSL
        }
    }
}
