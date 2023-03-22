using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PMRP
{
    public abstract class ICameraRenderer : IDisposable
    {
        protected int m_width  = 0;
        protected int m_height = 0;

        protected bool m_disposed = false;

#if UNITY_EDITOR
        private RenderTexture m_hackTexture;
#endif

        #region public interface

        public virtual void Init(Camera camera)
        {
#if UNITY_EDITOR
            m_hackTexture = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGB32);
            m_hackTexture.Create();
#endif

            m_width  = 0;
            m_height = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Render(GfxRenderContext context, CommandBuffer commands, SRPCamera camera, SRPRenderSettings renderSetting, WorldData worldData)
        {
            Resize(camera, renderSetting);

            if (BeginRender(context, commands, camera, renderSetting, worldData))
            {
                OnRender(context, commands, camera, renderSetting, worldData);
                EndRender(context, commands, camera, renderSetting, worldData);
            }
        }

#if UNITY_EDITOR
        public bool IsValid()
        {
            // TODO: During Android build process, SRP render function is invoked but all graphics resources including RenederTexture and Material were set to null
            // Can we find a better way to detect this case?
            return m_hackTexture != null;
        }
#endif

        #endregion

        #region protected virtual functions

        protected virtual bool BeginRender(GfxRenderContext context,
                                           CommandBuffer    commands,
                                           SRPCamera         camera,
                                           SRPRenderSettings settings,
                                           WorldData        worldData)
        {
            return true;
        }

        protected virtual void EndRender(GfxRenderContext context,
                                         CommandBuffer    commands,
                                         SRPCamera         camera,
                                         SRPRenderSettings settings,
                                         WorldData        worldData)
        {
        }

        protected abstract void OnRender(GfxRenderContext context,
                                         CommandBuffer    commands,
                                         SRPCamera         camera,
                                         SRPRenderSettings settings,
                                         WorldData        worldData);

        protected abstract void OnResize(SRPCamera camera, SRPRenderSettings settings);

        protected virtual void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (disposing)
            {
                // dispose managed state (managed objects).
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.

            m_disposed = true;
        }

        #endregion

        #region utils

        protected void ClearFrameBuffer(CommandBuffer cmd, SRPCamera camera)
        {
            if (camera.clearFlags == CameraClearFlags.Color ||
                camera.clearFlags == CameraClearFlags.SolidColor)
            {
                cmd.ClearRenderTarget(true, true, camera.backgroundColor);
            }
            else if (camera.clearFlags == CameraClearFlags.Depth)
            {
                cmd.ClearRenderTarget(true, false, Color.black);
            }
            else if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                cmd.ClearRenderTarget(true, true, Color.black);
            }
        }

        protected void SetupMotionVectorFlag(Camera camera)
        {
            // Can't get correct unity_MatrixPreviousM from engine without this
            camera.depthTextureMode |= DepthTextureMode.MotionVectors;
            camera.depthTextureMode |= DepthTextureMode.Depth;

#if UNITY_EDITOR
            if (Camera.main != null)
            {
                if (camera.cameraType == CameraType.SceneView ||
                    camera.cameraType == CameraType.Preview)
                {
                    Camera.main.depthTextureMode |= DepthTextureMode.MotionVectors;
                    Camera.main.depthTextureMode |= DepthTextureMode.Depth;
                }
            }
#endif
        }

        #endregion

        #region private functions

        private void Resize(SRPCamera camera, SRPRenderSettings settings)
        {
            if (ShouldResize(camera, settings))
            {
                m_width  = camera.pixelWidth;
                m_height = camera.pixelHeight;
                OnResize(camera, settings);
            }
        }

        protected virtual bool ShouldResize(SRPCamera camera, SRPRenderSettings settings)
        {
            if (m_width != camera.pixelWidth || m_height != camera.pixelHeight)
                return true;    

            return false;
        }

        ~ICameraRenderer()
        {
            Debug.LogError("Call Dispose on ICameraRenderer explicitly!!!");

            Dispose(false);
        }

        #endregion
    }
}