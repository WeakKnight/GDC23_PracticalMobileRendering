using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace PMRP
{
    class CameraRendererBundle
    {
        public int             RefCount;
        public SRPCamera        Camera;
        public ICameraRenderer Renderer;

        public void Cleanup()
        {
            if (Renderer != null)
                Renderer.Dispose();
        }

        public void ResetRefCount()
        {
            RefCount = 3;
        }
    }

    public partial class MobileRenderPipeline : RenderPipeline
    {
        private int m_frameId = 0;

        private CommandBuffer m_commands;
        private Dictionary<int, CameraRendererBundle> m_rendererBundleDict     = new Dictionary<int, CameraRendererBundle>();
        private Dictionary<int, CameraRendererBundle> m_tempRendererBundleDict = new Dictionary<int, CameraRendererBundle>();

        public MobileRenderPipeline()
        {
            Build();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            Cleanup();

            base.Dispose(disposing);
        }

        private void Build()
        {
            // Empty name of command buffer otherwise Unity runtime will insert profiler marker "Unnamed command buffer"
            // so that user provided marker will mismatch with CommandBuffer.Clear
            m_commands = new CommandBuffer();
            m_commands.name = "";
        }

        private void Cleanup()
        {
            CommonUtils.Reset();
            CleanupRendererBundles();
        }

        private void CleanupRendererBundles()
        {
            foreach (var kv in m_rendererBundleDict)
            {
                kv.Value.Cleanup();
            }
            m_rendererBundleDict.Clear();
        }

        ICameraRenderer CreateRenderer(SRPCamera camera)
        {
            ICameraRenderer renderer = SRPCamera.CreateRenderer(camera.RendererType);
            renderer.Init(camera.unityCamera);
            return renderer;
        }

        bool Update(Camera[] cameras)
        {
            m_tempRendererBundleDict.Clear();
            foreach (var camera in cameras)
            {
                CameraRendererBundle bundle = null;

                if (m_rendererBundleDict.ContainsKey(camera.GetInstanceID()))
                {
                    bundle = m_rendererBundleDict[camera.GetInstanceID()];

                    bool reset = bundle.Camera.ResetRenderer;
#if UNITY_EDITOR
                    reset = reset || !bundle.Renderer.IsValid();
#endif

                    if (reset)
                    {
                        bundle.Camera.ResetRenderer = false;

                        bundle.Cleanup();
                        bundle.Renderer = CreateRenderer(bundle.Camera);
                    }
                }
                else
                {
                    SRPCamera aCamera = camera.gameObject.GetComponent<SRPCamera>();
                    if (aCamera == null)
                        aCamera = camera.gameObject.AddComponent<SRPCamera>();

                    bundle = new CameraRendererBundle();
                    bundle.Camera = aCamera;
                    bundle.Renderer = CreateRenderer(aCamera);
                }

                bundle.ResetRefCount();
                m_tempRendererBundleDict[camera.GetInstanceID()] = bundle;
            }

            // Cleanup renderers that are not needed
            foreach (var kv in m_rendererBundleDict)
            {
                if (!m_tempRendererBundleDict.ContainsKey(kv.Key))
                {
                    // Decrease reference count once per frame
                    if (m_frameId != Time.frameCount)
                    {
                        kv.Value.RefCount -= 1;
                    }

                    if (kv.Value.RefCount <= 0)
                    {
                        // No ticking, but ref = 0 or invalid, dispose it and remove from list
                        kv.Value.Cleanup();
                    }
                    else
                    {
                        // RefCount is not equal to 0, copy to tempDict so that we can still access it
                        m_tempRendererBundleDict[kv.Key] = kv.Value;
                    }
                }
            }

            CommonUtils.Swap(ref m_rendererBundleDict, ref m_tempRendererBundleDict);
            m_tempRendererBundleDict.Clear();

            m_frameId = Time.frameCount;
            return true;
        }

#if UNITY_EDITOR
        void SetupDebugOptions(CommandBuffer commands, SRPRenderSettings renderSettings)
        {
            CommonSetting commonSetting = renderSettings.GetSettingComponent<CommonSetting>();
            commands.SetGlobalInt("g_DebugFlagsLightingComponents", (int)commonSetting.LightingComponents);
            if (commonSetting.ViewMode == YAViewMode.Lit)
            {
                commands.SetGlobalVector("g_DebugDiffuseOverrideParameter", new Vector4(0, 0, 0, 1));
                commands.SetGlobalVector("g_DebugSpecularOverrideParameter", new Vector4(0, 0, 0, 1));
            }
            else
            {
                commands.SetGlobalVector("g_DebugDiffuseOverrideParameter", new Vector4(0.3f, 0.3f, 0.3f, 0));
                if (commonSetting.ViewMode == YAViewMode.DetailLighting)
                    commands.SetGlobalVector("g_DebugSpecularOverrideParameter", new Vector4(0.1f, 0.1f, 0.1f, 0));
                else
                    commands.SetGlobalVector("g_DebugSpecularOverrideParameter", new Vector4(0, 0, 0, 0));
            }
        }
        
        void RenderWireFrame(GfxRenderContext context, CommandBuffer commands, SRPCamera camera, RenderTargetIdentifier backbuffer)
        {
            if (GL.wireframe)
            {
                using (new ProfilingSample(commands, "Render Wire Frame"))
                {
                    commands.SetRenderTarget(backbuffer);
                    commands.ClearRenderTarget(false, true, camera.backgroundColor);
                    commands.SetViewProjectionMatrices(camera.unityCamera.worldToCameraMatrix, camera.unityCamera.projectionMatrix);

                    GfxCullingResults cullingResults;
                    context.GetCullingResults(camera, out cullingResults);

                    RendererListDesc desc = new RendererListDesc(CommonUtils.ShaderPassTagId(ShaderPass.ForwardBase),
                                                                 cullingResults.Result,
                                                                 camera.unityCamera);
                    desc.renderQueueRange = RenderQueueRange.all;
                    CommonUtils.DrawRendererList(context.SRPContext(), commands, context.SRPContext().CreateRendererList(desc));
                }
            }
        }
#endif

#if UNITY_EDITOR
        void SaveScreenshotToFile(GfxRenderContext context, CommandBuffer commands, SRPCamera camera)
        {
            if (camera.unityCamera.cameraType == CameraType.SceneView && camera.CaptureScreenshot)
            {
                camera.CaptureScreenshot = false;

                context.ExecuteCommandBuffer(commands, true);
                context.Submit();

                EditorUtils.ExportRenderTextureToFile(camera.unityCamera.targetTexture,
                                                           EditorUtils.ETextureExportFormat.EXR,
                                                           camera.ScreenshotFilename);
            }
        }
#endif

        void RenderInternal(ScriptableRenderContext context, Camera[] cameras)
        {
            m_commands.Clear();

            GfxRenderContext gfxContext = new GfxRenderContext(context);
            CommandBuffer commands = m_commands;

            foreach (var camera in cameras)
            {
                CameraRendererBundle bundle = m_rendererBundleDict[camera.GetInstanceID()];

                WorldData worldData = TinySceneManager.GetInstance()?.GetWorldData();
                if (worldData == null)
                {
                    Debug.LogWarning("No world data found, have you add TinySceneManager in the scene?");
                    continue;
                }

                var settings = worldData.renderSettings;
                if (settings == null)
                {
                    GameObject go = new GameObject("Render Settings");
                    settings = go.AddComponent<SRPRenderSettings>();
                    worldData.renderSettings = settings;
                }

                bundle.Camera.BeginCameraRender();
                //if (camera.cameraType == CameraType.Game)
                    bundle.Camera.ScreenPercentage = Mathf.Max(settings.GetSettingComponent<CommonSetting>().PrimaryScreenPercentage, 0.25f);

                using (new ProfilingSample(commands, camera.name))
                {
                    worldData.UpdateShadowDistance(bundle.Camera);

                    // Update culling information per camera
                    GfxCullingParameters cullingParam = GfxCullingParameters.Create(bundle.Camera);
                    cullingParam.SetShadowDistance(worldData.shadowDistance.y);

                    GfxCullingResults cullingResults = gfxContext.UpdateCullingResults(bundle.Camera, cullingParam);
                    worldData.ApplyCullingResults(cullingResults);

#if UNITY_EDITOR
                    if (GL.wireframe)
                    {
                        RenderWireFrame(gfxContext, commands, bundle.Camera, BuiltinRenderTextureType.CameraTarget);
                        break;
                    }

                    SetupDebugOptions(commands, settings);
#endif

                    bundle.Renderer.Render(gfxContext,commands, bundle.Camera, settings, worldData);
                }
				
#if UNITY_EDITOR
                if (bundle.Camera.unityCamera.cameraType == CameraType.SceneView &&
                    UnityEditor.Handles.ShouldRenderGizmos())
                {
                    gfxContext.SetupCameraProperties(bundle.Camera, commands);

                    gfxContext.SRPContext().DrawWireOverlay(camera);

                    if (UnityEditor.Handles.ShouldRenderGizmos())
                        gfxContext.DrawGizmos(bundle.Camera, GizmoSubset.PostImageEffects);
                }
#endif

                bundle.Camera.EndCameraRender();

#if UNITY_EDITOR
                SaveScreenshotToFile(gfxContext, commands, bundle.Camera);
#endif
            }

            gfxContext.ExecuteCommandBuffer(commands, true);
            gfxContext.Submit();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(string.Format("{0} has been disposed. Do not call Render on disposed RenderLoops.", this));
            }

            if (Update(cameras))
            {
                RenderInternal(context, cameras);
            }
        }
    }
}

