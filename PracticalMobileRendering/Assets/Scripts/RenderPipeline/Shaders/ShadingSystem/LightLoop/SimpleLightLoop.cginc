#ifndef _SIMPLE_LIGHT_LOOP_CGINC_
#define _SIMPLE_LIGHT_LOOP_CGINC_

#include "../../ShaderLib/GlobalConfig.cginc"

half4 g_DominantLightDirection;
half3 g_DominantLightIntensity;

int g_NumPunctualLights;
half3 g_PunctualLightsDirection[SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS];
float4 g_PunctualLightsPosition[SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS];
half3 g_PunctualLightsIntensity[SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS];
half4 g_PunctualLightsFalloff[SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS];
half3 g_PunctualLightsInfo[SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS];


LightData getDirectionalLight(in Intersection its)
{
    LightData ld;
    ld.shape = YALIGHTSHAPE_DIRECTIONAL;
    ld.shadowSplit = 0;

    ld.intensity = g_DominantLightIntensity;

    ld.L = g_DominantLightDirection.xyz;
    ld.H = normalize(its.V + ld.L);
	ld.NoL = saturate(dot(its.shFrame.normal, ld.L));
	
	ld.NoH = saturate(dot(its.shFrame.normal, ld.H));
#if SHADER_API_MOBILE
    half3 NxH = cross(its.shFrame.normal, ld.H);
	ld.oneMinusSquaredNoH = dot(NxH, NxH);
#else
	ld.oneMinusSquaredNoH = 1 - ld.NoH * ld.NoH;
#endif

	ld.LoH = saturate(dot(ld.L, ld.H));
	
    ld.sqrDistance = 0;
    ld.shadowAngleOrRadius = g_DominantLightDirection.w;

    ld.shadowFactor = 1;
    return ld;
} 

void getPunctualLightIndexRange(in Intersection its, out int start, out int end)
{
#if (SHADEROPTIONS_MAX_PUNCTUAL_LIGHTS > 0)
    start = 0;
    end = g_NumPunctualLights;
#else
    start = 0;
    end = 0;
#endif
}

LightData getPunctualLight(in Intersection its, in int lightIdx)
{
    half3 lightVec = g_PunctualLightsPosition[lightIdx].xyz - its.posW;
    half squaredDistance = dot(lightVec, lightVec);
    half rcpDistance = rsqrt(squaredDistance);

    half invSqrRadius = g_PunctualLightsFalloff[lightIdx].x;
    half falloffExponent = g_PunctualLightsFalloff[lightIdx].y;
    half2 angularFalloff = g_PunctualLightsFalloff[lightIdx].zw;

    half falloff = punctualLightDistanceFalloff(squaredDistance, invSqrRadius, falloffExponent);

    half distanceInLightSpace = dot(lightVec, g_PunctualLightsDirection[lightIdx].xyz);
    falloff *= punctualLightAngleFalloff(distanceInLightSpace * rcpDistance, angularFalloff);

    LightData ld;
    ld.shape = g_PunctualLightsInfo[lightIdx].x;
    ld.shadowSplit = g_PunctualLightsInfo[lightIdx].y;

    ld.intensity = g_PunctualLightsIntensity[lightIdx].xyz * falloff;
    ld.L = lightVec * rcpDistance;
    ld.H = normalize(its.V + ld.L);
    ld.NoL = saturate(dot(its.shFrame.normal, ld.L));

    ld.NoH = saturate(dot(its.shFrame.normal, ld.H));
#if SHADER_API_MOBILE
	half3 NxH = cross(its.shFrame.normal, ld.H);
	ld.oneMinusSquaredNoH = dot(NxH, NxH);
#else
    ld.oneMinusSquaredNoH = 1 - ld.NoH * ld.NoH;
#endif

    ld.LoH = saturate(dot(ld.L, ld.H));

    ld.sqrDistance = squaredDistance;
    ld.shadowAngleOrRadius = g_PunctualLightsPosition[lightIdx].w;

    ld.shadowFactor = 1;
    return ld;
}

#endif
