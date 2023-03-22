using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    public struct ShadowVolume
    {
        public Vector3[] cornerPoints;

        public static ShadowVolume Create(Vector3 center, Vector3 size)
        {
            ShadowVolume volume = new ShadowVolume();
            volume.cornerPoints = new Vector3[8];
            volume.cornerPoints[0] = center + size.Mul(new Vector3(-1f, 1f, -1f));
            volume.cornerPoints[1] = center + size.Mul(new Vector3(1f, 1f, -1f));
            volume.cornerPoints[2] = center + size.Mul(new Vector3(1f, -1f, -1f));
            volume.cornerPoints[3] = center + size.Mul(new Vector3(-1f, -1f, -1f));
            volume.cornerPoints[4] = center + size.Mul(new Vector3(-1f, 1f, 1f));
            volume.cornerPoints[5] = center + size.Mul(new Vector3(1f, 1f, 1f));
            volume.cornerPoints[6] = center + size.Mul(new Vector3(1f, -1f, 1f));
            volume.cornerPoints[7] = center + size.Mul(new Vector3(-1f, -1f, 1f));
            return volume;
        }

        private static Vector3[] s_nearPlaneCorners = new Vector3[4];
        private static Vector3[] s_farPlaneCorners  = new Vector3[4];

        // Create a volume in view space
        public static ShadowVolume Create(Camera camera, float near, float far)
        {
            ShadowVolume volume = new ShadowVolume();
            volume.cornerPoints = new Vector3[8];

            // Remove camera jitter when calculating view frustum
            Matrix4x4 viewCameraProjMatBak = camera.projectionMatrix;
            camera.projectionMatrix = camera.nonJitteredProjectionMatrix;
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), -near, Camera.MonoOrStereoscopicEye.Mono, s_nearPlaneCorners);
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), -far, Camera.MonoOrStereoscopicEye.Mono, s_farPlaneCorners);
            camera.projectionMatrix = viewCameraProjMatBak;

            for (int i = 0; i < 4; ++i)
            {
                volume.cornerPoints[i]     = s_nearPlaneCorners[i];
                volume.cornerPoints[i + 4] = s_farPlaneCorners[i];
            }
            return volume;
        }

        public void ApplyTransform(Matrix4x4 transfo)
        {
            for (int i = 0; i < 8; ++i)
            {
                cornerPoints[i] = transfo.MultiplyPoint(cornerPoints[i]);
            }
        }

        public void GetMinMax(out Vector3 boundsMin, out Vector3 boundsMax)
        {
            boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            boundsMax = new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue);
            for (int i = 0; i < 8; ++i)
            {
                boundsMin = Vector3.Min(boundsMin, cornerPoints[i]);
                boundsMax = Vector3.Max(boundsMax, cornerPoints[i]);
            }
        }

#if UNITY_EDITOR
        public void DebugDraw(Color color)
        {
            for (int i = 0; i < 4; ++i)
            {
                Debug.DrawLine(cornerPoints[i], cornerPoints[(i + 1) % 4], color, 0.05f ,false);
                Debug.DrawLine(cornerPoints[i                  + 4], cornerPoints[(i + 1) % 4 + 4], color, 0.05f ,false);
                Debug.DrawLine(cornerPoints[i], cornerPoints[i + 4], color, 0.05f ,false);
            }
        }
#endif
    }
}

