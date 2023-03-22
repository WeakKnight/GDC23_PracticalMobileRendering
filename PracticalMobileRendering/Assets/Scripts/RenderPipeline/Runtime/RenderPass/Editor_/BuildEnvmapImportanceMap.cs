#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.Rendering;


namespace PMRP
{
    public sealed class BuildEnvmapImportanceMap : IRenderPass
    {
        public RenderTexture ImportanceMap;

        private Material m_material;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (ImportanceMap != null)
            {
                ImportanceMap.Release();
            }

            base.Dispose(disposing);
        }

        public void Init()
        {
            m_material = new Material(CommonUtils.FindShaderByPath("Editor/BuildEnvmapImportanceMap"));
        }

        public void Execute(Texture2D envmap)
        {
            RenderTexture tempEnvmap = null;

            CommandBuffer commands = new CommandBuffer();
            using (new ProfilingSample(commands, "BuildEnvmapImportanceMap"))
            {
                if (envmap.mipmapCount == 1 && (envmap.width > 1 || envmap.height > 1))
                {
                    // Build mipmap for envmap so that we can apply mipmap filtering when building importance map
                    tempEnvmap = new RenderTexture(envmap.width, envmap.height, 0, RenderTextureFormat.ARGBFloat);
                    tempEnvmap.useMipMap = true;
                    tempEnvmap.autoGenerateMips = false;

                    commands.Blit(envmap, tempEnvmap);
                    commands.GenerateMips(tempEnvmap);
                }

                int importanceMapSize = Mathf.NextPowerOfTwo(Mathf.Max(envmap.width, envmap.height));
                // 4 timers smaller resolution provide good enough result when very bright light source cover few pixels in HDRI
                importanceMapSize = Mathf.Max(2, importanceMapSize / 4);

                ImportanceMap                  = new RenderTexture(importanceMapSize, importanceMapSize, 0, RenderTextureFormat.RFloat);
                ImportanceMap.name             = "EnvmapImportanceMap";
                ImportanceMap.useMipMap        = true;
                ImportanceMap.autoGenerateMips = false;
                ImportanceMap.filterMode       = FilterMode.Point;
                ImportanceMap.wrapMode         = TextureWrapMode.Clamp;

                m_material.SetTexture("_Envmap", tempEnvmap != null ? tempEnvmap : envmap);
                commands.SetRenderTarget(ImportanceMap);
                CommonUtils.DrawQuad(commands, m_material);
                commands.GenerateMips(ImportanceMap);
            }
            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();

            if (tempEnvmap != null)
                tempEnvmap.Release();
        }
    }
}

#endif
