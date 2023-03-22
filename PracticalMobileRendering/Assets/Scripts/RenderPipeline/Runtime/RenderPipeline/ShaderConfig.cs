using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    [Flags]
    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum ShaderPass
    {
        Meta           = 0x01 << 0,
        ShadowCaster   = 0x01 << 1,
        ForwardBase    = 0x01 << 5,
    }

    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum YALightShape
    {
        Directional = LightType.Directional,
        Point       = LightType.Point,
        Spot        = LightType.Spot,
    }

    public enum YAViewMode
    {
        Lit            = 0,
        DetailLighting = 1,
        LightingOnly   = 2,
    }

    [Flags]
    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum YALightingComponent
    {
        Emissive                  = 0x01 << 0,
        DirectLight               = 0x01 << 1,
        DiffuseGlobalIllumination = 0x01 << 2,
        SpecularReflection        = 0x01 << 3,
    }

    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum ShadowMapFilter
    {
        None = 0,
        FixedSizePCF = 3,
    }

    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum ColorEncoding
    {
        HDR = 0,
        RGBM = 1,
    }

    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum YALightLoop
    {
        Simple,
    }

    [GenerateHLSL(PackingRules.Exact, sourcePath = ConstVars.ShaderLibrary + "ShaderConfig")]
    public enum ShaderOptions
    {
        IblNormalization = 1,
        IblColorEncoding = ColorEncoding.RGBM,
        // 9 mips in total including roughest mip offset, size of 256 per cubeface for perfect mirror reflection (roughness equal to 0)
        // We limit the minimum roughness to MIN_LINEAR_ROUGHNESS 0.089 and it's remapped to mip 1 so that 128 texels per cubeface is good enough in our case
        IblNumOfMipLevelsInUse = 8,
        // 2 texels per cubeface
        IblRoughestMip = 1,
        EnvmapRotation = 1,
        // Range from 1~4, can't greater than 4 cause some data was stored in float4
        CsmMaxCascades = 2,
        MaxAreaLights = 4,
        MaxPunctualLights = 16,
        MaxShadowedLights = 16,
        MaxShadowSplits = 64,
        MaxReflectionProbes = 8,
        LtcLutSize = 32,
        SpecularOcclusionLutSize = 16,
        ColorGradingLutSize = 16,
        LightLoopImpl = YALightLoop.Simple,
        LightLoopTileSize = 16,
        LightLoopMaxPunctualLightPerTile = 8,
        LumaHistogramBins = 256,
        MaxMetaPassOutputVariables = 8,
        MaxMetaPassRenderTargets = 4,
    };

    // Note: #define can't be use in include file in C# so we chose this way to configure both C# and hlsl
    // Changing a value in this enum Config here require to regenerate the hlsl include and recompile C# and shaders
    public class ShaderConfig
    {
        public const int s_IblNumOfMipLevels        = (int) ShaderOptions.IblNumOfMipLevelsInUse;
        public const int s_IblRoughestMip           = (int) ShaderOptions.IblRoughestMip;
        public const int s_IblNumOfMipLevelsInTotal = s_IblNumOfMipLevels + s_IblRoughestMip;
        public const int s_IblNormalization         = (int) ShaderOptions.IblNormalization;
        // TODO: proper astc HDR support
        public const int s_IblColorEncoding         = (int) ShaderOptions.IblColorEncoding;
        public const int s_EnvmapRotation           = (int) ShaderOptions.EnvmapRotation;
        public const int s_CsmMaxCascades           = (int) ShaderOptions.CsmMaxCascades;
        public const int s_MaxAreaLights            = (int) ShaderOptions.MaxAreaLights;
        public const int s_MaxPunctualLights        = (int) ShaderOptions.MaxPunctualLights;
        public const int s_MaxShadowedLights        = (int) ShaderOptions.MaxShadowedLights;
        public const int s_MaxShadowSplits          = (int) ShaderOptions.MaxShadowSplits;
        public const int s_MaxReflectionProbes      = (int) ShaderOptions.MaxReflectionProbes;
        public const int s_LtcLutSize               = (int) ShaderOptions.LtcLutSize;
        public const int s_SpecularOcclusionLutSize = (int) ShaderOptions.SpecularOcclusionLutSize;
        public const int s_ColorGradingLutSize      = (int) ShaderOptions.ColorGradingLutSize;
        public const int s_LightLoopTileSize        = (int) ShaderOptions.LightLoopTileSize;
        public const int s_LightLoopMaxPunctualLightPerTile = (int) ShaderOptions.LightLoopMaxPunctualLightPerTile;
        public const int s_LumaHistogramBins        = (int) ShaderOptions.LumaHistogramBins;
        public const int s_MaxMetaPassOutputVariables = (int) ShaderOptions.MaxMetaPassOutputVariables;
        public const int s_MaxMetaPassRenderTargets   = (int) ShaderOptions.MaxMetaPassRenderTargets;
    }
}
