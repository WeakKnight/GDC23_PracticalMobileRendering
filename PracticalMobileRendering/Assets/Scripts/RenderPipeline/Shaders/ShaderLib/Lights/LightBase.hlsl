#ifndef _LIGHT_BASE_HLSL_
#define _LIGHT_BASE_HLSL_

#include "../GlobalConfig.cginc"

struct LightSamplingRecord
{
    float3 Le;
    float pdf;

    float3 rayDirection;
    float rayT;

    float3 faceNormal;
};

#endif