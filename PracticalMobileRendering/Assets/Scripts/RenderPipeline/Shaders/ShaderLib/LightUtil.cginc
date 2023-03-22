#ifndef _LIGHT_UTIL_CGINC_
#define _LIGHT_UTIL_CGINC_

#include "GlobalConfig.cginc"

half punctualLightDistanceFalloff(half squaredDistance, half invSqrAttRadius, half falloffExponent = 0)
{
    if (falloffExponent == 0)
    {
        // squared distance falloff
        half distFalloff = 1.0 / max(squaredDistance, 0.01 * 0.01);

        // apply a smooth factor in order to limit the light range
        // Refer to https://seblagarde.files.wordpress.com/2015/07/course_notes_moving_frostbite_to_pbr_v32.pdf P32
        half factor = squaredDistance * invSqrAttRadius;
        half smoothFactor = saturate(1.0f - factor * factor);
        smoothFactor *= smoothFactor;

        return smoothFactor * distFalloff;
    }
    else
    {
        half normalizeDistanceSquared = 1 - saturate(squaredDistance * invSqrAttRadius);
        return pow(normalizeDistanceSquared, falloffExponent);
    }
}

half punctualLightAngleFalloff(const half3 lightDir, const half3 L, const half2 angleScaleOffset) {
    half cd = dot(lightDir, L);
    half attenuation = saturate(cd * angleScaleOffset.x + angleScaleOffset.y);
    return attenuation * attenuation;
}

half punctualLightAngleFalloff(const half cd, const half2 angleScaleOffset) {
    half attenuation = saturate(cd * angleScaleOffset.x + angleScaleOffset.y);
    return attenuation * attenuation;
}

#endif
