#ifndef _SHADER_UTILITIES_CGINC_
#define _SHADER_UTILITIES_CGINC_

#include "GlobalConfig.cginc"

// ---- Constants
#define M_PI            3.14159265359f
#define M_TWO_PI        6.28318530718f
#define M_FOUR_PI       12.56637061436f
#define M_INV_PI        0.31830988618f
#define M_INV_TWO_PI    0.15915494309f
#define M_INV_FOUR_PI   0.07957747155f
#define M_HALF_PI       1.57079632679f
#define M_INV_HALF_PI   0.636619772367f
#define LOG2_E          1.44269504088896340736

#define FP32_NaN (asfloat(0x7fc00000))
#define FP32_INF (asfloat(0x7F800000))
#define FP32_EPS (5.960464478e-8)  // 2^-24, machine epsilon: 1 + EPS = 1 (half of the ULP for 1.0f)
#define FP32_MIN (1.175494351e-38) // Minimum normalized positive floating-point number
#define FP32_MAX (3.402823466e+38) // Maximum representable floating-point number
#define FP16_EPS (4.8828125e-4)    // 2^-11, machine epsilon: 1 + EPS = 1 (half of the ULP for 1.0f)
#define FP16_MIN (6.103515625e-5)  // 2^-14, the same value for 10, 11 and 16-bit: https://www.khronos.org/opengl/wiki/Small_Float_Formats
#define FP16_MIN_SQRT (0.0078125)  // 2^-7 == sqrt(HALF_MIN), useful for ensuring HALF_MIN after x^2
#define FP16_MAX (65504.0)

static half3x3 k_IdentityMatrix3x3 = {
    1.0, 0.0, 0.0,
    0.0, 1.0, 0.0,
    0.0, 0.0, 1.0
};

static half4x4 k_IdentityMatrix4x4 = {
    1.0, 0.0, 0.0, 0.0,
    0.0, 1.0, 0.0, 0.0,
    0.0, 0.0, 1.0, 0.0,
    0.0, 0.0, 0.0, 1.0
};

float remap(float V, float L0, float H0, float Ln, float Hn)
{
    return Ln + (V - L0) * (Hn - Ln) / (H0 - L0);
}

half remap01(half x, half start, half end)
{
    half rcpLength = 1.0 / (end - start);
    return saturate(x * rcpLength - start * rcpLength);
}

half3 remap01(half3 x, half3 start, half3 end)
{
    half3 rcpLength = 1.0 / (end - start);
    return saturate(x * rcpLength - start * rcpLength);
}

//
// Trigonometric functions
//

// max absolute error 9.0x10^-3
// Eberly's polynomial degree 1 - respect bounds
// 4 VGPR, 12 FR (8 FR, 1 QR), 1 scalar
// input [-1, 1] and output [0, PI]
float acosFast(float inX)
{
    float x = abs(inX);
    float res = -0.156583f * x + (0.5 * UNITY_PI);
    res *= sqrt(1.0f - x);
    return (inX >= 0) ? res : UNITY_PI - res;
}

// Same cost as acosFast + 1 FR
// Same error
// input [-1, 1] and output [-PI/2, PI/2]
float asinFast(float x)
{
    return (0.5 * UNITY_PI) - acosFast(x);
}

// max absolute error 1.3x10^-3
// Eberly's odd polynomial degree 5 - respect bounds
// 4 VGPR, 14 FR (10 FR, 1 QR), 2 scalar
// input [0, infinity] and output [0, PI/2]
float atanFastPos(float x)
{
    float t0 = (x < 1.0f) ? x : 1.0f / x;
    float t1 = t0 * t0;
    float poly = 0.0872929f;
    poly = -0.301895f + poly * t1;
    poly = 1.0f + poly * t1;
    poly = poly * t0;
    return (x < 1.0f) ? poly : (0.5 * UNITY_PI) - poly;
}

// 4 VGPR, 16 FR (12 FR, 1 QR), 2 scalar
// input [-infinity, infinity] and output [-PI/2, PI/2]
float atanFast(float x)
{
    float t0 = atanFastPos(abs(x));
    return (x < 0) ? -t0 : t0;
}

float atan2Fast(float y, float x)
{
    float t0 = max(abs(x), abs(y));
    float t1 = min(abs(x), abs(y));
    float t3 = t1 / t0;
    float t4 = t3 * t3;

    // Same polynomial as atanFastPos
    t0 = +0.0872929;
    t0 = t0 * t4 - 0.301895;
    t0 = t0 * t4 + 1.0;
    t3 = t0 * t3;

    t3 = abs(y) > abs(x) ? (0.5 * UNITY_PI) - t3 : t3;
    t3 = x < 0 ? UNITY_PI - t3 : t3;
    t3 = y < 0 ? -t3 : t3;

    return t3;
}

float utPow2(float x)
{
    return x * x;
}

float utPow5(float x)
{
    float xx = x * x;
    return xx * xx * x;
}

float maxCoeff(float3 v)
{
    return max(v.x, max(v.y, v.z));
}

float minCoeff(float3 v)
{
    return min(v.x, min(v.y, v.z));
}

float3 utFaceForward(float3 v, float3 n)
{
    return (dot(v, n) < 0.f) ? -v : v;
}

float3 utReflect(float3 v, float3 n)
{
    return -v + 2 * dot(v, n) * n;
}

bool utRefract(float3 wi, float3 n, float eta, out float eta_p, out float3 wt)
{
    float cosTheta_i = dot(n, wi);
    // Potentially flip interface orientation for Snell's law
    if (cosTheta_i < 0)
    {
        eta = 1 / eta;
        cosTheta_i = -cosTheta_i;
        n = -n;
    }

    float sin2Theta_i = max(0, 1 - cosTheta_i * cosTheta_i);
    float sin2Theta_t = sin2Theta_i / (eta * eta);
    // Handle total internal reflection case
    if (sin2Theta_t >= 1)
        return false;

    float cosTheta_t = sqrt(1 - sin2Theta_t);

    wt = -wi / eta + (cosTheta_i / eta - cosTheta_t) * n;
    wt = normalize(wt);

    eta_p = eta;
    return true;
}

float safeSqrt(float x)
{
    return sqrt(max(x, 0));
}

// ---- Forward declaration
half3 getPerpendicularStark(half3 u);

// ---- Coordinate Frame
struct Frame
{
    half3 normal;
    half3 tangent;
    half3 bitangent;

    half3 toWorld(half3 v)
    {
        return v.x * tangent + v.y * bitangent + v.z * normal;
    }

    half3 toLocal(half3 v)
    {
        return half3(dot(v, tangent), dot(v, bitangent), dot(v, normal));
    }
};

static half Frame_cosTheta(half3 v)
{
    return v.z;
}

static half Frame_absCosTheta(half3 v)
{
    return abs(v.z);
}

Frame buildCoordinateFrame(half3 N)
{
    Frame f;
    f.normal = N;
    f.tangent = getPerpendicularStark(N);
    f.bitangent = cross(N, f.tangent);
    return f;
}

Frame buildCoordinateFrame(half3 N, half3 T)
{
    Frame f;
    f.normal = N;
    f.tangent = normalize(T - N * dot(T, N));
    f.bitangent = cross(N, f.tangent);
    return f;
}

Frame buildCoordinateFrame(half3 N, half3 T, half3 B)
{
    Frame f;
    f.normal = N;
    f.tangent = T;
    f.bitangent = B;
    return f;
}

void rotateFrame(inout Frame f, float angle)
{
    half s, c;
    sincos(angle * M_PI, s, c);
    f.tangent = f.tangent * c + f.bitangent * s;
    f.bitangent = cross(f.normal, f.tangent);
}

half3 rotateInAxisY(half3 dir, half4 rotationParam)
{
    return half3(dot(rotationParam.xy, dir.xz), dir.y, dot(rotationParam.zw, dir.xz));
}

// ---- Spherical mapping
// have to be consistent with Engine convention
float2 dirToSphericalCrd(float3 direction)
{
    float3 d = normalize(direction);
    float2 uv = float2(atan2Fast(d.z, d.x), acosFast(d.y));
    uv.x = -uv.x - M_PI/2;
    uv /= float2(2 * M_PI, M_PI);

    // Note: Special handling for Unity engine
    uv.y = 1-uv.y;

    // Make sure uv is continuous in order to perform texture filtering correctly
    if (uv.x < 0)
        uv.x += 1;

    return uv;
}

float3 sphericalCrdToDir(float2 uv)
{
    // Note: Special handling for Unity engine
    uv.y = 1-uv.y;

    const float theta = uv.y * M_PI;
    const float phi = -uv.x * M_PI * 2 - M_PI/2;

    float3 dir;
    dir.x = sin(theta) * cos(phi);
    dir.y = cos(theta);
    dir.z = sin(theta) * sin(phi);

    return normalize(dir);
}

half3 getPerpendicular(half3 a)
{
    half3 c;
    if (abs(a.x) > abs(a.y)) {
        half invLen = 1.0f / sqrt(a.x * a.x + a.z * a.z);
        c = half3(a.z * invLen, 0.0f, -a.x * invLen);
    } else {
        half invLen = 1.0f / sqrt(a.y * a.y + a.z * a.z);
        c = half3(0.0f, a.z * invLen, -a.y * invLen);
    }
    return c;
}

// Utility function to get a vector perpendicular to an input vector 
//    (from "Efficient Construction of Perpendicular Vectors Without Branching")
half3 getPerpendicularStark(half3 u)
{
    half3 a = abs(u);
    uint xm = ((a.x - a.y) < 0 && (a.x - a.z) < 0) ? 1 : 0;
    uint ym = (a.y - a.z) < 0 ? (1 ^ xm) : 0;
    uint zm = 1 ^ (xm | ym);
    return normalize(cross(u, half3(xm, ym, zm)));
}

half3 eliminateNaNs(half3 v) 
{
	return -min(-v, half3(0.f, 0.f, 0.f));
}

float3 computePositionFromDepth(float depthVal, float2 uv, float4x4 mInvProj)
{
    float4 tmp;
    tmp.xy = uv.xy * 2.0f - 1.0f;
#if NDC_DEPTH_NEGATIVE_ONE_TO_ONE
    tmp.z = depthVal * 2 - 1;
#else
    tmp.z = depthVal;
#endif
    tmp.w = 1.0f;

    float4 pos = mul(mInvProj, tmp);
    pos /= pos.w;
    return pos.xyz;
}

float computeDepth(float linearZ, float4x4 mProj)
{
    // linearZ from LinearEyeDepth is positive, however unity use right handed camera space with z pointing to the viewer
    linearZ *= -1;

#if 0
    float4 x = mul(mProj, float4(0, 0, linearZ, 1));
    return x.z / x.w;
#else
    return (mProj._m22 * linearZ + mProj._m23) / (mProj._m32 * linearZ);
#endif
}

float2 NdcToUv(float2 ndc)
{
    return (ndc * 0.5 + 0.5);
}

float3 utObjectToWorldPos(in float3 p)
{
#if SHADER_STAGE_RAYTRACING
    return mul(ObjectToWorld3x4(), float4(p, 1.0)).xyz;
#else
    return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
#endif
}

float3 utObjectToWorldNormal(in float3 norm)
{
#if SHADER_STAGE_RAYTRACING
    return normalize(mul(norm, (float3x3)WorldToObject3x4()));
#else
    return UnityObjectToWorldNormal(norm);
#endif
}

float3 utObjectToWorldDir(in float3 dir)
{
#if SHADER_STAGE_RAYTRACING
    return normalize(mul((float3x3)ObjectToWorld3x4(), dir));
#else
    return UnityObjectToWorldDir(dir);
#endif
}

#endif
