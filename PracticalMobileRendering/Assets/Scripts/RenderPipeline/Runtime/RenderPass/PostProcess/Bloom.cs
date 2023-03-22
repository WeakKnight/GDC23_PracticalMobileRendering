using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public sealed class Bloom : IRenderPass
    {
        public const int k_InvalidBloomTexture = -1;

        private const int k_MaxPyramidSize = 16;

        private Material m_bloomMaterial;
        private int      m_bloomPrefilterPass;
        private int      m_bloomDownsamplePass;
        private int      m_bloomUpsamplePass;

        int m_iterations = 0;
        bool m_dataPrepared = false;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            CommonUtils.Destroy(m_bloomMaterial);
            m_bloomMaterial = null;

            base.Dispose(disposing);
        }

        public void Init()
        {
            m_bloomMaterial           = new Material(CommonUtils.FindShaderByPath("RenderPass/Bloom"));
            m_bloomMaterial.hideFlags = HideFlags.HideAndDontSave;

            m_bloomPrefilterPass  = m_bloomMaterial.FindPass("PREFILTER");
            m_bloomDownsamplePass = m_bloomMaterial.FindPass("DOWNSAMPLE");
            m_bloomUpsamplePass   = m_bloomMaterial.FindPass("UPSAMPLE");

            // Bloom pyramid shader ids - can't use a simple stackalloc in the bloom function as we
            // unfortunately need to allocate strings
            ShaderConstants._BloomMipUp   = new int[k_MaxPyramidSize];
            ShaderConstants._BloomMipDown = new int[k_MaxPyramidSize];

            for (int i = 0; i < k_MaxPyramidSize; i++)
            {
                ShaderConstants._BloomMipUp[i]   = Shader.PropertyToID("_BloomMipUp"   + i);
                ShaderConstants._BloomMipDown[i] = Shader.PropertyToID("_BloomMipDown" + i);
            }
        }

        public int Render(CommandBuffer    cmd,
                          SRPRenderSettings setting,
                          RenderTexture    sourceColor,
                          bool             postExposure)
        {
            int bloomTex = k_InvalidBloomTexture;

            if (setting.GetSettingComponent<BloomSetting>().IsBloomActivated())
            {
                using (new ProfilingSample(cmd, "Bloom"))
                {
                    m_bloomMaterial.SetFloat(ShaderConstants._PostExposure, postExposure ? 1 : -1);

                    bloomTex = SetupBloom(cmd, setting.GetComponent<BloomSetting>(), sourceColor);
                }
            }
            return bloomTex;
        }

        void PrepareData(CommandBuffer cmd, BloomSetting setting, Material material, int tw, int th)
        {
            if (setting.BloomHalfSizeDownsample)
                material.EnableKeyword("_HALF_SIZED_PREFILTER");
            else
                material.DisableKeyword("_HALF_SIZED_PREFILTER");
            // Determine the iteration count
            int   s           = Mathf.Max(tw, th);
            float diffusion   = Mathf.Clamp(Mathf.Floor(setting.BloomDiffusion* 10.0f), 1.0f, 10.0f);
            float logs        = Mathf.Log(s, 2f) + Mathf.Min(diffusion, 10f) - 10f;
            int   logs_i      = Mathf.FloorToInt(logs);
            m_iterations = Mathf.Clamp(logs_i, 1, k_MaxPyramidSize);
            float sampleScale = 0.5f + logs - logs_i;
            material.SetFloat(ShaderConstants._SampleScale, sampleScale);

            cmd.SetGlobalVector(ShaderConstants._BloomSrcUvScale, new Vector4(0.5f, 0.5f));

            // Prefiltering parameters
            float lthresh   = setting.BloomThreshold;
            float knee      = lthresh * setting.BloomSoftKnee + 1e-5f;
            var   threshold = new Vector4(lthresh, lthresh - knee, knee * 2f, 0.25f / knee);
            material.SetVector(ShaderConstants._Threshold, threshold);

            float lclamp = Mathf.GammaToLinearSpace(setting.BloomClamp);
            material.SetVector(ShaderConstants._Params, new Vector4(lclamp, setting.BloomFireflyRemovalStrength, 0f, 0f));
        }
        
        public void PrepareData(CommandBuffer cmd, BloomSetting setting, Material material, int tw, int th, RenderTextureFormat format)
        {
            PrepareData(cmd, setting, material, tw, th);
            cmd.GetTemporaryRT(ShaderConstants._BloomMipDown[0], setting.BloomHalfSizeDownsample ? tw / 2 : tw, th, 0, FilterMode.Bilinear, format);
            m_dataPrepared = true;
        }

        public int GetMipDown()
        {
            return ShaderConstants._BloomMipDown[0];
        }

        int SetupBloom(CommandBuffer cmd, BloomSetting setting, RenderTexture source)
        {
            // Start at half-res
            int tw = source.width  >> 1;
            int th = source.height >> 1;

            if (!m_dataPrepared)
            {
                PrepareData(cmd, setting, m_bloomMaterial, tw, th, source.format);
                // Prefilter
                cmd.SetGlobalTexture(ShaderConstants._BloomSrcTex, source);
                cmd.SetRenderTarget(ShaderConstants._BloomMipDown[0]);
                CommonUtils.DrawQuad(cmd, m_bloomMaterial, m_bloomPrefilterPass);
            }
            else
            {
                PrepareData(cmd, setting, m_bloomMaterial, tw, th);
            }
            m_dataPrepared = false;
            cmd.GetTemporaryRT(ShaderConstants._BloomMipUp[0], tw, th, 0, FilterMode.Bilinear, source.format);
            // Downsample - gaussian pyramid
            int lastDown = ShaderConstants._BloomMipDown[0];
            for (int i = 1; i < m_iterations; i++)
            {
                tw = Mathf.Max(1, tw >> 1);
                th = Mathf.Max(1, th >> 1);

                int mipDown = ShaderConstants._BloomMipDown[i];
                int mipUp   = ShaderConstants._BloomMipUp[i];

                if (i == 1 && setting.BloomHalfSizeDownsample)
                    cmd.SetGlobalVector(ShaderConstants._BloomSrcUvScale, new Vector4(0.5f, 1f));
                else
                    cmd.SetGlobalVector(ShaderConstants._BloomSrcUvScale, new Vector4(1, 1));

                cmd.GetTemporaryRT(mipDown, tw, th, 0, FilterMode.Bilinear, source.format);
                cmd.GetTemporaryRT(mipUp,   tw, th, 0, FilterMode.Bilinear, source.format);

                cmd.SetGlobalTexture(ShaderConstants._BloomSrcTex, lastDown);
                cmd.SetRenderTarget(mipDown);
                CommonUtils.DrawQuad(cmd, m_bloomMaterial, m_bloomDownsamplePass);

                lastDown = mipDown;
            }

            // Upsample
            int lastUp = ShaderConstants._BloomMipDown[m_iterations - 1];
            int highestMip = setting.BloomQuaterSizeUpsample ? 1 : 0;
            for (int i = m_iterations - 2; i >= highestMip; i--)
            {
                int mipDown = ShaderConstants._BloomMipDown[i];
                int mipUp   = ShaderConstants._BloomMipUp[i];
                cmd.SetGlobalTexture(ShaderConstants._BloomTex, mipDown);
                cmd.SetGlobalTexture(ShaderConstants._BloomSrcTex, lastUp);
                cmd.SetRenderTarget(mipUp);
                CommonUtils.DrawQuad(cmd, m_bloomMaterial, m_bloomUpsamplePass);
                lastUp = mipUp;
            }

            // Cleanup
            for (int i = 0; i < m_iterations; i++)
            {
                if (ShaderConstants._BloomMipDown[i] != lastUp)
                    cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[i]);
                if (ShaderConstants._BloomMipUp[i] != lastUp)
                    cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[i]);
            }

            return lastUp;
        }

        static class ShaderConstants
        {
            public static readonly int _Params          = Shader.PropertyToID("_Params");
            public static readonly int _Threshold       = Shader.PropertyToID("_Threshold");
            public static readonly int _BloomTex        = Shader.PropertyToID("_BloomTex");
            public static readonly int _BloomSrcTex     = Shader.PropertyToID("_BloomSrcTex");
            public static readonly int _BloomSrcUvScale = Shader.PropertyToID("_BloomSrcUvScale");
            public static readonly int _SampleScale     = Shader.PropertyToID("_SampleScale");
            public static readonly int _PostExposure    = Shader.PropertyToID("_PostExposure");

            public static int[] _BloomMipUp;
            public static int[] _BloomMipDown;
        }
    }
}