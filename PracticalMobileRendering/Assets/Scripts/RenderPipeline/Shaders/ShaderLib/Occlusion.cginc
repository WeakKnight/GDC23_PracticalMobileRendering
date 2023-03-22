#ifndef _OCCLUSION_CGINC_
#define _OCCLUSION_CGINC_

#include "GlobalConfig.cginc"
#include "TypeDecl.cginc"
#include "QuasiRandom.cginc"
#include "BRDF.cginc"
#include "bsdfs/MicrofacetDistribution.cginc"


TEXTURE2D(s_SpecularOcclusionLut3D);
SAMPLER(samplers_SpecularOcclusionLut3D);


#define _SPECULAR_OCCLUSION_CONE_BRDF_BRUTE_FORCE   1
#define _SPECULAR_OCCLUSION_CONE_BRDF_4D_ALU        2
#define _SPECULAR_OCCLUSION_CONE_BRDF_3D_ALU        3
#define _SPECULAR_OCCLUSION_CONE_BRDF_3D_LUT        4
#define _SPECULAR_OCCLUSION_CONE_ALU                5
#define _SPECULAR_OCCLUSION_IMPL                    _SPECULAR_OCCLUSION_CONE_BRDF_3D_LUT


/*
Reference: Deferred Lighting in Uncharted 4
*/
half GGXRoughnessToConeCosAngle(half roughness)
{
	if (roughness <= 0.565213)
		return min(0.1925 * log2(-72.56 * roughness + 42.03), 0.999);
	else
		return 0.0005;
}

half segmentSolidAngle(half cosTheta1, half cosTheta2, half cosAlpha, half sinAlpha)
{
	half sinTheta1 = sqrt(clamp(1.0 - cosTheta1 * cosTheta1, 0.0001, 1.0));

	half ty = cosTheta2 - cosAlpha * cosTheta1;
	half tx = sinAlpha * cosTheta1;

	half cosPhi = clamp(ty * cosTheta1 / (tx * sinTheta1), -1.0, 1.0);
	half cosBeta = clamp(ty / (sinTheta1 * sqrt(tx * tx + ty * ty)), -1.0, 1.0);

	return acosFast(cosBeta) - acosFast(cosPhi) * cosTheta1;
}

/*
Reference: Solid Angle of Conical Surfaces, Polyhedral Cones, and  Intersecting Spherical Caps
*/
half solidAngleConeIntersectCone(half3 dir1, half3 dir2, half angle1, half cosTheta2)
{
	half cosAlpha = dot(dir1, dir2);
	half cosTheta1 = cos(angle1);

	if (angle1 >= M_HALF_PI)
	{
		cosAlpha = -cosAlpha;
		cosTheta1 = -cosTheta1;
	}

	half sinAlpha = sqrt(clamp(1.0 - cosAlpha * cosAlpha, 0.0001, 1.0));

	return (segmentSolidAngle(cosTheta1, cosTheta2, cosAlpha, sinAlpha) +
			segmentSolidAngle(cosTheta2, cosTheta1, cosAlpha, sinAlpha));
}

/*
Reference: Ambient Aperture Lighting
*/
half approxSolidAngleConeIntersectCone(half3 dir1, half3 dir2, half angle1, half angle2, half alpha)
{
	angle1 = min(M_HALF_PI, angle1);
	half minAngle = min(angle1, angle2);
	half full = M_TWO_PI * (1.0 - cos(minAngle));
	if (alpha <= max(angle1, angle2) - minAngle)
		return full;

	half absDiff = abs(angle1 - angle2);
	half factor = (alpha - absDiff) / (angle1 + angle2 - absDiff);

	return full * smoothstep(0.0, 1.0, 1.0 - factor);
}

half evalAmbientOcclusion(in VisibilityCone vCone, in half3 normal)
{
    // https://www.iquilezles.org/www/articles/sphereao/sphereao.htm
    // theta: angle between normal and visibility direction
    // alpha: cone angle, range from [0~1], 1 map to half pi

    half cosTheta = saturate(dot(vCone.direction, normal));

    // Trick to workaround the normal mapping
    // For a fully visibly cone, if the perturbed normal shift 90 angle with geometry normal, half of the cone is still visible.
    half cosTheta_ = lerp(cosTheta, cosTheta * 0.5 + 0.5, vCone.aperture);

    half alpha = vCone.aperture;
#if 1
    // cheap approximation of (1 - 1/(1 + tan(alpha)^2))
    half occlusion = alpha;
#else
    half tanAlpha = tan(alpha * UNITY_HALF_PI);
    half occlusion =  1 - 1/(1 + tanAlpha*tanAlpha);
#endif

    return cosTheta_ * occlusion * vCone.scale;
}

float integrateConeBrdf4D(float alphaV, float beta, float roughness, float thetaO)
{
    float thetaC = thetaO - beta;

    float3 B = float3(sin(thetaC), 0, cos(thetaC));
    float3 Nx = float3(0, 0, 1);
    float3 Wr = float3(sin(thetaO), 0, cos(thetaO));

    Frame localFrame;
    if (dot(Nx, Wr) >= 0.99)
    {
        // ignore Wr vector if it's too closed ot Nx
        localFrame = buildCoordinateFrame(Nx);
    }
    else
    {
        localFrame = buildCoordinateFrame(Nx, Wr);
    }
    float cosThetaO = cos(thetaO);
    float sinThetaO = sqrt(1-cosThetaO*cosThetaO);
    float3 V = localFrame.normal * cosThetaO - localFrame.tangent * sinThetaO;

    int sampleCount = 256;
    
    float accumValue = 0;
    float accumWeight = 0;

    UNITY_LOOP
    for (int i = 0; i < sampleCount; ++i)
    {
        float2 sample_ = hammersley2D(i, sampleCount);

        MicrofacetReflection bsdf = MicrofacetReflection_(true, linearRoughnessToAlpha(roughness));
        BSDFSamplingRecord bRec = BSDFSamplingRecord_(localFrame.toLocal(V));
        float sampleWeight = bsdf.sample(bRec, sample_);
        sampleWeight *= Frame_cosTheta(bRec.wi);
        float3 sampleDirection = localFrame.toWorld(bRec.wi);

        float cosThetaI = Frame_cosTheta(bRec.wi);
        if (cosThetaI > 0)
        {
            float visible = acos(dot(B, sampleDirection)) < alphaV * UNITY_HALF_PI ? 1.0 : 0.0;
            accumValue += visible * sampleWeight;
            accumWeight += sampleWeight;
        }
    }

    if (accumWeight > 0)
        return accumValue / accumWeight;
    else
        return 0;
}

half integrateConeBrdf3DLut(TEXTURE2D_PARAM(lutTex, lutSamp), half alphaV, half cosBeta, half roughness)
{
    const half lutSize = SHADEROPTIONS_SPECULAR_OCCLUSION_LUT_SIZE;

    half3 params = half3(roughness, cosBeta, alphaV);
    params = remap01(params, half3(MIN_LINEAR_ROUGHNESS, -1, 0), half3(1, 1, 1));

    half3 lutSt = params * (lutSize - 1);

    half zID1 = floor(lutSt.z);
    half zID2 = min(zID1 + 1.0, (lutSize - 1));
    half factor = lutSt.z - zID1;

    half2 stZ1 = lutSt.xy + half2(zID1 * lutSize, 0);
    half2 stZ2 = lutSt.xy + half2(zID2 * lutSize, 0);

    half lutZ1 = SAMPLE_TEXTURE2D(lutTex, lutSamp, (stZ1 + 0.5) / half2(lutSize*lutSize, lutSize)).x;
    half lutZ2 = SAMPLE_TEXTURE2D(lutTex, lutSamp, (stZ2 + 0.5) / half2(lutSize*lutSize, lutSize)).x;
    return lerp(lutZ1, lutZ2, factor);
}

float integrateConeBrdf3D(float alphaV, float cosBeta, float roughness)
{
    float beta = acos(cosBeta);

    float thetaC_0 = -beta/2;
    float thetaO_0 = beta/2; 

    float thetaO_1 = min(beta, M_PI/2);
    float thetaC_1 = thetaO_1 - beta;

    // 0.78 is a magic number that manually tweak for our reference implementation
    float factor = 0.78;
    float thetaO = lerp(thetaO_0, thetaO_1, factor);
    float thetaC = lerp(thetaC_0, thetaC_1, factor);


    float3 B = float3(sin(thetaC), 0, cos(thetaC));
    float3 Nx = float3(0, 0, 1);
    float3 Wr = float3(sin(thetaO), 0, cos(thetaO));

    Frame localFrame;
    if (dot(Nx, Wr) >= 0.99)
    {
        // ignore Wr vector if it's too closed ot Nx
        localFrame = buildCoordinateFrame(Nx);
    }
    else
    {
        localFrame = buildCoordinateFrame(Nx, Wr);
    }
    float cosThetaO = cos(thetaO);
    float sinThetaO = sqrt(1-cosThetaO*cosThetaO);
    float3 V = localFrame.normal * cosThetaO - localFrame.tangent * sinThetaO;

    int sampleCount = 256;
    
    float accumValue = 0;
    float accumWeight = 0;

    UNITY_LOOP
    for (int i = 0; i < sampleCount; ++i)
    {
        float2 sample_ = hammersley2D(i, sampleCount);

        MicrofacetReflection bsdf = MicrofacetReflection_(true, linearRoughnessToAlpha(roughness));
        BSDFSamplingRecord bRec = BSDFSamplingRecord_(localFrame.toLocal(V));
        float sampleWeight = bsdf.sample(bRec, sample_);
        sampleWeight *= Frame_cosTheta(bRec.wi);
        float3 sampleDirection = localFrame.toWorld(bRec.wi);

        float cosThetaI = Frame_cosTheta(bRec.wi);
        if (cosThetaI > 0)
        {
            float visible = acos(dot(B, sampleDirection)) < alphaV * UNITY_HALF_PI ? 1.0 : 0.0;
            accumValue += visible * sampleWeight;
            accumWeight += sampleWeight;
        }
    }

    if (accumWeight > 0)
        return accumValue / accumWeight;
    else
        return 0;
}

float integrateConeBrdf3D(float3 texcoord)
{
    const float lutSize = SHADEROPTIONS_SPECULAR_OCCLUSION_LUT_SIZE;

    // map [0.5/size, 1-0.5/size] -> [0,1]
    float3 params = remap01(texcoord, 0.5/lutSize, 1-0.5/lutSize);

    // 0.089 ~ 1
    float roughness = MIN_LINEAR_ROUGHNESS + params.x * (1 - MIN_LINEAR_ROUGHNESS);
    // -1 ~ 1
    float cosBeta = params.y * 2 - 1;
    // 0 ~ 1
    float alphaV = params.z;

    return integrateConeBrdf3D(alphaV, cosBeta, roughness);
}

half integrateConeBrdfBruteForce(in half3 normalWS, in half3 coneDir, in half coneAngle, in half3 Wo, in half linearRoughness)
{
    Frame localFrame = buildCoordinateFrame(normalWS);

    int sampleCount = 256;

    float accumValue = 0;
    float accumWeight = 0;

    UNITY_LOOP
    for (int i = 0; i < sampleCount; ++i)
    {
        float2 sample_  = hammersley2D(i, sampleCount);

        MicrofacetReflection bsdf = MicrofacetReflection_(true, linearRoughnessToAlpha(linearRoughness));
        BSDFSamplingRecord bRec = BSDFSamplingRecord_(localFrame.toLocal(Wo));
        float sampleWeight = bsdf.sample(bRec, sample_);
        sampleWeight *= Frame_cosTheta(bRec.wi);
        float3 sampleDirection = localFrame.toWorld(bRec.wi);

        float cosThetaI = Frame_cosTheta(bRec.wi);
        if (cosThetaI > 0)
        {
            float visible = dot(sampleDirection, coneDir) > cos(coneAngle * M_PI * 0.5) ? 1.0 : 0.0;
            accumValue += visible * sampleWeight;
            accumWeight += sampleWeight;
        }
    }

    if (accumWeight > 0)
        return accumValue / accumWeight;
    else
        return 0;
}

// Evaluate equation 14 of http://iryoku.com/downloads/Practical-Realtime-Strategies-for-Accurate-Indirect-Occlusion.pdf 
// Assume isotropic GGX NDF
half evalSpecularOcclusion(in half3 viewDirectionWS, in half3 normalWS, in half3 reflectDirectionWS,
                             in VisibilityCone vCone, in half linearRoughness)
{
    half so = 0;

#if _SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_ALU

    half brdfConeCosAngle = GGXRoughnessToConeCosAngle(linearRoughness);
    half alpha = acosFast(dot(vCone.direction, reflectDirectionWS));
    half intersectSolidAngle = approxSolidAngleConeIntersectCone(vCone.direction,
                                                                   reflectDirectionWS,
                                                                   vCone.aperture * M_HALF_PI,
                                                                   acosFast(brdfConeCosAngle),
                                                                   alpha);

    half brdfSolidAngle = M_TWO_PI * (1 - brdfConeCosAngle);
    so = intersectSolidAngle / brdfSolidAngle;

#elif _SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_BRDF_BRUTE_FORCE
    so = integrateConeBrdfBruteForce(normalWS, vCone.direction, vCone.aperture, viewDirectionWS, linearRoughness);
#elif _SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_BRDF_4D_ALU

    float3 B = vCone.direction;
    float3 Nx = normalWS;
    float3 Wr = reflectDirectionWS;

    float alphaV = vCone.aperture;
    float beta = acos(dot(B, Wr));
    float roughness = linearRoughness;
    float thetaO = acos(dot(Nx, Wr));
    so = integrateConeBrdf4D(alphaV, beta, roughness, thetaO);

#elif (_SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_BRDF_3D_ALU) || (_SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_BRDF_3D_LUT)

    half3 B = vCone.direction;
    half3 Nx = normalWS;
    half3 Wr = reflectDirectionWS;

    half alphaV = vCone.aperture;
    half cosBeta = dot(B, Wr);
    half roughness = linearRoughness;

    #if _SPECULAR_OCCLUSION_IMPL == _SPECULAR_OCCLUSION_CONE_BRDF_3D_ALU
        so = integrateConeBrdf3D(alphaV, cosBeta, roughness);
    #else
        so = integrateConeBrdf3DLut(TEXTURE2D_ARGS(s_SpecularOcclusionLut3D, samplers_SpecularOcclusionLut3D), alphaV, cosBeta, roughness);
    #endif
    so *= saturate(dot(B, normalWS));

#endif

	so *= vCone.scale;
	return so;
}

#endif
