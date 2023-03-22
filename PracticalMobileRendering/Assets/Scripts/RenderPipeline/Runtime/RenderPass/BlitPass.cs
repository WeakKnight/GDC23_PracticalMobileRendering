using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PMRP
{
    // Helper class for texture processing, Editor only
    public sealed class BlitPass : IRenderPass
    {
        // Keep in-sync with ColorEncoding_* in BlitPass.shader
        public enum ColorEncoding
        {
            None,
            RGBM,
            DepthOnly,
            LinearToGamma,
            GammaToLinear,
        }

        public static Vector4 s_SwizzleRGBA = new Vector4(0, 1, 2, 3);
        public static Vector4 s_SwizzleBGRA = new Vector4(2, 1, 0, 3);
        public static Vector4 s_SwizzleRRRR = new Vector4(0, 0, 0, 0);
        public static Vector4 s_SwizzleAAAA = new Vector4(3, 3, 3, 3);

        public bool FlipVertical { set; get; }

        public Vector2 UVOffset { set; get; }
        
        // Must be integer
        public Vector4 Swizzle { set; get; }

        public Vector4 ColorScale;
        public Vector4 ColorOffset;

        public ColorEncoding Encoding;

        public bool      EnableBlend = false;
        public BlendMode SrcBlend    = BlendMode.One;
        public BlendMode DstBlend    = BlendMode.Zero;
        public BlendOp   BlendOp     = BlendOp.Add;

        private MaterialPropertyBlock m_block;
        private Material m_material;
        private int m_pass;


        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            CommonUtils.Destroy(m_material);
            base.Dispose(disposing);
        }

        public void Init()
        {
            FlipVertical = false;
            Swizzle      = s_SwizzleRGBA;

            ColorScale  = Vector4.one;
            ColorOffset = Vector4.zero;
            Encoding    = ColorEncoding.None;

            m_material = new Material(CommonUtils.FindShaderByPath("RenderPass/BlitPass"));
            m_material.hideFlags = HideFlags.DontSave;

            m_block = new MaterialPropertyBlock();
        }

        private void SetupMaterial()
        {
            if (this.FlipVertical)
                m_block.SetVector("_UvScaleOffset", new Vector4(1, -1, UVOffset.x, 1-UVOffset.y));
            else
                m_block.SetVector("_UvScaleOffset", new Vector4(1, 1, UVOffset.x, UVOffset.y));
            m_block.SetVector("_Swizzle", Swizzle);
            m_block.SetVector("_ColorScale", ColorScale);
            m_block.SetVector("_ColorOffset", ColorOffset);

            if (Encoding == ColorEncoding.DepthOnly)
                m_pass = m_material.FindPass("BLIT_DEPTH");
            else
                m_pass = m_material.FindPass("BLIT");

            BlendMode srcBlend_ = BlendMode.One;
            BlendMode dstBlend_ = BlendMode.Zero;
            BlendOp blendOp_ = BlendOp.Add;
            if (EnableBlend)
            {
                srcBlend_ = SrcBlend;
                dstBlend_ = DstBlend;
                blendOp_ = BlendOp;
            }
            m_block.SetInt("_SrcBlend", (int)srcBlend_);
            m_block.SetInt("_DstBlend", (int)dstBlend_);
            m_block.SetInt("_BlendOp", (int)blendOp_);
            m_block.SetInt("_Encoding", (int)Encoding);
        }

        public void Render(Texture src, RenderTargetIdentifier dst)
        {
            CommandBuffer cmd = new CommandBuffer();
            Render(cmd, src, dst);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }

        public void Render(CommandBuffer cmd, Texture src, RenderTargetIdentifier dst, int mipLevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            SetupMaterial();
            m_block.SetTexture("_SrcTex", src);
            cmd.SetRenderTarget(dst, mipLevel, cubemapFace, depthSlice);
            CommonUtils.DrawQuad(cmd, m_material, m_pass, m_block);
            m_block.Clear();
        }
    }
}
