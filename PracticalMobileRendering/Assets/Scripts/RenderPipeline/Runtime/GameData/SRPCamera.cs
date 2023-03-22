using System;
using UnityEngine;


namespace PMRP
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SRPCamera : MonoBehaviour
    {
        private Camera m_unityCamera;
        public Camera unityCamera
        {
            get
            {
                if (m_unityCamera == null)
                    m_unityCamera = GetComponent<Camera>();
                return m_unityCamera;
            }
        }

        public void BeginCameraRender()
        {
            // Correct camera projection matrix change due to change of FOV
            unityCamera.ResetWorldToCameraMatrix();
            unityCamera.ResetProjectionMatrix();
        }

        public void EndCameraRender()
        {
        }

        [NonSerialized]
        public bool CaptureScreenshot = false;

        [NonSerialized]
        public string ScreenshotFilename = "";

        [NonSerialized]
        public bool ResetRenderer = false;

        [SerializeField, HideInInspector]
        private string m_rendererType = ConstVars.DefaultRendererType;

        public string RendererType
        {
            set
            {
                ResetRenderer = (m_rendererType != value);
                m_rendererType = value;
            }
            get
            {
                return m_rendererType;
            }
        }

        [NonSerialized]
        public float ScreenPercentage = 1.0f;

        public int pixelWidth
        {
            get
            {
                return (int)(ScreenPercentage * unityCamera.pixelWidth);
            }
        }

        public int pixelHeight
        {
            get
            {
                return (int)(ScreenPercentage * unityCamera.pixelHeight);
            }
        }

        public CameraClearFlags clearFlags
        {
            get
            {
#if UNITY_EDITOR
                if (unityCamera.cameraType == CameraType.Preview)
                {
                    return CameraClearFlags.SolidColor;
                }
                else if (unityCamera.cameraType == CameraType.SceneView)
                {
                    // Keep clear flags consistent with main camera in Scene view
                    if (Camera.main != null)
                        return Camera.main.clearFlags;
                    else
                        return unityCamera.clearFlags;
                }
                else
                {
                    return unityCamera.clearFlags;
                }
#else
                return unityCamera.clearFlags;
#endif
            }
        }

        public Color backgroundColor
        {
            get
            {
#if UNITY_EDITOR
                if (unityCamera.cameraType == CameraType.SceneView)
                {
                    // Keep clear flags consistent with main camera in Scene view
                    if (Camera.main != null)
                        return Camera.main.backgroundColor;
                    else
                        return unityCamera.backgroundColor;
                }
                else
                {
                    return unityCamera.backgroundColor;
                }
#else
                return unityCamera.backgroundColor;
#endif
            }
        }

        public static string[] AllRendererTypes()
        {
            return new string[] { LiteForwardRenderer.name };
        }

        public static ICameraRenderer CreateRenderer(string rendererType)
        {
            if (rendererType == LiteForwardRenderer.name)
            {
                return new LiteForwardRenderer();
            }

            return null;
        }
    }
}