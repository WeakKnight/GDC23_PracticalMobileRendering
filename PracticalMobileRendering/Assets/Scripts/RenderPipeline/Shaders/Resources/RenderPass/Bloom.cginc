#include "../../ShaderLib/GlobalConfig.cginc"
#include "../../ShaderLib/ShaderUtilities.cginc"
#include "../../ShaderLib/DrawQuadVS.cginc"
#include "../../ShaderLib/ColorSpace.cginc"
#include "../../ShadingSystem/GlobalVariables.cginc"

#define _KILL_FIREFLY 1

TEXTURE2D(_BloomSrcTex);
SAMPLER(sampler_BloomSrcTex);
TEXTURE2D(_BloomTex);
SAMPLER(sampler_BloomTex);

float4 _BloomSrcTex_TexelSize;

half4 _Threshold; // x: threshold value (linear), y: threshold - knee, z: knee * 2, w: 0.25 / knee
half4 _Params; // x: clamp, y firefly removal strength, zw: unused
half  _SampleScale;
half2 _BloomSrcUvScale;

half _PostExposure;

#define filterStrength (_Params.y)
half3 Downsample4Tap(half3 s0, half3 s1, half3 s2, half3 s3)
{
#if SANITY_CHECK
    s0 = eliminateNaNs(s0);
    s1 = eliminateNaNs(s1);
    s2 = eliminateNaNs(s2);
    s3 = eliminateNaNs(s3);
#endif

    half3 s = 0;
#if _KILL_FIREFLY
    half w0 = 1 / (filterStrength + luminance_sRGB(s0.rgb));
    half w1 = 1 / (filterStrength + luminance_sRGB(s1.rgb));
    half w2 = 1 / (filterStrength + luminance_sRGB(s2.rgb));
    half w3 = 1 / (filterStrength + luminance_sRGB(s3.rgb));
    half w = w0 + w1 + w2 + w3;
    s = (s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3) / w;
#else
    s = s0 + s1 + s2 + s3;
    s *= 0.25;
#endif

    return s;
}

half3 DownsampleBox4Tap(TEXTURE2D_PARAM(tex, samp), half2 uv, half2 texelSize)
{
    half4 d = texelSize.xyxy * half4(-1.0, -1.0, 1.0, 1.0);

    half3 s0 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xy), 0);
    half3 s1 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zy), 0);
    half3 s2 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xw), 0);
    half3 s3 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zw), 0);

    return Downsample4Tap(s0, s1, s2, s3);
}

half3 Downsample6Tap(half3 s0, half3 s1, half3 s2, half3 s3, half3 s4, half3 s5)
{
#if SANITY_CHECK
    s0 = eliminateNaNs(s0);
    s1 = eliminateNaNs(s1);
    s2 = eliminateNaNs(s2);
    s3 = eliminateNaNs(s3);
    s4 = eliminateNaNs(s4);
    s5 = eliminateNaNs(s5);
#endif

    half3 s = 0;
#if _KILL_FIREFLY
    half w0 = 1 / (filterStrength + luminance_sRGB(s0.rgb));
    half w1 = 1 / (filterStrength + luminance_sRGB(s1.rgb));
    half w2 = 1 / (filterStrength + luminance_sRGB(s2.rgb));
    half w3 = 1 / (filterStrength + luminance_sRGB(s3.rgb));
    half w4 = 1 / (filterStrength + luminance_sRGB(s4.rgb));
    half w5 = 1 / (filterStrength + luminance_sRGB(s5.rgb));
    half w = w0 + w1 + w2 + w3 + w4 + w5;
    s = (s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3 + s4 * w4 + s5 * w5) / w;
#else
    s = s0 + s1 + s2 + s3 + s4 + s5;
    s *= 1.0 / 6;
#endif

    return s;
}
half3 Downsample8Tap(half3 s0, half3 s1, half3 s2, half3 s3, half3 s4, half3 s5, half3 s6, half3 s7)
{
#if SANITY_CHECK
    s0 = eliminateNaNs(s0);
    s1 = eliminateNaNs(s1);
    s2 = eliminateNaNs(s2);
    s3 = eliminateNaNs(s3);
    s4 = eliminateNaNs(s4);
    s5 = eliminateNaNs(s5);
    s6 = eliminateNaNs(s6);
    s7 = eliminateNaNs(s7);
#endif

    half3 s = 0;
#if _KILL_FIREFLY
    half w0 = 1 / (filterStrength + luminance_sRGB(s0.rgb));
    half w1 = 1 / (filterStrength + luminance_sRGB(s1.rgb));
    half w2 = 1 / (filterStrength + luminance_sRGB(s2.rgb));
    half w3 = 1 / (filterStrength + luminance_sRGB(s3.rgb));
    half w4 = 1 / (filterStrength + luminance_sRGB(s4.rgb));
    half w5 = 1 / (filterStrength + luminance_sRGB(s5.rgb));
    half w6 = 1 / (filterStrength + luminance_sRGB(s6.rgb));
    half w7 = 1 / (filterStrength + luminance_sRGB(s7.rgb));
    half w = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7;
    s = (s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3 + s4 * w4 + s5 * w5 + s6 * w6 + s7 * w7) / w;
#else
    s = s0 + s1 + s2 + s3 + s4 + s5 + s6 + s7;
    s *= 0.125;
#endif

    return s;
}

half3 DownsampleBox3x2(TEXTURE2D_PARAM(tex, samp), half2 uv, half2 texelSize)
{
    half3 s0 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2(-2, -1)), 0);
    half3 s1 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2( 0, -1)), 0);
    half3 s2 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2( 2, -1)), 0);
    half3 s3 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2(-2,  1)), 0);
    half3 s4 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2( 0,  1)), 0);
    half3 s5 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + texelSize * half2( 2,  1)), 0);
    return Downsample6Tap(s0, s1, s2, s3, s4, s5);
}

half3 UpsampleBox(TEXTURE2D_PARAM(tex, samp), half2 uv, half2 texelSize, half sampleScale)
{
    half4 d = texelSize.xyxy * half4(-1.0, -1.0, 1.0, 1.0) * (sampleScale * 0.5);

    half3 s = 0;
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xy), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zy), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xw), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zw), 0);

    return s * (1.0 / 4.0);
}

half3 DownsampleKawaseDualFilter(TEXTURE2D_PARAM(tex, samp), half2 uv, half2 texelSize)
{
    half4 d = texelSize.xyxy * half4(-1.0, -1.0, 1.0, 1.0);

    half3 s0 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xy), 0);
    half3 s1 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zy), 0);
    half3 s2 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.xw), 0);
    half3 s3 = SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + d.zw), 0);
    half3 s4 = SAMPLE_TEXTURE2D_LOD(tex, samp, uv, 0);

#if SANITY_CHECK
    s0 = eliminateNaNs(s0);
    s1 = eliminateNaNs(s1);
    s2 = eliminateNaNs(s2);
    s3 = eliminateNaNs(s3);
    s4 = eliminateNaNs(s4);
#endif

    half3 s = 0;
    half centerWeight = 4;
#if _KILL_FIREFLY
    half w0 = 1 / (filterStrength + luminance_sRGB(s0.rgb));
    half w1 = 1 / (filterStrength + luminance_sRGB(s1.rgb));
    half w2 = 1 / (filterStrength + luminance_sRGB(s2.rgb));
    half w3 = 1 / (filterStrength + luminance_sRGB(s3.rgb));
    half w4 = 1 / (filterStrength + luminance_sRGB(s4.rgb));
    half w = w0 + w1 + w2 + w3 + w4*centerWeight;
    s = (s0*w0 + s1*w1 + s2*w2 + s3*w3 + s4*w4*centerWeight) / w;
#else
    s = s0 + s1 + s2 + s3 + s4 * centerWeight;
    s *= 1.0/(4 + centerWeight);
#endif

    return s;
}

half3 UpsampleKawaseDualFilter(TEXTURE2D_PARAM(tex, samp), half2 uv, half2 texelSize, half sampleScale)
{
    half2 sampleOffset = texelSize * (0.5 * sampleScale);

    half3 s = 0;
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2(-2, 0)), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2(-1, 1)), 0) * 2;
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2( 0, 2)), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2( 1, 1)), 0) * 2;
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2( 2, 0)), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2( 1,-1)), 0) * 2;
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2( 0,-2)), 0);
    s += SAMPLE_TEXTURE2D_LOD(tex, samp, (uv + sampleOffset * half2(-1,-1)), 0) * 2;

    return s * (1.0 / 12.0);
}

//
// Quadratic color thresholding
// curve = (threshold - knee, knee * 2, 0.25 / knee)
//
half3 QuadraticThreshold(half3 color, half threshold, half3 curve)
{
    // Pixel brightness
    half br = max(color.r, max(color.g, color.b));

    // Under-threshold part: quadratic curve
    half rq = clamp(br - curve.x, 0.0, curve.y);
    rq = curve.z * rq * rq;

    // Combine and apply the brightness response curve.
    color *= max(rq, br - threshold) / max(br, 1.0e-4);

    return color;
}

half3 Prefilter(half3 color)
{
    // Assume exposure is calculated before Bloom pass
    half4 EV = getExposureValue();
#if SHADEROPTIONS_PRE_EXPOSURE
    if (_PostExposure > 0)
        color *= EV.x;
    else
        color *= (EV.x * EV.w);
#else
    color *= EV.x;
#endif

    color = min(_Params.x, color); // clamp to max
    color = QuadraticThreshold(color, _Threshold.x, _Threshold.yzw);
    return color;
}

half3 Combine(half3 bloom, half2 uv)
{
    half3 color = SAMPLE_TEXTURE2D_LOD(_BloomTex, sampler_BloomTex, uv, 0);
    return bloom + color;
}

half4 FragPrefilter(VsOutFullScreen i) : SV_Target
{
#if _HALF_SIZED_PREFILTER
    half3 color = DownsampleBox3x2(TEXTURE2D_ARGS(_BloomSrcTex, sampler_BloomSrcTex), i.texcoord, _BloomSrcTex_TexelSize.xy * _BloomSrcUvScale);
#else
    half3 color = DownsampleBox4Tap(TEXTURE2D_ARGS(_BloomSrcTex, sampler_BloomSrcTex), i.texcoord, _BloomSrcTex_TexelSize.xy * _BloomSrcUvScale);
#endif
    return half4(Prefilter(color), 1);
}

half4 FragDownsample(VsOutFullScreen i) : SV_Target
{
    half3 color = DownsampleKawaseDualFilter(TEXTURE2D_ARGS(_BloomSrcTex, sampler_BloomSrcTex), i.texcoord, _BloomSrcTex_TexelSize.xy * _BloomSrcUvScale);
    return half4(color, 1);
}

half4 FragUpsample(VsOutFullScreen i) : SV_Target
{
    half3 bloom = UpsampleKawaseDualFilter(TEXTURE2D_ARGS(_BloomSrcTex, sampler_BloomSrcTex), i.texcoord, _BloomSrcTex_TexelSize.xy, _SampleScale);
    return half4(Combine(bloom, i.texcoord), 1);
}
