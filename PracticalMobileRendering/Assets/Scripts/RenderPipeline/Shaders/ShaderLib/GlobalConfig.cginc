#ifndef _GLOBAL_CONFIG_CGINC_
#define _GLOBAL_CONFIG_CGINC_

// This file should be included in all shader files
// It's aim to provide a place to statically toggle global options

#if SHADER_STAGE_VERTEX
#elif SHADER_STAGE_FRAGMENT
#elif SHADER_STAGE_DOMAIN
#elif SHADER_STAGE_HULL
#elif SHADER_STAGE_GEOMETRY
#elif SHADER_STAGE_COMPUTE
#else
    // In Unity 2020.1, SHADER_STAGE_RAYTRACING is not defined for closesthit shader so that we define it manually
    #define SHADER_STAGE_RAYTRACING 1
    #define SHADER_STAGE_RAY_TRACING 1
    #define UNITY_COMPILER_DXC 1
#endif

#if SHADER_STAGE_RAYTRACING
	//#define USE_NVAPI 1

	// Ray tracing shader are compiled with HV 2021, we explicit define the macro to help IDE to provide correct syntax highlight
	#ifndef __HLSL_VERSION
		#define __HLSL_VERSION 2021
	#endif

	#if USE_NVAPI
		#include "NVAPI/nvHLSLExtns.h"
	#endif
#endif

// Include internal version of UnityCG.cginc
#include "UnityBuiltin/UnityCG.hlsl"

// Un-define below macros, use definition from API/*.hlsl
#undef UNITY_NEAR_CLIP_VALUE
#undef CBUFFER_START
#undef CBUFFER_END
#undef UNITY_BRANCH
#undef UNITY_FLATTEN
#undef UNITY_UNROLL
#undef UNITY_LOOP

// Copy from com.unity.render-pipelines.core\ShaderLibrary
// Include language header
#if defined(SHADER_API_D3D11) && !SHADER_STAGE_RAYTRACING
#include "API/D3D11.hlsl"
#elif defined(SHADER_API_D3D11) && SHADER_STAGE_RAYTRACING
#include "API/DXR.hlsl"
#elif defined(SHADER_API_METAL)
#include "API/Metal.hlsl"
#elif defined(SHADER_API_VULKAN)
#include "API/Vulkan.hlsl"
#elif defined(SHADER_API_SWITCH)
#include "API/Switch.hlsl"
#elif defined(SHADER_API_GLCORE)
#include "API/GLCore.hlsl"
#elif defined(SHADER_API_GLES3)
#include "API/GLES3.hlsl"
#elif defined(SHADER_API_GLES)
#include "API/GLES2.hlsl"
#else
#include "API/DummyAPI.hlsl"
#endif
#include "API/Validate.hlsl"

#include "../ShaderConfig.hlsl"

// Inline samplers
#if SHADER_API_D3D11
SamplerState inline_sampler_point_repeat;
SamplerState inline_sampler_point_clamp;
SamplerState inline_sampler_linear_repeat;
SamplerState inline_sampler_linear_clamp;
SamplerState inline_sampler_linear_repeatU_clampV;
SamplerState inline_sampler_trilinear_repeat;
SamplerState inline_sampler_trilinear_clamp;
SamplerState inline_sampler_trilinear_repeatU_clampV;
SamplerState inline_sampler_trilinear_aniso8_repeat;
#endif


// Option to enable sanity check, a bit like assert in C++ but it's up to dev to implement it's testing code
#define SANITY_CHECK 0


#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    // It's configurable with glClipControl but it's not used in Unity5.6 codebase
    #define NDC_DEPTH_NEGATIVE_ONE_TO_ONE 1
#endif


// It's not necessary to use float*_t since the highest precision on a consumer GPU is 32 bits
// However we want to explicitly declare some variables with FP32 and do NOT consider to change it to FP16
#define float_t float
#define float2_t float2
#define float3_t float3
#define float4_t float4


#endif
