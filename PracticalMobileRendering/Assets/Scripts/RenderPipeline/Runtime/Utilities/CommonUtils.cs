using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityObject = UnityEngine.Object;


namespace PMRP
{
    public static class CommonUtils
    {
        public static GraphicsBuffer EmptyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Raw, 1, 4);

        public static void Reset()
        {
            if (s_blitPass != null)
            {
                s_blitPass.Dispose();
                s_blitPass = null;
            }

            if (EmptyBuffer != null)
            {
                EmptyBuffer.Release();
                EmptyBuffer = null;
            }
        }

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs  = rhs;
            rhs  = temp;
            temp = default(T);
        }

        public static float Luminance_sRGB(Color color)
        {
            return color.r * 0.2126729f + color.g * 0.7151522f + color.b * 0.072175f;
        }

        public static int CalcNumOfMipmaps(int size)
        {
            Debug.Assert(Mathf.IsPowerOfTwo(size));
            return (int)Mathf.Log(size, 2) + 1;
        }

        public static int DivideRoundUp(int numerator, int denominator)
        {
            return Math.Max(0, (numerator + denominator - 1) / denominator);
        }

        public static uint DivideRoundUp(uint numerator, uint denominator)
        {
            return Math.Max(0, (numerator + denominator - 1) / denominator);
        }

        public static float UnityNearClipValue()
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                // reverse z is enabled for all [0,1] clip space coordinate
                return 1.0f;
            }
            else
            {
                // OGL family
                return -1.0f;
            }
        }

        public static bool GraphicsApiOpenGL()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
                   SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
        }

        public static Matrix4x4 NdcToUvMatrix()
        {
            return s_NdcToUvMat;
        }

        public static void ComputeCameraDifferentials(Matrix4x4 invProjMat, float z, int width, int height,
                                                      out Vector3 dxCamera, out Vector3 dyCamera)
        {
            Vector3 p  = invProjMat.MultiplyPoint(new Vector3(0, 0, z));
            Vector3 px = invProjMat.MultiplyPoint(new Vector3(1.0f / width, 0, z));
            Vector3 py = invProjMat.MultiplyPoint(new Vector3(0, 1.0f / height, z));
            dxCamera = px - p;
            dyCamera = py - p;
        }

        public static void Destroy(UnityObject obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    UnityObject.Destroy(obj);
                else
                    UnityObject.DestroyImmediate(obj);
#else
                UnityObject.Destroy(obj);
#endif
            }
        }

        public static Shader FindShaderByPath(string resourceRelativePath)
        {
            return FindResourceByPath<Shader>(resourceRelativePath);
        }

        public static ComputeShader FindComputeShaderByPath(string resourceRelativePath)
        {
            return FindResourceByPath<ComputeShader>(resourceRelativePath);
        }

        public static T FindResourceByPath<T>(string resourceRelativePath) where T : UnityEngine.Object
        {
            var res = Resources.Load<T>(resourceRelativePath);
            if (!res)
            {
                throw new FileNotFoundException(String.Format("Fail to find resource [{0}]", resourceRelativePath));
            }

            return res;
        }

        // Hack to avoid GC
        private static RenderBufferLoadAction[] s_colorBufferLoadActions = new RenderBufferLoadAction[8];
        private static RenderBufferStoreAction[] s_colorBufferStoreActions = new RenderBufferStoreAction[8];
        private static RenderTargetBinding[] s_renderTargetBindings = null;

        public static RenderTargetBinding GetRenderTargetBinding(int numCb)
        {
            if (s_renderTargetBindings == null)
            {
                s_renderTargetBindings = new RenderTargetBinding[9];
                for (int i = 1; i < 9; ++i)
                {
                    s_renderTargetBindings[i].colorRenderTargets = new RenderTargetIdentifier[i];
                    s_renderTargetBindings[i].colorLoadActions   = new RenderBufferLoadAction[i];
                    s_renderTargetBindings[i].colorStoreActions  = new RenderBufferStoreAction[i];
                }
            }

            Debug.Assert(1 <= numCb && numCb <= 8);
            return s_renderTargetBindings[numCb];
        }

        public static void ApplyFbo(CommandBuffer cmd,
                                    FBO fbo,
                                    RenderBufferLoadAction cbLoadAction = RenderBufferLoadAction.Load,
                                    RenderBufferStoreAction cbStoreAction = RenderBufferStoreAction.Store,
                                    RenderBufferLoadAction dsLoadAction = RenderBufferLoadAction.Load,
                                    RenderBufferStoreAction dsStoreAction = RenderBufferStoreAction.Store,
                                    bool readOnlyDepth = false,
                                    bool readOnlyStencil = false)
        {
            int numCb = fbo.GetNumColorTargets();
            RenderBufferLoadAction[] cbLoadActions = s_colorBufferLoadActions;
            RenderBufferStoreAction[] cbStoreActions = s_colorBufferStoreActions;
            for (int i = 0; i < numCb; ++i)
            {
                cbLoadActions[i]  = cbLoadAction;
                cbStoreActions[i] = cbStoreAction;
            }
            ApplyFbo(cmd, fbo, cbLoadActions, cbStoreActions, dsLoadAction, dsStoreAction, readOnlyDepth, readOnlyStencil);
        }

        public static void ApplyFbo(CommandBuffer cmd,
                                    FBO fbo,
                                    RenderBufferLoadAction[] cbLoadActions,
                                    RenderBufferStoreAction[] cbStoreActions,
                                    RenderBufferLoadAction dsLoadAction = RenderBufferLoadAction.Load,
                                    RenderBufferStoreAction dsStoreAction = RenderBufferStoreAction.Store,
                                    bool readOnlyDepth = false,
                                    bool readOnlyStencil = false)
        {
            if (fbo.GetDepthStencilTarget())
            {
                int numCb = fbo.GetNumColorTargets();
                RenderTargetBinding binding = GetRenderTargetBinding(numCb);

                if (numCb > 0)
                {
                    Debug.Assert(numCb <= cbLoadActions.Length &&
                                 numCb <= cbStoreActions.Length);

                    for (int i = 0; i < numCb; ++i)
                    {
                        binding.colorRenderTargets[i] = fbo.GetColorTarget(i);
                        binding.colorLoadActions[i]   = cbLoadActions[i];
                        binding.colorStoreActions[i]  = cbStoreActions[i];
                    }
                }

                binding.depthRenderTarget = fbo.GetDepthStencilTarget();
                binding.depthLoadAction   = dsLoadAction;
                binding.depthStoreAction  = dsStoreAction;

                RenderTargetFlags flags = RenderTargetFlags.None;
                if (readOnlyDepth)
                    flags |= RenderTargetFlags.ReadOnlyDepth;
                if (readOnlyStencil)
                    flags |= RenderTargetFlags.ReadOnlyStencil;
                binding.flags = flags;

                cmd.SetRenderTarget(binding);
            }
            else
            {
                // TODO: MRT without depth
                Debug.Assert(fbo.GetNumColorTargets() == 1);
                cmd.SetRenderTarget(fbo.GetColorTarget(0), cbLoadActions[0], cbStoreActions[0]);
            }
        }

        public static void ClearFbo(CommandBuffer cmd, FBO fbo, bool clearDepth, bool clearColor, Color backgroundColor)
        {
            if (fbo.GetDepthStencilTarget())
            {
                ApplyFbo(cmd, fbo);
                cmd.ClearRenderTarget(clearDepth, clearColor, backgroundColor);
            }
            else
            {
                for (int i = 0; i < fbo.GetNumColorTargets(); ++i)
                {
                    cmd.SetRenderTarget(fbo.GetColorTarget(i));
                    cmd.ClearRenderTarget(clearDepth, clearColor, backgroundColor);
                }
            }
        }

        public static Matrix4x4 GetProjMatrix(Camera camera, bool renderIntoTexture = false)
        {
            return GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderIntoTexture);
        }

        public static Matrix4x4 GetProjMatrix(SRPCamera camera, bool renderIntoTexture = false)
        {
            return GL.GetGPUProjectionMatrix(camera.unityCamera.projectionMatrix, renderIntoTexture);
        }

        public static Matrix4x4 GetNonJitteredProjMatrix(Camera camera, bool renderIntoTexture = false)
        {
            return GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, renderIntoTexture);
        }

        public static Matrix4x4 GetNonJitteredProjMatrix(SRPCamera camera, bool renderIntoTexture = false)
        {
            return GL.GetGPUProjectionMatrix(camera.unityCamera.nonJitteredProjectionMatrix, renderIntoTexture);
        }

        public static Matrix4x4 GetViewProjMatrix(Camera camera, bool renderIntoTexture = false)
        {
            Matrix4x4 viewProjMatrix = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderIntoTexture) * camera.worldToCameraMatrix);
            return viewProjMatrix;
        }

        public static Matrix4x4 GetViewProjMatrix(SRPCamera camera, bool renderIntoTexture = false)
        {
            Matrix4x4 viewProjMatrix = (GL.GetGPUProjectionMatrix(camera.unityCamera.projectionMatrix, renderIntoTexture) * camera.unityCamera.worldToCameraMatrix);
            return viewProjMatrix;
        }

        public static Matrix4x4 GetNonJitteredViewProjMatrix(Camera camera, bool renderIntoTexture = false)
        {
            Matrix4x4 viewProjMatrix = (GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, renderIntoTexture) * camera.worldToCameraMatrix);
            return viewProjMatrix;
        }

        public static Matrix4x4 GetNonJitteredViewProjMatrix(SRPCamera camera, bool renderIntoTexture = false)
        {
            Matrix4x4 viewProjMatrix = (GL.GetGPUProjectionMatrix(camera.unityCamera.nonJitteredProjectionMatrix, renderIntoTexture) * camera.unityCamera.worldToCameraMatrix);
            return viewProjMatrix;
        }

        public static RenderTargetIdentifier GetCameraTexture(Camera camera)
        {
            if (camera.targetTexture)
            {
                return camera.targetTexture;
            }
            return BuiltinRenderTextureType.CameraTarget;
        }

        public static string ShaderPassName(ShaderPass pass)
        {
            switch (pass)
            {
                case ShaderPass.Meta:              return "META";
                case ShaderPass.ShadowCaster:      return "SHADOWCASTER";
                case ShaderPass.ForwardBase:       return "FORWARDBASE";
            }

            Debug.AssertFormat(false, "ShaderPassName: UnknownShaderPass");
            return "";
        }

        private static ShaderTagId s_ShaderTagId_Meta              = new ShaderTagId(ShaderPassName(ShaderPass.Meta));
        private static ShaderTagId s_ShaderTagId_ShadowCaster      = new ShaderTagId(ShaderPassName(ShaderPass.ShadowCaster));
        private static ShaderTagId s_ShaderTagId_ForwardBase       = new ShaderTagId(ShaderPassName(ShaderPass.ForwardBase));

        public static ShaderTagId ShaderPassTagId(ShaderPass pass)
        {
            switch (pass)
            {
                case ShaderPass.Meta:              return s_ShaderTagId_Meta;
                case ShaderPass.ShadowCaster:      return s_ShaderTagId_ShadowCaster;
                case ShaderPass.ForwardBase:       return s_ShaderTagId_ForwardBase;
            }

            Debug.AssertFormat(false, "ShaderPassTagId: UnknownShaderPass");
            return ShaderTagId.none;
        }

        public static void DispatchRays(CommandBuffer cmd, RayTracingShader shader, string rayGenName, ShaderPass shaderPass, UInt32 width, UInt32 height, UInt32 depth, Camera camera = null)
        {
            cmd.SetRayTracingShaderPass(shader, ShaderPassName(shaderPass));
            cmd.DispatchRays(shader, rayGenName, width, height, depth, camera);
        }

        public static void DispatchRays(CommandBuffer cmd, RayTracingShader shader, string rayGenName, ShaderPass shaderPass, SRPCamera camera)
        {
            DispatchRays(cmd, shader, rayGenName, shaderPass, (UInt32) camera.pixelWidth, (UInt32) camera.pixelHeight, 1, camera.unityCamera);
        }

        public static void DrawRendererList(ScriptableRenderContext renderContext, CommandBuffer cmd, RendererList rendererList)
        {
#if UNITY_2023_1_OR_NEWER
            cmd.DrawRendererList(rendererList);
#else
            if (!rendererList.isValid)
                throw new ArgumentException("Invalid renderer list provided to DrawRendererList");

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders or things will be executed out of order
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            if (rendererList.stateBlock == null)
            {
                renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings);
            }
            else
            {
                var renderStateBlock = rendererList.stateBlock.Value;
                renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings, ref renderStateBlock);
            }
#endif
        }

        public static void DrawQuad(CommandBuffer cmd, Material mat, int shaderPass = 0, MaterialPropertyBlock properties = null, bool flip = false)
        {
            if (flip)
            {
                if (!s_filpQuadMesh)
                    s_filpQuadMesh = BuildDrawQuadMesh(flip);
                cmd.DrawMesh(s_filpQuadMesh, Matrix4x4.identity, mat, 0, shaderPass, properties);
            }
            else
            {
                if (!s_quadMesh)
                    s_quadMesh = BuildDrawQuadMesh(flip);
                cmd.DrawMesh(s_quadMesh, Matrix4x4.identity, mat, 0, shaderPass, properties);
            }
        }

        public static void Blit(CommandBuffer          commands,
                                Texture                src,
                                RenderTargetIdentifier dst,
                                BlitPass.ColorEncoding dstEncoding  = BlitPass.ColorEncoding.None,
                                bool                   flipVertical = false,
                                int                    mipLevel     = 0,
                                CubemapFace            cubemapFace  = CubemapFace.Unknown,
                                int                    depthSlice   = 0)
        {
            if (s_blitPass == null)
            {
                s_blitPass = new BlitPass();
                s_blitPass.Init();
            }

            s_blitPass.Swizzle = BlitPass.s_SwizzleRGBA;
            s_blitPass.Encoding = dstEncoding;
            s_blitPass.FlipVertical = flipVertical;
            s_blitPass.Render(commands, src, dst, mipLevel, cubemapFace, depthSlice);
        }

        static void ClearRawBufferData(CommandBuffer commands, BufferWrapper buffer)
        {
            if (s_clearRawBufferData == null)
            {
                s_clearRawBufferData = FindComputeShaderByPath("RenderPass/Utils/ClearRawBufferData");
            }

            int bufferSizeInBytes = buffer.count * buffer.stride;
            int countInQuadBytes = DivideRoundUp(bufferSizeInBytes, 4);

            // Keep in-sync with ClearRawBufferData
            int numItemsPerThreadGroup = 256 * 32;
            int groupSizeX = DivideRoundUp(countInQuadBytes, numItemsPerThreadGroup);

            commands.SetComputeBufferParam(s_clearRawBufferData, 0, "_Buffer", buffer);
            commands.DispatchCompute(s_clearRawBufferData, 0, groupSizeX, 1, 1);
        }

        public static void ClearRawBufferData(CommandBuffer commands, GraphicsBuffer buffer)
        {
            Debug.Assert((buffer.target & GraphicsBuffer.Target.Raw) != 0);
            ClearRawBufferData(commands, new BufferWrapper(buffer, null));
        }

        public static void ClearRawBufferData(CommandBuffer commands, ComputeBuffer buffer)
        {
            ClearRawBufferData(commands, new BufferWrapper(null, buffer));
        }

        static void CopyRawBufferData(CommandBuffer commands,
                                      BufferWrapper srcBuffer, int srcOffsetInBytes, int srcLengthInBytes,
                                      BufferWrapper dstBuffer, int dstOffsetInBytes)
        {
            if (s_copyRawBufferData == null)
            {
                s_copyRawBufferData = FindComputeShaderByPath("RenderPass/Utils/CopyRawBufferData");
            }

            int countInQuadBytes = CommonUtils.DivideRoundUp(srcLengthInBytes, 4);

            // Keep in-sync with CopyRawBufferData
            int numItemsPerThreadGroup = 64 * 16;
            int groupSizeX = DivideRoundUp(countInQuadBytes, numItemsPerThreadGroup);

            commands.SetComputeBufferParam(s_copyRawBufferData, 0, "_SrcBuffer", srcBuffer);
            commands.SetComputeIntParam(s_copyRawBufferData, "_SrcOffsetInBytes", srcOffsetInBytes);
            commands.SetComputeIntParam(s_copyRawBufferData, "_SrcLengthInBytes", srcLengthInBytes);
            commands.SetComputeBufferParam(s_copyRawBufferData, 0, "_DstBuffer", dstBuffer);
            commands.SetComputeIntParam(s_copyRawBufferData, "_DstOffsetInBytes", dstOffsetInBytes);
            commands.DispatchCompute(s_copyRawBufferData, 0, groupSizeX, 1, 1);
        }

        public static void CopyRawBufferData(CommandBuffer commands,
                                             GraphicsBuffer srcBuffer, int srcOffsetInBytes, int srcLengthInBytes,
                                             GraphicsBuffer dstBuffer, int dstOffsetInBytes)
        {
            Debug.Assert((srcBuffer.target & GraphicsBuffer.Target.Raw) != 0);
            Debug.Assert((dstBuffer.target & GraphicsBuffer.Target.Raw) != 0);

            CopyRawBufferData(commands,
                              new BufferWrapper(srcBuffer, null), srcOffsetInBytes, srcLengthInBytes,
                              new BufferWrapper(dstBuffer, null), dstOffsetInBytes);
        }

        public static void CopyRawBufferData(CommandBuffer commands,
                                             ComputeBuffer srcBuffer, int srcOffsetInBytes, int srcLengthInBytes,
                                             ComputeBuffer dstBuffer, int dstOffsetInBytes)
        {
            CopyRawBufferData(commands,
                              new BufferWrapper(null, srcBuffer), srcOffsetInBytes, srcLengthInBytes,
                              new BufferWrapper(null, dstBuffer), dstOffsetInBytes);
        }

        public static void CopyRawBufferData(CommandBuffer commands,
                                             GraphicsBuffer srcBuffer, int srcOffsetInBytes, int srcLengthInBytes,
                                             ComputeBuffer dstBuffer, int dstOffsetInBytes)
        {
            Debug.Assert((srcBuffer.target & GraphicsBuffer.Target.Raw) != 0);

            CopyRawBufferData(commands,
                              new BufferWrapper(srcBuffer, null), srcOffsetInBytes, srcLengthInBytes,
                              new BufferWrapper(null, dstBuffer), dstOffsetInBytes);
        }

        public static void CopyRawBufferData(CommandBuffer commands,
                                             ComputeBuffer srcBuffer, int srcOffsetInBytes, int srcLengthInBytes,
                                             GraphicsBuffer dstBuffer, int dstOffsetInBytes)
        {
            Debug.Assert((dstBuffer.target & GraphicsBuffer.Target.Raw) != 0);

            CopyRawBufferData(commands,
                              new BufferWrapper(null, srcBuffer), srcOffsetInBytes, srcLengthInBytes,
                              new BufferWrapper(dstBuffer, null), dstOffsetInBytes);
        }

        private static Mesh BuildDrawQuadMesh(bool flip)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector2> texCoords = new List<Vector2>();
            for (int vertId = 0; vertId < 3; ++vertId)
            {
                /* 
                 * Draw a triangle like this, uv origin at left bottom (OpenGL style)
                 * v0 _______ v2
                 *   |     /
                 *   |   /
                 *   | /
                 *   v1 
                 */
                Vector2 uv = new Vector2((vertId & 0x02) * 1.0f, (vertId & 0x01) * 2.0f);
                Vector3 p  = new Vector3(uv.x * 2 - 1, -uv.y * 2 + 1, 0);
                // Convert to OGL style uv coordinate convention
                if (!flip)
                    uv.y = 1.0f - uv.y;

                // Apply OGL to Native API transform matrix since we will NOT apply it in shader
                Matrix4x4 m = GL.GetGPUProjectionMatrix(Matrix4x4.identity, true);
                p = m.MultiplyPoint(p);

                positions.Add(p);
                texCoords.Add(uv);
            }

            int[] indices = new int[3] {0, 1, 2};

            var mesh = new Mesh();
            mesh.SetVertices(positions);
            mesh.SetUVs(0, texCoords);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            
            return mesh;
        }


        private static BlitPass s_blitPass;
        private static Mesh s_quadMesh;
        private static Mesh s_filpQuadMesh;

        private static ComputeShader s_clearRawBufferData;
        private static ComputeShader s_copyRawBufferData;

        // We should check if graphicsDeviceType is OpenGL*, but it's interchangeable with usesReversedZBuffer for current version of unity
        // and it's unlikely to break this assumption in future version
        private static Matrix4x4 s_NdcToUvMat = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, SystemInfo.usesReversedZBuffer ? 0 : 0.5f)) *
                                                Matrix4x4.Scale(new Vector3(0.5f, 0.5f, SystemInfo.usesReversedZBuffer ? 1 : 0.5f));

        private struct BufferWrapper
        {
            public GraphicsBuffer graphicsBuffer;
            public ComputeBuffer  computeBuffer;

            public int count { get { return graphicsBuffer != null ? graphicsBuffer.count : computeBuffer.count; } }
            public int stride { get { return graphicsBuffer != null ? graphicsBuffer.stride : computeBuffer.stride; } }

            public BufferWrapper(GraphicsBuffer graphicsBuffer_, ComputeBuffer computeBuffer_)
            {
                graphicsBuffer = graphicsBuffer_;
                computeBuffer = computeBuffer_;
            }

            public void SetComputeBufferParam(CommandBuffer commands, ComputeShader computeShader, int kernelIndex, string name)
            {
                if (graphicsBuffer != null)
                    commands.SetComputeBufferParam(computeShader, kernelIndex, name, graphicsBuffer);
                else
                    commands.SetComputeBufferParam(computeShader, kernelIndex, name, computeBuffer);
            }
        };

        private static void SetComputeBufferParam(this CommandBuffer commands, ComputeShader computeShader, int kernelIndex, string name, BufferWrapper buffer)
        {
            if (buffer.graphicsBuffer != null)
                commands.SetComputeBufferParam(computeShader, kernelIndex, name, buffer.graphicsBuffer);
            else
                commands.SetComputeBufferParam(computeShader, kernelIndex, name, buffer.computeBuffer);
        }
    }
}
