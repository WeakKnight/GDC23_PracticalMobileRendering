using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public static class MaterialSystem
    {
        private static int g_EnvmapIntensity               = Shader.PropertyToID("g_EnvmapIntensity");
        private static int g_EnvmapRotationParam           = Shader.PropertyToID("g_EnvmapRotationParam");
        private static int g_EnvmapMipmapOffset            = Shader.PropertyToID("g_EnvmapMipmapOffset");
        private static int g_EnvmapFilterWithGGX           = Shader.PropertyToID("g_EnvmapFilterWithGGX");
        private static int g_EnvmapSH9Coeffs           = Shader.PropertyToID("g_EnvmapSH9Coeffs");
        private static int g_NumOfCsmCascades              = Shader.PropertyToID("g_NumOfCsmCascades");
        private static int g_CsmShadowMatrices             = Shader.PropertyToID("g_CsmShadowMatrices");
        private static int g_CsmNormalBias                 = Shader.PropertyToID("g_CsmNormalBias");
        private static int g_CsmCascadeValidUv             = Shader.PropertyToID("g_CsmCascadeValidUv");
        private static int g_ShadowTexture                 = Shader.PropertyToID("g_ShadowTexture");
        private static int g_ShadowDistanceFalloff         = Shader.PropertyToID("g_ShadowDistanceFalloff");
        private static int g_ShadowNormalBias              = Shader.PropertyToID("g_ShadowNormalBias");
        private static int g_ShadowSplitMatrices           = Shader.PropertyToID("g_ShadowSplitMatrices");
        private static int g_ShadowSplitUvRange            = Shader.PropertyToID("g_ShadowSplitUvRange");
        private static int g_ScreenParams                  = Shader.PropertyToID("g_ScreenParams");
        private static int g_ExposureValue                 = Shader.PropertyToID("g_ExposureValue");
        private static int g_FrameIndexModX                = Shader.PropertyToID("g_FrameIndexModX");
        private static int g_ViewCameraPosition            = Shader.PropertyToID("g_ViewCameraPosition");
        private static int g_ViewCameraDirection           = Shader.PropertyToID("g_ViewCameraDirection");
        private static int g_ViewCameraViewProjMat         = Shader.PropertyToID("g_ViewCameraViewProjMat");
        private static int g_ViewCameraInvViewProjMat      = Shader.PropertyToID("g_ViewCameraInvViewProjMat");

        private static int s_SpecularOcclusionLut3D = Shader.PropertyToID("s_SpecularOcclusionLut3D");

        private static Vector4[] s_defaultSHCoeffs = new Vector4[9]
        {
            Vector4.zero,
            Vector4.zero, Vector4.zero, Vector4.zero,
            Vector4.zero, Vector4.zero,  Vector4.zero, Vector4.zero, Vector4.zero
        };

        public static void SetupPrimaryCamera(CommandBuffer cmd, SRPRenderSettings settings, SRPCamera viewCamera, bool hashedAlphaTest = false)
        {
            // Global LUT
            cmd.SetGlobalTexture(s_SpecularOcclusionLut3D, GlobalResources.GetSpecularOcclusionLut3D());

            // Exposure
            {
                float exposure = settings.GetSettingComponent<CommonSetting>().FixedExposure;
                float EV = Mathf.Pow(2, exposure);
                cmd.SetGlobalVector(g_ExposureValue, new Vector2(EV, 1.0f / EV));
            }

            Vector4 screenParams = new Vector4(viewCamera.pixelWidth, viewCamera.pixelHeight, 1.0f / viewCamera.pixelWidth, 1.0f / viewCamera.pixelHeight);
            cmd.SetGlobalVector(g_ScreenParams, screenParams);
            cmd.SetGlobalVector(g_FrameIndexModX,
                                new Vector4((float)(Time.frameCount % 4), (float)(Time.frameCount % 8), (float)(Time.frameCount % 16)));
            cmd.SetGlobalFloat("g_HashedAlphaTest", hashedAlphaTest ? 1 : 0);

            cmd.SetGlobalVector(g_ViewCameraPosition, viewCamera.transform.position);
            cmd.SetGlobalVector(g_ViewCameraDirection, viewCamera.transform.forward);
            cmd.SetGlobalMatrix(g_ViewCameraViewProjMat, CommonUtils.GetNonJitteredViewProjMatrix(viewCamera));
            cmd.SetGlobalMatrix(g_ViewCameraInvViewProjMat, CommonUtils.GetNonJitteredViewProjMatrix(viewCamera).inverse);
        }

        public static void SetupRayTracingCommon(CommandBuffer cmd, SRPCamera camera, WorldData worldData, SRPRenderSettings settings)
        {
            SetupLightingPass(cmd, camera, worldData, settings, null, null, null);
        }

        public static void SetupLightingPass(CommandBuffer    cmd,
                                             SRPCamera         camera,
                                             WorldData        worldData,
                                             SRPRenderSettings settings,
                                             ShadowMapData    shadowData,
                                             RenderTexture    aoTexture = null,
                                             RenderTexture    ssrTexture = null)
        {
            Vector4 param = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
            cmd.SetGlobalVector(g_ScreenParams, param);

            SetupShadows(cmd, camera, worldData, settings, shadowData);
            SetupReflectionProbes(cmd, worldData);
        }

        private static void SetupShadows(CommandBuffer cmd, SRPCamera camera, WorldData worldData, SRPRenderSettings settings, ShadowMapData shadowData)
        {
            cmd.DisableShaderKeyword("_SHADOW_FILTER_FIXED_SIZE_PCF");

            if (shadowData == null)
                return;

            /* -- Global shadow parameters --*/
            cmd.SetGlobalTexture(g_ShadowTexture, shadowData.ShadowTexture);
            cmd.SetGlobalVector(g_ShadowDistanceFalloff, worldData.shadowDistanceFade);

            cmd.EnableShaderKeyword("_SHADOW_FILTER_FIXED_SIZE_PCF");

            Debug.Assert(worldData.sunLight == null || worldData.sunLight.ShadowSplitIndex <= 0);

            cmd.SetGlobalVectorArray(g_ShadowNormalBias, shadowData.ShadowNormalBias);
            cmd.SetGlobalMatrixArray(g_ShadowSplitMatrices, shadowData.ShadowMatrices);
            cmd.SetGlobalVectorArray(g_ShadowSplitUvRange, shadowData.ShadowSplitsUvRange);

            cmd.SetGlobalFloat(g_NumOfCsmCascades, shadowData.CascadeCount);
            cmd.SetGlobalMatrixArray(g_CsmShadowMatrices, shadowData.CSMShadowMatrices);
            cmd.SetGlobalVectorArray(g_CsmCascadeValidUv, shadowData.CSMValidUv);
            cmd.SetGlobalVector(g_CsmNormalBias, shadowData.CSMNormalBias);
        }
        
        private static void SetupReflectionProbes(CommandBuffer cmd, WorldData worldData)
        {
            SRPEnvironmentLight envmap = worldData.envLight;

            if (envmap != null && envmap.IsValid())
            {
                cmd.SetGlobalFloat(g_EnvmapMipmapOffset, envmap.EnvmapGgxFiltered.mipmapCount - ShaderConfig.s_IblNumOfMipLevelsInTotal);
                cmd.SetGlobalTexture(g_EnvmapFilterWithGGX, envmap.EnvmapGgxFiltered);

                cmd.SetGlobalVectorArray(g_EnvmapSH9Coeffs, envmap.IrradianceSH9Coeffs);

                Vector4 envmapIntensity = envmap.Intensity;
                envmapIntensity.w = envmap.IBLNormalization ? 1 : 0;
                cmd.SetGlobalVector(g_EnvmapIntensity, envmapIntensity);

                if (ShaderConfig.s_EnvmapRotation != 0)
                    cmd.SetGlobalVector(g_EnvmapRotationParam, envmap.GetRotationParameter());
            }
            else
            {
                cmd.SetGlobalFloat(g_EnvmapMipmapOffset, 0);
                cmd.SetGlobalTexture(g_EnvmapFilterWithGGX, Texture2D.blackTexture);
                cmd.SetGlobalColor(g_EnvmapIntensity, Color.clear);
                if (ShaderConfig.s_EnvmapRotation != 0)
                    cmd.SetGlobalVector(g_EnvmapRotationParam, Vector4.zero);

                cmd.SetGlobalVectorArray(g_EnvmapSH9Coeffs, s_defaultSHCoeffs);
            }
        }
    }
}
