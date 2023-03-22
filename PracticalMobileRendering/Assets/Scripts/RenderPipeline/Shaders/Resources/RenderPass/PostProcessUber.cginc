#include "../../ShaderLib/GlobalConfig.cginc"
#include "../../ShaderLib/DrawQuadVS.cginc"
#include "../../ShaderLib/ShaderUtilities.cginc"
#include "../../ShadingSystem/ColorOutput.cginc"


TEXTURE2D(_BlitTex);
SAMPLER(sampler_BlitTex);
float4 _BlitTex_TexelSize;

TEXTURE2D(_BloomTex);
SAMPLER(sampler_BloomTex);

float4 _Bloom_Params;
float4 _BloomTex_TexelSize;
half _PostExposure;

#define BloomIntensity          _Bloom_Params.x
#define BloomTint               _Bloom_Params.yzw

// https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
float3 ToneMap_AcesFilmic(float3 color)
{
    float A = 2.51;
    float B = 0.03;
    float C = 2.43;
    float D = 0.59;
    float E = 0.14;

    color = saturate((color * (A * color + B)) / (color * (C * color + D) + E));
    return color;
}

half4 Frag(VsOutFullScreen input) : SV_Target
{
    float2 uv = input.texcoord;

// #if UNITY_UV_STARTS_AT_TOP
//     if (_ProjectionParams.x > 0)
//     {
//         uv.y = 1 - uv.y;
//     }
// #endif

    half4 inputTex = SAMPLE_TEXTURE2D(_BlitTex, sampler_BlitTex, uv);
    
    half3 color = inputTex.rgb;
    half alpha = inputTex.a;

    // We do pre-exposure in material shader so that it's not necessary to apply exposure in PP for most of the case
    // Unfortunately there are renderer that didn't setup this way (ex, Path tracer)
    // Anyhow it's more flexible to apply the scale in shader and do setup code in c#
    half4 EV = getExposureValue();
#if SHADEROPTIONS_PRE_EXPOSURE
    if (_PostExposure > 0)
        color *= EV.x;
    else
        color *= (EV.x * EV.w); 
#else
    color *= EV.x;
#endif

    #if defined(_BLOOM)
    {
        half4 bloom = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, uv);
        bloom.xyz *= BloomIntensity;
        color += bloom.xyz * BloomTint;
    }
    #endif

	color.rgb = ToneMap_AcesFilmic(color.rgb);

    // Vignette
    {
        const half strength = 10.0f;
        const half power = 0.12f;
        half2 tuv = uv * (half2(1.0f, 1.0f) - uv.yx);
        half vign = tuv.x * tuv.y * strength;
        vign = pow(vign, power);
        color *= vign;
    }

    half4 outColor = half4(color, alpha);
    outColor.rgb = max(outColor.rgb, 0);

    return outColor;
}
