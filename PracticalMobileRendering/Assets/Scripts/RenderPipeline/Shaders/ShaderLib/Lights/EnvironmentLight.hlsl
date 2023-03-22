#ifndef _ENVIRONMENT_LIGHT_HLSL_
#define _ENVIRONMENT_LIGHT_HLSL_

#include "../GlobalConfig.cginc"
#include "../ImportanceSampling.cginc"
#include "../ColorSpace.cginc"
#include "LightBase.hlsl"


struct EnvironmentLight
{
    Texture2D<float4> envmap;
    Hierarchical2D distr;

    float3 intensity;
    float4 localToWorld;
    float4 worldToLocal;

    float sceneBoundingSphereRadius;

    float3 Le(float3 direction)
    {
        direction = rotateInAxisY(direction, worldToLocal);
        return _sampleEnvmap(envmap, direction) * intensity;
    }

    LightSamplingRecord sample(float2 u)
    {
        float2 uv;
        float pdf;
        distr.sampleContinuous(u, uv, pdf);

        LightSamplingRecord rec = (LightSamplingRecord)0;
        if (pdf > 0)
        {
            float3 direction = squareToSphereProjection(uv);
            direction = rotateInAxisY(direction, localToWorld);

            rec.Le = Le(direction);
            rec.rayDirection = direction;
            rec.rayT = sceneBoundingSphereRadius;
            rec.faceNormal = 0;
            rec.pdf  = pdf * squareToSphereJacobian();
        }
        return rec;
    }

    float pdf(float3 rayDirection)
    {
        float3 direction = rotateInAxisY(rayDirection, worldToLocal);
        float2 uv = sphereToSquareProjection(direction);
        return distr.pdfContinuous(uv) * squareToSphereJacobian();
    }

    static float importance(Texture2D<float4> envmap, float2 uv)
    {
        float3 dir = squareToSphereProjection(uv);

        float3 color = _sampleEnvmap(envmap, dir);
        float luma = luminance_sRGB(color);
        return max(luma, 0);
    }

    //
    // Implementation of Paper "Parameterization-Independent Importance Sampling of Environment Maps"
    //
    static float squareToSphereJacobian()
    {
        return UNITY_INV_FOUR_PI;
    }

    /** Converts normalized direction to the octahedral map (equal-area, unsigned normalized).
        \param[in] n Normalized direction.
        \return Position in octahedral map in [0,1] for each component.
    */
    static float2 sphereToSquareProjection(float3 n)
    {
        n = n.xzy;

        // Use atan2 to avoid explicit div-by-zero check in atan(y/x).
        float r = sqrt(1.f - abs(n.z));
        float phi = atan2(abs(n.y), abs(n.x));

        // Compute p = (u,v) in the first quadrant.
        float2 p;
        p.y = r * phi * (2 * UNITY_INV_PI);
        p.x = r - p.y;

        // Reflect p over the diagonals, and move to the correct quadrant.
        if (n.z < 0.f) p = 1.f - p.yx;
        p *= sign(n.xy);

        return p * 0.5f + 0.5f;
    }

    /** Converts point in the octahedral map to normalized direction (equal area, unsigned normalized).
        \param[in] p Position in octahedral map in [0,1] for each component.
        \return Normalized direction.
    */
    static float3 squareToSphereProjection(float2 p)
    {
        p = p * 2.f - 1.f;

        // Compute radius r without branching. The radius r=0 at +z (center) and at -z (corners).
        float d = 1.f - (abs(p.x) + abs(p.y));
        float r = 1.f - abs(d);

        // Compute phi in [0,pi/2] (first quadrant) and sin/cos without branching.
        // TODO: Analyze fp32 precision, do we need a small epsilon instead of 0.0 here?
        float phi = (r > 0.f) ? ((abs(p.y) - abs(p.x)) / r + 1.f) * (UNITY_PI / 4) : 0.f;

        // Convert to Cartesian coordinates. Note that sign(x)=0 for x=0, but that's fine here.
        float f = r * sqrt(2.f - r * r);
        float x = f * sign(p.x) * cos(phi);
        float y = f * sign(p.y) * sin(phi);
        float z = sign(d) * (1.f - r * r);

        float3 n = float3(x, y, z);
        return n.xzy;
    }

    static float3 _sampleEnvmap(Texture2D<float4> tex, float3 dir)
    {
        float2 uv = dirToSphericalCrd(dir);

#if SHADER_STAGE_FRAGMENT
        float3 color = tex.Sample(inline_sampler_trilinear_repeatU_clampV, uv).xyz;
#else
        float3 color = tex.SampleLevel(inline_sampler_linear_repeatU_clampV, uv, 0).xyz;
#endif

#if SHADEROPTIONS_UNITY_PROJECT_GAMMA_COLOR_SPACE
        color = GammaToLinearSpace(color);
#endif
        return color;
    }
};

EnvironmentLight EnvironmentLight_(Texture2D<float4> envmap,
                                   Texture2D<float> importanceMap,
                                   float3 intensity = float3(1, 1, 1),
                                   float4 localToWorld = float4(1, 0, 0, 1),
                                   float sceneBoundingSphereRadius = 1e5)
{
    EnvironmentLight light;
    light.envmap = envmap;
    light.distr = Hierarchical2D_(importanceMap);
    light.intensity = intensity;
    light.localToWorld = localToWorld;
    light.worldToLocal = half4(localToWorld.x, -localToWorld.y, -localToWorld.z, localToWorld.w);
    light.sceneBoundingSphereRadius = sceneBoundingSphereRadius;
    return light;
}

#endif
