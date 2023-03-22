#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public sealed class IntegrateSpecularOcclusion : IRenderPass
    {
        public RenderTexture SpecularOcclusionLut3D;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (SpecularOcclusionLut3D != null)
            {
                SpecularOcclusionLut3D.Release();
                SpecularOcclusionLut3D = null;
            }

            base.Dispose(disposing);
        }

        public void Init()
        {
        }

        void IntegrateLut3D()
        {
            if (SpecularOcclusionLut3D != null)
                SpecularOcclusionLut3D.Release();

            const int LutSize = ShaderConfig.s_SpecularOcclusionLutSize;

            SpecularOcclusionLut3D = new RenderTexture(LutSize * LutSize, LutSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            SpecularOcclusionLut3D.useMipMap  = false;
            SpecularOcclusionLut3D.filterMode = FilterMode.Bilinear;
            SpecularOcclusionLut3D.wrapMode   = TextureWrapMode.Clamp;
            SpecularOcclusionLut3D.Create();

            Material material = new Material(CommonUtils.FindShaderByPath("Editor/IntegrateSpecularOcclusion"));
            
            CommandBuffer cmd = new CommandBuffer();
            for (int i = 0; i < LutSize; ++i)
            {
                float texcoordZ = (float) (i + 0.5) / LutSize;

                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                propBlock.SetFloat("_TexcoordZ", texcoordZ);

                cmd.SetRenderTarget(SpecularOcclusionLut3D);
                cmd.SetViewport(new Rect(LutSize * i, 0, LutSize, LutSize));
                CommonUtils.DrawQuad(cmd, material, material.FindPass("LUT3D"), propBlock);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }
        
        public void Execute()
        {
            IntegrateLut3D();
        }
    }
}

#endif
