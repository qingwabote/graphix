Shader "Graphix/Unlit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Surface("__surface", Float) = 0.0

        [ToggleUI] _AlphaClip ("__clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Blend("__mode", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        // Blend One Zero with BlendOp Add == Blend Off "When we see Blend One Zero with BlendOp Add, we turn blending off." https://discussions.unity.com/t/is-there-extra-performance-cost-for-blend-one-zero/851542/2
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0

        [HideInInspector] _ZWrite("__zw", Float) = 1.0

        _QueueOffset("Queue offset", Float) = 0.0

        _ZTest("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert (Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                Varyings output = (Varyings)0;
                output.uv = input.texcoord;
                output.positionHCS = mul(GetWorldToHClipMatrix(), mul(GetObjectToWorldMatrix(), input.positionOS));
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                half4 color = _BaseColor;
                color *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                    clip(color.a - _Cutoff);
                #endif
                return color;
            }
            ENDHLSL
        }
    }

    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.UnlitShader"
}
