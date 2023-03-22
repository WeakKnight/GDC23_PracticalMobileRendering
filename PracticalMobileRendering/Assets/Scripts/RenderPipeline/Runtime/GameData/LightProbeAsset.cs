using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using DDXForUnity;

// This is editor only class, however we can't place it in Editor folder cause it's been referenced by non-editor module
namespace PMRP
{
    public class LightProbeAsset : ScriptableObject
    {
        public Texture2D PanoramicMap;

        [Header("Generated Data")]
        public Cubemap EnvmapGgxFiltered;
        public Vector4[] IrradianceSH9Coeffs;

        public bool IsValid()
        {
            return EnvmapGgxFiltered != null;
        }
    }

    [Serializable]
    public class LightProbeAssetEd : EditorOnlyAsset<LightProbeAsset> { }

#if UNITY_EDITOR
    public static class LightProbeAssetExt
    {
        public static bool Save(this LightProbeAsset probeAsset, string path)
        {
            path = PathUtils.RelativeToUnityProject(PathUtils.GetFullPath(path));

            string rootResFolder = Path.GetDirectoryName(path);
            if (!PathUtils.CreateDirectory(rootResFolder))
                return false;

            AssetDatabase.Refresh();
            if (AssetDatabase.Contains(probeAsset))
                AssetDatabase.SaveAssets();
            else
                AssetDatabase.CreateAsset(probeAsset, path);
            AssetDatabase.Refresh();
            return true;
        }

        public static void PreFilter(this LightProbeAsset probeAsset, string assetPath)
        {
            if (!probeAsset.PanoramicMap)
                return;

            var buildImportanceMap  = new BuildEnvmapImportanceMap();
            var integrateIrradiance = new IntegrateLightProbeIrradiance();
            var integrateSpecular   = new IntegrateLightProbeSpecular();

            buildImportanceMap.Init();
            integrateIrradiance.Init();
            integrateSpecular.Init();

            using (new ScopedTimer("pre-integration"))
            {
                bool useMIS = true;
                bool isCubemap = true;

                // DO pre-integration
                buildImportanceMap.Execute(probeAsset.PanoramicMap);
                integrateIrradiance.Execute(probeAsset.PanoramicMap);
                integrateSpecular.Execute(probeAsset.PanoramicMap,
                                          ConstVars.ReflectionProbeTextureSize,
                                          isCubemap,
                                          useMIS,
                                          buildImportanceMap.ImportanceMap);
            }

            probeAsset.IrradianceSH9Coeffs = integrateIrradiance.SH9Coeffs;

            string saveRoot = Path.GetDirectoryName(PathUtils.GetFullPath(assetPath));
            saveRoot = PathUtils.RelativeToUnityProject(saveRoot);

            string filename = Path.GetFileName(assetPath);
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            using (new ScopedTimer("save specularLd cubemap"))
            {
                bool isRGBM = ShaderConfig.s_IblColorEncoding == (int)ColorEncoding.RGBM;

                string filePath = Path.Combine(saveRoot, filenameWithoutExt + "_PreIntegratedGGX.ddx");
                DDXTextureImporter importer = DDXTextureUtils.Save(DDXTextureUtils.ExportTexture(integrateSpecular.PrefilterGGX, !isRGBM, isRGBM, true), filePath);
                importer.filterMode = FilterMode.Trilinear;

                var standaloneImportSetting = importer.GetPlatformTextureSettings(BuildTargetGroup.Standalone);
                standaloneImportSetting.overridden = true;
                standaloneImportSetting.textureCompression = TextureImporterCompression.Uncompressed;
                standaloneImportSetting.format = TextureImporterFormat.ARGB32;
                importer.SetPlatformTextureSettings(standaloneImportSetting);

                var androidImportSetting = importer.GetPlatformTextureSettings(BuildTargetGroup.Android);
                androidImportSetting.overridden = true;
                androidImportSetting.textureCompression = TextureImporterCompression.CompressedHQ;
                androidImportSetting.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(androidImportSetting);

                var iosImportSetting = importer.GetPlatformTextureSettings(BuildTargetGroup.iOS);
                iosImportSetting.overridden = true;
                iosImportSetting.textureCompression = TextureImporterCompression.CompressedHQ;
                iosImportSetting.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(iosImportSetting);

                using (new ScopedTimer("change compression format"))
                {
                    importer.SaveAndReimport();
                }

                probeAsset.EnvmapGgxFiltered = AssetDatabase.LoadAssetAtPath(filePath, typeof(Cubemap)) as Cubemap;
            }

            buildImportanceMap.Dispose();
            integrateSpecular.Dispose();
            integrateIrradiance.Dispose();
        }
    }
#endif
}
