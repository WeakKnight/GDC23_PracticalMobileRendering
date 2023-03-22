using System;
using UnityEngine;
using UnityEngine.Rendering;


namespace PMRP
{
    public sealed class PostProcessPass : IRenderPass
    {
        private Material m_uberMaterial;

        private Bloom m_bloom = new Bloom();

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            CommonUtils.Destroy(m_uberMaterial);
            m_uberMaterial = null;

            m_bloom.Dispose();
            base.Dispose(disposing);
        }

        public void Init()
        {
            m_uberMaterial = new Material(CommonUtils.FindShaderByPath("RenderPass/PostProcessUber"));
            m_uberMaterial.hideFlags = HideFlags.HideAndDontSave;

            m_bloom.Init();
        }

        public void Execute(CommandBuffer cmd,
                            SRPCamera viewCamera,
                            SRPRenderSettings setting,
                            RenderTexture sourceColor,
                            RenderTexture sourceDepth,
                            RenderTargetIdentifier target,
                            bool postExposure = false)
        {
            using (new ProfilingSample(cmd, "PostProcessing"))
            {
                RenderInternal(cmd, viewCamera, setting, sourceColor, sourceDepth, target, postExposure);
            }
        }

        void RenderInternal(CommandBuffer cmd,
                            SRPCamera viewCamera,
                            SRPRenderSettings setting,
                            RenderTexture sourceColor,
                            RenderTexture sourceDepth,
                            RenderTargetIdentifier target,
                            bool postExposure)
        {
            m_uberMaterial.shaderKeywords = null;

            bool isFlip = viewCamera.unityCamera.targetTexture == null && !CommonUtils.GraphicsApiOpenGL();

            // Bloom goes first
            int bloomTex = m_bloom.Render(cmd, setting, sourceColor, postExposure);
            SetupBloom(cmd, setting.GetComponent<BloomSetting>(), bloomTex);

            // Setup other effects constants
            SetupExposure(postExposure);

            {
                cmd.SetGlobalTexture(ShaderConstants._BlitTex, sourceColor);
                cmd.SetRenderTarget(target);
                cmd.SetViewport(viewCamera.unityCamera.pixelRect);
                CommonUtils.DrawQuad(cmd, m_uberMaterial, 0, null, isFlip);
            }

            // Cleanup
            if (bloomTex != Bloom.k_InvalidBloomTexture)
                cmd.ReleaseTemporaryRT(bloomTex);
        }

        void SetupBloom(CommandBuffer cmd, BloomSetting setting, int bloomTex)
        {
            if (bloomTex != Bloom.k_InvalidBloomTexture)
            {
                var tint = setting.BloomTint.linear;
                var luma = CommonUtils.Luminance_sRGB(tint);
                tint = luma > 0f ? tint * (1f / luma) : Color.white;

                var bloomParams = new Vector4(setting.BloomIntensity, tint.r, tint.g, tint.b);
                m_uberMaterial.SetVector(ShaderConstants._Bloom_Params, bloomParams);
                m_uberMaterial.EnableKeyword("_BLOOM");

                cmd.SetGlobalTexture(ShaderConstants._BloomTex, bloomTex);
            }
        }

        void SetupExposure(bool postExposure)
        {
            m_uberMaterial.SetFloat(ShaderConstants._PostExposure, postExposure ? 1 : -1);
        }

        static class ShaderConstants
        {
            public static readonly int _BloomTex = Shader.PropertyToID("_BloomTex");
            public static readonly int _Bloom_Params = Shader.PropertyToID("_Bloom_Params");
            public static readonly int _BlitTex = Shader.PropertyToID("_BlitTex");
            public static readonly int _PostExposure = Shader.PropertyToID("_PostExposure");
        }
    }
}