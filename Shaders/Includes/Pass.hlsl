#if defined(_SKINNING_ON)
#include "Skinning.hlsl"
#endif

Varyings vert (Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    #if defined(_SKINNING_ON)
    SkinningDeform(positionOS, input.normalOS, input.indices, input.weights, _JointMap, _JointMap_TexelSize.z, UNITY_ACCESS_INSTANCED_PROP(PerInstance, _JointOffset));
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

    Surface surface = CalculateSurface(
        #if defined(_BASEMAP_ON)
        input.uv
        #else
            0
        #endif
        );

    return surface.albedo * CalculateLighting(input.positionWS, input.normalWS);
}