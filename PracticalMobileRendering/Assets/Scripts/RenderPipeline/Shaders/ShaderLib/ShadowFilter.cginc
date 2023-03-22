#ifndef _SHADOW_FILTER_CGINC_
#define _SHADOW_FILTER_CGINC_

#include "GlobalConfig.cginc"

#ifndef CONF_PCF_FILTER_SIZE
    #define CONF_PCF_FILTER_SIZE 5
#endif


half _sampleShadowCmp(TEXTURE2D_SHADOW_PARAM(shadowTexture, textureSampler), float2 uv, float compareValue)
{
    half shadowFactor = SAMPLE_TEXTURE2D_SHADOW(shadowTexture, textureSampler, float3(uv, compareValue)).r;
    return shadowFactor;
}


//-------------------------------------------------------------------------------------------------
// The method used in The Witness
//-------------------------------------------------------------------------------------------------
half sampleShadowMapOptimizedPCF(TEXTURE2D_SHADOW_PARAM(shadowTexture, textureSampler), float4 texelSize, float3 shadowUvDepth)
{
    float2 shadowMapSize = texelSize.xy;
    float2 shadowMapSizeInv = texelSize.zw;

    float lightDepth = shadowUvDepth.z;

    float2 uv = shadowUvDepth.xy * shadowMapSize; // 1 unit - 1 texel

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= shadowMapSizeInv;

    half sum = 0;

    #if CONF_PCF_FILTER_SIZE == 2
        return _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), shadowUvDepth.xy, lightDepth);
    #elif CONF_PCF_FILTER_SIZE == 3

        float uw0 = (3 - 2 * s);
        float uw1 = (1 + 2 * s);

        float u0 = (2 - s) / uw0 - 1;
        float u1 = s / uw1 + 1;

        float vw0 = (3 - 2 * t);
        float vw1 = (1 + 2 * t);

        float v0 = (2 - t) / vw0 - 1;
        float v1 = t / vw1 + 1;

        sum += uw0 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v0) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v0) * shadowMapSizeInv, lightDepth);
        sum += uw0 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v1) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v1) * shadowMapSizeInv, lightDepth);

        return sum * 1.0f / 16;

    #elif CONF_PCF_FILTER_SIZE == 5

        float uw0 = (4 - 3 * s);
        float uw1 = 7;
        float uw2 = (1 + 3 * s);

        float u0 = (3 - 2 * s) / uw0 - 2;
        float u1 = (3 + s) / uw1;
        float u2 = s / uw2 + 2;

        float vw0 = (4 - 3 * t);
        float vw1 = 7;
        float vw2 = (1 + 3 * t);

        float v0 = (3 - 2 * t) / vw0 - 2;
        float v1 = (3 + t) / vw1;
        float v2 = t / vw2 + 2;

        sum += uw0 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v0) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v0) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v0) * shadowMapSizeInv, lightDepth);

        sum += uw0 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v1) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v1) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v1) * shadowMapSizeInv, lightDepth);

        sum += uw0 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v2) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v2) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v2) * shadowMapSizeInv, lightDepth);

        return sum * 1.0f / 144;

    #else // CONF_PCF_FILTER_SIZE == 7

        float uw0 = (5 * s - 6);
        float uw1 = (11 * s - 28);
        float uw2 = -(11 * s + 17);
        float uw3 = -(5 * s + 1);

        float u0 = (4 * s - 5) / uw0 - 3;
        float u1 = (4 * s - 16) / uw1 - 1;
        float u2 = -(7 * s + 5) / uw2 + 1;
        float u3 = -s / uw3 + 3;

        float vw0 = (5 * t - 6);
        float vw1 = (11 * t - 28);
        float vw2 = -(11 * t + 17);
        float vw3 = -(5 * t + 1);

        float v0 = (4 * t - 5) / vw0 - 3;
        float v1 = (4 * t - 16) / vw1 - 1;
        float v2 = -(7 * t + 5) / vw2 + 1;
        float v3 = -t / vw3 + 3;

        sum += uw0 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v0) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v0) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v0) * shadowMapSizeInv, lightDepth);
        sum += uw3 * vw0 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u3, v0) * shadowMapSizeInv, lightDepth);

        sum += uw0 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v1) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v1) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v1) * shadowMapSizeInv, lightDepth);
        sum += uw3 * vw1 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u3, v1) * shadowMapSizeInv, lightDepth);

        sum += uw0 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v2) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v2) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v2) * shadowMapSizeInv, lightDepth);
        sum += uw3 * vw2 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u3, v2) * shadowMapSizeInv, lightDepth);

        sum += uw0 * vw3 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u0, v3) * shadowMapSizeInv, lightDepth);
        sum += uw1 * vw3 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u1, v3) * shadowMapSizeInv, lightDepth);
        sum += uw2 * vw3 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u2, v3) * shadowMapSizeInv, lightDepth);
        sum += uw3 * vw3 * _sampleShadowCmp(TEXTURE2D_SHADOW_ARGS(shadowTexture, textureSampler), base_uv + float2(u3, v3) * shadowMapSizeInv, lightDepth);;

        return sum * 1.0f / 2704;

    #endif
}


#endif
