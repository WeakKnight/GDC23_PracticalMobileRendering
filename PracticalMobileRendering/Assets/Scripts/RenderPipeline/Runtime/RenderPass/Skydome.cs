using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PMRP
{
    public sealed class Skydome : IRenderPass
    {
        private Material skydomeMaterial;

        public void Init()
        {
            skydomeMaterial = new Material(CommonUtils.FindShaderByPath("RenderPass/Skydome"));
        }

        public void Render(CommandBuffer cmd, 
                           SRPCamera viewCamera,
                           Matrix4x4 viewProjMat,
                           Texture2D skyTexture,
                           Color skyIntensity,
                           float angularRotation)
        {
            if (skyTexture == null)
                return;

            using (new ProfilingSample(cmd, "Skydome"))
            {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetMatrix("_InvViewProjMatrix", viewProjMat.inverse);
                materialPropertyBlock.SetVector("_CameraPositionW", viewCamera.transform.position);
                materialPropertyBlock.SetTexture("_Envmap", skyTexture);
                materialPropertyBlock.SetVector("_Intensity", skyIntensity);
                materialPropertyBlock.SetVector("_OffsetUV", new Vector2(angularRotation / 360f, 0));

                CommonUtils.DrawQuad(cmd, skydomeMaterial, 0, materialPropertyBlock);
            }
        }
        
        public void Render(CommandBuffer cmd,
                           SRPCamera      viewCamera,
                           Texture2D     skyTexture,
                           Color         skyIntensity,
                           float         angularRotation)
        {
            Render(cmd, viewCamera, CommonUtils.GetNonJitteredViewProjMatrix(viewCamera), skyTexture, skyIntensity, angularRotation);
        }
    }
}
