#ifndef _GLOBAL_VARIABLES_CGINC_
#define _GLOBAL_VARIABLES_CGINC_

#include "../ShaderLib/GlobalConfig.cginc"
#include "../ShaderLib/TypeDecl.cginc"
#include "../ShaderLib/ShadowMap.cginc"
#include "../ShaderLib/SphericalHarmonics.cginc"
#include "../ShaderLib/CubemapUtilities.cginc"

#if SHADER_API_D3D11
uint g_DebugFlagsLightingComponents;
half4 g_DebugDiffuseOverrideParameter;
half4 g_DebugSpecularOverrideParameter;
#else
static uint g_DebugFlagsLightingComponents = -1;
static half4 g_DebugDiffuseOverrideParameter = half4(0, 0, 0, 1);
static half4 g_DebugSpecularOverrideParameter = half4(0, 0, 0, 1);
#endif

CBUFFER_START(PerFrameCB_PrimaryCamera)
    float4 g_ScreenParams;
    half g_HashedAlphaTest;

    half4 g_FrameIndexModX;

    float4 g_ViewCameraPosition;
    float4 g_ViewCameraDirection;
    float4x4 g_ViewCameraViewProjMat;
    float4x4 g_ViewCameraInvViewProjMat;
CBUFFER_END

#define g_FrameIndexMod4  g_FrameIndexModX.x
#define g_FrameIndexMod8  g_FrameIndexModX.y
#define g_FrameIndexMod16 g_FrameIndexModX.z


// ---- GGX filtered global envmap
float g_EnvmapMipmapOffset;
TEXTURECUBE(g_EnvmapFilterWithGGX);
SAMPLER(samplerg_EnvmapFilterWithGGX);


// ---- irradiance map
half4 g_EnvmapSH9Coeffs[9];
SH9Color getEnvmapSH9Coefficients()
{
    SH9Color coeffs;
    for (int i = 0; i < 9; ++i)
    {
        coeffs.c[i] = g_EnvmapSH9Coeffs[i].xyz;
    }
    return coeffs;
}

half4 g_EnvmapIntensity;
#if SHADEROPTIONS_ENVMAP_ROTATION
half4 g_EnvmapRotationParam;
#else
static half4 g_EnvmapRotationParam = half4(1,0,0,1);
#endif
#define g_EnvmapRotationParamInv half4(g_EnvmapRotationParam.x, -g_EnvmapRotationParam.y, -g_EnvmapRotationParam.z, g_EnvmapRotationParam.w)


// ---- shadow map

TEXTURE2D_SHADOW(g_ShadowTexture);
SAMPLER_CMP(samplerg_ShadowTexture);

float4 g_ShadowTexture_TexelSize;
float2 g_ShadowDistanceFalloff;

float4x4 g_CsmShadowMatrices[SHADEROPTIONS_CSM_MAX_CASCADES];
float4 g_CsmCascadeValidUv[SHADEROPTIONS_CSM_MAX_CASCADES];
float4 g_CsmNormalBias;
#if SHADEROPTIONS_CSM_MAX_CASCADES > 1
half g_NumOfCsmCascades;
#endif

float4x4 g_ShadowSplitMatrices[SHADEROPTIONS_MAX_SHADOW_SPLITS];
float4 g_ShadowSplitUvRange[SHADEROPTIONS_MAX_SHADOW_SPLITS];
float4 g_ShadowNormalBias[SHADEROPTIONS_MAX_SHADOW_SPLITS];


CSMData getCSMData()
{
    CSMData csmData;

    csmData.shadowHwPcfTexture = g_ShadowTexture;
    ASSIGN_SAMPLER(csmData.shadowHwPcfSampler, samplerg_ShadowTexture);

    csmData.shadowmapTexelSize = g_ShadowTexture_TexelSize.zwxy;

    csmData.shadowMatrices = g_CsmShadowMatrices;
    csmData.cascadeValidUv = g_CsmCascadeValidUv;
    csmData.normalBias = g_CsmNormalBias;

#if SHADEROPTIONS_CSM_MAX_CASCADES > 1
    csmData.cascadeCount = g_NumOfCsmCascades;
#else
    csmData.cascadeCount = 1;
#endif

    return csmData;
}

half getDirectionalLightShadow(in Intersection its, in LightData ld)
{
    half distanceToCamera = length(_WorldSpaceCameraPos - its.posW);
    half shadowFalloff = saturate(distanceToCamera * g_ShadowDistanceFalloff.x + g_ShadowDistanceFalloff.y);

    half shadow = 1.0;
    UNITY_BRANCH
    if (shadowFalloff > 0)
    {
        shadow = getCascadeShadowFactor(its.posW, its.geoFrame.normal, dot(its.geoFrame.normal, ld.L), getCSMData());
        shadow = lerp(1, shadow, shadowFalloff);
    }
    return shadow;
}

half getPunctualLightShadow(in Intersection its, in LightData ld)
{
    half shadow = 1.0;
    if (ld.shadowSplit >= 0)
    {
        half shadowNormalBias = g_ShadowNormalBias[ld.shadowSplit].x;
        shadowNormalBias *= sqrt(ld.sqrDistance);

        int faceIdx = 0;
        if (ld.shape == YALIGHTSHAPE_POINT)
            faceIdx = cubeFaceIndex(-ld.L);

        float4x4 shadowMatrix = g_ShadowSplitMatrices[ld.shadowSplit + faceIdx];
        float4 validUv = g_ShadowSplitUvRange[ld.shadowSplit + faceIdx];

        shadow = getPunctualShadowFactor(its.posW, its.geoFrame.normal, dot(its.geoFrame.normal, ld.L), shadowNormalBias,
                                         g_ShadowTexture, samplerg_ShadowTexture, g_ShadowTexture_TexelSize.zwxy,
                                         shadowMatrix, validUv).x;
    }
    return shadow;
}


// Exposure
#if SHADEROPTIONS_AUTO_EXPOSURE
TEXTURE2D_HALF(g_ExposureTexture);
half4 getExposureValue()
{
    return g_ExposureTexture[int2(0, 0)];
}
#else
half2 g_ExposureValue;
half4 getExposureValue()
{
    return g_ExposureValue.xyxy;
}
#endif

#endif
