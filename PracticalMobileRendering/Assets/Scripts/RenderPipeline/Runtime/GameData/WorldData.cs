using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    public class WorldData
    {
        public SRPLight sunLight;
        public SRPEnvironmentLight envLight;

        public SRPLight[] allPunctualLights;
        public SRPLight[] allAreaLights;

        public Vector2 shadowDistance;
        public Vector2 shadowDistanceFade;

        public SRPRenderSettings renderSettings;

        private List<SRPLight> m_visiblePunctualLights = new List<SRPLight>();
        private List<SRPLight> m_visibleAreaLights = new List<SRPLight>();

        public void UpdateShadowDistance(SRPCamera camera)
        {
            if (sunLight)
            {
                ShadowUtilities.ComputeDirectionalShadowDistance(camera.unityCamera,
                                                                 sunLight.ShadowSetting,
                                                                 sunLight.transform,
                                                                 out shadowDistance,
                                                                 out shadowDistanceFade);
            }
            else
            {
                // set shadow distance same as camera near/far clip plane so that punctual light will cast shadows
                shadowDistance     = new Vector2(camera.unityCamera.nearClipPlane, camera.unityCamera.farClipPlane);
                shadowDistanceFade = new Vector2(0, 1);
            }
        }

        public void ApplyCullingResults(GfxCullingResults cullingResults)
        {
            m_visiblePunctualLights.Clear();
            m_visibleAreaLights.Clear();

            for (int i = 0; i < allPunctualLights?.Length; ++i)
            {
                SRPLight aLight = allPunctualLights[i];
                aLight.VisibleLightIndex = cullingResults.GetVisibleLightIndex(aLight.unityLight);
                aLight.ShadowCasterBoundsVisibility = cullingResults.ShadowCasterBoundsVisibility(aLight.VisibleLightIndex);
                aLight.ShadowSplitIndex = -1;

#if UNITY_2020_1_OR_NEWER
                bool contributeDirectLight = aLight.unityLight.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime ||
                                             aLight.unityLight.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed;
#else
                bool contributeDirectLight = aLight.unityLight.lightmapBakeType == LightmapBakeType.Realtime ||
                                             aLight.unityLight.lightmapBakeType == LightmapBakeType.Mixed;
#endif
                if (aLight.VisibleLightIndex >= 0 && contributeDirectLight)
                    m_visiblePunctualLights.Add(aLight);
            }

            for (int i = 0; i < allAreaLights.Length; ++i)
            {
                SRPLight aLight = allAreaLights[i];

#if UNITY_2020_1_OR_NEWER
                bool contributeDirectLight = aLight.unityLight.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime ||
                                             aLight.unityLight.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed;
#else
                bool contributeDirectLight = aLight.unityLight.lightmapBakeType == LightmapBakeType.Realtime ||
                                             aLight.unityLight.lightmapBakeType == LightmapBakeType.Mixed;
#endif
                if (contributeDirectLight)
                    m_visibleAreaLights.Add(aLight);
            }
        }

        public List<SRPLight> GetAllVisiblePunctualLight()
        {
            return m_visiblePunctualLights;
        }

        public List<SRPLight> GetAllVisibleAreaLight()
        {
            return m_visibleAreaLights;
        }
    }
}
