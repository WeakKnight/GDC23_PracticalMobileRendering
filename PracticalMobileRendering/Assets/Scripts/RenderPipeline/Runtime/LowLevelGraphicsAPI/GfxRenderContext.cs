using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public struct GfxCullingParameters
    {
#if UNITY_2019_4_OR_NEWER
        public ScriptableCullingParameters Params;
#else
        public CullingParameters Params;
#endif

        public static GfxCullingParameters Create(SRPCamera camera)
        {
            GfxCullingParameters param = new GfxCullingParameters();
#if UNITY_2019_3_OR_NEWER
            camera.unityCamera.TryGetCullingParameters(out param.Params);
#else
            CullResults.GetCullingParameters(camera.unityCamera, out param.Params);
#endif
            param.SetShadowDistance(0);
            return param;
        }

        public void SetShadowDistance(float dist)
        {
            Params.shadowDistance = dist;
        }
    }


    public struct GfxCullingResults
    {
        public bool IsValid;
#if UNITY_2019_4_OR_NEWER
        public CullingResults Result;
#else
        public CullResults Result;
#endif

#if UNITY_2019_4_OR_NEWER
        public GfxCullingResults(CullingResults result)
#else
        public GfxCullingResults(CullResults result)
#endif
        {
            IsValid = true;
            Result = result;
        }

        public int GetVisibleLightIndex(Light light)
        {
            int lightIdx = -1;
            for (int i = 0; i < Result.visibleLights.Length; ++i)
            {
                var visLight = Result.visibleLights[i];
                if (visLight.light == light)
                {
                    lightIdx = i;
                    break;
                }
            }

            return lightIdx;
        }

        public bool ShadowCasterBoundsVisibility(int lightIdx)
        {
            if (lightIdx >= 0)
            {
                Bounds _;
                return Result.GetShadowCasterBounds(lightIdx, out _);
            }
            return false;
        }

        public bool GetShadowCasterBounds(int lightIdx, out Bounds outBounds)
        {
            if (lightIdx >= 0)
                return Result.GetShadowCasterBounds(lightIdx, out outBounds);

            outBounds = new Bounds(Vector3.zero, Vector3.zero);
            return false;
        }
    }

    public struct GfxShadowDrawingSettings
    {
        public int             VisibleLightIndex;
        public ShadowSplitData SplitData;
    }


    public struct GfxRenderContext
    {
        private ScriptableRenderContext m_context;
        private static Dictionary<int, GfxCullingResults> m_cullingResults = new Dictionary<int, GfxCullingResults>();
        private static GfxCullingResults s_invalidCullingResults = new GfxCullingResults{IsValid = false};

        public GfxRenderContext(ScriptableRenderContext context)
        {
            m_cullingResults.Clear();
            m_context = context;
        }

        public ScriptableRenderContext SRPContext()
        {
            return m_context;
        }

        public GfxCullingResults UpdateCullingResults(SRPCamera camera, GfxCullingParameters cullingParams)
        {

#if UNITY_2019_3_OR_NEWER
            CullingResults result = m_context.Cull(ref cullingParams.Params);
#else
            CullResults result = CullResults.Cull(ref cullingParams.Params, m_context);
#endif

            GfxCullingResults cullingResults = new GfxCullingResults(result);
            m_cullingResults[camera.unityCamera.GetInstanceID()] = cullingResults;
            return cullingResults;
        }

        public void GetCullingResults(SRPCamera camera, out GfxCullingResults cullingResults)
        {
            if (m_cullingResults.ContainsKey(camera.unityCamera.GetInstanceID()))
                cullingResults = m_cullingResults[camera.unityCamera.GetInstanceID()];
            else
                cullingResults = s_invalidCullingResults;
        }

        // Setup camera for rendering (sets render target, view/projection matrices and other
        // per-camera built-in shader variables).
        public void SetupCameraProperties(SRPCamera camera, CommandBuffer commands)
        {
            m_context.ExecuteCommandBuffer(commands);
            commands.Clear();

#if UNITY_2019_3_OR_NEWER
            m_context.SetupCameraProperties(camera.unityCamera, false);
#else
            m_context.SetupCameraProperties(camera.unityCamera);
#endif
        }

        public void DrawShadows(CommandBuffer commands, SRPCamera camera, GfxShadowDrawingSettings drawingSettings)
        {
            m_context.ExecuteCommandBuffer(commands);
            commands.Clear();

            GfxCullingResults cullingResults;
            GetCullingResults(camera, out cullingResults);
            if (!cullingResults.IsValid)
            {
                Debug.LogError("Invalid culling result to DrawShadows");
                return;
            }

#if UNITY_2019_3_OR_NEWER
            var settings = new ShadowDrawingSettings(cullingResults.Result, drawingSettings.VisibleLightIndex);
#else
            var settings = new DrawShadowsSettings(cullingResults.Result, drawingSettings.VisibleLightIndex);
#endif

            settings.splitData = drawingSettings.SplitData;
            m_context.DrawShadows(ref settings);
        }

#if UNITY_2020_1_OR_NEWER
        public void DrawGizmos(SRPCamera camera, GizmoSubset subset)
        {
            m_context.DrawGizmos(camera.unityCamera, subset);
        }
#endif

        public void ExecuteCommandBuffer(CommandBuffer commandBuffer, bool clearAfterExec = false)
        {
            m_context.ExecuteCommandBuffer(commandBuffer);

            if (clearAfterExec)
                commandBuffer.Clear();
        }

        public void Submit()
        {
            m_context.Submit();
        }
    }
}

