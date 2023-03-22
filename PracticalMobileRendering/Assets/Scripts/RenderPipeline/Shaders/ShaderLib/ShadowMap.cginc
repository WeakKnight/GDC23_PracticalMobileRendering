#ifndef _SHADOWMAP_CGINC_
#define _SHADOWMAP_CGINC_

#include "GlobalConfig.cginc"
#include "ShadowFilter.cginc"

struct CSMData
{
    TEXTURE2D_SHADOW(shadowHwPcfTexture);
	SAMPLER_CMP(shadowHwPcfSampler);

    float4 shadowmapTexelSize;
    float4 normalBias;
    float4x4 shadowMatrices[SHADEROPTIONS_CSM_MAX_CASCADES];
    float4 cascadeValidUv[SHADEROPTIONS_CSM_MAX_CASCADES];
    half cascadeCount;
};

// idea from https://github.com/godotengine/godot/issues/17260
half3 _shadowPositionOffset(in half NoL, in half3 normalW, in half normalOffsetScale)
{
    half slopeScale = 2 - NoL;
    normalOffsetScale *= slopeScale;
    return normalOffsetScale * normalW;
}

bool _isValidCascadeUv(float2 uv, float4 validUvRange)
{
#if UNITY_COMPILER_DXC
    return uv.x > validUvRange.x && uv.y > validUvRange.y &&
           uv.x < validUvRange.z && uv.y < validUvRange.w;
#else
    return all(validUvRange.xy < uv && uv < validUvRange.zw);
#endif
}

half3 getPunctualShadowFactor(float3 posW, half3 normalW, half NoL, half normalBias,
                              TEXTURE2D_SHADOW_PARAM(shadowTexture, shadowSampler), float4 shadowTexelSize,
                              float4x4 shadowMatrix, float4 validUv)
{
    posW += _shadowPositionOffset(NoL, normalW, normalBias);

    float4 shadowPos = mul(shadowMatrix, float4(posW, 1));
    shadowPos /= shadowPos.w;

    if (!_isValidCascadeUv(shadowPos.xy, validUv))
        return 1;

    half shadowFactor = sampleShadowMapOptimizedPCF(TEXTURE2D_SHADOW_ARGS(shadowTexture, shadowSampler),
                                                     shadowTexelSize,
                                                     shadowPos.xyz);
    return shadowFactor;
}

half getCascadeShadowFactor(float3 posW, half3 normalW, half NoL, in CSMData csmData)
{
#if SHADEROPTIONS_CSM_MAX_CASCADES == 1
    // Apply normal offset
    float3 offsetPosW = posW + _shadowPositionOffset(NoL, normalW, csmData.normalBias.x);
    float3 shadowUvDepth = mul(csmData.shadowMatrices[0], float4(offsetPosW, 1)).xyz;
    if (!_isValidCascadeUv(shadowUvDepth.xy, csmData.cascadeValidUv[0]))
        return 1.0f;

#else

    int cascadeIdx = SHADEROPTIONS_CSM_MAX_CASCADES;
    float3 shadowUvDepth = 0;

    UNITY_UNROLL
    for (int i = SHADEROPTIONS_CSM_MAX_CASCADES - 1; i >= 0; --i)
    {
        float3 offsetPosW = posW + _shadowPositionOffset(NoL, normalW, csmData.normalBias[i]);
        float3 p = mul(csmData.shadowMatrices[i], float4(offsetPosW, 1)).xyz;
        if (_isValidCascadeUv(p.xy, csmData.cascadeValidUv[i]))
        {
            cascadeIdx = i;
            shadowUvDepth = p;
        }
    }

    if (cascadeIdx >= csmData.cascadeCount)
        return 1.0f;
#endif

    // HACK: clamp depth value so that object outside z range of shadow volume is considered as unoccluded
    // it happens with user define shadow volume was used
    shadowUvDepth.z = max(shadowUvDepth.z, 1e-4);

    half shadowFactor = sampleShadowMapOptimizedPCF(TEXTURE2D_SHADOW_ARGS(csmData.shadowHwPcfTexture, csmData.shadowHwPcfSampler),
                                                      csmData.shadowmapTexelSize,
                                                      shadowUvDepth);
    return shadowFactor;
}

#endif
