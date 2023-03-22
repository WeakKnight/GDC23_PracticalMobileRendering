#ifndef _MICROFACET_DISTRIBUTION_CGINC_
#define _MICROFACET_DISTRIBUTION_CGINC_

#include "../GlobalConfig.cginc"
#include "../ShaderUtilities.cginc"
#include "../BRDF.cginc"
#include "../ImportanceSampling.cginc"
#include "BSDF.cginc"


float3 _sampleGGXVNDF(float3 Ve, float alpha_x, float alpha_y, float U1, float U2);


// GGX microfacet distribution
struct MicrofacetDistribution
{
    bool sampleVisible;
    bool isotropic;
    float alpha_x;
    float alpha_y;
    
    bool effectivelySmooth()
    {
        return max(alpha_x, alpha_y) < MIN_GGX_ALPHA;
    }

    float D(float3 wh)
    {
        if (isotropic)
            return NDF_GGX(alpha_x, Frame_cosTheta(wh));
        else
            return NDF_GGX_Anisotropic(alpha_x, alpha_y, wh.z, wh.x, wh.y);
    }

    float G1(float3 w)
    {
        if (isotropic)
            return smithMaskingFunction(Frame_cosTheta(w), alpha_x);
        else
            return smithAnisotropicMaskingFunction(w.z, w.x, w.y, alpha_x, alpha_y);
    }

    float G(float3 wo, float3 wi) 
    {
        if (isotropic)
            return G_GGX(Frame_cosTheta(wo), Frame_cosTheta(wi), alpha_x);
        else
            return G_GGX_Anisotropic(wo.z, wo.x, wo.y,
                                     wi.z, wi.x, wi.y,
                                     alpha_x, alpha_y);
    }

    float3 sample_wh(float3 wo, float2 u)
    {
        if (sampleVisible)
        {
            return _sampleGGXVNDF(utFaceForward(wo, float3(0, 0, 1)), alpha_x, alpha_y, u.x, u.y);
        }
        else
        {
            // TODO: support anisotropic GGX sampling
            if (isotropic)
                return squareToGgxDistribution(u, alpha_x);
            else
                return FP32_NaN;
        }
    }

    float pdf(float3 wo, float3 wh)
    {
        if (sampleVisible)
            return D(wh) * G1(wo) * abs(dot(wo, wh)) / Frame_absCosTheta(wo);
        else
            return D(wh) * Frame_absCosTheta(wh);
    }
};

MicrofacetDistribution MicrofacetDistribution_(bool sampleVisible_, float alpha_)
{
    MicrofacetDistribution distr;
    distr.sampleVisible = sampleVisible_;
    distr.isotropic = true;
    distr.alpha_x = alpha_;
    distr.alpha_y = alpha_;
    return distr;
}

MicrofacetDistribution MicrofacetDistribution_(bool sampleVisible_, float alpha_x_, float alpha_y_)
{
    MicrofacetDistribution distr;
    distr.sampleVisible = sampleVisible_;
    distr.isotropic = false;
    distr.alpha_x = alpha_x_;
    distr.alpha_y = alpha_y_;
    return distr;
};


struct MicrofacetReflection
{
    MicrofacetDistribution distr;

    uint BSDFFlags()
    {
        return distr.effectivelySmooth() ? BSDF_FLAGS_SPECULAR_REFLECTION : BSDF_FLAGS_GLOSSY_REFLECTION;
    }

    float sample(inout BSDFSamplingRecord bRec, in float2 sample_)
    {
        float3 wh = distr.sample_wh(bRec.wo, sample_);
        wh *= sign(bRec.wo.z);

        float3 wi = utReflect(bRec.wo, wh);
        if (Frame_cosTheta(bRec.wo) * Frame_cosTheta(wi) <= 0)
        {
            bRec.wi = 0;
            return 0;
        }

        bRec.wi = wi;
        bRec.bsdfFlags = distr.effectivelySmooth() ? BSDF_FLAGS_SPECULAR_REFLECTION : BSDF_FLAGS_GLOSSY_REFLECTION;

        if (distr.sampleVisible)
        {
            float cosThetaI = Frame_cosTheta(bRec.wi);
            return distr.G(bRec.wo, bRec.wi) / abs(distr.G1(bRec.wo) * cosThetaI);
        }
        else
        {
            float cosThetaO = Frame_cosTheta(bRec.wo);
            float cosThetaI = Frame_cosTheta(bRec.wi);
            float cosThetaH = Frame_cosTheta(wh);
            return distr.G(bRec.wo, bRec.wi) * dot(bRec.wi, wh) / abs(cosThetaO * cosThetaI * cosThetaH);
        }
    }

    float eval(in BSDFSamplingRecord bRec)
    {
        if (Frame_cosTheta(bRec.wo) * Frame_cosTheta(bRec.wi) <= 0)
            return 0;

        float3 wh = normalize(bRec.wo + bRec.wi);
        return distr.D(wh) * distr.G(bRec.wo, bRec.wi) / (4 * (Frame_cosTheta(bRec.wo) * Frame_cosTheta(bRec.wi)));
    }

    float pdf(in BSDFSamplingRecord bRec)
    {
        if (Frame_cosTheta(bRec.wo) * Frame_cosTheta(bRec.wi) <= 0)
            return 0;

        float3 wh = normalize(bRec.wo + bRec.wi);
        float dwh_dwi = 1.0 / (4.0 * abs(dot(bRec.wi, wh)));
#if SANITY_CHECK
        if (dwh_dwi <= 0) { return FP32_NaN; }
#endif
        return distr.pdf(bRec.wo, wh) * dwh_dwi;
    }
};

MicrofacetReflection MicrofacetReflection_(bool sampleVisible_, float alpha_)
{
    MicrofacetReflection bsdf;
    bsdf.distr = MicrofacetDistribution_(sampleVisible_, alpha_);
    return bsdf;
}

MicrofacetReflection MicrofacetReflection_(bool sampleVisible_, float alpha_x_, float alpha_y_)
{
    MicrofacetReflection bsdf;
    bsdf.distr = MicrofacetDistribution_(sampleVisible_, alpha_x_, alpha_y_);
    return bsdf;
}


struct MicrofacetTransmission
{
    MicrofacetDistribution distr;
    float eta;

    uint BSDFFlags()
    {
        return distr.effectivelySmooth() ? BSDF_FLAGS_SPECULAR_TRANSMISSION : BSDF_FLAGS_GLOSSY_TRANSMISSION;
    }

    float sample(inout BSDFSamplingRecord bRec, in float2 sample_)
    {
        float3 wh = distr.sample_wh(bRec.wo, sample_);

        float eta_p;
        float3 wi;
        bool tir = !utRefract(bRec.wo, wh, eta, eta_p, wi);
        if (dot(bRec.wo, wi) > 0)
            return 0;

        if (tir || wi.z == 0)
            return 0;

        float denom = dot(bRec.wo, wh) + eta_p * dot(wi, wh);
        denom *= denom;
        float dwh_dwi = (eta_p * eta_p) * abs(dot(wi, wh)) / denom;

        float bsdfPdf = distr.pdf(bRec.wo, wh) * dwh_dwi;

        float factor = 1 / (eta_p * eta_p);

        bRec.wi = wi;
        bRec.bsdfFlags = distr.effectivelySmooth() ? BSDF_FLAGS_SPECULAR_TRANSMISSION : BSDF_FLAGS_GLOSSY_TRANSMISSION;
        float btdf = distr.D(wh) * distr.G(bRec.wo, wi) * dwh_dwi * dot(bRec.wo, wh) * factor
                   / (Frame_cosTheta(bRec.wo) * Frame_cosTheta(bRec.wi));
        return abs(btdf) / bsdfPdf;
    }

    float eval(in BSDFSamplingRecord bRec)
    {
        float eta_p = Frame_cosTheta(bRec.wo) > 0 ? eta : 1 / eta;
        float3 wh = normalize(bRec.wo + eta_p * bRec.wi);

        // Compute transmission at non-delta dielectric interface
        // Return no transmission if _wi_ and _wo_ are on the same side of _wh_
        if (dot(bRec.wi, wh) * dot(bRec.wo, wh) > 0)
            return 0;

        // Evaluate BTDF for transmission through microfacet interface
        float denom = dot(bRec.wo, wh) + eta_p * dot(bRec.wi, wh);
        denom *= denom;

        float dwh_dwi = (eta_p * eta_p) * abs(dot(bRec.wi, wh)) / denom;

        float factor = 1 / (eta_p * eta_p);

        float v = distr.D(wh) * distr.G(bRec.wo, bRec.wi) * dwh_dwi * dot(bRec.wo, wh) * factor
                / (Frame_cosTheta(bRec.wo) * Frame_cosTheta(bRec.wi));

        return abs(v);
    }

    float pdf(in BSDFSamplingRecord bRec)
    {
        float eta_p = Frame_cosTheta(bRec.wo) > 0 ? eta : 1 / eta;
        float3 wh = normalize(bRec.wo + eta_p * bRec.wi);

        if (dot(bRec.wi, wh) * dot(bRec.wo, wh) > 0)
            return 0;

        float denom = dot(bRec.wo, wh) + eta_p * dot(bRec.wi, wh);
        denom *= denom;

        float dwh_dwi = (eta_p * eta_p) * abs(dot(bRec.wi, wh)) / denom;

        return distr.pdf(bRec.wo, wh) * dwh_dwi;
    }
};

MicrofacetTransmission MicrofacetTransmission_(bool sampleVisible_, float alpha_, float eta)
{
    MicrofacetTransmission bsdf;
    bsdf.distr = MicrofacetDistribution_(sampleVisible_, alpha_);
    bsdf.eta = eta;
    return bsdf;
}

MicrofacetTransmission MicrofacetTransmission_(bool sampleVisible_, float alpha_x_, float alpha_y_, float eta)
{
    MicrofacetTransmission bsdf;
    bsdf.distr = MicrofacetDistribution_(sampleVisible_, alpha_x_, alpha_y_);
    bsdf.eta = eta;
    return bsdf;
}


// Refer to http://jcgt.org/published/0007/04/01/
// Input Ve: view direction
// Input alpha_x, alpha_y: roughness parameters
// Input U1, U2: uniform random numbers
// Output Ne: normal sampled with PDF D_Ve(Ne) = G1(Ve) * max(0, dot(Ve, Ne)) * D(Ne) / Ve.z
float3 _sampleGGXVNDF(float3 Ve, float alpha_x, float alpha_y, float U1, float U2)
{
    // Section 3.2: transforming the view direction to the hemisphere configuration
    float3 Vh = normalize(float3(alpha_x * Ve.x, alpha_y * Ve.y, Ve.z));
    // Section 4.1: orthonormal basis (with special case if cross product is zero)
    float lensq = Vh.x * Vh.x + Vh.y * Vh.y;
    float3 T1 = lensq > 0 ? float3(-Vh.y, Vh.x, 0) * rsqrt(lensq) : float3(1, 0, 0);
    float3 T2 = cross(Vh, T1);
    // Section 4.2: parameterization of the projected area
    float r = sqrt(U1);
    float phi = 2.0 * M_PI * U2;
    float t1 = r * cos(phi);
    float t2 = r * sin(phi);
    float s = 0.5 * (1.0 + Vh.z);
    t2 = (1.0 - s) * sqrt(1.0 - t1 * t1) + s * t2;
    // Section 4.3: reprojection onto hemisphere
    float3 Nh = t1 * T1 + t2 * T2 + sqrt(max(0.0, 1.0 - t1 * t1 - t2 * t2)) * Vh;
    // Section 3.4: transforming the normal back to the ellipsoid configuration
    float3 Ne = normalize(float3(alpha_x * Nh.x, alpha_y * Nh.y, max(0.0, Nh.z)));
    return Ne;
}

#endif
