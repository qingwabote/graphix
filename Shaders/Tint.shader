Shader "Graphix/Tint"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle] _BASEMAP ("Enable Base Map", Float) = 0
        _BaseMap("Base Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

		_RimAmount("Rim Amount", Range(0, 1)) = 0.716

        [Toggle] _SKINNING ("Enable Vertex Skinning", Float) = 0

        [Toggle] _INSTANCED_BASECOLOR ("Enable Base Color Instancing", Float) = 0

        _TintMask("Tint Mask Map", 2D) = "white" {}
        _TintColor1("Tint Color 1", Color) = (1, 1, 1, 1)
        _TintColor2("Tint Color 2", Color) = (1, 1, 1, 1)
        _TintColor3("Tint Color 3", Color) = (1, 1, 1, 1)

    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _BASEMAP_ON
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

			half _RimAmount;

            half4 _TintColor1;
            half4 _TintColor2;
            half4 _TintColor3;
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

            TEXTURE2D(_TintMask);
            SAMPLER(sampler_TintMask);

            struct Surface
            {
                half4 albedo;
            };

            half3 LightingSpecular(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir, half4 specular, half smoothness)
            {
                float3 halfVec = SafeNormalize(lightDir + viewDir);
                half NdotH = saturate(dot(normal, halfVec));
                half modifier = smoothstep(0.005, 0.01, pow(float(NdotH), float(smoothness))); // Half produces banding, need full precision
                // NOTE: In order to fix internal compiler error on mobile platforms, this needs to be float3
                float3 specularReflection = specular.rgb * modifier;
                return lightColor * specularReflection;
            }

            Surface CalculateSurface(float2 uv)
            {
                #if defined(_INSTANCED_BASECOLOR_ON)
                    half4 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                #else
                    half4 albedo = _BaseColor;
                #endif
                #if defined(_BASEMAP_ON)
                albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                #endif

                float4 mask = SAMPLE_TEXTURE2D(_TintMask, sampler_TintMask, uv);
                float4 color = min(mask.r, _TintColor1) + min(mask.g, _TintColor2) + min(mask.b, _TintColor3);
                albedo = lerp(albedo, color * albedo, mask.r + mask.g + mask.b);

                Surface surface;
                surface.albedo = albedo;
                return surface;
            }

            half4 CalculateLighting(float3 position, half3 normal)
            {
                // https://roystan.net/articles/toon-shader/

                Light light = GetMainLight();
                half NdotL = saturate(dot(normal, light.direction));
                half3 diffuse = light.color * smoothstep(0, 0.01, NdotL); 
                half3 viewDir = GetWorldSpaceNormalizeViewDir(position);
                half3 specular = LightingSpecular(light.color, light.direction, normal, viewDir, half4(0.5, 0.5, 0.5, 1.0), exp2(10 * _Smoothness + 1));

                // Calculate rim lighting.
				// We only want rim to appear on the lit side of the surface,
				// so multiply it by NdotL, raised to a power to smoothly blend it.
				// half rim = (1 - dot(viewDir, normal)) * pow(NdotL, 0.1);
                half rim = (1 - dot(viewDir, normal)) * step(0.001, NdotL);
				rim = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rim);

                // https://discussions.unity.com/t/get-ambient-color-in-custom-shader/814307/3
                return half4(diffuse + specular + half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) + rim, 1.0);
            }

            #include "Packages/graphix/Shaders/Includes/Pass.hlsl"
            ENDHLSL
        }
    }
}
