#ifndef _COLOR_SPACE_CGINC_
#define _COLOR_SPACE_CGINC_

#include "GlobalConfig.cginc"

// RGBM encode/decode
static const half k_RGBMRange = 6.0;

half4 encodeRGBM(half3 color)
{
    color = sqrt(color);

    color *= 1.0 / k_RGBMRange;
    half m = max(max(color.x, color.y), max(color.z, 1e-5));
    m = ceil(m * 255) / 255;
    return half4(color / m, m);
}

half3 decodeRGBM(half4 rgbm)
{
    half3 color = rgbm.xyz * (rgbm.w * k_RGBMRange);
    color *= color;
    return color;
}

half luminance_sRGB(half3 c)
{
    return dot(c, half3(0.2125, 0.7154, 0.0721));
}

half luminance_Avg(half3 c)
{
    return dot(c, half3(0.3333, 0.3333, 0.3333));
}

// All tone mapping has Gamma 2.2 baked-in
half3 Tonemap_ACES(half3 x)
{
    // Narkowicz 2015, "ACES Filmic Tone Mapping Curve"
    const half a = 2.51;
    const half b = 0.03;
    const half c = 2.43;
    const half d = 0.59;
    const half e = 0.14;
    half3 ret = (x * (a * x + b)) / (x * (c * x + d) + e);
    return LinearToGammaSpace(ret.rgb);
}

half3 RGBToYCgCo(half3 rgb)
{
    half Y = dot(rgb, half3(0.25f, 0.50f, 0.25f));
    half Cg = dot(rgb, half3(-0.25f, 0.50f, -0.25f));
    half Co = dot(rgb, half3(0.50f, 0.00f, -0.50f));
    return half3(Y, Cg, Co);
}

half3 YCgCoToRGB(half3 YCgCo)
{
    half tmp = YCgCo.x - YCgCo.y;
    half r = tmp + YCgCo.z;
    half g = YCgCo.x + YCgCo.y;
    half b = tmp - YCgCo.z;
    return half3(r, g, b);
}

float3 RGB_to_xyY(float3 rgb)
{
    float Y = rgb.r + rgb.g + rgb.b;
    if (Y <= 0)
        return 0;

    return float3(rgb.rg / Y, Y);
}

float3 xyY_to_RGB(float3 xyY)
{
    return float3(xyY.xy, 1 - xyY.x - xyY.y) * xyY.z;
}

#endif
