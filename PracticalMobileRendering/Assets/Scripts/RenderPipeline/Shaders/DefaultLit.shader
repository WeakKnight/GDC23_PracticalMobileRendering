Shader "PMRP/DefaultLit"
{
    Properties
    {
        // ---- Begin build-in properties
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [SimpleToggle] _ZWrite("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] [ShaderPass(ForwardBase)] _ZTestMode("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.YABlendMode)] [ShaderPass(ForwardBase)] _BlendMode("Blend Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] [ShaderPass(ForwardBase)] _SrcBlend("SrcBlend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] [ShaderPass(ForwardBase)] _DstBlend("DstBlend", Float) = 0
        [Toggle] [AutoVariant] _AlphaTest("Alpha Test", Float) = 0
        [SimpleToggle] _AlphaToMask("Alpha To Mask", Float) = 0
        [Toggle] [ShaderPass(Meta, ForwardBase)] [AutoVariant] _Visibility("Use Visibility", Float) = 1
        [SimpleToggle] _TwoSided("Two Sided", Float) = 0
        // ---- End build-in properties

        // ---- Begin material properties
        _BaseColorMap("Base Color Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1.0, 1.0, 1.0, 1)

        _MaskMap("MaskMap", 2D) = "white"{}
        [Swizzle] _RoughnessChSwizzle("Roughness Channel", Vector) = (1, 0, 0, 0)
        [Swizzle] _MetallicChSwizzle("Metallic Channel", Vector) = (0, 1, 0, 0)
        [Swizzle] _OcclusionChSwizzle("Occlusion Channel", Vector) = (0, 0, 1, 0)

        [MinMaxRemap(0, 1)] _AOMinMaxRemap("AO Min Max Remap", Vector) = (0,1, 0, 0)
        [MinMaxRemap(0, 1)] _MetallicMinMaxRemap("Metallic Min Max Remap", Vector) = (0,1, 0, 0)
        [MinMaxRemap(0, 1)] _RoughnessMinMaxRemap("Roughness Min Max Remap", Vector) = (0,1, 0, 0)
        [MinMaxRemap(0, 1)] _OpacityMinMaxRemap("Opacity Min Max Remap", Vector) = (0,1, 0, 0)

        _NormalMap("Normal Map", 2D) = "bump" {}

        [HDR] _EmissiveColor("Emissive Color", Color) = (0.0, 0.0, 0.0, 1)
        _EmissiveColorMap("Emissive Color Map", 2D) = "white" {}
        // ---- End material properties
    }

    HLSLINCLUDE

    #pragma exclude_renderers d3d9 d3d11_9x
    //#pragma enable_d3d11_debug_symbols
    #pragma target 4.6

    // ---- Begin build-in properties
    half _Cull;
    half _ZWrite;
    half _ZTestMode;
    half _AlphaTest;
    half _AlphaToMask;
    half _Visibility;
    half _TwoSided;
    // ---- End build-in properties

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        // ---- Begin common state
        LOD 200
        Cull [_Cull]
        ZWrite [_ZWrite]
        AlphaToMask [_AlphaToMask]
        // ---- End common state

        Pass
        {
            name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM

            #pragma vertex VertShadowCaster
            #pragma fragment FragShadowCaster

            // ---- Begin build-in variants
            #pragma shader_feature __ _ALPHATEST_ON
            // ---- End build-in variants

            #define CONF_SHADER_PASS SHADERPASS_SHADOW_CASTER

            #include "DefaultLit.cginc"

            ENDHLSL
        }

        Pass
        {
            name "ForwardBase"
            Tags
            {
                "LightMode" = "ForwardBase"
            }

            ZTest [_ZTestMode]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM

            #pragma vertex VertMain
            #pragma fragment FragMain
            #pragma enable_d3d11_debug_symbols

            // ---- Begin build-in variants
            #pragma shader_feature __ _ALPHATEST_ON
            #pragma shader_feature __ _VISIBILITY_ON
            #pragma multi_compile __ _LIGHTMAP_GI _VLM_GI
            // ---- End build-in variants

            #define CONF_SHADER_PASS SHADERPASS_FORWARD_BASE

            #include "DefaultLit.cginc"

            ENDHLSL
        }
    }

    SubShader
    {
        Pass
        {
            name "VisibilityDXR"
            Tags
            {
                "LightMode" = "VisibilityDXR"
            }


            HLSLPROGRAM

            #pragma only_renderers d3d11

            #pragma raytracing surface_shader
            //#pragma enable_ray_tracing_shader_debug_symbols

            #define CONF_SHADER_PASS SHADERPASS_VISIBILITY_DXR

            #include "DefaultLit.cginc"

            ENDHLSL
        }

    }
}
