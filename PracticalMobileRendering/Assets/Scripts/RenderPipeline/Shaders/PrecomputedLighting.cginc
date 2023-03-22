#pragma once
#include "UnityCG.cginc"

UNITY_DECLARE_TEX2D(_Lightmap0);
float4 _LightmapScaleAndOffset;

half3 sampleLightmap(half2 uv1, half3 normal)
{
    half2 lightmapUV = uv1 * _LightmapScaleAndOffset.xy + _LightmapScaleAndOffset.zw;
    half3 irradiance = UNITY_SAMPLE_TEX2D(_Lightmap0, float2(lightmapUV.x, 1.0f - lightmapUV.y)).xyz;
    return irradiance;
}

TEXTURE3D(_VLM_Texture);
SAMPLER(sampler_VLM_Texture);
float3 _VLM_BoundingBoxMin;
float3 _VLM_InvVolumeSize;
float4x4 _VLM_LocalToWorld;
float4x4 _VLM_WorldToLocal;

float3 decodeRGBM(float4 col)
{
    float3 result = 6.0f * col.rgb * col.a;
    return result * result;
}

float3 positionToVolumetricLightmapUVW(float3 posW)
{
    float3 posL = mul(_VLM_WorldToLocal, float4(posW, 1.0)).xyz;
    float3 uvw = (posL - _VLM_BoundingBoxMin) * _VLM_InvVolumeSize.xyz;
    return uvw;
}

float3 readAmibentCubeFace(float3 UVW, uint faceIndex)
{
    float3 finalUVW = UVW + float3(0.0f, 0.0f, (float)faceIndex * (1.0f / 6.0f));
    float4 col = SAMPLE_TEXTURE3D(_VLM_Texture, sampler_VLM_Texture, finalUVW);

    return decodeRGBM(col);
}

float3 sampleVolumetricLightmap(float3 posW, float3 normal)
{
    float3 uvw = positionToVolumetricLightmapUVW(posW);
    float3 transformedDirection = normalize(mul(normal, (float3x3)_VLM_LocalToWorld).xyz);
    float3 dirSquared = transformedDirection * transformedDirection;

    float3 result
        = dirSquared.x * readAmibentCubeFace(uvw, transformedDirection.x < 0 ? 1 : 0)
        + dirSquared.y * readAmibentCubeFace(uvw, transformedDirection.y < 0 ? 2 : 3)
        + dirSquared.z * readAmibentCubeFace(uvw, transformedDirection.z < 0 ? 4 : 5);

    return result;
}
