#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public sealed class IntegrateLightProbeSpecular : IRenderPass
    {
        public RenderTexture PrefilterGGX;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (PrefilterGGX != null)
            {
                PrefilterGGX.Release();
                PrefilterGGX = null;
            }

            base.Dispose(disposing);
        }

        public void Init()
        {
        }

        private void PreFilterEnvmapWithGGX(Texture2D     envmap,
                                            int           sizeOfEnvmap,
                                            bool          cubemap,
                                            bool          useMIS,
                                            RenderTexture envmapImportanceMap)
        {
            if (useMIS)
                Debug.Assert(envmapImportanceMap != null);
            else
                Debug.Assert(envmap.mipmapCount > 1);

            if (PrefilterGGX != null)
                PrefilterGGX.Release();

            PrefilterGGX = new RenderTexture(sizeOfEnvmap, sizeOfEnvmap, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            PrefilterGGX.dimension        = cubemap ? TextureDimension.Cube : TextureDimension.Tex2D;
            PrefilterGGX.useMipMap        = true;
            PrefilterGGX.filterMode       = FilterMode.Trilinear;
            PrefilterGGX.wrapMode         = TextureWrapMode.Repeat;
            PrefilterGGX.autoGenerateMips = false;
            PrefilterGGX.Create();

            int   mipLevels    = CommonUtils.CalcNumOfMipmaps(sizeOfEnvmap);
            float mipmapOffset = Mathf.Max(ShaderConfig.s_IblNumOfMipLevelsInTotal - mipLevels, 0);

            Material material = new Material(CommonUtils.FindShaderByPath("Editor/IntegrateEnvmapSpecularLD"));

            material.SetInt("_MIS", useMIS ? 1 : 0);
            material.SetTexture("_ImportanceMap", envmapImportanceMap);

            if (PrefilterGGX.dimension == TextureDimension.Tex2D)
            {
                material.SetInt("_RenderToCubeface", 0);
                material.SetVector("_MainTex_Dimensions",
                    new Vector4(envmap.width, envmap.height, envmap.mipmapCount, 0));

                for (int mip = 0; mip < mipLevels; ++mip)
                {
                    material.SetFloat("_MipmapLevel", (float) mip + mipmapOffset);
                    Graphics.SetRenderTarget(PrefilterGGX, mip);
                    Graphics.Blit(envmap, material);
                }
            }
            else if (PrefilterGGX.dimension == TextureDimension.Cube)
            {
                material.SetInt("_RenderToCubeface", 1);
                material.SetVector("_MainTex_Dimensions",
                    new Vector4(envmap.width, envmap.height, envmap.mipmapCount, 0));

                for (int mip = 0; mip < mipLevels; ++mip)
                {
                    for (int face = 0; face < 6; ++face)
                    {
                        material.SetFloat("_MipmapLevel", (float) mip + mipmapOffset);
                        material.SetInt("_CubefaceIdx",  face);
                        material.SetInt("_CubefaceSize", PrefilterGGX.width >> mip);
                        Graphics.SetRenderTarget(PrefilterGGX, mip, (CubemapFace) face);
                        Graphics.Blit(envmap, material);
                    }
                }
            }
        }

        public void Execute(Texture2D          envmap,
                            int                sizeOfEnvmap,
                            bool               cubemap,
                            bool               useMIS,
                            RenderTexture      envmapImportanceMap)
        {
            PreFilterEnvmapWithGGX(envmap, sizeOfEnvmap, cubemap, useMIS, envmapImportanceMap);
        }
    }
}

#endif

