using System;
using UnityEditor;
using UnityEngine;

namespace PMRP
{
    public class GlobalResourcesEditor
    {
        [MenuItem("Rendering/Development/Update Global Resources", false, 3000)]
        static void UpdateGlobalResources()
        {
            UpdateSpecularOcclusionLut();

            GlobalResources.Reset();
        }

        static void UpdateSpecularOcclusionLut()
        {
            string lut3dFilePath;

            string[] paths = AssetDatabase.FindAssets("SpecularOcclusionLut3D");
            if (paths.Length == 0)
            {
                string folderPath = EditorUtility.SaveFolderPanel("Save SpecularOcclusionLut", "Assets/", "");
                if (String.IsNullOrEmpty(folderPath))
                    return;

                lut3dFilePath = folderPath + "/SpecularOcclusionLut3D.exr";
                PathUtils.RelativeToUnityProject(lut3dFilePath, out lut3dFilePath);
            }
            else
            {
                lut3dFilePath = AssetDatabase.GUIDToAssetPath(paths[0]);
            }

            var integrateSoLut = new IntegrateSpecularOcclusion();
            integrateSoLut.Init();
            integrateSoLut.Execute();
            EditorUtils.SaveRenderTextureToFile(integrateSoLut.SpecularOcclusionLut3D, EditorUtils.ETextureExportFormat.EXR, lut3dFilePath);
            integrateSoLut.Dispose();
        }
    }
}