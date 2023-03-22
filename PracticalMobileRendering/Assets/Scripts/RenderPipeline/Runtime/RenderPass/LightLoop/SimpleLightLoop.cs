using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace PMRP
{
    public sealed class SimpleLightLoop : ILightLoop
    {
        private Vector4 m_dominantLightDirection;
        private Vector4 m_dominantLightIntensity;

        private int m_numPunctualLights = 0;
        private Vector4[] m_lightDirection = new Vector4[ShaderConfig.s_MaxPunctualLights];
        private Vector4[] m_lightIntensity = new Vector4[ShaderConfig.s_MaxPunctualLights];
        private Vector4[] m_lightPosition = new Vector4[ShaderConfig.s_MaxPunctualLights];
        private Vector4[] m_lightFalloff = new Vector4[ShaderConfig.s_MaxPunctualLights];
        private Vector4[] m_lightInfo = new Vector4[ShaderConfig.s_MaxPunctualLights];

        public void Dispose()
        {
        }

        public void Init()
        {
        }

        void FillOneLight(SRPLight aLight, int lightIdx)
        {
            Debug.Assert(aLight.lightType == LightType.Point || aLight.lightType == LightType.Spot);

            Color intensity = aLight.unityLight.color * aLight.unityLight.intensity;
            Vector4 position = aLight.transform.position;
#if UNITY_EDITOR
            position.w = Math.Abs(aLight.unityLight.shadowRadius);
#else
            position.w = 0;
#endif
            Vector3 direction = aLight.lightType == LightType.Spot ? -aLight.unityLight.transform.forward : Vector3.zero;

            float invSqrRadius = 1.0f / (aLight.unityLight.range * aLight.unityLight.range);
            float falloffExponent = aLight.UseInverseSquaredFalloff ? 0 : aLight.LightFalloffExponent;
            float angularFalloffScale  = 0;
            float angularFalloffOffset = 1;
            if (aLight.lightType == LightType.Spot)
            {
                float cosInner = Mathf.Cos(Mathf.Deg2Rad * aLight.unityLight.innerSpotAngle * 0.5f);                
                float cosOuter = Mathf.Cos(Mathf.Deg2Rad * aLight.unityLight.spotAngle      * 0.5f);

                angularFalloffScale = 1 / Mathf.Max(0.001f, cosInner - cosOuter);
                angularFalloffOffset = -cosOuter * angularFalloffScale;
            }

            m_lightDirection[lightIdx] = direction;
            m_lightPosition[lightIdx] = position;
            m_lightIntensity[lightIdx] = intensity;
            m_lightFalloff[lightIdx] = new Vector4(invSqrRadius, falloffExponent, angularFalloffScale, angularFalloffOffset);
            m_lightInfo[lightIdx] = new Vector4((int)aLight.lightType, aLight.ShadowSplitIndex, 0, 0);
        }

        void FillLightsList(List<SRPLight> punctualLights, LightType lightType)
        {
            for (int i = 0; i < punctualLights.Count; ++i)
            {
                SRPLight aLight = punctualLights[i];
                if (aLight.lightType == lightType)
                {
                    if (m_numPunctualLights < ShaderConfig.s_MaxPunctualLights)
                    {
                        FillOneLight(aLight, m_numPunctualLights);
                        m_numPunctualLights += 1;
                    }
                    else
                    {
                        #if UNITY_EDITOR
                            Debug.LogError("Maximum number of visible punctual lights reached, consider to delete some of them");
                        #endif

                        break;
                    }
                }
            }
        }

        public void Build(WorldData worldData, SRPCamera viewCamera)
        {
            SRPLight sunLight = worldData.sunLight;
            if (sunLight)
            {
                m_dominantLightDirection = -sunLight.transform.forward;
                m_dominantLightIntensity = sunLight.unityLight.color * sunLight.unityLight.intensity;
#if UNITY_EDITOR
                m_dominantLightDirection.w = sunLight.unityLight.shadowAngle;
#else
                m_dominantLightDirection.w = 0;
#endif
            }
            else
            {
                m_dominantLightDirection = Vector4.zero;
                m_dominantLightIntensity = Vector4.zero;
            }

            m_numPunctualLights = 0;
            List<SRPLight> visiblePunctualLights = worldData.GetAllVisiblePunctualLight();
            FillLightsList(visiblePunctualLights, LightType.Point);
            FillLightsList(visiblePunctualLights, LightType.Spot);
        }

        public void Setup(CommandBuffer cmd, SRPCamera camera)
        {
            cmd.SetGlobalVector("g_DominantLightDirection", m_dominantLightDirection);
            cmd.SetGlobalVector("g_DominantLightIntensity", m_dominantLightIntensity);

            cmd.SetGlobalInt("g_NumPunctualLights", m_numPunctualLights);
            cmd.SetGlobalVectorArray("g_PunctualLightsDirection", m_lightDirection);
            cmd.SetGlobalVectorArray("g_PunctualLightsPosition", m_lightPosition);
            cmd.SetGlobalVectorArray("g_PunctualLightsIntensity", m_lightIntensity);
            cmd.SetGlobalVectorArray("g_PunctualLightsFalloff", m_lightFalloff);
            cmd.SetGlobalVectorArray("g_PunctualLightsInfo", m_lightInfo);
        }
    }
}
