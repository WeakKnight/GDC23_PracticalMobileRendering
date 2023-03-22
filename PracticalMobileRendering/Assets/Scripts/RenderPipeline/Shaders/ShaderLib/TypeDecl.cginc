#ifndef _TYPE_DECL_CGINC_
#define _TYPE_DECL_CGINC_

#include "GlobalConfig.cginc"
#include "ShaderUtilities.cginc"

struct VisibilityCone
{
    half3 direction;
    half aperture;

    // Overall scale of cone
    half scale;
};

VisibilityCone VisibilityCone_(float3 direction, float aperture, float scale)
{
    VisibilityCone cone;
    cone.direction = direction;
    cone.aperture = aperture;
    cone.scale = scale;
    return cone;
}

struct Intersection
{
    // Position in world space
    float3 posW;

    // Position in viewport
    float2 posVP;
    half2 screenUV;

    half2 lightmapUV;

    // Depth value in [0,1] on D3D, Metal, Vulkan. In [-1,1] on OpenGL
    float depth;

    // View vector in world space
    half3 V;

    // Reflected view vector over shading normal
    half3 R;

    // Dot product of view vector and shading normal
    half NoV;

    // Shading frame (based on the shading normal)
    Frame shFrame;

    // Geometric frame (based on the true geometry)
    Frame geoFrame;

    // 1 or -1 depend on uv winding order of the face
    half bitangentSign;

    // Pre-computed visibility cone
    VisibilityCone vCone;
};

struct LightData
{
    int shape;
    int shadowSplit;

    half3 intensity;

    half3 L;
    half3 H;
	half NoL;
	half NoH;
	half oneMinusSquaredNoH;
	half LoH;

    // Square distance between light and intersection, 0 for directional light
    half sqrDistance;
    // Solid angle for directional light, radius for point/spot light
    half shadowAngleOrRadius;

    half shadowFactor;
};

struct PayloadVisibilityDXR
{
    float hitT;
};

#endif
