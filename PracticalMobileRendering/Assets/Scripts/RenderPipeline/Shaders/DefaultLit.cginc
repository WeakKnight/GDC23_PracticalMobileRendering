#include "ShaderLib/GlobalConfig.cginc"
#include "ShaderLib/BRDF.cginc"
#include "ShaderLib/Occlusion.cginc"
#include "ShaderLib/ImageBasedLighting.cginc"
#include "ShadingSystem/GlobalVariables.cginc"
#include "ShadingSystem/LightLoop/LightLoop.cginc"
#include "PrecomputedLighting.cginc"

struct VertexInputData
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord0 : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
};

struct VSOut
{
	half3 normalWS : NORMAL;
	half vConeScale : CONESCALE;
	half4 tangentWS : TANGENT;
	float3 positionWS : POSITIONWS;
	float4 uv0_uv1 : TEXCOORD0;
	half4 visibilityCone : TEXCOORD1;
	float4 positionCS : SV_POSITION;
};

struct VSOutShadow
{
#if _ALPHATEST_ON
    float2 uv0 : TEXCOORD0;
#endif
    float4 positionCS : SV_POSITION;
};

struct SurfaceData
{
    half3 emissive;
    half3 diffuse;
    half3 specular;

    half opacity;

    half linearRoughness;
    half GGXAlpha;

    half occlusion;

    half specularOcclusion;
};


// ---- Shadow map pass uniforms
CBUFFER_START(CBufferShadowCasterPass)
float g_OrthoCamera;
float g_ShadowNearClipPlane;
float3 g_dxCamera;
float3 g_dyCamera;
// x: slope based bias, y : slope bias clamp, z : constant bias
float3 g_DepthBias2;
float2 g_ShadowSplitRez;
CBUFFER_END


// ---- Material properties
Texture2D _BaseColorMap;
SamplerState sampler_BaseColorMap;
half4 _BaseColor;

Texture2D _MaskMap;
SamplerState sampler_MaskMap;
half4 _RoughnessChSwizzle;
half4 _MetallicChSwizzle;
half4 _OcclusionChSwizzle;

half2 _AOMinMaxRemap;
half2 _MetallicMinMaxRemap;
half2 _RoughnessMinMaxRemap;
half2 _OpacityMinMaxRemap;

Texture2D _NormalMap;
SamplerState sampler_NormalMap;

half4 _EmissiveColor;
Texture2D _EmissiveColorMap;
SamplerState sampler_EmissiveColorMap;


// ---- Material instance flags
bool MI_IsTwoSided()
{
    return false;
}

bool MI_IsVisibilityOn()
{
#if _VISIBILITY_ON
    return true;
#else
    return false;
#endif
}

// ---- Utils functions

void alphaTest(half opacity)
{
    clip(opacity - 0.23);
}

bool _lightingComponentsEnabled(uint comp)
{
    return (g_DebugFlagsLightingComponents & comp) == comp;
}

half4 sampleTexture(Texture2D tex, SamplerState texSampler, float2 uv)
{
    return tex.Sample(texSampler, uv);
}

half minMaxRemap(half v, half2 range)
{
    return v * range.y + range.x;
}

bool isOrtho()
{
    return g_OrthoCamera > 0;
}

float getDepthBiasClamp(float3 posV)
{
    if (isOrtho())
        return g_DepthBias2.y;
    else
        return g_DepthBias2.y * abs(posV.z) / g_ShadowNearClipPlane;
}

float4 getClipSpacePositionWithShadowBiasApplied(in float3 positionVS, in float3 normalVS)
{
    // Plane Eq: dot(N, P) = d
    float d = dot(normalVS, positionVS);

    float3 rayDirection = 0;
    float3 rxIntersection = 0;
    float3 ryIntersection = 0;

    UNITY_BRANCH
    if (isOrtho())
    {
        rayDirection = float3(0, 0, -1);

        rxIntersection = positionVS + g_dxCamera;
        ryIntersection = positionVS + g_dyCamera;

        float tx = (d - dot(rxIntersection, normalVS)) / dot(rayDirection, normalVS);
        float ty = (d - dot(ryIntersection, normalVS)) / dot(rayDirection, normalVS);

        rxIntersection += rayDirection * tx;
        ryIntersection += rayDirection * ty;
    }
    else
    {
        rayDirection = normalize(positionVS);

        // near clip plane has no signed, and unity use right handed viewing system
        float3 nearPlaneIntersection = rayDirection / rayDirection.z * (-g_ShadowNearClipPlane);
        rxIntersection = nearPlaneIntersection + g_dxCamera;
        ryIntersection = nearPlaneIntersection + g_dyCamera;

        rxIntersection *= d / dot(rxIntersection, normalVS);
        ryIntersection *= d / dot(ryIntersection, normalVS);
    }

    // Calculate max depth slope
    float dzdx = rxIntersection.z - positionVS.z;
    float dzdy = ryIntersection.z - positionVS.z;
    float dz = max(max(abs(dzdx), abs(dzdy)), abs(dzdx + dzdy));

    // Offset view space position in ray direction
    float depthBiasClamp = getDepthBiasClamp(positionVS);
    positionVS += min(g_DepthBias2.x * dz, depthBiasClamp) * rayDirection;

    // Calculate clip space position
    float4 positionCS = mul(UNITY_MATRIX_P, float4(positionVS, 1));
    return positionCS;
}

half3 backfacedVisibilityDirection(Frame geoFrame, half3 vDirection, bool isFrontFace)
{
    // Assume cone visibility direction is always toward the upper hemisphere
    // we should negate toward geometric normal direction in backfaced case
    const half scaleSign = isFrontFace ? 0 : -2;
    return vDirection + geoFrame.normal * (dot(vDirection, geoFrame.normal) * scaleSign);
}

void biasVisibilityByNormal(half3 geoNormal, half3 shNormal, inout VisibilityCone vCone)
{
    half3 vConeBias = shNormal - dot(shNormal, geoNormal) * geoNormal;
    half3 vConeDirectionBiased = normalize(vConeBias + vCone.direction);

    half cosTheta = dot(vCone.direction, vConeDirectionBiased);
    half bumpIntensity = 0.5;
    vCone.aperture = saturate(vCone.aperture - (0.5 - cosTheta * 0.5) * bumpIntensity);

    vCone.direction = vConeDirectionBiased;
}

half3 getEnvmapIrradiance(half3 direction, bool averageIrradiance = true)
{
    SH9Color coeff = getEnvmapSH9Coefficients();
    if (averageIrradiance)
        return coeff.c[0] * (0.5 * sqrt(UNITY_PI));
    else
        return getPrefilterIrradiance(coeff, g_EnvmapRotationParam, direction);
}

Intersection buildIntersection(in VSOut vsOut, in half4 normal, in bool isFrontFace)
{
    Intersection its;

    its.lightmapUV = vsOut.uv0_uv1.zw;

    its.posW = vsOut.positionWS;

    // Setup screen space coordinate
    its.posVP = vsOut.positionCS.xy;
    its.screenUV = its.posVP * g_ScreenParams.zw;

    its.depth = vsOut.positionCS.z;

    half flipNormal = (!MI_IsTwoSided() || isFrontFace) ? 1.0 : -1.0;
    half flipBitangent = vsOut.tangentWS.w;

    // we do have asset with negative scale
    half oddNegativeScale = unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0;
    flipBitangent *= oddNegativeScale;

    // Geometric frame
    its.geoFrame = buildCoordinateFrame(normalize(vsOut.normalWS), vsOut.tangentWS.xyz);
    its.geoFrame.normal *= flipNormal;
    its.geoFrame.bitangent *= flipBitangent;

    // Tangent space (1 or 0) or World space (-1) normal
    half3 normalWS = normal.w < 0.0 ? normal.xyz : its.geoFrame.toWorld(normal.xyz);

    // Shading frame
    its.shFrame = buildCoordinateFrame(normalWS, its.geoFrame.tangent);
    its.shFrame.bitangent *= (flipNormal * flipBitangent);

    its.bitangentSign = (flipNormal * flipBitangent);

    its.V = normalize(_WorldSpaceCameraPos - vsOut.positionWS);
    its.NoV = saturate(dot(its.V, its.shFrame.normal));
    its.R = reflect(-its.V, its.shFrame.normal);

    its.vCone = VisibilityCone_(normalize(vsOut.visibilityCone.xyz), vsOut.visibilityCone.w, vsOut.vConeScale);

    return its;
}

SurfaceData getSurfaceData(VSOut psIn, Intersection its)
{
    half2 uv0 = psIn.uv0_uv1.xy;

    half4 baseColor = sampleTexture(_BaseColorMap, sampler_BaseColorMap, uv0);
    baseColor.rgb *= _BaseColor;

    half4 mask = sampleTexture(_MaskMap, sampler_MaskMap, uv0);
    half roughness = dot(mask, _RoughnessChSwizzle);
    half metallic = dot(mask, _MetallicChSwizzle);
    half occlusion = dot(mask, _OcclusionChSwizzle);

    SurfaceData sd;

    sd.emissive = sampleTexture(_EmissiveColorMap, sampler_EmissiveColorMap,  uv0) * _EmissiveColor;
    sd.opacity = minMaxRemap(baseColor.a, _OpacityMinMaxRemap);

    // Setup linear roughness and GGX alpha
    sd.linearRoughness = clampLinearRoughness(minMaxRemap(roughness, _RoughnessMinMaxRemap));
    sd.GGXAlpha = linearRoughnessToAlpha(sd.linearRoughness);

    // Setup material appearance
    metallic = minMaxRemap(metallic, _MetallicMinMaxRemap);
    sd.diffuse = lerp(baseColor.rgb, 0, metallic);
    sd.specular = lerp(0.04f, baseColor.rgb, metallic);


    half ambientOcclusion = 1;
    half specularOcclusion = 1;
    if (MI_IsVisibilityOn())
    {
        ambientOcclusion = evalAmbientOcclusion(its.vCone, its.shFrame.normal);
        specularOcclusion = evalSpecularOcclusion(its.V, its.shFrame.normal, its.R, its.vCone, sd.linearRoughness);
    }

    sd.occlusion = minMaxRemap(occlusion, _AOMinMaxRemap) * ambientOcclusion;
    sd.specularOcclusion = specularOcclusion;

    return sd;
}

half3 evalPunctualLight(in Intersection its, in SurfaceData sd, in LightData ld)
{
    half3 specularLobe = evalSpecularBRDF(sd.GGXAlpha, sd.specular, its.NoV, ld.NoL, ld.NoH, ld.LoH, ld.oneMinusSquaredNoH);
    half3 diffuseLobe = evalDiffuseBRDF(sd.diffuse);
    return (specularLobe + diffuseLobe) * (ld.intensity * (ld.shadowFactor * ld.NoL));
}

half3 evalDiffuseGI(Intersection its, SurfaceData sd)
{
    half3 irradiance;
#if defined(_LIGHTMAP_GI)
    irradiance = sampleLightmap(its.lightmapUV, its.shFrame.normal);
#elif defined(_VLM_GI)
    irradiance = sampleVolumetricLightmap(its.posW, its.shFrame.normal);
#else
    irradiance = getPrefilterIrradiance(getEnvmapSH9Coefficients(), g_EnvmapRotationParam, its.shFrame.normal);
#endif
    return irradiance * sd.occlusion;
}

half3 evalSpecularReflection(Intersection its, SurfaceData sd)
{
    half3 specularLd = getPrefilterSpecularLD(TEXTURECUBE_ARGS(g_EnvmapFilterWithGGX, samplerg_EnvmapFilterWithGGX),
                                          g_EnvmapMipmapOffset, g_EnvmapRotationParam,
                                          its.shFrame.normal, its.V,
                                          sd.linearRoughness);

    specularLd *= g_EnvmapIntensity.xyz;
    specularLd *= sd.specularOcclusion;
    return evalSpecularIBL(specularLd, sd.specular, its.NoV, sd.linearRoughness);
}

// ---- Shader Entry functions
VSOut VertMain(VertexInputData vsIn)
{
	VSOut vsOut;

	vsOut.positionWS = utObjectToWorldPos(vsIn.vertex.xyz);
	vsOut.positionCS = mul(UNITY_MATRIX_VP, float4(vsOut.positionWS, 1));

	vsOut.normalWS = utObjectToWorldNormal(vsIn.normal);

	vsOut.tangentWS.xyz = utObjectToWorldDir(vsIn.tangent.xyz);
	vsOut.tangentWS.w = vsIn.tangent.w;

	vsOut.uv0_uv1.xy = vsIn.texcoord0.xy;
	vsOut.uv0_uv1.zw = vsIn.texcoord1.xy;

	{
		half4 visibilityCone = vsIn.texcoord2;
		half3 bitangent = cross(vsOut.normalWS, vsOut.tangentWS.xyz) * vsOut.tangentWS.w;
		half3 orthoTangent = cross(bitangent, vsOut.normalWS);
		half tangentAngle = visibilityCone.y;
		half tangentLength = sqrt(max(1 - visibilityCone.x * visibilityCone.x, 0));
		half3 coneDir = visibilityCone.x * bitangent + (cos(tangentAngle) * vsOut.normalWS + sin(tangentAngle) * orthoTangent) * tangentLength;

		vsOut.visibilityCone.xyz = coneDir;
		vsOut.visibilityCone.w = saturate(visibilityCone.z / UNITY_HALF_PI);
		vsOut.vConeScale = visibilityCone.w;
	}

	return vsOut;
}

half4 FragMain(VSOut psIn, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    half3 normalTS = normalize(UnpackNormal(sampleTexture(_NormalMap, sampler_NormalMap, psIn.uv0_uv1.xy)));

    Intersection its = buildIntersection(psIn, half4(normalTS, 0), isFrontFace);
    SurfaceData sd = getSurfaceData(psIn, its);

    sd.diffuse = sd.diffuse * g_DebugDiffuseOverrideParameter.w + g_DebugDiffuseOverrideParameter.xyz;
    sd.specular = sd.specular * g_DebugSpecularOverrideParameter.w + g_DebugSpecularOverrideParameter.xyz;

	half3 result = 0.0f;

    if (_lightingComponentsEnabled(YALIGHTINGCOMPONENT_EMISSIVE))
    {
        result += sd.emissive;
    }

    if (_lightingComponentsEnabled(YALIGHTINGCOMPONENT_DIRECT_LIGHT))
    {
        LightData ld = getDirectionalLight(its);
        ld.shadowFactor = getDirectionalLightShadow(its, ld);
        result += evalPunctualLight(its, sd, ld);
    }

    if (_lightingComponentsEnabled(YALIGHTINGCOMPONENT_DIRECT_LIGHT))
    {
        int lightStart, lightEnd;
        getPunctualLightIndexRange(its, lightStart, lightEnd);

        for (int lightIdx = lightStart; lightIdx < lightEnd; ++lightIdx)
        {
            LightData ld = getPunctualLight(its, lightIdx);

            if (any(ld.intensity) > 0)
            {
                ld.shadowFactor = getPunctualLightShadow(its, ld);
                result += evalPunctualLight(its, sd, ld);
            }
        }
    }

    half3 irradiance = evalDiffuseGI(its, sd);
    if (_lightingComponentsEnabled(YALIGHTINGCOMPONENT_DIFFUSE_GLOBAL_ILLUMINATION))
    {
        result += irradiance * sd.diffuse;
    }

    if (_lightingComponentsEnabled(YALIGHTINGCOMPONENT_SPECULAR_REFLECTION))
    {
        half3 reflectionTerm = evalSpecularReflection(its, sd);

        // IBL normalized
        if (g_EnvmapIntensity.w > 0)
        {
            half lumaEnvmap = luminance_sRGB(getEnvmapIrradiance(its.shFrame.normal));
            half lumaDiffuseGI = luminance_sRGB(irradiance);
            reflectionTerm *= lumaDiffuseGI / max(lumaEnvmap, 0.001);
        }

        result += reflectionTerm;
    }

    // Display GI without albedo 
    if (g_DebugFlagsLightingComponents == YALIGHTINGCOMPONENT_DIFFUSE_GLOBAL_ILLUMINATION)
    {
        result = irradiance;
    }

	return half4(result, 1.0f);
}

VSOutShadow VertShadowCaster(VertexInputData vsIn)
{
    VSOut vsOut = VertMain(vsIn);

    VSOutShadow o;

    float3 posV = UnityWorldToViewPos(vsOut.positionWS);
    if (isOrtho())
    {
        posV.z = step(posV.z, 0) * posV.z;
    }
    // UNITY_MATRIX_IT_V is equal to UNITY_MATRIX_V if each axis is orthogonal which is correct in most of the case
    float3 normalV = normalize(mul((float3x3)UNITY_MATRIX_V, vsOut.normalWS));
    o.positionCS = getClipSpacePositionWithShadowBiasApplied(posV, normalV);

#if _ALPHATEST_ON
    o.uv0 = vsOut.uv0_uv1.xy;
#endif

    return o;
}

half4 FragShadowCaster(VSOutShadow psIn) : SV_Target
{
#if _ALPHATEST_ON
    half opacity = sampleTexture(_BaseColorMap, sampler_BaseColorMap, psIn.uv0.xy).a;
    alphaTest(opacity);
#endif

    return 1;
}

#if CONF_SHADER_PASS == SHADERPASS_VISIBILITY_DXR

[shader("anyhit")]
void AnyHitVisibilityDXR(inout PayloadVisibilityDXR payload : SV_RayPayload, BuiltInTriangleIntersectionAttributes attributeData : SV_IntersectionAttributes)
{
    // TODO: alpha test
}

[shader("closesthit")]
void ClosestHitVisibilityDXR(inout PayloadVisibilityDXR payload : SV_RayPayload, BuiltInTriangleIntersectionAttributes attributeData : SV_IntersectionAttributes)
{
    payload.hitT = RayTCurrent();
}

#endif
