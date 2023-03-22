using System;
using UnityEngine;

namespace PMRP
{
    public static class Math3D
    {
        public static void FlipZ(ref Matrix4x4 m)
        {
            #if true
                m.m20 *= -1;
                m.m21 *= -1;
                m.m22 *= -1;
                m.m23 *= -1;
            #else
                Matrix4x4 s = Matrix4x4.Scale(new Vector3(1, 1, -1));
                m = s * m;
            #endif
        }

        public static void FlipY(ref Matrix4x4 m)
        {
            #if true
                m.m10 *= -1;
                m.m11 *= -1;
                m.m12 *= -1;
                m.m13 *= -1;
            #else
                Matrix4x4 s = Matrix4x4.Scale(new Vector3(1, -1, 1));
                m = s * m;
            #endif
        }

        public static Matrix4x4 ScaleOffsetMatrix(Vector2 scale, Vector2 offset)
        {
            // TODO: optimize
            return Matrix4x4.Translate(new Vector3(offset.x, offset.y, 0)) * Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1));
        }

        public static Matrix4x4 ScaleOffsetMatrix(Vector3 scale, Vector3 offset)
        {
            // TODO: optimize
            return Matrix4x4.Translate(offset) * Matrix4x4.Scale(scale);
        }

        public static BoundingSphere SphereFitViewFrustum(Vector3[] frustumCorners)
        {
            if (true)
            {
                // Refer to https://lxjk.github.io/2017/04/15/Calculate-Minimal-Bounding-Sphere-of-Frustum.html
                float n = frustumCorners[0].z;
                float f = frustumCorners[4].z;

                float tan  = Vector2.Distance(frustumCorners[0], Vector2.zero) / frustumCorners[0].z;
                float tan2 = tan                                               * tan;
                float tan4 = tan2                                              * tan2;

                float f_add_n   = f + n;
                float f_minus_n = f - n;

                float C;
                float R;
                if (tan2 >= f_minus_n / f_add_n)
                {
                    C = f;
                    R = f * tan;
                }
                else
                {
                    C = 0.5f * (1 + tan2) * f_add_n;
                    R = 0.5f              * Mathf.Sqrt(f_minus_n * f_minus_n + tan4 * f_add_n * f_add_n + 2 * tan2 * (f * f + n * n));
                }

                return new BoundingSphere(new Vector3(0, 0, C), R);
            }
            else
            {
                Vector3 center = Vector3.zero;
                for (int i = 0; i < 8; ++i)
                {
                    center += frustumCorners[i];
                }

                center /= 8;

                float radius = 0;
                for (int i = 0; i < 8; ++i)
                {
                    radius = Mathf.Max(radius, Vector3.Distance(frustumCorners[i], center));
                }

                return new BoundingSphere(center, radius);
            }
        }

        // Create the rounding matrix, by projecting the world-space origin and determining
        // the fractional offset in texel space
        public static Matrix4x4 CalculateRoundingMatrix(Matrix4x4 projMat, Matrix4x4 viewMat, Vector2 shadowMapSize)
        {
            Matrix4x4 glMat       = GL.GetGPUProjectionMatrix(Matrix4x4.identity, true);
            Matrix4x4 viewProjMat = (glMat * projMat) * viewMat;

            Vector4 somePoint = new Vector4(0, 0, 0, 1);
            somePoint = viewProjMat * somePoint;

            Vector2 shadowUv  = new Vector2(somePoint.x * 0.5f, somePoint.y           * 0.5f);
            Vector2 shadowSt  = new Vector2(shadowUv.x  * shadowMapSize.x, shadowUv.y * shadowMapSize.y);
            Vector2 roundedSt = new Vector2(Mathf.Round(shadowSt.x), Mathf.Round(shadowSt.y));
            Vector2 stOffset  = roundedSt - shadowSt;
            Vector2 uvOffset  = new Vector2(stOffset.x * 2 / shadowMapSize.x, stOffset.y * 2 / shadowMapSize.y);

            Matrix4x4 roundMatrix = Matrix4x4.Translate(new Vector3(uvOffset.x, uvOffset.y * glMat.m11, 0));

#if UNITY_EDITOR
            if (false)
            {
                // Sanity check
                Vector4   randomPoint_         = new Vector4(0, 0, 0, 1);
                Matrix4x4 viewProjMat_         = GL.GetGPUProjectionMatrix(roundMatrix * projMat, true) * viewMat;
                Vector3   randomPointClip      = viewProjMat_ * randomPoint_;
                Vector2   randomPointSt        = new Vector2(randomPointClip.x * 0.5f * shadowMapSize.x, randomPointClip.y * 0.5f * shadowMapSize.y);
                Vector2   randomPointRoundedSt = new Vector2(Mathf.Round(randomPointSt.x), Mathf.Round(randomPointSt.y));
                if (Mathf.Abs(randomPointSt.x - randomPointRoundedSt.x) >= 1e-5 ||
                    Mathf.Abs(randomPointSt.y - randomPointRoundedSt.y) >= 1e-5)
                {
                    Debug.Assert(false);
                }
            }
#endif

            return roundMatrix;
        }
        
        // TODO: better frustum culling
        // https://iquilezles.org/articles/frustumcorrect/
        public static bool SphereFrustumTest(Camera camera, BoundingSphere sphere)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, s_frustumPlanes);

            // Check sphere is outside of frustum
            for (int i = 0; i < 6; ++i)
            {
                float dist = s_frustumPlanes[i].GetDistanceToPoint(sphere.position);
                if (dist < -sphere.radius)
                    return false;
            }

            return true;
        }

        static Plane[] s_frustumPlanes = new Plane[6];
    }
}

namespace UnityEngine
{
    public static class BoundingSphereExt
    {
        public static void Encapsulate(ref this BoundingSphere src, BoundingSphere dst)
        {
            if (dst.radius <= 0)
                return;

            if (src.radius <= 0)
            {
                src = dst;
                return;
            }

            Vector3 dP = dst.position - src.position;
            float   dL = dP.magnitude;

            float maxRadius = Mathf.Max(src.radius, dst.radius);
            float minRadius = Mathf.Min(src.radius, dst.radius);
            if (maxRadius >= minRadius + dL)
            {
                if (dst.radius > src.radius)
                {
                    src = dst;
                }

                return;
            }

            float   radius   = (dL + src.radius + dst.radius) * 0.5f;
            Vector3 position = src.position + (radius - src.radius) / dL * dP;
            src.position = position;
            src.radius   = radius;
        }

        public static BoundingSphere Transform(this BoundingSphere sphere, Matrix4x4 m)
        {
#if UNITY_EDITOR
            // We don't support a transform contain scale components
            for (int i = 0; i < 3; ++i)
            {
                Debug.Assert(Mathf.Abs(Mathf.Abs(m.lossyScale[i]) - 1.0f) < 1e-4f);
            }
#endif
            return new BoundingSphere(m.MultiplyPoint3x4(sphere.position), sphere.radius);
        }
    }

    public static class Vector3Ext
    {
        public static float Dot3(this Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static Vector3 Mul(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static float MaxComponent(this Vector3 a)
        {
            return Mathf.Max(a.x, Mathf.Max(a.y, a.z));
        }

        public static float MinComponent(this Vector3 a)
        {
            return Mathf.Min(a.x, Mathf.Min(a.y, a.z));
        }
    }
}
