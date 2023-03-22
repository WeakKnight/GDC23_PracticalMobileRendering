using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public class ShadowMapData
    {
        public RenderTexture ShadowTexture;

        public int[] SplitCount = new int[ShaderConfig.s_MaxPunctualLights + 1];
        public int[] SplitOffset = new int[ShaderConfig.s_MaxPunctualLights + 1];

        public Matrix4x4[] ViewMatrices        = new Matrix4x4[(int) ShaderOptions.MaxShadowSplits];
        public Matrix4x4[] ProjMatrices        = new Matrix4x4[(int) ShaderOptions.MaxShadowSplits];
        public Matrix4x4[] ShadowMatrices      = new Matrix4x4[(int) ShaderOptions.MaxShadowSplits];
        public Vector4[]   ShadowSplitsUvRange = new Vector4[(int) ShaderOptions.MaxShadowSplits];
        public Vector4[]   ShadowAtlasCoords   = new Vector4[(int) ShaderOptions.MaxShadowSplits];
        public Vector4[]   ShadowNormalBias    = new Vector4[(int) ShaderOptions.MaxShadowSplits];

        public int         CascadeCount;
        public Vector4[]   CSMValidUv        = new Vector4[ShaderConfig.s_CsmMaxCascades];
        public Matrix4x4[] CSMShadowMatrices = new Matrix4x4[ShaderConfig.s_CsmMaxCascades];
        public Vector4     CSMNormalBias;
    }

    public sealed class ShadowMapPass : IRenderPass
    {
        private const int k_ShadowAtlasSize = 2048;
        private const int k_DepthBufferBits = 24;

        private ShadowMapData m_shadowData = new ShadowMapData();
        
        public ShadowMapData ShadowData
        {
            get { return m_shadowData;  }
        }

        private RenderTexture m_shadowTexture;
        
        private RenderTexture ShadowTexture
        {
            get
            {
                if (m_shadowTexture == null)
                {
                    m_shadowTexture      = new RenderTexture(k_ShadowAtlasSize, k_ShadowAtlasSize, k_DepthBufferBits, RenderTextureFormat.Shadowmap);
                    m_shadowTexture.name = "ShadowMap Atlas";
                }
                return m_shadowTexture;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (m_shadowTexture != null)
            {
                m_shadowTexture.Release();
                m_shadowTexture = null;
            }

            base.Dispose(disposing);
        }

        public void Init()
        {
        }

        private int GetNumOfShadowSplits(SRPCamera viewCamera, SRPLight aLight)
        {
            if (aLight.Type == LightType.Spot)
            {
                return 1;
            }
            else if (aLight.Type == LightType.Point)
            {
                return 6;
            }
            else if (aLight.Type == LightType.Directional)
            {
                return aLight.ShadowSetting.GetDirectionalShadowSplitsCount();
            }

            return 0;
        }

        public Vector4 GetAtlasUvScaleOffset(Vector4 coord, Vector2 size)
        {
            return new Vector4(coord.z / size.x, coord.w / size.y,
                               coord.x / size.x, coord.y / size.y);
        }

        private bool UpdateShadowSplitsAssignment(SRPCamera viewCamera, List<SRPLight> lights)
        {
            // Reset CSM
            m_shadowData.CascadeCount = 0;

            // Step 1: loop over all lights, calculate shadow split count and index offset for each light
            int numSplitsInTotal = 0;
            for (int i = 0; i < lights.Count; ++i)
            {
                SRPLight aLight = lights[i];

                if (aLight.ShadowCasterBoundsVisibility)
                {
                    int numSplits = GetNumOfShadowSplits(viewCamera, lights[i]);
                    m_shadowData.SplitOffset[i] = numSplitsInTotal;
                    m_shadowData.SplitCount[i] = numSplits;
                    numSplitsInTotal += numSplits;

                    aLight.ShadowSplitIndex = m_shadowData.SplitOffset[i];
                }
                else
                {
                    m_shadowData.SplitOffset[i] = 0;
                    m_shadowData.SplitCount[i] = 0;

                    aLight.ShadowSplitIndex = -1;
                }
            }

            m_shadowData.ShadowTexture = numSplitsInTotal > 0 ? ShadowTexture : null;


            // Step 2: sort all lights by the size of shadow map resolution, we will do shadow atlas allocation for bigger shadowmap
            int[] sortedLightIndices = new int[lights.Count];
            for (int i = 0; i < sortedLightIndices.Length; ++i)
            {
                sortedLightIndices[i] = i;
            }
            Array.Sort(sortedLightIndices, delegate (int a, int b)
            {
                int aSize = (int)lights[a].ShadowSetting.ShadowMapResolution;
                int bSize = (int)lights[b].ShadowSetting.ShadowMapResolution;
                if (aSize == bSize)
                    return 0;
                else if (aSize > bSize)
                    return 1;
                else
                    return -1;
            });


            // TODO: optimization

            // Step 3: stupid shadow atlas allocation
            int currentX = 0;
            int currentY = 0;
            int yNextRowAdd = 0;

            for (int j = 0; j < sortedLightIndices.Length; ++j)
            {
                int lightIdx = sortedLightIndices[j];

                int numSplits = m_shadowData.SplitCount[lightIdx];
                int splitIdx = m_shadowData.SplitOffset[lightIdx];

                if (numSplits <= 0)
                    continue;

                SRPLight aLight = lights[lightIdx];
                int shadowMapRez = (int)aLight.ShadowSetting.ShadowMapResolution;

                int splitIdxEnd = splitIdx + numSplits;
                for (; splitIdx < splitIdxEnd; ++splitIdx)
                {
                    bool found = false;
                    Rect rect = Rect.zero;
                    while (true)
                    {
                        int nextX = currentX + shadowMapRez;
                        int nextY = currentY + shadowMapRez;

                        if (nextX <= k_ShadowAtlasSize &&
                            nextY <= k_ShadowAtlasSize)
                        {
                            rect        =  new Rect(currentX, currentY, shadowMapRez, shadowMapRez);
                            currentX    += shadowMapRez;
                            yNextRowAdd =  Mathf.Max(yNextRowAdd, shadowMapRez);
                            found       =  true;
                            break;
                        }
                        else if (nextY > k_ShadowAtlasSize)
                        {
                            break;
                        }
                        else
                        {
                            currentX    =  0;
                            currentY    += yNextRowAdd;
                            yNextRowAdd =  0;
                        }
                    }

                    if (found)
                    {
                        Vector4 atlasCoord = new Vector4(rect.x, rect.y, rect.width, rect.height);
                        m_shadowData.ShadowAtlasCoords[splitIdx] = atlasCoord;
                    }
                    else
                    {
                        Debug.LogWarning("Shadow atlas full!!!");
                        m_shadowData.ShadowAtlasCoords[splitIdx] = Vector4.zero;
                    }
                }
            }

            return numSplitsInTotal > 0;
        }

        private void ComputeShadowMatrices(SRPCamera viewCamera, List<SRPLight> lights)
        {
            m_shadowData.CascadeCount = 0;

            // Step 1: compute shadow view projection matrices
            for (int lightIdx = 0; lightIdx < lights.Count; ++lightIdx)
            {
                int numSplits = m_shadowData.SplitCount[lightIdx];
                int splitStart  = m_shadowData.SplitOffset[lightIdx];

                if (numSplits <= 0)
                    continue;

                SRPLight aLight = lights[lightIdx];

                int shadowSplitRez = (int)aLight.ShadowSetting.ShadowMapResolution;

                if (aLight.Type == LightType.Spot)
                {
                    ShadowUtilities.ComputeSpotShadowMatrices(viewCamera.unityCamera, aLight,
                                                              out m_shadowData.ViewMatrices[splitStart],
                                                              out m_shadowData.ProjMatrices[splitStart]);
                }
                else if (aLight.Type == LightType.Point)
                {
                    for (int face = 0; face < numSplits; ++face)
                    {
                        ShadowUtilities.ComputePointShadowMatrices(viewCamera.unityCamera,
                                                                   aLight,
                                                                   (CubemapFace)face,
                                                                   0,
                                                                   out m_shadowData.ViewMatrices[splitStart + face],
                                                                   out m_shadowData.ProjMatrices[splitStart + face]);
                    }
                }
                else if (aLight.Type == LightType.Directional)
                {
                    Debug.Assert(m_shadowData.CascadeCount == 0);
                    Debug.Assert(splitStart == 0);

                    ShadowUtilities.ComputeDirectionalShadowMatrices(viewCamera.unityCamera,
                                                                     aLight,
                                                                     m_shadowData.ViewMatrices,
                                                                     m_shadowData.ProjMatrices,
                                                                     numSplits,
                                                                     splitStart);

                    m_shadowData.CascadeCount = numSplits;

                    for (int cascadeIdx = 0; cascadeIdx < ShaderConfig.s_CsmMaxCascades; ++cascadeIdx)
                    {
                        if (cascadeIdx < numSplits)
                        {
                            int splitIdx = splitStart + cascadeIdx;

                            Vector4 atlasCoord = m_shadowData.ShadowAtlasCoords[splitIdx];
                            Vector2 atlasSize = new Vector2(ShadowTexture.width, ShadowTexture.height);
                            Vector4 uvScaleOffset = GetAtlasUvScaleOffset(atlasCoord, atlasSize);

                            float borderPixels = 4;
                            m_shadowData.CSMValidUv[cascadeIdx] = new Vector4((atlasCoord.x + borderPixels) / atlasSize.x,
                                                                                  (atlasCoord.y + borderPixels) / atlasSize.y,
                                                                                  (atlasCoord.x - borderPixels + atlasCoord.z) / atlasSize.x,
                                                                                  (atlasCoord.y - borderPixels + atlasCoord.w) / atlasSize.y);

                            Matrix4x4 uvScaleOffsetMat = Matrix4x4.Translate(new Vector3(uvScaleOffset.z, uvScaleOffset.w, 0)) * Matrix4x4.Scale(new Vector3(uvScaleOffset.x, uvScaleOffset.y, 1));
                            Matrix4x4 projMat          = GL.GetGPUProjectionMatrix(uvScaleOffsetMat * CommonUtils.NdcToUvMatrix() * m_shadowData.ProjMatrices[splitIdx], false);
                            Matrix4x4 viewMat          = m_shadowData.ViewMatrices[splitIdx];
                            m_shadowData.CSMShadowMatrices[cascadeIdx] = projMat * viewMat;
                        }
                        else
                        {
                            m_shadowData.CSMValidUv[cascadeIdx] = Vector4.one;

                            m_shadowData.CSMShadowMatrices[cascadeIdx] = Matrix4x4.zero;
                        }
                    }
                }
            }

            // Step 2: compute normal bias
            for (int lightIdx = 0; lightIdx < lights.Count; ++lightIdx)
            {
                int numSplits = m_shadowData.SplitCount[lightIdx];
                int splitStart = m_shadowData.SplitOffset[lightIdx];

                if (numSplits <= 0)
                    continue;

                SRPLight aLight = lights[lightIdx];

                for (int splitIdx = splitStart; splitIdx < splitStart + numSplits; ++splitIdx)
                {
                    int shadowMapRez = (int)m_shadowData.ShadowAtlasCoords[splitIdx].z;
                    Matrix4x4 projMat = GL.GetGPUProjectionMatrix(m_shadowData.ProjMatrices[splitIdx], true);

                    if (aLight.Type == LightType.Directional)
                    {
                        float normalBias = ShadowUtilities.ComputeDirectionalShadowNormalBias(projMat,
                                                                                              shadowMapRez,
                                                                                              aLight.ShadowSetting.NormalBias);
                        m_shadowData.ShadowNormalBias[splitIdx] = new Vector4(normalBias, 0, 0, 0);

                        int cascadeIdx = splitIdx - splitStart;
                        m_shadowData.CSMNormalBias[cascadeIdx] = normalBias;
                    }
                    else
                    {
                        float nearClipValue = CommonUtils.UnityNearClipValue();

                        Vector3 dxCamera;
                        Vector3 dyCamera;
                        CommonUtils.ComputeCameraDifferentials(projMat.inverse,
                                                             nearClipValue, shadowMapRez, shadowMapRez,
                                                             out dxCamera, out dyCamera);

                        float normalBias = Mathf.Sqrt(dxCamera.x * dxCamera.x + dyCamera.y * dyCamera.y);
                        normalBias *= 1 / aLight.ShadowSetting.NearPlane * aLight.ShadowSetting.NormalBias;
                        m_shadowData.ShadowNormalBias[splitIdx] = new Vector4(normalBias, 0, 0, 0);

                        if (aLight.Type == LightType.Point)
                        {
                            // border pixel for shadow filter
                            float shadowFilterSize = 2;
                            float borderBias = shadowFilterSize * 2 / shadowMapRez;
                            float halfFov = Mathf.Atan(1 + normalBias * Mathf.Sqrt(2) * 2 + borderBias) * Mathf.Rad2Deg;
                            float fovBias = halfFov * 2 - 90;

                            ShadowUtilities.ComputePointShadowMatrices(viewCamera.unityCamera,
                                                                       aLight,
                                                                       (CubemapFace)(splitIdx - splitStart),
                                                                       fovBias,
                                                                       out m_shadowData.ViewMatrices[splitIdx],
                                                                       out m_shadowData.ProjMatrices[splitIdx]);

                        }
                    }
                }
            }

            // Step 3: compute final shadow matrices, take uv offset into account
            for (int i = 0; i < m_shadowData.ShadowMatrices.Length; ++i)
            {
                Vector4 atlasCoord = m_shadowData.ShadowAtlasCoords[i];
                Vector2 atlasSize  = new Vector2(ShadowTexture.width, ShadowTexture.height);

                Vector4 uvScaleOffset = GetAtlasUvScaleOffset(atlasCoord, new Vector2(ShadowTexture.width, ShadowTexture.height));

                Matrix4x4 uvScaleOffsetMat = Matrix4x4.Translate(new Vector3(uvScaleOffset.z, uvScaleOffset.w, 0)) *
                                             Matrix4x4.Scale(new Vector3(uvScaleOffset.x, uvScaleOffset.y, 1));
                Matrix4x4 projMat = GL.GetGPUProjectionMatrix(uvScaleOffsetMat * CommonUtils.NdcToUvMatrix() * m_shadowData.ProjMatrices[i], false);
                Matrix4x4 viewMat = m_shadowData.ViewMatrices[i];
                m_shadowData.ShadowMatrices[i] = projMat * viewMat;

                float borderPixels = 0;
                m_shadowData.ShadowSplitsUvRange[i] = new Vector4((atlasCoord.x                + borderPixels) / atlasSize.x,
                                                                  (atlasCoord.y                + borderPixels) / atlasSize.y,
                                                                  (atlasCoord.x - borderPixels + atlasCoord.z) / atlasSize.x,
                                                                  (atlasCoord.y - borderPixels + atlasCoord.w) / atlasSize.y);
            }
        }

        // Hack to avoid GC
        static Plane[] s_frustumCullingPlanes = new Plane[6];

        private void RenderShadowMap(GfxRenderContext renderContext, CommandBuffer commands, SRPCamera viewCamera, List<SRPLight> lights)
        {
            commands.SetRenderTarget(ShadowTexture);
            commands.ClearRenderTarget(true, true, Color.black, 1.0f);

            for (int i = 0; i < lights.Count; ++i)
            {
                int numSplits = m_shadowData.SplitCount[i];
                int splitIdx  = m_shadowData.SplitOffset[i];

                if (numSplits <= 0)
                    continue;

                SRPLight aLight          = lights[i];
                int     visibleLightIdx = aLight.VisibleLightIndex;

                for (int j = 0; j < numSplits; ++j)
                {
                    GfxShadowDrawingSettings drawingSettings = new GfxShadowDrawingSettings();
                    drawingSettings.VisibleLightIndex = visibleLightIdx;

                    Matrix4x4 projMat = m_shadowData.ProjMatrices[splitIdx + j];
                    Matrix4x4 viewMat = m_shadowData.ViewMatrices[splitIdx + j];

                    Plane[] cullingPlanes = s_frustumCullingPlanes;
                    ShadowUtilities.ComputeCullingPlanes(projMat, viewMat, cullingPlanes);

                    if (aLight.unityLight.type == LightType.Directional)
                    {
                        // Disable near plane culling so that we can project objects behind camera into near culling plane
                        drawingSettings.SplitData.cullingPlaneCount = cullingPlanes.Length - 1;

                        int planeIdx = 0;
                        for (int k = 0; k < cullingPlanes.Length; ++k)
                        {
                            if (k != (int) ShadowUtilities.FrustumPlaneOrder.Near)
                            {
                                drawingSettings.SplitData.SetCullingPlane(planeIdx, cullingPlanes[k]);
                                planeIdx += 1;
                            }
                        }
                    }
                    else
                    {
                        drawingSettings.SplitData.cullingPlaneCount = cullingPlanes.Length;
                        for (int k = 0; k < cullingPlanes.Length; ++k)
                        {
                            drawingSettings.SplitData.SetCullingPlane(k, cullingPlanes[k]);
                        }
                    }

                    Vector4 atlasCoord = m_shadowData.ShadowAtlasCoords[splitIdx + j];
                    int shadowMapSize = (int)atlasCoord.z;

                    commands.SetViewport(new Rect(atlasCoord.x, atlasCoord.y, atlasCoord.z, atlasCoord.w));
                    commands.SetViewProjectionMatrices(viewMat, projMat);

                    Vector3 dxCamera;
                    Vector3 dyCamera;
                    CommonUtils.ComputeCameraDifferentials(GL.GetGPUProjectionMatrix(projMat, true).inverse,
                                                         CommonUtils.UnityNearClipValue(), shadowMapSize, shadowMapSize,
                                                         out dxCamera, out dyCamera);

                    commands.SetGlobalFloat("g_OrthoCamera", aLight.Type == LightType.Directional ? 1.0f : 0.0f);
                    commands.SetGlobalVector("g_DepthBias2", new Vector4(aLight.ShadowSetting.DepthSlopeBias,
                                                                         aLight.ShadowSetting.DepthBiasClamp * dxCamera.x));
                    commands.SetGlobalVector("g_ShadowSplitRez", new Vector2(shadowMapSize, 1.0f / shadowMapSize));
                    commands.SetGlobalVector("g_dxCamera", dxCamera);
                    commands.SetGlobalVector("g_dyCamera", dyCamera);
                    commands.SetGlobalFloat("g_ShadowNearClipPlane", aLight.ShadowSetting.NearPlane);

                    renderContext.DrawShadows(commands, viewCamera, drawingSettings);
                }
            }
        }

        public void Render(GfxRenderContext renderContext, CommandBuffer commands, SRPCamera viewCamera, List<SRPLight> lights)
        {
            if (UpdateShadowSplitsAssignment(viewCamera, lights))
            {
                ComputeShadowMatrices(viewCamera, lights);

                using (new ProfilingSample(commands, "Shadow Map Pass"))
                {
                    RenderShadowMap(renderContext, commands, viewCamera, lights);
                }
            }
        }
    }
}
