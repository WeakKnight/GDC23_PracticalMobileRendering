using System;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class SRPLight : MonoBehaviour
    {
        private Light m_light;
        public Light unityLight
        {
            get
            {
                if (m_light == null)
                    m_light = GetComponent<Light>();
                return m_light;
            }
        }

        public LightType lightType => unityLight.type;

        public bool IsPunctualLight()
        {
            return lightType == LightType.Directional ||
                   lightType == LightType.Point       ||
                   lightType == LightType.Spot;
        }

        public bool TwoSided = true;

        public bool UseInverseSquaredFalloff = true;

        public float LightFalloffExponent = 8;

        public LightType Type
        {
            get { return unityLight.type; }
        }

        public ShadowMapSetting ShadowSetting = new ShadowMapSetting();

        [NonSerialized]
        public int VisibleLightIndex;

        [NonSerialized]
        public bool ShadowCasterBoundsVisibility;

        [NonSerialized]
        public int ShadowSplitIndex;
        
        public void OnValidate()
        {
            ShadowSetting.FarPlane = Mathf.Max(ShadowSetting.FarPlane, 0.1f);

            ShadowSetting.NearPlane = Mathf.Max(ShadowSetting.NearPlane, 0.01f);
            ShadowSetting.NearPlane = Mathf.Min(ShadowSetting.NearPlane, ShadowSetting.FarPlane - 0.01f);

            if (ShadowSetting.ShadowSplits.Count > ShaderConfig.s_CsmMaxCascades)
            {
                ShadowSetting.ShadowSplits.RemoveRange(ShaderConfig.s_CsmMaxCascades,
                                                       ShadowSetting.ShadowSplits.Count - ShaderConfig.s_CsmMaxCascades);
            }
        }
    }

    [Serializable]
    public class ShadowMapSetting
    {
        public enum EShadowMapSize
        {
            ShadowMapSize_256x256   = 256,
            ShadowMapSize_512x512   = 512,
            ShadowMapSize_1024x1024 = 1024,
        }

        [Range(1, ShaderConfig.s_CsmMaxCascades)]
        public int NumOfSplits = 1;

        [Range(0, 1.0f)]
        public float PSSMLambda = 0.8f;

        public bool OverrideShadowBoundingSphere = false;

        public List<SRPShadowSplitBoundingSphere> ShadowSplits = new List<SRPShadowSplitBoundingSphere>(ShaderConfig.s_CsmMaxCascades);

        public bool UseOverrideShadowBoundingSphere()
        {
            if (OverrideShadowBoundingSphere && ShadowSplits.Count > 0)
            {
                for (int i = 0; i < ShadowSplits.Count; ++i)
                {
                    if (ShadowSplits[i] != null)
                        return true;
                }
            }
            return false;
        }

        public int GetDirectionalShadowSplitsCount()
        {
            if (UseOverrideShadowBoundingSphere())
            {
                int cnt = 0;
                for (int i = 0; i < ShadowSplits.Count; ++i)
                {
                    if (ShadowSplits[i] != null)
                        cnt += 1;
                }
                return cnt;
            }
            else
            {
                return Mathf.Clamp(NumOfSplits, 1, ShaderConfig.s_CsmMaxCascades);
            }
        }

        public BoundingSphere GetFirstShadowBoundingSphere()
        {
            Debug.Assert(OverrideShadowBoundingSphere);

            for (int i = 0; i < ShadowSplits.Count; ++i)
            {
                if (ShadowSplits[i] != null)
                {
                    return ShadowSplits[i].BoundingSphere;
                }
            }
            return new BoundingSphere(Vector3.zero, 0);
        }

        public void GetShadowSplitBoundingSpheres(List<BoundingSphere> shadowSplitBoundingSpheres, bool sort = true)
        {
            Debug.Assert(OverrideShadowBoundingSphere);

            shadowSplitBoundingSpheres.Clear();
            for (int i = 0; i < ShadowSplits.Count; ++i)
            {
                if (ShadowSplits[i] != null)
                    shadowSplitBoundingSpheres.Add(ShadowSplits[i].BoundingSphere);
            }

            if (sort)
            {
                shadowSplitBoundingSpheres.Sort((sph0, sph1) =>
                {
                    float texelDensity0 = (float) ShadowMapResolution / sph0.radius;
                    float texelDensity1 = (float) ShadowMapResolution / sph1.radius;
                    return -(texelDensity0.CompareTo(texelDensity1));
                });
            }
        }

        [Range(0, 1)]
        public float DirectionalShadowDistanceFade = 0.8f;

        public float NearPlane = 0.1f;

        public float FarPlane = 10;

        [Min(0)]
        public float ZOffset = 0;

        [Range(0, 4)]
        public float DepthSlopeBias = 2f;

        [Range(0, 4)]
        public float NormalBias = 2f;

        public float DepthBiasClamp = 16.0f;

        public EShadowMapSize ShadowMapResolution = EShadowMapSize.ShadowMapSize_512x512;
    }
}