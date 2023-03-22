#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine.Rendering;

namespace DDXForUnity
{
    [ScriptedImporter(1, "ddx")]
    public class DDXTextureImporter : ScriptedImporter
    {
        public static string s_DefaultPlatformName = "DefaultTexturePlatform";


        // https://docs.unity3d.com/560/Documentation/ScriptReference/Texture-anisoLevel.html
        [Range(1, 9)]
        public int anisoLevel = 1;

        public TextureWrapMode  wrapMode         = TextureWrapMode.Clamp;
        public FilterMode       filterMode       = FilterMode.Bilinear;
        public TextureFormat    sourceFormat     = TextureFormat.RGBAFloat;
        public TextureDimension textureDimension = TextureDimension.Tex2D;
        public bool readable                     = false;

        public List<TextureImporterPlatformSettings> platformSettings = new List<TextureImporterPlatformSettings>();

        public new void SaveAndReimport()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);

            base.SaveAndReimport();
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            DDXTextureData rawData = DDXTextureUtils.Load(ctx.assetPath);

            // Setup importer
            sourceFormat = rawData.Format;
            Texture unityTexture = DDXTextureUtils.ImportTexture(rawData, true);
            textureDimension = unityTexture.dimension;

            // Get default settings explicitly so that it will be saved after first import
            var defaultSetting = GetDefaultPlatformTextureSettings();
            var platformSetting = GetPlatformTextureSettings(ctx.selectedBuildTarget);

            TextureFormat textureFormat = (TextureFormat)platformSetting.format;
            if (platformSetting.format == TextureImporterFormat.Automatic)
                textureFormat = GetAutomaticTextureImportFormat(sourceFormat, textureDimension,
                                                                BuildPipeline.GetBuildTargetGroup(ctx.selectedBuildTarget),
                                                                platformSetting.textureCompression);

            using (new PMRP.ScopedTimer("DDX compress texture"))
            {
                if (platformSetting.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    if (unityTexture.dimension == TextureDimension.Tex2D)
                        EditorUtility.CompressTexture(unityTexture as Texture2D, textureFormat,
                            GetCompressionQuality(platformSetting.textureCompression));
                    else if (unityTexture.dimension == TextureDimension.Cube)
                        EditorUtility.CompressCubemapTexture(unityTexture as Cubemap, textureFormat,
                            GetCompressionQuality(platformSetting.textureCompression));
                    else
                        Debug.AssertFormat(false, "Target format {0} is not supported yet for texture dimension {1}",
                            textureFormat, unityTexture.dimension);
                }
            }

            unityTexture.wrapMode   = wrapMode;
            unityTexture.filterMode = filterMode;
            unityTexture.anisoLevel = anisoLevel;

            if (!readable)
            {
                MakeUnreadable(unityTexture);
            }

            Debug.Assert(readable == unityTexture.isReadable);
            ctx.AddObjectToAsset("UnityTexture", unityTexture);
            ctx.SetMainObject(unityTexture);
        }

        protected void MakeUnreadable(Texture texture)
        {
            if (texture.isReadable)
            {
                Texture2D tex2d = texture as Texture2D;
                if (tex2d != null)
                    tex2d.Apply(false, true);

                Texture2DArray tex2darray = texture as Texture2DArray;
                if (tex2darray != null)
                    tex2darray.Apply(false, true);

                Cubemap cubemap = texture as Cubemap;
                if (cubemap != null)
                    cubemap.Apply(false, true);

                Texture3D volume = texture as Texture3D;
                if (volume != null)
                    volume.Apply(false, true);
            }
        }

        public TextureImporterPlatformSettings GetDefaultPlatformTextureSettings()
        {
            return GetPlatformTextureSettings(s_DefaultPlatformName);
        }

        public TextureImporterPlatformSettings GetPlatformTextureSettings(BuildTarget platform)
        {
            return GetPlatformTextureSettings(BuildPipeline.GetBuildTargetGroup(platform));
        }

        public TextureImporterPlatformSettings GetPlatformTextureSettings(BuildTargetGroup group)
        {
            return GetPlatformTextureSettings(GetBuildTargetGroupName(group));
        }

        public void ClearPlatformTextureSettings(string platform)
        {
            for (int i = 0; i < platformSettings.Count; ++i)
            {
                if (platformSettings[i].name == platform)
                {
                    platformSettings.RemoveAt(i);
                }
            }
        }

        public void SetPlatformTextureSettings(TextureImporterPlatformSettings setting)
        {
            for (int i = 0; i < platformSettings.Count; ++i)
            {
                if (platformSettings[i].name == setting.name)
                {
                    // Replace setting
                    platformSettings[i] = setting;
                    return;
                }
            }
            // Add new one if not exist
            platformSettings.Add(setting);
        }

        public TextureImporterPlatformSettings GetPlatformTextureSettings(string platform)
        {
            TextureImporterPlatformSettings setting = FindWithBuildTargetByName(platform);
            if (setting == null)
            {
                // If it does not exist, create the default platform and return it.
                TextureImporterPlatformSettings defaultSettings = FindWithBuildTargetByName(s_DefaultPlatformName);
                if (defaultSettings == null)
                {
                    defaultSettings                    = new TextureImporterPlatformSettings();
                    defaultSettings.name               = s_DefaultPlatformName;
                    defaultSettings.overridden         = false;
                    defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
                    platformSettings.Add(defaultSettings);
                }

                if (platform == s_DefaultPlatformName)
                    return defaultSettings;

                setting = new TextureImporterPlatformSettings();
                defaultSettings.CopyTo(setting);
                setting.name       = platform;
                setting.overridden = false;
                if (setting.format == TextureImporterFormat.Automatic)
                    setting.format = (TextureImporterFormat) GetAutomaticTextureImportFormat(sourceFormat, textureDimension,
                                                                                             GetBuildTargetGroupFromName(platform),
                                                                                             setting.textureCompression);
            }

            return setting;
        }

        protected TextureImporterPlatformSettings FindWithBuildTargetByName(string platform)
        {
            foreach (var setting in platformSettings)
            {
                if (setting.name == platform)
                    return setting;
            }
            return null;
        }

        public static TextureFormat GetAutomaticTextureImportFormat(TextureFormat format, TextureDimension dimension, BuildTargetGroup platform, TextureImporterCompression compression)
        {
            bool compressionEnabled = compression != TextureImporterCompression.Uncompressed;

            // Compression disabled, leave it as it is
            if (!compressionEnabled)
                return format;

            if (format == TextureFormat.ARGB32)
            {
                if (platform == BuildTargetGroup.Android || platform == BuildTargetGroup.iOS ||
                    dimension == TextureDimension.Tex3D)
                    return TextureFormat.ASTC_4x4;
                else
                    return TextureFormat.BC5;
            }
            else if (format == TextureFormat.RGBAHalf ||
                     format == TextureFormat.RGBAFloat)
            {
                if (platform == BuildTargetGroup.Android || platform == BuildTargetGroup.iOS || dimension == TextureDimension.Tex3D)
                    return TextureFormat.ASTC_HDR_4x4;
                else
                    return TextureFormat.BC6H;
            }

            return format;
        }

        // Same naming convention as https://docs.unity3d.com/ScriptReference/TextureImporter.GetPlatformTextureSettings.html
        public static string GetBuildTargetGroupName(BuildTargetGroup group)
        {
            switch (group)
            {
                case BuildTargetGroup.Standalone:     return "Standalone";
                case BuildTargetGroup.iOS:            return "iPhone";
                case BuildTargetGroup.Android:        return "Android";
                case BuildTargetGroup.WebGL:          return "WebGL";
                case BuildTargetGroup.WSA:            return "Windows Store Apps";
                case BuildTargetGroup.PS4:            return "PS4";
                case BuildTargetGroup.XboxOne:        return "XboxOne";
                case BuildTargetGroup.tvOS:           return "tvOS";
                case BuildTargetGroup.Switch:         return "Nintendo Switch";
                case BuildTargetGroup.Stadia:         return "Stadia";
                case BuildTargetGroup.LinuxHeadlessSimulation: return "CloudRendering";
                case BuildTargetGroup.PS5:            return "PS5";
            }

            Debug.AssertFormat(false, "Unknown build target group");
            return "";
        }

        public static BuildTargetGroup GetBuildTargetGroupFromName(string platform)
        {
            if (platform == "Standalone") return BuildTargetGroup.Standalone;
            if (platform == "iPhone") return BuildTargetGroup.iOS;
            if (platform == "Android") return BuildTargetGroup.Android;
            if (platform == "WebGL") return BuildTargetGroup.WebGL;
            if (platform == "Windows Store Apps") return BuildTargetGroup.WSA;
            if (platform == "PS4") return BuildTargetGroup.PS4;
            if (platform == "XboxOne") return BuildTargetGroup.XboxOne;
            if (platform == "tvOS") return BuildTargetGroup.tvOS;
            if (platform == "Nintendo Switch") return BuildTargetGroup.Switch;
            if (platform == "Stadia") return BuildTargetGroup.Stadia;
            if (platform == "CloudRendering") return BuildTargetGroup.LinuxHeadlessSimulation;
            if (platform == "PS5") return BuildTargetGroup.PS5;
            Debug.AssertFormat(false, "Unknown build target group");
            return BuildTargetGroup.Unknown;
        }

        protected TextureCompressionQuality GetCompressionQuality(TextureImporterCompression compression)
        {
            if (compression == TextureImporterCompression.Compressed)
                return TextureCompressionQuality.Normal;
            if (compression == TextureImporterCompression.CompressedHQ)
                return TextureCompressionQuality.Best;
            return TextureCompressionQuality.Fast;
        }
    }
}
#endif
