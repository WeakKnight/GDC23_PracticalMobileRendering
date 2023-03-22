using System;
using UnityEngine;

namespace PMRP
{
    [DisallowMultipleComponent]
    public class SRPEnvironmentLight : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private LightProbeAssetEd m_lightProbeAsset = new LightProbeAssetEd();

        [ColorUsage(false, true)]
        public Color Intensity = Color.white;

        [Range(0f, 360f)]
        public float RotationAngle = 0.0f;

        public bool IBLNormalization = true;

        public Cubemap EnvmapGgxFiltered;

        public Vector4[] IrradianceSH9Coeffs;

        public Texture2D SkyTexture;

#if UNITY_EDITOR
        public LightProbeAsset lightProbeAsset
        {
            get
            {
                return m_lightProbeAsset.Object;
            }
            set
            {
                m_lightProbeAsset.Object = value;

                if (value && value.IsValid())
                {
                    IrradianceSH9Coeffs = value.IrradianceSH9Coeffs;
                    EnvmapGgxFiltered   = value.EnvmapGgxFiltered;
                    SkyTexture          = value.PanoramicMap;
                }
                else
                {
                    IrradianceSH9Coeffs = null;
                    EnvmapGgxFiltered   = null;
                    SkyTexture          = null;
                }

                UpdateSkyTextureInternal();
            }
        }

        [NonSerialized]
        public RenderTexture SkyTextureImportanceMap;

        private int m_skyTextureId;
#endif

        public bool IsValid()
        {
            return EnvmapGgxFiltered != null;
        }

        public Vector4 GetRotationParameter()
        {
            float phi = 0;
            if (ShaderConfig.s_EnvmapRotation != 0)
            {
                phi = RotationAngle / 180.0f * Mathf.PI;
            }

            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            return new Vector4(cosPhi, -sinPhi, sinPhi, cosPhi);
        }

#if UNITY_EDITOR

        void OnValidate()
        {
            UpdateSkyTextureInternal();
        }

        void OnEnable()
        {
            UpdateSkyTextureInternal();
        }

        private void UpdateSkyTextureInternal()
        {
#if UNITY_2020_1_OR_NEWER
            if (SkyTexture != null)
            {
                if (SkyTexture.GetInstanceID() != m_skyTextureId || SkyTextureImportanceMap == null)
                {
                    using (var buildImportanceMap = new BuildEnvmapImportanceMap())
                    {
                        buildImportanceMap.Init();
                        buildImportanceMap.Execute(SkyTexture);

                        SkyTextureImportanceMap = new RenderTexture(buildImportanceMap.ImportanceMap);
                        SkyTextureImportanceMap.name = "SkyTextureImportanceMap";
                        Graphics.CopyTexture(buildImportanceMap.ImportanceMap, SkyTextureImportanceMap);
                    }

                    m_skyTextureId = SkyTexture.GetInstanceID();
                }
            }
            else
            {
                if (SkyTextureImportanceMap != null)
                {
                    SkyTextureImportanceMap.Release();
                    SkyTextureImportanceMap = null;
                }

                m_skyTextureId = 0;
            }
#endif
        }

#endif
    }
}

