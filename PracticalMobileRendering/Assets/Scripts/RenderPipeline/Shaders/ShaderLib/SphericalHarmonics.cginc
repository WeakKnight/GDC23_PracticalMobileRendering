#ifndef _SPHERICAL_HARMONICS_CGINC_
#define _SPHERICAL_HARMONICS_CGINC_

#include "GlobalConfig.cginc"

//=================================================================================================
//
//  MJP's DX11 Sample Framework
//  http://mynameismjp.wordpress.com/
//
//  All code licensed under the MIT license
//
//=================================================================================================

struct SH4
{
	float c[4];
};

struct SH4Color
{
	float3 c[4];
};

struct SH9
{
	float c[9];
};

struct SH9Color
{
	float3 c[9];
};

// Cosine kernel for SH
static const float CosineA0 = UNITY_PI;
static const float CosineA1 = (2.0f * UNITY_PI) / 3.0f;
static const float CosineA2 = UNITY_PI / 4.0f;

// == SH4 =========================================================================================

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH and convolves with a given kernel
//-------------------------------------------------------------------------------------------------
SH4 ProjectOntoSH4(in float3 dir, in float intensity, in float A0, in float A1)
{
    SH4 sh;

    // Band 0
    sh.c[0] = 0.282095f * A0 * intensity;

    // Band 1
    sh.c[1] = -0.488603f * dir.y * A1 * intensity;
    sh.c[2] = 0.488603f * dir.z * A1 * intensity;
    sh.c[3] = -0.488603f * dir.x * A1 * intensity;

    return sh;
}

SH4Color ProjectOntoSH4Color(in float3 dir, in float3 color, in float A0, in float A1)
{
    SH4Color sh;

    // Band 0
    sh.c[0] = 0.282095f * A0 * color;

    // Band 1
    sh.c[1] = -0.488603f * dir.y * A1 * color;
    sh.c[2] = 0.488603f * dir.z * A1 * color;
    sh.c[3] = -0.488603f * dir.x * A1 * color;

    return sh;
}

SH4 ProjectOntoSH4(in float3 dir, in float intensity)
{
    return ProjectOntoSH4(dir, intensity, 1.0f, 1.0f);
}

SH4Color ProjectOntoSH4Color(in float3 dir, in float3 color)
{
    return ProjectOntoSH4Color(dir, color, 1.0f, 1.0f);
}

SH4 ProjectOntoSH4(in float3 dir)
{
    return ProjectOntoSH4(dir, 1.0f, 1.0f, 1.0f);
}

SH4Color ProjectOntoSH4Color(in float3 dir)
{
    return ProjectOntoSH4Color(dir, 1.0f, 1.0f, 1.0f);
}


//-------------------------------------------------------------------------------------------------
// Computes the dot project of two SH4 vectors
//-------------------------------------------------------------------------------------------------
float SHDotProduct(in SH4 a, in SH4 b)
{
	float result = 0.0f;

    UNITY_UNROLL
	for(uint i = 0; i < 4; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

float3 SHDotProduct(in SH4 a, in SH4Color b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 4; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

float3 SHDotProduct(in SH4Color a, in SH4 b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 4; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

float3 SHDotProduct(in SH4Color a, in SH4Color b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 4; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH4 and dots it with another SH4 vector
//-------------------------------------------------------------------------------------------------
float3 EvalSH4(in float3 dir, in SH4 sh)
{
	SH4 dirSH = ProjectOntoSH4(dir);
	return SHDotProduct(dirSH, sh);
}

float3 EvalSH4(in float3 dir, in SH4Color sh)
{
	SH4Color dirSH = ProjectOntoSH4Color(dir, 1.0f);
	return SHDotProduct(dirSH, sh);
}

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH4, convolves with a cosine kernel, and dots it with another
// SH4 vector
//-------------------------------------------------------------------------------------------------
float EvalSH4Irradiance(in float3 dir, in SH4 sh)
{
	SH4 dirSH = ProjectOntoSH4(dir, 1.0f, CosineA0, CosineA1);
	return SHDotProduct(dirSH, sh);
}

float3 EvalSH4Irradiance(in float3 dir, in SH4Color sh)
{
	SH4Color dirSH = ProjectOntoSH4Color(dir, 1.0f, CosineA0, CosineA1);
	return SHDotProduct(dirSH, sh);
}

//-------------------------------------------------------------------------------------------------
// Evaluates the irradiance from a set of SH4 coeffecients using the non-linear fit from
// the paper by Graham Hazel from Geomerics.
// https://grahamhazel.com/blog/2017/12/22/converting-sh-radiance-to-irradiance/
//-------------------------------------------------------------------------------------------------
float EvalSH4IrradianceGeomerics(in float3 dir, in SH4 sh)
{
    float R0 = sh.c[0];

    float3 R1 = 0.5f * float3(-sh.c[3], -sh.c[1], sh.c[2]);
    float lenR1 = length(R1);

    float q = 0.5f * (1.0f + dot(R1 / lenR1, dir));

    float p = 1.0f + 2.0f * lenR1 / R0;
    float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);

    return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(abs(q), p));
}

float3 EvalSH4IrradianceGeomerics(in float3 dir, in SH4Color sh)
{
    SH4 shr = { sh.c[0].x, sh.c[1].x, sh.c[2].x, sh.c[3].x };
    SH4 shg = { sh.c[0].y, sh.c[1].y, sh.c[2].y, sh.c[3].y };
    SH4 shb = { sh.c[0].z, sh.c[1].z, sh.c[2].z, sh.c[3].z };

    return float3(EvalSH4IrradianceGeomerics(dir, shr),
                  EvalSH4IrradianceGeomerics(dir, shg),
                  EvalSH4IrradianceGeomerics(dir, shb));
}

//-------------------------------------------------------------------------------------------------
// Converts from 3-band to 2-band SH
//-------------------------------------------------------------------------------------------------
SH4 ConvertToSH4(in SH9 sh9)
{
    SH4 sh4;
    UNITY_UNROLL
    for(uint i = 0; i < 4; ++i)
        sh4.c[i] = sh9.c[i];
    return sh4;
}

SH4Color ConvertToSH4(in SH9Color sh9)
{
    SH4Color sh4;
    UNITY_UNROLL
    for(uint i = 0; i < 4; ++i)
        sh4.c[i] = sh9.c[i];
    return sh4;
}

//-------------------------------------------------------------------------------------------------
// Converts from 2-band to 3-band SH
//-------------------------------------------------------------------------------------------------
SH9 ConvertToSH9(in SH4 sh4)
{
    SH9 sh9 = (SH9)0.0f;
    UNITY_UNROLL
    for(uint i = 0; i < 4; ++i)
        sh9.c[i] = sh4.c[i];
    return sh9;
}

SH9Color ConvertToSH9(in SH4Color sh4)
{
    SH9Color sh9 = (SH9Color)0.0f;
    UNITY_UNROLL
    for(uint i = 0; i < 4; ++i)
        sh9.c[i] = sh4.c[i];
    return sh9;
}

//-------------------------------------------------------------------------------------------------
// Computes the "optimal linear direction" for a set of SH coefficients
//-------------------------------------------------------------------------------------------------
float3 OptimalLinearDirection(in SH4 sh)
{
    float x = sh.c[3];
    float y = sh.c[1];
    float z = sh.c[2];
    return normalize(float3(x, y, z));
}

float3 OptimalLinearDirection(in SH4Color sh)
{
    float x = dot(sh.c[3], 1.0f / 3.0f);
    float y = dot(sh.c[1], 1.0f / 3.0f);
    float z = dot(sh.c[2], 1.0f / 3.0f);
    return normalize(float3(x, y, z));
}

//-------------------------------------------------------------------------------------------------
// Computes the direction and color of a directional light that approximates a set of SH
// coefficients. Uses Peter Pike-Sloan's method from "Stupid SH Tricks"
//-------------------------------------------------------------------------------------------------
void ApproximateDirectionalLight(in SH4Color sh, out float3 direction, out float3 color)
{
    direction = OptimalLinearDirection(sh);
    SH4Color dirSH = ProjectOntoSH4Color(direction, 1.0f);
    dirSH.c[0] = 0.0f;
    sh.c[0] = 0.0f;
    color = SHDotProduct(dirSH, sh) * 867.0f / (316.0f * UNITY_PI);
}

//-------------------------------------------------------------------------------------------------
// Rotates 2-band SH coefficients
//-------------------------------------------------------------------------------------------------
SH4 RotateSH4(in SH4 sh, in float3x3 rotation)
{
    const float r00 = rotation._m00;
    const float r10 = rotation._m01;
    const float r20 = rotation._m02;

    const float r01 = rotation._m10;
    const float r11 = rotation._m11;
    const float r21 = rotation._m12;

    const float r02 = rotation._m20;
    const float r12 = rotation._m21;
    const float r22 = rotation._m22;

    SH4 result;

    // Constant
    result.c[0] = sh.c[0];

    // Linear
    result.c[1] = r11 * sh.c[1] - r12 * sh.c[2] + r10 * sh.c[3];
    result.c[2] = -r21 * sh.c[1] + r22 * sh.c[2] - r20 * sh.c[3];
    result.c[3] = r01 * sh.c[1] - r02 * sh.c[2] + r00 * sh.c[3];

    return result;
}

SH4Color RotateSH4(in SH4Color sh, in float3x3 rotation)
{
    const float r00 = rotation._m00;
    const float r10 = rotation._m01;
    const float r20 = rotation._m02;

    const float r01 = rotation._m10;
    const float r11 = rotation._m11;
    const float r21 = rotation._m12;

    const float r02 = rotation._m20;
    const float r12 = rotation._m21;
    const float r22 = rotation._m22;

    SH4Color result;

    // Constant
    result.c[0] = sh.c[0];

    // Linear
    result.c[1] = r11 * sh.c[1] - r12 * sh.c[2] + r10 * sh.c[3];
    result.c[2] = -r21 * sh.c[1] + r22 * sh.c[2] - r20 * sh.c[3];
    result.c[3] = r01 * sh.c[1] - r02 * sh.c[2] + r00 * sh.c[3];

    return result;
}

// == SH9 =========================================================================================

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH and convolves with a given kernel
//-------------------------------------------------------------------------------------------------
SH9 ProjectOntoSH9(in float3 n, in float intensity, in float A0, in float A1, in float A2)
{
    SH9 sh;

    // Band 0
    sh.c[0] = 0.282095f * A0 * intensity;

    // Band 1
    sh.c[1] = -0.488603f * n.y * A1 * intensity;
    sh.c[2] = 0.488603f * n.z * A1 * intensity;
    sh.c[3] = -0.488603f * n.x * A1 * intensity;

    // Band 2
    sh.c[4] = 1.092548f * n.x * n.y * A2 * intensity;
    sh.c[5] = -1.092548f * n.y * n.z * A2 * intensity;
    sh.c[6] = 0.315392f * (3.0f * n.z * n.z - 1.0f) * A2 * intensity;
    sh.c[7] = -1.092548f * n.x * n.z * A2 * intensity;
    sh.c[8] = 0.546274f * (n.x * n.x - n.y * n.y) * A2 * intensity;

    return sh;
}

SH9Color ProjectOntoSH9Color(in float3 n, in float3 color, in float A0, in float A1, in float A2)
{
    SH9Color sh;

    // Band 0
    sh.c[0] = 0.282095f * A0 * color;

    // Band 1
    sh.c[1] = -0.488603f * n.y * A1 * color;
    sh.c[2] = 0.488603f * n.z * A1 * color;
    sh.c[3] = -0.488603f * n.x * A1 * color;

    // Band 2
    sh.c[4] = 1.092548f * n.x * n.y * A2 * color;
    sh.c[5] = -1.092548f * n.y * n.z * A2 * color;
    sh.c[6] = 0.315392f * (3.0f * n.z * n.z - 1.0f) * A2 * color;
    sh.c[7] = -1.092548f * n.x * n.z * A2 * color;
    sh.c[8] = 0.546274f * (n.x * n.x - n.y * n.y) * A2 * color;

    return sh;
}

SH9 ProjectOntoSH9(in float3 dir, in float intensity)
{
	return ProjectOntoSH9(dir, intensity, 1.0f, 1.0f, 1.0f);
}

SH9Color ProjectOntoSH9Color(in float3 dir, in float3 color)
{
	return ProjectOntoSH9Color(dir, color, 1.0f, 1.0f, 1.0f);
}

SH9 ProjectOntoSH9(in float3 dir)
{
	return ProjectOntoSH9(dir, 1.0f, 1.0f, 1.0f, 1.0f);
}

SH9Color ProjectOntoSH9Color(in float3 dir)
{
	return ProjectOntoSH9Color(dir, 1.0f, 1.0f, 1.0f, 1.0f);
}

SH9 SHAdd(in SH9 a, in SH9 b)
{
    SH9 result;
    for(uint i = 0; i < 9; ++i)
    {
        result.c[i] = a.c[i] + b.c[i];
    }
    return result;
}

SH9Color SHAdd(in SH9Color a, in SH9Color b)
{
    SH9Color result;
    for(uint i = 0; i < 9; ++i)
    {
        result.c[i] = a.c[i] + b.c[i];
    }
    return result;
}

SH9Color SHMul(in SH9Color a, in float b)
{
    SH9Color result;
    for(uint i = 0; i < 9; ++i)
    {
        result.c[i] = a.c[i] * b;
    }
    return result;
}

SH9Color SHMul(in SH9Color a, in float3 b)
{
    SH9Color result;
    for(uint i = 0; i < 9; ++i)
    {
        result.c[i] = a.c[i] * b;
    }
    return result;
}

//-------------------------------------------------------------------------------------------------
// Computes the dot project of two SH9 vectors
//-------------------------------------------------------------------------------------------------
float SHDotProduct(in SH9 a, in SH9 b)
{
	float result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 9; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

float3 SHDotProduct(in SH9Color a, in SH9 b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 9; ++i)
		result += a.c[i] * b.c[i];

	return result;
}


float3 SHDotProduct(in SH9 a, in SH9Color b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 9; ++i)
		result += a.c[i] * b.c[i];

	return result;
}


float3 SHDotProduct(in SH9Color a, in SH9Color b)
{
	float3 result = 0.0f;

	UNITY_UNROLL
	for(uint i = 0; i < 9; ++i)
		result += a.c[i] * b.c[i];

	return result;
}

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH9 and dots it with another SH9 vector
//-------------------------------------------------------------------------------------------------
float EvalSH9(in float3 dir, in SH9 sh)
{
	SH9 dirSH = ProjectOntoSH9(dir);
	return SHDotProduct(dirSH, sh);
}

float3 EvalSH9(in float3 dir, in SH9Color sh)
{
	SH9Color dirSH = ProjectOntoSH9Color(dir);
	return SHDotProduct(dirSH, sh);
}

//-------------------------------------------------------------------------------------------------
// Projects a direction onto SH9, convolves with a cosine kernel, and dots it with another
// SH9 vector
//-------------------------------------------------------------------------------------------------
float EvalSH9Irradiance(in float3 dir, in SH9 sh)
{
	SH9 dirSH = ProjectOntoSH9(dir, 1.0f, CosineA0, CosineA1, CosineA2);
	return SHDotProduct(dirSH, sh);
}

float3 EvalSH9Irradiance(in float3 dir, in SH9Color sh)
{
	SH9Color dirSH = ProjectOntoSH9Color(dir, 1.0f, CosineA0, CosineA1, CosineA2);
	return SHDotProduct(dirSH, sh);
}

//-------------------------------------------------------------------------------------------------
// Convolves 3-band SH coefficients with a cosine kernel
//-------------------------------------------------------------------------------------------------
SH9 ConvolveWithCosineKernel(in SH9 sh)
{
    sh.c[0] *= CosineA0;

    sh.c[1] *= CosineA1;
    sh.c[2] *= CosineA1;
    sh.c[3] *= CosineA1;

    sh.c[4] *= CosineA2;
    sh.c[5] *= CosineA2;
    sh.c[6] *= CosineA2;
    sh.c[7] *= CosineA2;
    sh.c[8] *= CosineA2;

    return sh;
}

SH9Color ConvolveWithCosineKernel(in SH9Color sh)
{
    sh.c[0] *= CosineA0;

    sh.c[1] *= CosineA1;
    sh.c[2] *= CosineA1;
    sh.c[3] *= CosineA1;

    sh.c[4] *= CosineA2;
    sh.c[5] *= CosineA2;
    sh.c[6] *= CosineA2;
    sh.c[7] *= CosineA2;
    sh.c[8] *= CosineA2;

    return sh;
}

//-------------------------------------------------------------------------------------------------
// Computes the "optimal linear direction" for a set of SH coefficients
//-------------------------------------------------------------------------------------------------
float3 OptimalLinearDirection(in SH9 sh)
{
    SH4 sh4 = ConvertToSH4(sh);
    return OptimalLinearDirection(sh4);
}

float3 OptimalLinearDirection(in SH9Color sh)
{
    SH4Color sh4 = ConvertToSH4(sh);
    return OptimalLinearDirection(sh4);
}

//-------------------------------------------------------------------------------------------------
// Computes the direction and color of a directional light that approximates a set of SH
// coefficients. Uses Peter Pike-Sloan's method from "Stupid SH Tricks"
//-------------------------------------------------------------------------------------------------
void ApproximateDirectionalLight(in SH9Color sh, out float3 direction, out float3 color)
{
    direction = OptimalLinearDirection(sh);
    SH9Color dirSH = ProjectOntoSH9Color(direction, 1.0f);
    dirSH.c[0] = 0.0f;
    sh.c[0] = 0.0f;
    color = SHDotProduct(dirSH, sh) * 867.0f / (316.0f * UNITY_PI);
}

//-------------------------------------------------------------------------------------------------
// Rotates 3-band SH coefficients
//-------------------------------------------------------------------------------------------------
SH9 RotateSH9(in SH9 sh, in float3x3 rotation)
{
    const float r00 = rotation._m00;
    const float r10 = rotation._m01;
    const float r20 = rotation._m02;

    const float r01 = rotation._m10;
    const float r11 = rotation._m11;
    const float r21 = rotation._m12;

    const float r02 = rotation._m20;
    const float r12 = rotation._m21;
    const float r22 = rotation._m22;

    SH9 result;

    // Constant
    result.c[0] = sh.c[0];

    // Linear
    result.c[1] = r11 * sh.c[1] - r12 * sh.c[2] + r10 * sh.c[3];
    result.c[2] = -r21 * sh.c[1] + r22 * sh.c[2] - r20 * sh.c[3];
    result.c[3] = r01 * sh.c[1] - r02 * sh.c[2] + r00 * sh.c[3];

    // Quadratic
    const float t41 = r01 * r00;
    const float t43 = r11 * r10;
    const float t48 = r11 * r12;
    const float t50 = r01 * r02;
    const float t55 = r02 * r02;
    const float t57 = r22 * r22;
    const float t58 = r12 * r12;
    const float t61 = r00 * r02;
    const float t63 = r10 * r12;
    const float t68 = r10 * r10;
    const float t70 = r01 * r01;
    const float t72 = r11 * r11;
    const float t74 = r00 * r00;
    const float t76 = r21 * r21;
    const float t78 = r20 * r20;

    const float v173 = 0.1732050808e1f;
    const float v577 = 0.5773502693e0f;
    const float v115 = 0.1154700539e1f;
    const float v288 = 0.2886751347e0f;
    const float v866 = 0.8660254040e0f;

    float r[25];
    r[0] = r11 * r00 + r01 * r10;
    r[1] = -r01 * r12 - r11 * r02;
    r[2] = v173 * r02 * r12;
    r[3] = -r10 * r02 - r00 * r12;
    r[4] = r00 * r10 - r01 * r11;
    r[5] = -r11 * r20 - r21 * r10;
    r[6] = r11 * r22 + r21 * r12;
    r[7] = -v173 * r22 * r12;
    r[8] = r20 * r12 + r10 * r22;
    r[9] = -r10 * r20 + r11 * r21;
    r[10] = -v577* (t41 + t43) + v115 * r21 * r20;
    r[11] = v577* (t48 + t50) - v115 * r21 * r22;
    r[12] = -0.5f * (t55 + t58) + t57;
    r[13] = v577 * (t61 + t63) - v115 * r20 * r22;
    r[14] = v288 * (t70 - t68 + t72 - t74) - v577 * (t76 - t78);
    r[15] = -r01 * r20 - r21 * r00;
    r[16] = r01 * r22 + r21 * r02;
    r[17] = -v173 * r22 * r02;
    r[18] = r00 * r22 + r20 * r02;
    r[19] = -r00 * r20 + r01 * r21;
    r[20] = t41 - t43;
    r[21] = -t50 + t48;
    r[22] = v866 * (t55 - t58);
    r[23] = t63 - t61;
    r[24] = 0.5f *(t74 - t68 - t70 + t72);

    UNITY_UNROLL
    for(uint i = 0; i < 5; ++i) {
        const uint base = i * 5;
        result.c[4 + i] = r[base + 0] * sh.c[4] + r[base + 1] * sh.c[5] +
                          r[base + 2] * sh.c[6] + r[base + 3] * sh.c[7] +
                          r[base + 4] * sh.c[8];
    }

    return result;
}

//-------------------------------------------------------------------------------------------------
// Rotates 3-band SH coefficients
//-------------------------------------------------------------------------------------------------
SH9Color RotateSH9(in SH9Color sh, in float3x3 rotation)
{
    const float r00 = rotation._m00;
    const float r10 = rotation._m01;
    const float r20 = rotation._m02;

    const float r01 = rotation._m10;
    const float r11 = rotation._m11;
    const float r21 = rotation._m12;

    const float r02 = rotation._m20;
    const float r12 = rotation._m21;
    const float r22 = rotation._m22;

    SH9Color result;

    // Constant
    result.c[0] = sh.c[0];

    // Linear
    result.c[1] = r11 * sh.c[1] - r12 * sh.c[2] + r10 * sh.c[3];
    result.c[2] = -r21 * sh.c[1] + r22 * sh.c[2] - r20 * sh.c[3];
    result.c[3] = r01 * sh.c[1] - r02 * sh.c[2] + r00 * sh.c[3];

    // Quadratic
    const float t41 = r01 * r00;
    const float t43 = r11 * r10;
    const float t48 = r11 * r12;
    const float t50 = r01 * r02;
    const float t55 = r02 * r02;
    const float t57 = r22 * r22;
    const float t58 = r12 * r12;
    const float t61 = r00 * r02;
    const float t63 = r10 * r12;
    const float t68 = r10 * r10;
    const float t70 = r01 * r01;
    const float t72 = r11 * r11;
    const float t74 = r00 * r00;
    const float t76 = r21 * r21;
    const float t78 = r20 * r20;

    const float v173 = 0.1732050808e1f;
    const float v577 = 0.5773502693e0f;
    const float v115 = 0.1154700539e1f;
    const float v288 = 0.2886751347e0f;
    const float v866 = 0.8660254040e0f;

    float r[25];
    r[0] = r11 * r00 + r01 * r10;
    r[1] = -r01 * r12 - r11 * r02;
    r[2] =  v173 * r02 * r12;
    r[3] = -r10 * r02 - r00 * r12;
    r[4] = r00 * r10 - r01 * r11;
    r[5] = - r11 * r20 - r21 * r10;
    r[6] = r11 * r22 + r21 * r12;
    r[7] = -v173 * r22 * r12;
    r[8] = r20 * r12 + r10 * r22;
    r[9] = -r10 * r20 + r11 * r21;
    r[10] = -v577 * (t41 + t43) + v115 * r21 * r20;
    r[11] = v577 * (t48 + t50) - v115 * r21 * r22;
    r[12] = -0.5000000000e0f * (t55 + t58) + t57;
    r[13] = v577 * (t61 + t63) - v115 * r20 * r22;
    r[14] =  v288 * (t70 - t68 + t72 - t74) - v577 * (t76 - t78);
    r[15] = -r01 * r20 -  r21 * r00;
    r[16] = r01 * r22 + r21 * r02;
    r[17] = -v173 * r22 * r02;
    r[18] = r00 * r22 + r20 * r02;
    r[19] = -r00 * r20 + r01 * r21;
    r[20] = t41 - t43;
    r[21] = -t50 + t48;
    r[22] =  v866 * (t55 - t58);
    r[23] = t63 - t61;
    r[24] = 0.5000000000e0f * (t74 - t68 - t70 +  t72);

    UNITY_UNROLL
    for(uint i = 0; i < 5; ++i) {
        const uint base = i * 5;
        result.c[4 + i] = r[base + 0] * sh.c[4] + r[base + 1] * sh.c[5] +
                          r[base + 2] * sh.c[6] + r[base + 3] * sh.c[7] +
                          r[base + 4] * sh.c[8];
    }

    return result;
}

#endif
