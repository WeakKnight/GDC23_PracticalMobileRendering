using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    [ExecuteInEditMode]
    public class PrecomputedLightingManager : MonoBehaviour
    {
        [SerializeField]
        public Texture2D lightmap;
        [NonSerialized]
        public RenderTexture lightmapRT;

        [SerializeField]
        public Texture3D volumetricLightmap;
        [NonSerialized]
        public RenderTexture volumetricLightmapRT;

        [SerializeField]
        public int lightmapResolution = 0;

        [SerializeField]
        public int lightmapSampleCount = 1024;

        [SerializeField]
        public float lightmapTexelDensity = 10.0f;

        [SerializeField]
        public float diffuseBoost = 1.0f;

        [SerializeField]
        public int lightmapNum = 0;

        [SerializeField]
        public List<MeshRenderer> lightmappedRenderers = new();

        // Lights with dynamic baking support
        [SerializeField]
        public List<Light> dynamicLights;
        [SerializeField]
        public List<DynamicBaking> dynamicBakings = new();

        [SerializeField]
        public List<LightmapReceiverData> oneLightLightmapReceiverDatas = new();
        [SerializeField]
        public List<LightmapReceiverData> twoLightLightmapReceiverDatas = new();
        [SerializeField]
        public List<LightmapReceiverData> threeLightLightmapReceiverDatas = new();

        [SerializeField]
        public List<VolumetricLightmapReceiverData> oneLightVolumetricLightmapReceiverDatas = new();
        [SerializeField]
        public List<VolumetricLightmapReceiverData> twoLightVolumetricLightmapReceiverDatas = new();
        [SerializeField]
        public List<VolumetricLightmapReceiverData> threeLightVolumetricLightmapReceiverDatas = new();

        [SerializeField, HideInInspector]
        public string lightmapReceiverGUID;
        [SerializeField, HideInInspector]
        public string volumetricLightmapReceiverGUID;

        private LightmapDynamicBakingPass lightmapDynamicBakingPass;
        private VolumetricLightmapDynamicBakingPass volumetricLightmapDynamicBakingPass;

        private ComputeShader texture3DBlitShader;

        public static bool UseComputeShaderForLightmapWithDynamicBaking
        {
            get
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    return true;
                }

                return false;
            }
        }

        void OnEnable()
        {
            Refresh();
        }

        public void Clear()
        {
            if (lightmapRT != null)
            {
                lightmapRT.Release();
                lightmapRT = null;
            }

            lightmapDynamicBakingPass = null;

            volumetricLightmapDynamicBakingPass?.Dispose();
            volumetricLightmapDynamicBakingPass = null;
        }

        void OnDisable()
        {
            Clear();
        }

        void Update()
        {
            foreach (var dynamicBaking in dynamicBakings)
            {
                if (dynamicBaking.prevIntensity < 0.0f)
                {
                    dynamicBaking.delta = 0.0f;
                }
                else
                {
                    dynamicBaking.delta = Mathf.Abs(dynamicBaking.intensity - dynamicBaking.prevIntensity);
                }
                dynamicBaking.prevIntensity = dynamicBaking.intensity;

                if (dynamicBaking.emissiveSync != null)
                {
                    dynamicBaking.emissiveSync.Sync(dynamicBaking.intensity);
                }
            }

            if (dynamicBakings.Count > 0)
            {
                if (lightmapDynamicBakingPass != null)
                {
                    if (lightmapDynamicBakingPass.Begin())
                    {
                        lightmapDynamicBakingPass.End();
                    }
                }

                if (volumetricLightmapDynamicBakingPass != null)
                {
                    if (volumetricLightmapDynamicBakingPass.Begin())
                    {
                        volumetricLightmapDynamicBakingPass.End();
                    }
                }
            }
        }

        public Texture GetLightmap()
        {
            if (dynamicLights.Count > 0)
            {
                return lightmapRT == null ? Texture2D.grayTexture : lightmapRT;
            }
            else
            {
                return lightmap == null ? Texture2D.grayTexture : lightmap;
            }
        }

        public Texture GetVolumetricLightmap()
        {
            if (dynamicLights.Count > 0)
            {
                return volumetricLightmapRT == null ? Texture2D.grayTexture : volumetricLightmapRT;
            }
            else
            {
                return volumetricLightmap == null ? Texture2D.grayTexture : volumetricLightmap;
            }
        }

        public void Refresh()
        {
            if (dynamicLights.Count > 0)
            {
                if (UseComputeShaderForLightmapWithDynamicBaking)
                {
                    lightmapRT = new RenderTexture(lightmap.width, lightmap.height, 0, RenderTextureFormat.ARGBHalf, 0);
                    lightmapRT.enableRandomWrite = true;
                }
                else
                {
                    lightmapRT = new RenderTexture(lightmap.width, lightmap.height, 0, RenderTextureFormat.RGB111110Float, 0);
                }
                lightmapRT.filterMode = FilterMode.Bilinear;
                lightmapRT.wrapMode = TextureWrapMode.Clamp;
                lightmapRT.useMipMap = false;
                lightmapRT.Create();
                Graphics.Blit(lightmap, lightmapRT);

                foreach (var lightmapReceiverData in oneLightLightmapReceiverDatas)
                {
                    lightmapReceiverData.CreateReceiverBuffer();
                }

                foreach (var lightmapReceiverData in twoLightLightmapReceiverDatas)
                {
                    lightmapReceiverData.CreateReceiverBuffer();
                }

                foreach (var lightmapReceiverData in threeLightLightmapReceiverDatas)
                {
                    lightmapReceiverData.CreateReceiverBuffer();
                }

                lightmapDynamicBakingPass = new LightmapDynamicBakingPass(this);

                volumetricLightmapRT = new RenderTexture(volumetricLightmap.width, volumetricLightmap.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                volumetricLightmapRT.volumeDepth = volumetricLightmap.depth;
                volumetricLightmapRT.dimension = TextureDimension.Tex3D;
                volumetricLightmapRT.enableRandomWrite = true;
                volumetricLightmapRT.filterMode = FilterMode.Bilinear;
                volumetricLightmapRT.wrapMode = TextureWrapMode.Clamp;
                volumetricLightmapRT.useMipMap = false;
                volumetricLightmapRT.Create();

                if (texture3DBlitShader == null)
                {
                    texture3DBlitShader = Resources.Load<ComputeShader>("Shaders/Texture3DBlit");
                }

                int texture3DBlitKernel = texture3DBlitShader.FindKernel("Texture3DBlit");
                texture3DBlitShader.SetTexture(texture3DBlitKernel, "SrcTex", volumetricLightmap);
                texture3DBlitShader.SetTexture(texture3DBlitKernel, "DstTex", volumetricLightmapRT);
                Vector3 texDimension = new Vector3(volumetricLightmap.width, volumetricLightmap.height, volumetricLightmap.depth);
                texture3DBlitShader.SetVector("TexDimension", texDimension);
                texture3DBlitShader.Dispatch(texture3DBlitKernel, Mathf.CeilToInt(volumetricLightmap.width / 8) + 1, Mathf.CeilToInt(volumetricLightmap.height / 8) + 1, Mathf.CeilToInt(volumetricLightmap.depth / 8) + 1);

                volumetricLightmapDynamicBakingPass = new VolumetricLightmapDynamicBakingPass(this);
            }

            SceneEventDelegate.OnPrecomputedGIChanged?.Invoke();
        }

#if UNITY_EDITOR
        public void ImportLightmapReceivers()
        {
            string lightmapReceiverPath = AssetDatabase.GUIDToAssetPath(lightmapReceiverGUID);
            ReceiverAsset receiverAsset = AssetDatabase.LoadAssetAtPath<ReceiverAsset>(lightmapReceiverPath);

            ReceiverAsset ReceiverPostProcessing()
            {
                var result = ScriptableObject.CreateInstance<ReceiverAsset>();
                int lmReceiverBaseIndex = 0;
                for (int i = 0; i < receiverAsset.offsets.Count; i++)
                {
                    int offset = receiverAsset.offsets[i].x;
                    int length = receiverAsset.offsets[i].y;

                    var subReceivers = receiverAsset.receivers.GetRange((int)offset, (int)length);

                    DynamicBaking dynamicBaking = dynamicBakings[i];
                    subReceivers = CullAndSmootReceivers(subReceivers, dynamicBaking.cullingRatioForLightmap, dynamicBaking.conservativeFactorForLightmap);
                    result.receivers.AddRange(subReceivers);
                    result.offsets.Add(new Vector2Int(lmReceiverBaseIndex, subReceivers.Count));
                    lmReceiverBaseIndex += subReceivers.Count;
                }
                return result;
            }

            ReceiverAsset postProcessedReceivers = ReceiverPostProcessing();

            var context = new LightmapReceiverImporter.Context(postProcessedReceivers, dynamicLights.Count, lightmapResolution, lightmapResolution);
            var result = LightmapReceiverImporter.ImportLightmapReceiverMeshes(context);

            oneLightLightmapReceiverDatas = result.oneLightData;
            twoLightLightmapReceiverDatas = result.twoLightData;
            threeLightLightmapReceiverDatas = result.threeLightData;
        }

        public void ImportVolumetricLightmapReceivers()
        {
            string receiverPath = AssetDatabase.GUIDToAssetPath(volumetricLightmapReceiverGUID);
            ReceiverAsset receiverAsset = AssetDatabase.LoadAssetAtPath<ReceiverAsset>(receiverPath);

            ReceiverAsset ReceiverPostProcessing()
            {
                var result = ScriptableObject.CreateInstance<ReceiverAsset>();
                int lmReceiverBaseIndex = 0;
                for (int i = 0; i < receiverAsset.offsets.Count; i++)
                {
                    int offset = receiverAsset.offsets[i].x;
                    int length = receiverAsset.offsets[i].y;

                    var subReceivers = receiverAsset.receivers.GetRange((int)offset, (int)length);

                    DynamicBaking dynamicBaking = dynamicBakings[i];
                    subReceivers = CullAndSmootReceivers(subReceivers, dynamicBaking.cullingRatioForVolumetricLightmap, dynamicBaking.conservativeFactorForVolumetricLightmap);
                    result.receivers.AddRange(subReceivers);
                    result.offsets.Add(new Vector2Int(lmReceiverBaseIndex, subReceivers.Count));
                    lmReceiverBaseIndex += subReceivers.Count;
                }
                return result;
            }

            ReceiverAsset postProcessedReceivers = ReceiverPostProcessing();

            var context = new VolumetricLightmapReceiverImporter.Context(postProcessedReceivers, dynamicLights.Count, volumetricLightmap.width, volumetricLightmap.height, volumetricLightmap.depth);
            var result = VolumetricLightmapReceiverImporter.ImportVolumetricLightmapReceivers(context);

            oneLightVolumetricLightmapReceiverDatas = result.oneLightData;
            twoLightVolumetricLightmapReceiverDatas = result.twoLightData;
            threeLightVolumetricLightmapReceiverDatas = result.threeLightData;
        }

        private List<Receiver> CullAndSmootReceivers(List<Receiver> srcReceivers, float cullingRatio, float conservativeFactor)
        {
            List<Receiver> result = new List<Receiver>();

            if (srcReceivers.Count == 0)
            {
                return result;
            }

            srcReceivers.RemoveAll((Receiver receiver) =>
            {
                RuntimeUtils.DecodeRGBM(receiver.irradiance);
                float luminance = receiver.Luminance();
                return (luminance < 0.001f);
            });

            float maxLuminance = 0.0f;
            List<float> luminanceVec = new List<float>();
            for (int i = 0; i < srcReceivers.Count; i++)
            {
                Receiver receiver = srcReceivers[i];
                float luminance = receiver.Luminance();
                if (maxLuminance < luminance)
                {
                    maxLuminance = luminance;
                }

                luminanceVec.Add(receiver.Luminance());
            }

            if (luminanceVec.Count <= 0)
            {
                return result;
            }

            luminanceVec.Sort();
            int targetIndex = (int)(cullingRatio * luminanceVec.Count);
            if (targetIndex >= luminanceVec.Count)
            {
                targetIndex = luminanceVec.Count - 1;
            }

            float luminanceThreshold = luminanceVec[targetIndex];
            int conservativeIndex = (int)((cullingRatio + (1.0f - conservativeFactor) * (1.0f - cullingRatio)) * (float)luminanceVec.Count);
            conservativeIndex = Mathf.Max(targetIndex, conservativeIndex);
            conservativeIndex = Mathf.Min(luminanceVec.Count - 1, conservativeIndex);
            float conservativeLuminanceThreshold = luminanceVec[conservativeIndex];

            for (int i = 0; i < srcReceivers.Count; i++)
            {
                if (srcReceivers[i].Luminance() < luminanceThreshold)
                {
                    continue;
                }

                Receiver receiver = srcReceivers[i];
                if (targetIndex > 0)
                {
                    Color32 SmoothIrradiance(Color32 irradianceWithRGBMEncoding, float maxLuminance, float luminanceThreshold, float conservativeThreshold = -1.0f)
                    {
                        Color linearCol = RuntimeUtils.DecodeRGBM(irradianceWithRGBMEncoding);
                        float luminance = RuntimeUtils.Luminance(linearCol);
                        float luminanceUpperBound = maxLuminance;
                        if (conservativeThreshold > 0.0f)
                        {
                            luminanceUpperBound = conservativeThreshold;
                        }

                        float range = luminanceUpperBound - luminanceThreshold;
                        float distance = luminanceUpperBound - luminance;
                        float factor = Mathf.Clamp(distance / range, 0.0f, 1.0f);
                        factor = factor * factor;
                        float attenuation = Mathf.Clamp01(1.0f - (factor * factor));

                        return RuntimeUtils.EncodeRGBM(new Color(attenuation * linearCol.r, attenuation * linearCol.g, attenuation * linearCol.b, 1.0f));
                    }

                    receiver.irradiance = SmoothIrradiance(receiver.irradiance, maxLuminance, luminanceThreshold, conservativeLuminanceThreshold);
                }
                result.Add(receiver);
            }

            return result;
        }
#endif
    }
}
