#ifndef _IMPORTANCE_SAMPLING_CGINC_
#define _IMPORTANCE_SAMPLING_CGINC_

#include "GlobalConfig.cginc"
#include "ShaderUtilities.cginc"
#include "BRDF.cginc"

float powerHeuristicMIS(int nf, float fPdf, int ng, float gPdf)
{
    float f = nf * fPdf, g = ng * gPdf;
    return (f * f) / (f * f + g * g);
}


/*
 * GGX distribution
 */
float3 squareToGgxDistribution(float2 u, float alpha)
{
    float a2 = alpha * alpha;

    float phi = M_PI * 2 * u.x;
    float cosTheta = sqrt(max(0, (1 - u.y)) / (1 + (a2 - 1) * u.y));
    float sinTheta = sqrt(max(0, 1 - cosTheta * cosTheta));
    return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

float squareToGgxDistributionPdf(float3 v, float alpha)
{
    float cosTheta = saturate(Frame_cosTheta(v));
    return NDF_GGX(alpha, cosTheta) * cosTheta;
}


/*
 * Uniform sphere
 */
float3 squareToUniformSphere(float2 u)
{
    float theta = acos(1 - 2 * u.x);
    float phi = u.y * 2 * M_PI;

    float cosTheta = cos(theta);
    float sinTheta = sqrt(max(0, 1 - cosTheta * cosTheta));
    return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

float squareToUniformSpherePdf(float3 v)
{
    return 1 / (4 * M_PI);
}

/*
 * Uniform hemisphere
 */
float3 squareToUniformHemisphere(float2 u)
{
    float phi = M_PI * 2 * u.y;
    float cosTheta = 1 - u.x;
    float sinTheta = sqrt(max(0, 1 - cosTheta * cosTheta));
    return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

float squareToUniformHemispherePdf(float3 v)
{
    if (Frame_cosTheta(v) > 0)
        return 1 / (2 * M_PI);
    else
        return 0;
}

/*
 * Cosine hemisphere
 */
float3 squareToCosineHemisphere(const float2 u)
{
    float cosTheta2 = 1 - u.x;
    float cosTheta = sqrt(cosTheta2);
    float sinTheta = sqrt(1 - cosTheta2);
    float phi = u.y * 2 * M_PI;
    return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

float squareToCosineHemispherePdf(const float3 v)
{
    return max(0, Frame_cosTheta(v)) / M_PI;
}

/*
 * Uniform disk
 */
float2 squareToUniformDisk(const float2 u)
{
    float r = sqrt(u.x);
    float theta = u.y * 2 * M_PI;
    float s, c;
    sincos(theta, s, c);
    return float2(r * c, r * s);
}

float squareToUniformDiskPdf(const float2 v)
{
    return 1 / M_PI;
}


/*
 * Sample 2D function by hierarchical sampling
 */
struct Hierarchical2D
{
    Texture2D<float> importanceMap;
    float4 texelSize;
    uint numMipLevels;

    void sampleContinuous(float2 rnd, out float2 uv, out float pdf)
    {
        uv = 0;
        pdf = 1;

        const uint maxMip = numMipLevels - 1;

        float2 p = rnd;
        uint2 pos = 0;

        // Iterate over mips of 2x2...NxN resolution.
        for (int mip = maxMip - 1; mip >= 0; mip--)
        {
            // Scale position to current mip.
            pos *= 2;

            // Load the four texels at the current position.
            float w[4];
            w[0] = importanceMap.Load(int3(pos, mip));
            w[1] = importanceMap.Load(int3(pos + uint2(1, 0), mip));
            w[2] = importanceMap.Load(int3(pos + uint2(0, 1), mip));
            w[3] = importanceMap.Load(int3(pos + uint2(1, 1), mip));

            float q[2];
            q[0] = w[0] + w[2];
            q[1] = w[1] + w[3];

            const float sum = q[0] + q[1];
            if (sum <= 0)
            {
                pdf = 0;
                return;
            }

            uint2 off;

            // Horizontal warp.
            float d = q[0] / sum;

            if (p.x < d)
            {
                // left
                pdf *= d;
                off.x = 0;
                p.x = p.x / d;
            }
            else
            {
                // right
                pdf *= (1 - d);
                off.x = 1;
                p.x = (p.x - d) / (1.f - d);
            }

            // Vertical warp.
            float e = off.x == 0 ? (w[0] / q[0]) : (w[1] / q[1]);

            if (p.y < e)
            {
                // bottom
                pdf *= e;
                off.y = 0;
                p.y = p.y / e;
            }
            else
            {
                // top
                pdf *= (1 - e);
                off.y = 1;
                p.y = (p.y - e) / (1.f - e);
            }

            pos += off;
        }

        uv = ((float2)pos + p) * texelSize.xy;
        pdf = pdf * texelSize.z * texelSize.w;
    }

    float pdfContinuous(float2 uv)
    {
        const uint maxMip = numMipLevels - 1;

        float avg_w = importanceMap.Load(int3(0, 0, maxMip));

        uint2 pos = uint2(uv * texelSize.zw);
        return importanceMap[pos] / avg_w;
    }
};

Hierarchical2D Hierarchical2D_(Texture2D<float> importanceMap)
{
    Hierarchical2D distr;
    distr.importanceMap = importanceMap;

    uint w, h;
    importanceMap.GetDimensions(0, w, h, distr.numMipLevels);
    distr.texelSize = float4(1.0/w, 1.0/h, w, h);

    return distr;
}

#endif
