using PMRP;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace PMRP
{
    public sealed class LiteForwardRenderer : ICameraRenderer
    {
        public static readonly string name = "ForwardLite";
        public static readonly uint LIGHTMAP_LAYER_MASK = 1 << 13;
        public static readonly uint VLM_LAYER_MASK = 1 << 14;
        public static readonly string LIGHTMAP_KEYWORD = "_LIGHTMAP_GI";
        public static readonly string VLM_KEYWORD = "_VLM_GI";

        private static readonly int LIGHTMAP0_PROP = Shader.PropertyToID("_Lightmap0");
        private static readonly int LIGHTMAP_SCALE_AND_OFFSET_PROP = Shader.PropertyToID("_LightmapScaleAndOffset");
        private static readonly int VLM_TEXTURE_PROP = Shader.PropertyToID("_VLM_Texture");
        private static readonly int VLM_BOUNDING_BOX_MIN = Shader.PropertyToID("_VLM_BoundingBoxMin");
        private static readonly int VLM_INV_VOLUME_SIZE = Shader.PropertyToID("_VLM_InvVolumeSize");
        private static readonly int VLM_LOCAL_TO_WORLD = Shader.PropertyToID("_VLM_LocalToWorld");
        private static readonly int VLM_WORLD_TO_LOCAL = Shader.PropertyToID("_VLM_WorldToLocal");

        private ShadowMapPass m_shadowPass = new ShadowMapPass();
        private Skydome m_skyPass = new Skydome();
        private LightLoop m_lightLoop = new LightLoop();
        private PostProcessPass m_postProcessPass = new PostProcessPass();

        private int m_MSAASampleCount = 1;
        private FBO m_mainFbo;

        public override void Init(Camera camera)
        {
            base.Init(camera);

            m_shadowPass.Init();
            m_skyPass.Init();
            m_lightLoop.Init();
            m_postProcessPass.Init();

            FBO.Desc desc = new FBO.Desc();
            desc.SetColorTarget(0, camera.targetTexture ? camera.targetTexture.format : RenderTextureFormat.RGB111110Float)
                .SetDepthStencilTarget(DepthTextureFormat.D24S8)
                .SetMultiSamples(m_MSAASampleCount);

            m_mainFbo = FBO.Create(desc, 0, 0);

            SetupLightmap();

            SceneEventDelegate.OnPrecomputedGIChanged -= SetupLightmap;
            SceneEventDelegate.OnPrecomputedGIChanged += SetupLightmap;
        }

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (disposing)
            {
                m_shadowPass.Dispose();
                m_skyPass.Dispose();
                m_lightLoop.Dispose();
                m_postProcessPass.Dispose();

                m_mainFbo.Dispose();
            }

            SceneEventDelegate.OnPrecomputedGIChanged -= SetupLightmap;

            base.Dispose(disposing);
        }

        protected override void OnResize(SRPCamera camera, SRPRenderSettings settings)
        {
            m_mainFbo.Resize(m_width, m_height);
        }

        void PostProcessPass(CommandBuffer cmd, SRPCamera camera, SRPRenderSettings Setting, RenderTexture srcRT, RenderTexture srcDS)
        {
            m_postProcessPass.Execute(cmd, camera, Setting, srcRT, srcDS, CommonUtils.GetCameraTexture(camera.unityCamera));

#if UNITY_EDITOR
            if (camera.unityCamera.cameraType == CameraType.SceneView)
            {
                CommonUtils.Blit(cmd, srcDS, CommonUtils.GetCameraTexture(camera.unityCamera), BlitPass.ColorEncoding.DepthOnly);
            }
#endif
        }

        void SetupLightmap()
        {
            var precomputedLightingManager = UnityEngine.Object.FindAnyObjectByType<PrecomputedLightingManager>();
            foreach (var lightmappedRenderer in precomputedLightingManager.lightmappedRenderers)
            {
                if (lightmappedRenderer == null)
                {
                    continue;
                }

                var rendererData = RuntimeUtils.GetAdditionalRendererData(lightmappedRenderer);
                var propBlock = new MaterialPropertyBlock();
                lightmappedRenderer.GetPropertyBlock(propBlock);
                propBlock.SetTexture(LIGHTMAP0_PROP, precomputedLightingManager.GetLightmap());
                propBlock.SetVector(LIGHTMAP_SCALE_AND_OFFSET_PROP, rendererData.scaleAndOffset);
                lightmappedRenderer.SetPropertyBlock(propBlock);
            }
        }

        protected override void OnRender(GfxRenderContext context,
                                         CommandBuffer commands,
                                         SRPCamera camera,
                                         SRPRenderSettings settings,
                                         WorldData worldData)
        {
            using (new ProfilingSample(commands, "PerFrame Setup"))
            {
                MaterialSystem.SetupPrimaryCamera(commands, settings, camera);
            }

            using (new ProfilingSample(commands, "Shadow"))
            {
                m_shadowPass.Render(context, commands, camera, worldData.GetAllVisiblePunctualLight());
            }

            using (new ProfilingSample(commands, "LightLoop"))
            {
                m_lightLoop.Build(worldData, camera);
                m_lightLoop.Setup(commands, camera);
            }

            context.SetupCameraProperties(camera, commands);

            using (new ProfilingSample(commands, "LightingPass Material Setup"))
            {
                MaterialSystem.SetupLightingPass(commands, camera, worldData, settings, m_shadowPass.ShadowData);
            }

            using (new ProfilingSample(commands, "Setup Frame Buffer"))
            {
#if UNITY_EDITOR || PLATFORM_STANDALONE
                var storeAction = RenderBufferStoreAction.Store;
#else
                var storeAction = RenderBufferStoreAction.Resolve;
#endif

                // clear render targets
                CommonUtils.ApplyFbo(commands,
                                   m_mainFbo,
                                   RenderBufferLoadAction.DontCare,
                                   storeAction,
                                   RenderBufferLoadAction.DontCare,
                                   storeAction);
                ClearFrameBuffer(commands, camera);
            }

            using (new ProfilingSample(commands, "Opaque-Lightmapped"))
            {
                commands.EnableShaderKeyword(LIGHTMAP_KEYWORD);

                ShaderTagId[] passes = { CommonUtils.ShaderPassTagId(ShaderPass.ForwardBase) };

                GfxCullingResults cullingResults;
                context.GetCullingResults(camera, out cullingResults);

                RendererListDesc desc = new RendererListDesc(passes, cullingResults.Result, camera.unityCamera);
                desc.renderQueueRange = RenderQueueRange.opaque;
                desc.sortingCriteria = SortingCriteria.CommonOpaque;
                desc.renderingLayerMask = LIGHTMAP_LAYER_MASK;

                CommonUtils.DrawRendererList(context.SRPContext(), commands, context.SRPContext().CreateRendererList(desc));

                commands.DisableShaderKeyword(LIGHTMAP_KEYWORD);
            }

            using (new ProfilingSample(commands, "Opaque-VLM"))
            {
                var precomputedLightingManager = UnityEngine.Object.FindAnyObjectByType<PrecomputedLightingManager>();
                var volumetricLightmapVolume = UnityEngine.Object.FindAnyObjectByType<VolumetricLightmapVolume>();

                commands.EnableShaderKeyword(VLM_KEYWORD);

                if (precomputedLightingManager != null && volumetricLightmapVolume != null)
                {
                    commands.SetGlobalTexture(VLM_TEXTURE_PROP, precomputedLightingManager.GetVolumetricLightmap());
                    commands.SetGlobalVector(VLM_BOUNDING_BOX_MIN, volumetricLightmapVolume.GetBoundingBoxMin());
                    commands.SetGlobalVector(VLM_INV_VOLUME_SIZE, volumetricLightmapVolume.GetInvVolumeSize());
                    commands.SetGlobalMatrix(VLM_LOCAL_TO_WORLD, volumetricLightmapVolume.GetLocalToWorldMatrix());
                    commands.SetGlobalMatrix(VLM_WORLD_TO_LOCAL, volumetricLightmapVolume.GetWorldToLocalMatrix());
                }

                ShaderTagId[] passes = { CommonUtils.ShaderPassTagId(ShaderPass.ForwardBase) };

                GfxCullingResults cullingResults;
                context.GetCullingResults(camera, out cullingResults);

                RendererListDesc desc = new RendererListDesc(passes, cullingResults.Result, camera.unityCamera);
                desc.renderQueueRange = RenderQueueRange.opaque;
                desc.sortingCriteria = SortingCriteria.CommonOpaque;
                desc.renderingLayerMask = VLM_LAYER_MASK;

                CommonUtils.DrawRendererList(context.SRPContext(), commands, context.SRPContext().CreateRendererList(desc));

                commands.DisableShaderKeyword(VLM_KEYWORD);
            }

            if (camera.clearFlags == CameraClearFlags.Skybox && worldData.envLight != null)
            {
                using (new ProfilingSample(commands, "Skydome"))
                {
                    m_skyPass.Render(commands,
                                     camera,
                                     worldData.envLight.SkyTexture,
                                     worldData.envLight.Intensity,
                                     worldData.envLight.RotationAngle);
                }
            }

            using (new ProfilingSample(commands, "Transparent"))
            {
                GfxCullingResults cullingResults;
                context.GetCullingResults(camera, out cullingResults);

                RendererListDesc desc = new RendererListDesc(CommonUtils.ShaderPassTagId(ShaderPass.ForwardBase),
                                                             cullingResults.Result,
                                                             camera.unityCamera);
                desc.renderQueueRange = RenderQueueRange.transparent;
                desc.sortingCriteria = SortingCriteria.CommonTransparent;

                CommonUtils.DrawRendererList(context.SRPContext(), commands, context.SRPContext().CreateRendererList(desc));
            }

            RenderTexture resolvedColorBuffer;
            RenderTexture resolvedDepthBuffer;
            resolvedColorBuffer = m_mainFbo.GetColorTarget();
            resolvedDepthBuffer = m_mainFbo.GetDepthStencilTarget();

            using (new ProfilingSample(commands, "Post Processing"))
            {
                PostProcessPass(commands, camera, settings, resolvedColorBuffer, resolvedDepthBuffer);
            }
        }
    }
}