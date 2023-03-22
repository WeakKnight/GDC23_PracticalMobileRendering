using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;


namespace PMRP
{
    public class YARenderPipelineMenuItem
    {
        [MenuItem("GameObject/Yarp/Light/Directional Light", false, 1)]
        public static void CreateDirectionalLight(MenuCommand menuCommand)
        {
            YALightEditorHelper.CreatePunctualLight(menuCommand, LightType.Directional, "Directional Light");
        }

        [MenuItem("GameObject/Yarp/Light/Point Light", false, 2)]
        public static void CreatePointLight(MenuCommand menuCommand)
        {
            YALightEditorHelper.CreatePunctualLight(menuCommand, LightType.Point, "Point Light");
        }

        [MenuItem("GameObject/Yarp/Light/Spot Light", false, 3)]
        public static void CreateSpotLight(MenuCommand menuCommand)
        {
            YALightEditorHelper.CreatePunctualLight(menuCommand, LightType.Spot, "Spot Light");
        }

        [MenuItem("GameObject/Yarp/Light/Environment Light", false, 4)]
        public static void CreateSkyLight(MenuCommand menuCommand)
        {
            EditorUtils.CreateGameObjectWithComponent<SRPEnvironmentLight>(menuCommand, "Environment Light");
        }

        [MenuItem("GameObject/Yarp/Light/Shadow Split", false, 1001)]
        public static void CreateShadowSplit(MenuCommand menuCommand)
        {
            EditorUtils.CreateGameObjectWithComponent<SRPShadowSplitBoundingSphere>(menuCommand, "Shadow Split Bounding Sphere");
        }

        [MenuItem("GameObject/Yarp/Rendering/Render Settings", false, 11)]
        public static void CreateRenderSettingsVolume(MenuCommand menuCommand)
        {
            EditorUtils.CreateGameObjectWithComponent<SRPRenderSettings>(menuCommand, "Render Settings");
        }
    }
}
