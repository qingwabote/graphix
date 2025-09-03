Shader "Graphix/Phong"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle] _BASEMAP ("Enable Base Map", Float) = 0
        _BaseMap("Base Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Toggle] _SKINNING ("Enable Vertex Skinning", Float) = 0
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

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
            CBUFFER_END

            #if defined(_BASEMAP_ON)
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            #endif

            #if defined(_SKINNING_ON)
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _JointOffset)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            TEXTURE2D(_JointMap);
            float4 _JointMap_TexelSize;
            #endif

            Varyings vert (Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS = input.positionOS.xyz;
                #if defined(_SKINNING_ON)
                SkinningDeform(positionOS, input.indices, input.weights, _JointMap, _JointMap_TexelSize.z, UNITY_ACCESS_INSTANCED_PROP(PerInstance, _JointOffset));
                #endif
                float3 positionWS = TransformObjectToWorld(positionOS);

                Varyings output = (Varyings)0;
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
                Light light = GetMainLight();

                half3 diffuse = LightingLambert(light.color, light.direction, input.normalWS);

                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 specular = LightingSpecular(light.color, light.direction, input.normalWS, viewDir, half4(0.5, 0.5, 0.5, 1.0), exp2(10 * _Smoothness + 1));

                half4 color = _BaseColor;
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
