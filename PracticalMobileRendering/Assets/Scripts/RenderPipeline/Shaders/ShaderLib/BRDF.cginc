#ifndef _BRDF_CGINC_
#define _BRDF_CGINC_

#include "GlobalConfig.cginc"
#include "ShaderUtilities.cginc"

// ---- Constants
#if SHADER_STAGE_RAY_TRACING
#define MIN_LINEAR_ROUGHNESS    0.045
#define MIN_GGX_ALPHA           0.002025
#else
#define MIN_LINEAR_ROUGHNESS    0.089
#define MIN_GGX_ALPHA           0.007921
#endif

// Refer to https://google.github.io/filament/Filament.md.html#materialsystem/standardmodel, Section: Roughness remapping and clamping
half clampLinearRoughness(half linearRoughness)
{
    return max(MIN_LINEAR_ROUGHNESS, linearRoughness);
}

half linearRoughnessToAlpha(half linearRoughness)
{
    return linearRoughness * linearRoughness;
}

void getAnisotropicAlpha(half alpha, half anisotropic, out half at, out half ab)
{
    // Kulla 2017, "Revisiting Physically Based Shading At Imageworks"
    // Original formula listed below, we reformulate in MAD form
    // at = alpha * (1 + anisotropic)
    // ab = alpha * (1 - anisotropic)
    at = max(alpha + alpha * anisotropic, MIN_GGX_ALPHA);
    ab = max(alpha - alpha * anisotropic, MIN_GGX_ALPHA);
}

half NDF_GGX(half alpha, half NoH)
{
#if 0
    half a2 = alpha * alpha;
    float_t d = ((NoH * a2 - NoH) * NoH + 1);
    return min(a2 / (UNITY_PI * d * d), FP16_MAX);
#else

#if SHADER_API_MOBILE
    NoH = min(NoH, half(0.99951172));
#endif

    half a2 = alpha * alpha;
    half d = ((NoH * a2 - NoH) * NoH + 1);
    half t = alpha / d;
    return t * t * UNITY_INV_PI;
#endif
}

// Both form are equivalent in terms of math, however beblow formulation has better arithmetic precision in FP16 when NoH is close to 1 and alpha close to 0
half NDF_GGX(half alpha, half NoH, half oneMinusSquaredNoH)
{
    half a = NoH * alpha;
    half k = alpha * (1 / (a * a + oneMinusSquaredNoH));
    return (k * k * (1 / UNITY_PI));
}

half NDF_GGX_Anisotropic(half at, half ab, half NdotH, half TdotH, half BdotH)
{
    // D = 1 / (M_PI * at * ab * cosTheta^4 * (1 + tanTheta^2 * (cosPhi^2 / at^2 + sinPhi^2 / ab^2))^2)
    //   = 1 / (M_PI * at * ab * (cosTheta^2 + sinTheta^2 * (cosPhi^2 / at^2 + sinPhi^2 / ab^2))^2)
    //   = 1 / (M_PI * at * ab * (cosTheta^2 + (sinTheta * cosPhi / at)^2 + (sinTheta * sinPhi / ab)^2)^2)
    // plugin into following variable NdotH = cosTheta, TdotH = sinTheta * cosPhi, BdotH = sinTheta * sinPhi we got
    // D = 1 / (M_PI * at * ab * (NdotH^2 + (TdotH / at)^2 + (BdotH / ab)^2)^2)
    //   = (at * ab)^3 / (M_PI * (at * ab)^4 * (NdotH^2 + (TdotH / at)^2 + (BdotH / ab)^2)^2)
    //   = (at * ab)^3 / (M_PI * ((NdotH * at * ab)^2 + (TdotH * ab)^2 + (BdotH * at)^2)^2)
    // let a2 = at * ab
    // D = a2^3 / (M_PI * ((NdotH * a2)^2 + (TdotH * ab)^2 + (BdotH * at)^2)^2)
    float_t a2 = at * ab;
    float3_t d = float3_t(a2 * NdotH, ab * TdotH, at * BdotH);
    float_t d2 = dot(d, d);
    half b2 = a2 / d2;
    return a2 * b2 * b2 * (1.0 / UNITY_PI);
}

half3 fresnelSchlick(half3 f0, half3 f90, half cosTheta)
{
    half Fc = utPow5(1 - cosTheta);
    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate(50.0 * f0.g) * Fc + (1 - Fc) * f0;
}

half smithMaskingFunction(half NdotV, half alpha)
{
    half denom = 1 + sqrt(1 + alpha*alpha*(1 - NdotV*NdotV)/(NdotV*NdotV));
    return 2.0 / denom;
}

half smithAnisotropicMaskingFunction(half NdotV, half TdotV, half BdotV, half at, half ab)
{
    NdotV = abs(NdotV);
    return 2 * NdotV / (NdotV + sqrt(utPow2(TdotV*at) + utPow2(BdotV*ab) + utPow2(NdotV)));
}

half G_GGX(half NdotV, half NdotL, half alpha)
{
    return smithMaskingFunction(NdotV, alpha) * smithMaskingFunction(NdotL, alpha);
}

half G_GGX_Anisotropic(half NdotV, half TdotV, half BdotV, 
                        half NdotL, half TdotL, half BdotL,
                        half at, half ab)
{
    return smithAnisotropicMaskingFunction(NdotV, TdotV, BdotV, at, ab) * smithAnisotropicMaskingFunction(NdotL, TdotL, BdotL, at, ab);
}

// Same as G_GGX but have 1/(4*NdotV*NdotL) included
// It doesn't handle cases wi/wo within lower hemisphere since it's not possible in game rendering, do not used it for dielectric material
half Gvis_GGX(half NdotV, half NdotL, half alpha)
{
    half a2 = alpha*alpha;
    half G_V = NdotV + sqrt( (NdotV - NdotV * a2) * NdotV + a2 );
    half G_L = NdotL + sqrt( (NdotL - NdotL * a2) * NdotL + a2 );
    return rcp( G_V * G_L );
}

half Gvis_GGX_Anisotropic(half NdotV, half TdotV, half BdotV, 
                           half NdotL, half TdotL, half BdotL,
                           half at, half ab)
{
    half G_V = (NdotV + sqrt(utPow2(TdotV*at) + utPow2(BdotV*ab) + utPow2(NdotV)));
    half G_L = (NdotL + sqrt(utPow2(TdotL*at) + utPow2(BdotL*ab) + utPow2(NdotL)));
    return rcp( G_V * G_L );
}

half3 evalSpecularBRDF(half alpha, half3 specular, half NdotV, half NdotL, half NdotH, half LdotH, half oneMinusSquaredNoH)
{
#if 1
    half D = NDF_GGX(alpha, NdotH, oneMinusSquaredNoH);
#else
    half D = NDF_GGX(alpha, NdotH);
#endif
    half Gvis = Gvis_GGX(NdotV, NdotL, alpha);
    half3 F = fresnelSchlick(specular, 1, LdotH);
    return F * (D * Gvis);
}

half3 evalAnisotropicSpecularBRDF(half alpha, half anisotropic, half3 specular, half NdotV, half NdotL, half NdotH, half LdotH,
                                   half3 V, half3 L, half3 H, half3 T, half3 B)
{
    half at, ab;
    getAnisotropicAlpha(alpha, anisotropic, at, ab);

    half TdotH = dot(T, H);
    half BdotH = dot(B, H);
    half TdotV = dot(T, V);
    half BdotV = dot(B, V);
    half TdotL = dot(T, L);
    half BdotL = dot(B, L);

    half D = NDF_GGX_Anisotropic(at, ab, NdotH, TdotH, BdotH);
    half Gvis = Gvis_GGX_Anisotropic(NdotV, TdotV, BdotV, NdotL, TdotL, BdotL, at, ab);
    half3 F = fresnelSchlick(specular, 1, LdotH);
    return F * min((D * Gvis), FP16_MAX);
}

half3 evalDiffuseBRDF(half3 albedo)
{
    return albedo / UNITY_PI;
}

#endif
