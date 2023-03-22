#if UNITY_EDITOR

using UnityEngine;

namespace PMRP
{
    public sealed class IntegrateLightProbeIrradiance : IRenderPass
    {
        public Vector4[]     SH9Coeffs;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            base.Dispose(disposing);
        }

        public void Init()
        {
        }

        private void IntegrateIrradianceSH9(Texture2D envmap)
        {
            // 9 float3
            ComputeBuffer sh9Buffer = new ComputeBuffer(3 * 4, 12);

            ComputeShader compute = CommonUtils.FindComputeShaderByPath("Editor/IntegrateIrradianceSH");

            int kernelIdx = 0;
            compute.SetTexture(kernelIdx, "InputTex", envmap);
            compute.SetBuffer(kernelIdx, "OutputBuffer", sh9Buffer);
            compute.Dispatch(kernelIdx, 1, 1, 1);

            float[] tmp = new float[sh9Buffer.count * sh9Buffer.stride / 4];
            sh9Buffer.GetData(tmp);

            SH9Coeffs = new Vector4[9];
            for (int i = 0; i < 9; ++i)
            {
                SH9Coeffs[i] = new Vector4(tmp[i * 3 + 0], tmp[i * 3 + 1], tmp[i * 3 + 2], 1);
            }

            sh9Buffer.Release();
        }

        public void Execute(Texture2D envmap)
        {
            IntegrateIrradianceSH9(envmap);
        }
    }
}

#endif
