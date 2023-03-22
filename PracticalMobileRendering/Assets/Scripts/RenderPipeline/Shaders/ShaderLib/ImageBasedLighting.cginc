#ifndef _IMAGE_BASED_LIGHTING_CGINC_
#define _IMAGE_BASED_LIGHTING_CGINC_

#include "GlobalConfig.cginc"
#include "ShaderUtilities.cginc"
#include "QuasiRandom.cginc"
#include "ImportanceSampling.cginc"
#include "BRDF.cginc"
#include "SphericalHarmonics.cginc"
#include "ColorSpace.cginc"
#include "Lights/EnvironmentLight.hlsl"
#include "bsdfs/MicrofacetDistribution.cginc"


half4 encodeIBLColor(half3 color)
{
#if SHADEROPTIONS_IBL_COLOR_ENCODING == COLORENCODING_RGBM
    return encodeRGBM(color);
#elif SHADEROPTIONS_IBL_COLOR_ENCODING == COLORENCODING_HDR
    return half4(color,1.0);
#else
#error unknow color encoding
#endif
}

half3 decodeIBLColor(half4 color)
{
#if SHADEROPTIONS_IBL_COLOR_ENCODING == COLORENCODING_RGBM
    return decodeRGBM(color);
#elif SHADEROPTIONS_IBL_COLOR_ENCODING == COLORENCODING_HDR
    return color.xyz;
#else
#error unknow color encoding
#endif       
}

// Refer to https://seblagarde.files.wordpress.com/2015/07/course_notes_moving_frostbite_to_pbr_v32.pdf P63 and https://zero-radiance.github.io/post/split-sum/ for derivation
float4 integrateSpecularLd(TEXTURE2D_PARAM(envmap, samp), float3 texDimensions, uint sampleCount, float3 V, float3 N, float alpha)
{
    // Resource Dimensions
    float width, height, mipCount;
    width = texDimensions.x;
    height = texDimensions.y;
    mipCount = texDimensions.z;
    //_MainTex.GetDimensions(0, width, height, mipCount);

    float3 accumulateValue = 0;
    float accumulateWeight = 0;
    float NdotV = saturate(dot(N, V));
    
    Frame frame = buildCoordinateFrame(N);

    for (uint i = 0; i < sampleCount; i++)
    {
        float2 u = hammersley2D(i, sampleCount);
        float3 H = frame.toWorld(squareToGgxDistribution(u, alpha));
        float3 L = reflect(-V, H);
        float NdotL = dot(N, L);

        if(NdotL > 0)
        {
            float NdotH = saturate(dot(N, H));
            float LdotH = saturate(dot(L, H));

            float pdf = squareToGgxDistributionPdf(frame.toLocal(H), alpha) / (4 * LdotH);

            // https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch20.html
            // - OmegaS: Solid angle associated to a sample
            // - OmegaP: Solid angle associated to a pixel of the input texture
            float omegaS = 1 / (sampleCount * pdf);
            float omegaP = 4.0 * M_PI / (width * height);
            float mipLevel = clamp(0.5 * log2(omegaS / omegaP), 0, mipCount);

            float2 uv = dirToSphericalCrd(L);
            float3 Li = SAMPLE_TEXTURE2D_LOD(envmap, samp, uv, mipLevel).rgb;
#if SHADEROPTIONS_UNITY_PROJECT_GAMMA_COLOR_SPACE
            Li = GammaToLinearSpace(Li);
#endif
            float weight = NdotL;

            accumulateValue += Li * weight;
            accumulateWeight += weight;
        }
    }

    return encodeIBLColor(accumulateValue / accumulateWeight);
}

float4 integrateSpecularLdMIS(EnvironmentLight envmap, uint sampleCount, float3 V, float3 N, float alpha)
{
    float3 accumulateValue = 0;
    float accumulateWeight = 0;

    Frame frame = buildCoordinateFrame(N);
    float4 u = nthSampleR4Sequence(0);

    for (uint i = 0; i < sampleCount; i++)
    {
        float2 u0 = u.xy;
        float2 u1 = u.zw;

        MicrofacetReflection bsdf = MicrofacetReflection_(true, alpha);

        // Sample BRDF
        {
            BSDFSamplingRecord bRec = BSDFSamplingRecord_(frame.toLocal(V));
            float bsdfWeight = bsdf.sample(bRec, u0);

            if (Frame_cosTheta(bRec.wi) > 0 && bsdfWeight > 0)
            {
                float3 lightDirection = frame.toWorld(bRec.wi);

                float3 Li = envmap.Le(lightDirection);
                float lightPdf = envmap.pdf(lightDirection);

                float misWeight = powerHeuristicMIS(1, bsdf.pdf(bRec), 1, lightPdf);

                accumulateValue += Li * Frame_cosTheta(bRec.wi) * bsdfWeight * misWeight;
                accumulateWeight += Frame_cosTheta(bRec.wi) * bsdfWeight;
            }
        }

        // Sample light
        {
            LightSamplingRecord lightRec = envmap.sample(u1);
            BSDFSamplingRecord bsdfRec = BSDFSamplingRecord_(frame.toLocal(V),
                                                             frame.toLocal(lightRec.rayDirection));

            if (Frame_cosTheta(bsdfRec.wi) && lightRec.pdf > 0)
            {
                float bsdfPdf = bsdf.pdf(bsdfRec);

                float3 Li = lightRec.Le;
                float lightPdf = lightRec.pdf;

                if (bsdfPdf > 0)
                {
                    float misWeight = powerHeuristicMIS(1, lightPdf, 1, bsdfPdf);
                    accumulateValue += Li * bsdf.eval(bsdfRec) * (Frame_cosTheta(bsdfRec.wi) / lightPdf * misWeight);
                }
            }
        }

        u = nextSampleR4Sequence(u);
    }

    return encodeIBLColor(accumulateValue / accumulateWeight);
}

float4 integrateDFG(uint sampleCount, float3 N, float3 V, float linearRoughness)
{
    const float alpha = linearRoughnessToAlpha(linearRoughness);

    float NdotV = saturate(dot(N, V));
    float2 accumulation = 0;

    Frame frame = buildCoordinateFrame(N);

    for(uint i = 0; i < sampleCount; i++)
    {
        float2 u = hammersley2D(i, sampleCount);

        // Specular GGX DFG integration (stored in RG)
        float3 H = frame.toWorld(squareToGgxDistribution(u, alpha));
        float3 L = reflect(-V, H);

        float NdotH = saturate(dot(N, H));
        float LdotH = saturate(dot(L, H));
        float NdotL = saturate(dot(N, L));

        float G = G_GGX(NdotV, NdotL, alpha);
        if(NdotL > 0 && G > 0)
        {
            float GVis = (G * LdotH) / (NdotV * NdotH);
            float Fc = fresnelSchlick(0, 1, LdotH).r;
            accumulation.r += (1 - Fc) * GVis;
            accumulation.g += Fc * GVis;
        }
    }

    return float4(accumulation / float(sampleCount), 0, 1);
}

half4 integrateDFGApprox(half NoV, half linearRoughness)
{
    // Refer to https://www.unrealengine.com/en-US/blog/physically-based-shading-on-mobile
	const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
	const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
	half4 r = linearRoughness * c0 + c1;
	half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
	half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
    return half4(AB, 0, 1);
}

half4 integrateDFGApprox(half3 N, half3 V, half linearRoughness)
{
    half NoV = saturate(dot(N, V));
    return integrateDFGApprox(NoV, linearRoughness);
}

half3 getSpecularDominantDir(half3 N, half3 R, half alpha)
{
    // The result is not normalized as we fetch from cubemap
    half a2 = alpha * alpha;
    return lerp(R, N, a2);

}

// The *approximated* version of the non-linear remapping. It works by
// approximating the cone of the specular lobe, and then computing the MIP map level
// which (approximately) covers the footprint of the lobe with a single texel.
// Improves the perceptual roughness distribution.
// Linear roughness (0.089) is mapped to mip 1 (128x128 per cube face) in our configuration.
// Mipmap offset is used to compensate the size of cubemap so that certain sized cubemap will always have the same roughness regardless
// of how may mips are used, high rez cubemap just allows sharper reflection to be supported
half linearRoughnessToMipmapLevel(half linearRoughness, half mipmapOffset)
{
    linearRoughness = linearRoughness * (1.7 - 0.7 * linearRoughness);
    return max(0, linearRoughness * (SHADEROPTIONS_IBL_NUM_OF_MIP_LEVELS_IN_USE - 1) + mipmapOffset);
}

// The inverse of the *approximated* version of linearRoughnessToMipmapLevel().
float mipmapLevelToLinearRoughness(float mipmapLevel)
{
    float linearRoughness = saturate(mipmapLevel / (SHADEROPTIONS_IBL_NUM_OF_MIP_LEVELS_IN_USE - 1));

    return saturate(1.7 / 1.4 - sqrt(2.89 - 2.8 * linearRoughness) / 1.4);
}

half3 evalSpecularIBL(half3 ld, half3 specularColor, half NdotV, half linearRoughness)
{
    half2 dfg = integrateDFGApprox(NdotV, linearRoughness).xy;

    // Anything less than 2% is physically impossible and is instead considered to be shadowing 
    return ld * (specularColor * dfg.x + saturate(50.0 * specularColor.g) * dfg.y);
}

half3 getPrefilterSpecularLD(TEXTURECUBE_PARAM(envmap, samp), half envmapMipOffset, half4 rotationParam,
                                   half3 N, half3 V,
                                   half linearRoughness)
{
    half mipLevel = linearRoughnessToMipmapLevel(linearRoughness, envmapMipOffset);

    // Calculate the reflection vector
    half3 L = reflect(-V, N);
    half3 dominantDir = getSpecularDominantDir(N, L, linearRoughnessToAlpha(linearRoughness));

    dominantDir = rotateInAxisY(dominantDir, rotationParam);
    return decodeIBLColor(SAMPLE_TEXTURECUBE_LOD(envmap, samp, dominantDir, mipLevel));
}

half3 getPrefilterIrradiance(SH9Color shCoeff, half4 rotationParam, half3 N, bool divPi = true)
{
    half3 irradianceDivPi = EvalSH9Irradiance(rotateInAxisY(N, rotationParam), shCoeff);
    if (!divPi)
        return irradianceDivPi * UNITY_PI;
    return irradianceDivPi;    
}

#endif
