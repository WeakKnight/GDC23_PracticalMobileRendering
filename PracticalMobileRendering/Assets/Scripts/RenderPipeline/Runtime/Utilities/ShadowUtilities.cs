using System;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    public static class ShadowUtilities
    {
        // Copy from https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
        public enum FrustumPlaneOrder
        {
            Left  = 0,
            Right = 1,
            Down  = 2,
            Up    = 3,
            Near  = 4,
            Far   = 5,
        }

        public static void ComputeSpotShadowMatrices(Camera viewCamera, SRPLight light,
                                                     out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            viewMatrix = light.transform.worldToLocalMatrix;
            Math3D.FlipZ(ref viewMatrix);

            Debug.Assert(light.ShadowSetting.NearPlane < light.unityLight.range);

            projMatrix = Matrix4x4.Perspective(light.unityLight.spotAngle, 1,
                                               light.ShadowSetting.NearPlane,
                                               light.unityLight.range);

            if (light.ShadowSetting.UseOverrideShadowBoundingSphere())
            {
                var boundingSphere = light.ShadowSetting.GetFirstShadowBoundingSphere();

                Vector3 sphereCenterWS = boundingSphere.position;
                Vector3 sphereCenterLS = viewMatrix.MultiplyPoint3x4(sphereCenterWS);

                float targetSphereRadius = boundingSphere.radius;
                float originalSphereRadius = Mathf.Tan(Mathf.Deg2Rad * light.unityLight.spotAngle * 0.5f) * Mathf.Abs(sphereCenterLS.z);

                float scale = originalSphereRadius / targetSphereRadius;
                if (scale < 1)
                    return;

                Vector3 sphereCenterCS = projMatrix.MultiplyPoint(sphereCenterLS);
                Matrix4x4 scaleOffsetMatrix = Math3D.ScaleOffsetMatrix(new Vector2(scale, scale),
                                                                       new Vector2(-sphereCenterCS.x * scale, -sphereCenterCS.y * scale));
                projMatrix = scaleOffsetMatrix * projMatrix;
            }
        }

        public static void ComputePointShadowMatrices(Camera viewCamera, SRPLight light, CubemapFace face, float fovBias,
                                                      out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            viewMatrix = Matrix4x4.LookAt(light.transform.position,
                                          light.transform.position + CubemapUtilities.Forward[(int)face],
                                          CubemapUtilities.Up[(int)face]).inverse;
            Math3D.FlipZ(ref viewMatrix);

            Debug.Assert(light.ShadowSetting.NearPlane < light.unityLight.range);

            projMatrix = Matrix4x4.Perspective(90 + fovBias, 1, 
                                               light.ShadowSetting.NearPlane,
                                               light.unityLight.range);
        }

        public static void ComputeDirectionalShadowMatrices(Camera camera, SRPLight aLight, Matrix4x4[] viewMatrices, Matrix4x4[] projMatrices, int numSplits, int splitStart = 0)
        {
            Debug.Assert((viewMatrices.Length >= splitStart + numSplits) &&
                         (projMatrices.Length >= splitStart + numSplits));

            // Setup light coordinate frame (forward and up direction),
            // must be consistent across whole calculation so that the shadow volume extents is consistent also
            Vector3   lightForwardDir = aLight.transform.forward;
            Vector3   lightUpDir      = new Vector3(0, 1, 0);
            if (Mathf.Abs(lightUpDir.Dot3(lightForwardDir)) > (1 - 1e-2))
                lightUpDir = new Vector3(0, 0, 1);

            Matrix4x4 lightToWorldMat = Matrix4x4.LookAt(aLight.transform.position, aLight.transform.position + lightForwardDir, lightUpDir);
            Matrix4x4 worldToLightMat = lightToWorldMat.inverse;
            
            BoundingSphere boundingSphereWS = ComputeDirectionalShadowBoundingSphere(camera, aLight.ShadowSetting);
            BoundingSphere boundingSphereLS = boundingSphereWS.Transform(worldToLightMat);
            float zExtent = boundingSphereWS.radius + aLight.ShadowSetting.ZOffset;

            bool useShadowBoundingSphere = aLight.ShadowSetting.UseOverrideShadowBoundingSphere();

            List<BoundingSphere> shadowSplitBoundingSpheres = new List<BoundingSphere>(numSplits);
            if (useShadowBoundingSphere)
            {
                aLight.ShadowSetting.GetShadowSplitBoundingSpheres(shadowSplitBoundingSpheres);
            }
            else
            {
                float[] cascadeSplits = new float[numSplits + 1];
                ShadowUtilities.ComputePSSMPartition(aLight.ShadowSetting.NearPlane,
                                                     aLight.ShadowSetting.FarPlane,
                                                     numSplits,
                                                     aLight.ShadowSetting.PSSMLambda,
                                                     cascadeSplits);

                for (int splitIdx = 0; splitIdx < numSplits; ++splitIdx)
                {
                    var sph = BoundingSphereFromCameraFrustrum(camera, cascadeSplits[splitIdx], cascadeSplits[splitIdx + 1]);
                    shadowSplitBoundingSpheres.Add(sph.Transform(camera.cameraToWorldMatrix));
                }
            }

            // Calculate view and projection matrix per split
            for (int splitIdx = 0; splitIdx < numSplits; ++splitIdx)
            {
                int shadowMapSize = (int) aLight.ShadowSetting.ShadowMapResolution;

                BoundingSphere perSplitBoundingSphere = shadowSplitBoundingSpheres[splitIdx].Transform(worldToLightMat);

                Vector3 perSplitSphereCenterLS = new Vector3(perSplitBoundingSphere.position.x, perSplitBoundingSphere.position.y, boundingSphereLS.position.z);
                Vector3 shadowCameraPos = lightToWorldMat.MultiplyPoint3x4(perSplitSphereCenterLS) - lightForwardDir * zExtent;

                // Refer to : https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.SetViewProjectionMatrices.html
                // Note: The camera space in Unity matches OpenGL convention, so the negative z-axis is the camera's forward.
                // This is different from usual Unity convention, where the camera's forward is the positive z-axis.
                // If you are manually creating the view matrix, for example with an inverse of Matrix4x4.LookAt, you need to
                // scale it by -1 along the z-axis to get a proper view matrix.
                Matrix4x4 shadowViewMat = Matrix4x4.LookAt(shadowCameraPos, shadowCameraPos + lightForwardDir, lightUpDir).inverse;
                Math3D.FlipZ(ref shadowViewMat);

                Matrix4x4 shadowProjMat = Matrix4x4.Ortho(-perSplitBoundingSphere.radius, perSplitBoundingSphere.radius,
                                                          -perSplitBoundingSphere.radius, perSplitBoundingSphere.radius,
                                                          0, 2 * zExtent);

                Matrix4x4 roundingMat = Math3D.CalculateRoundingMatrix(shadowProjMat, shadowViewMat, new Vector2(shadowMapSize, shadowMapSize));
                shadowProjMat = roundingMat * shadowProjMat;

                viewMatrices[splitStart + splitIdx] = shadowViewMat;
                projMatrices[splitStart + splitIdx] = shadowProjMat;
            }
        }

        public static BoundingSphere ComputeDirectionalShadowBoundingSphere(Camera camera, ShadowMapSetting setting)
        {
            BoundingSphere boundingSphere;
            if (setting.UseOverrideShadowBoundingSphere())
            {
                boundingSphere.position = Vector3.zero;
                boundingSphere.radius   = 0;

                List<BoundingSphere> shadowSplitBoundingSpheres = new List<BoundingSphere>();
                setting.GetShadowSplitBoundingSpheres(shadowSplitBoundingSpheres, false);
                for (int i = 0; i < shadowSplitBoundingSpheres.Count; ++i)
                {
                    if (Math3D.SphereFrustumTest(camera, shadowSplitBoundingSpheres[i]))
                    {
                        boundingSphere.Encapsulate(shadowSplitBoundingSpheres[i]);
                    }
                }
            }
            else
            {
                boundingSphere = BoundingSphereFromCameraFrustrum(camera, setting.NearPlane, setting.FarPlane);
                boundingSphere.position = camera.cameraToWorldMatrix.MultiplyPoint3x4(boundingSphere.position);
            }

            return boundingSphere;
        }

        public static void ComputeDirectionalShadowDistance(Camera viewCamera, ShadowMapSetting shadowSetting, Transform lightTransform, out Vector2 shadowDistance, out Vector2 distanceFade, bool debugDisplay = false)
        {
            // Setup light coordinate frame (forward and up direction),
            // must be consistent across whole calculation so that the shadow volume extents is consistent also
            Vector3   lightForwardDir = lightTransform.forward;
            Vector3   lightUpDir      = new Vector3(0, 1, 0);
            if (Mathf.Abs(lightUpDir.Dot3(lightForwardDir)) > (1 - 1e-2))
                lightUpDir = new Vector3(0, 0, 1);

            Matrix4x4 lightToWorldMat = Matrix4x4.LookAt(lightTransform.position, lightTransform.position + lightForwardDir, lightUpDir);
            Matrix4x4 worldToLightMat = lightToWorldMat.inverse;

            BoundingSphere sphereVolumeWS = ComputeDirectionalShadowBoundingSphere(viewCamera, shadowSetting);
            BoundingSphere sphereVolumeLS = sphereVolumeWS.Transform(worldToLightMat);

            float zExtent = Mathf.Max(sphereVolumeLS.radius, sphereVolumeLS.radius + shadowSetting.ZOffset);

            // Update shadow min and max distance
            ShadowVolume shadowVolumeCS = ShadowVolume.Create(sphereVolumeLS.position, new Vector3(sphereVolumeLS.radius, sphereVolumeLS.radius, zExtent));
            shadowVolumeCS.ApplyTransform(viewCamera.worldToCameraMatrix * lightToWorldMat);

            Vector3 shadowMin, shadowMax;
            shadowVolumeCS.GetMinMax(out shadowMin, out shadowMax);

            // camera's forward is the negative Z axis
            shadowDistance.x = Mathf.Max(-shadowMax.z, 0);
            shadowDistance.y = -shadowMin.z;
            
#if UNITY_EDITOR
            if (debugDisplay)
            {
                Vector3 p = viewCamera.transform.position;
                Vector3 d = viewCamera.transform.forward.normalized;
                Debug.DrawLine(p + d * shadowDistance.x, p + d * shadowDistance.y);
            }
#endif

            if (shadowSetting.UseOverrideShadowBoundingSphere() || shadowSetting.DirectionalShadowDistanceFade >= 1.0)
            {
                distanceFade.x = 0;
                distanceFade.y = 1;
            }
            else
            {
                BoundingSphere sphereVolumeCS = new BoundingSphere(sphereVolumeLS.position, sphereVolumeLS.radius);
                sphereVolumeCS.position = (viewCamera.worldToCameraMatrix * lightToWorldMat).MultiplyPoint3x4(sphereVolumeCS.position);

                // camera's forward is the negative Z axis
                float rayHitDist = -sphereVolumeCS.position.z + sphereVolumeCS.radius;

                float fadeNear    = 0;
                float fadeFar     = Mathf.Max(rayHitDist, shadowSetting.FarPlane);
                float fadeStartDist = fadeNear + shadowSetting.DirectionalShadowDistanceFade * (fadeFar - fadeNear);
                float fadeRange     = fadeFar  - fadeStartDist;

                distanceFade.x = -1      / fadeRange;
                distanceFade.y = fadeFar / fadeRange;

#if UNITY_EDITOR
                if (debugDisplay)
                {
                    Vector3 p = viewCamera.transform.position;
                    Vector3 d = viewCamera.transform.forward.normalized;
                    Debug.DrawLine(p, p + d * fadeStartDist, Color.green);
                    Debug.DrawLine(p    + d * fadeStartDist, p + d * fadeFar, Color.blue);
                }
#endif
            }
        }

        public static float ComputeDirectionalShadowNormalBias(Matrix4x4 projMat, int shadowMapRez, float normalBias)
        {
            float metersPerTexel = (1.0f / projMat.m00) / shadowMapRez;
            return normalBias * metersPerTexel;
        }

        public static void ComputePSSMPartition(float shadowNear, float shadowFar, int numOfCascades, float lambda, float[] partition)
        {
#if UNITY_EDITOR
            if (shadowNear <= 0)
                Debug.LogError("CSM near plane distance can not be less or equal to 0");

            if (shadowFar <= shadowNear)
                Debug.LogError("CSM far plane distance can not be less or equal to near plane");
#endif

            Debug.Assert(partition.Length >= numOfCascades);

            partition[0] = shadowNear;
            for (int i = 1; i <= numOfCascades; ++i)
            {
                float p                = (float) i  / numOfCascades;
                float logPartition     = shadowNear * Mathf.Pow(shadowFar / shadowNear, p);
                float uniformPartition = shadowNear + p * (shadowFar - shadowNear);
                partition[i] = (lambda * logPartition + (1 - lambda) * uniformPartition);
            }

            for (int i = numOfCascades + 1; i < partition.Length; ++i)
            {
                partition[i] = 0;
            }
        }

        public static void ComputeCascadeScaleOffset(int numOfCascades, Matrix4x4[] viewMatrices, Matrix4x4[] projMatrices,
                                                     out Matrix4x4 shadowMatrix, Vector4[] cascadeScale, Vector4[] cascadeOffset)
        {
            if (numOfCascades > 1)
            {
                shadowMatrix = viewMatrices[0];

                Vector3 cornerLeftBottomNear = new Vector3(-1, -1, 0);
                Vector3 cornerRightTopFar = new Vector3(1, 1, 1);
                for (int i = 0; i < numOfCascades; ++i)
                {
                    Matrix4x4 viewProjMat = GL.GetGPUProjectionMatrix(projMatrices[i], false) * viewMatrices[i];
                    Matrix4x4 invViewProjMat = viewProjMat.inverse;

                    Vector3 pt0 = invViewProjMat.MultiplyPoint(cornerLeftBottomNear);
                    Vector3 pt1 = invViewProjMat.MultiplyPoint(cornerRightTopFar);
                    pt0 = shadowMatrix.MultiplyPoint3x4(pt0);
                    pt1 = shadowMatrix.MultiplyPoint3x4(pt1);
                    // Eq:
                    // pt0 * scale + offset = 0
                    // pt1 * scale + offset = 1

                    Vector3 diff = pt1 - pt0;
                    Vector3 scale = new Vector3(1 / diff.x, 1 / diff.y, 1 / diff.z);
                    Vector3 offset = -(pt0.Mul(scale));
                    cascadeScale[i] = scale;
                    cascadeOffset[i] = offset;

                    if (CommonUtils.GraphicsApiOpenGL())
                    {
                        cascadeScale[i].z *= 0.5f;
                        cascadeOffset[i].z = cascadeOffset[i].z * 0.5f + 0.5f;
                    }
                }
            }
            else
            {
                shadowMatrix = GL.GetGPUProjectionMatrix(CommonUtils.NdcToUvMatrix() * projMatrices[0], false) * viewMatrices[0];

                cascadeScale[0] = Vector4.one;
                cascadeOffset[0] = Vector4.zero;
            }

            for (int i = numOfCascades; i < cascadeScale.Length; ++i)
            {
                cascadeScale[i]  = Vector4.zero;
                cascadeOffset[i] = Vector4.zero;
            }
        }

        public static void ComputeCullingPlanes(Matrix4x4 projMat, Matrix4x4 viewMat, Plane[] cullingPlanes)
        {
            Matrix4x4 viewProjMat   = projMat * viewMat;
#if UNITY_2020_1_OR_NEWER
            GeometryUtility.CalculateFrustumPlanes(viewProjMat, cullingPlanes);
#else
            GeometryUtility.CalculateFrustumPlanesNonAlloc(viewProjMat, cullingPlanes);
#endif
            Debug.Assert(cullingPlanes.Length == 6);
        }

        // Hack to avoid GC
        static Vector3[] s_frustrumNearPlaneCorners = new Vector3[4];
        static Vector3[] s_frustrumFarPlaneCorners  = new Vector3[4];
        static Vector3[] s_frustrumCornerPoints     = new Vector3[8];

        public static BoundingSphere BoundingSphereFromCameraFrustrum(Camera camera, float near, float far)
        {
            BoundingSphere volume = new BoundingSphere();

            // Remove camera jitter when calculating view frustum
            Matrix4x4 viewCameraProjMatBak = camera.projectionMatrix;
            camera.projectionMatrix = camera.nonJitteredProjectionMatrix;

            Vector3[] nearPlaneCorners = s_frustrumNearPlaneCorners;
            Vector3[] farPlaneCorners  = s_frustrumFarPlaneCorners;
            Vector3[] cornerPoints     = s_frustrumCornerPoints;

            // https://docs.unity3d.com/560/Documentation/ScriptReference/Camera-cameraToWorldMatrix.html
            // Note that camera space matches OpenGL convention: camera's forward is the negative Z axis.
            // This is different from Unity's convention, where forward is the positive Z axis.
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), -near, Camera.MonoOrStereoscopicEye.Mono, nearPlaneCorners);
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), -far, Camera.MonoOrStereoscopicEye.Mono, farPlaneCorners);

            camera.projectionMatrix = viewCameraProjMatBak;

            for (int i = 0; i < 4; ++i)
            {
                cornerPoints[i] = nearPlaneCorners[i];
                cornerPoints[i + 4] = farPlaneCorners[i];
            }
            var sphereBounding = Math3D.SphereFitViewFrustum(cornerPoints);
            volume.position = sphereBounding.position;
            volume.radius   = sphereBounding.radius;

            return volume;
        }

#if UNITY_EDITOR
        public static void DebugDrawCullingPlanes(Matrix4x4 projMat, Matrix4x4 viewMat, Color color, Matrix4x4 transfo)
        {
            Plane[] cullingPlanes = new Plane[6];
            ComputeCullingPlanes(projMat, viewMat, cullingPlanes);
            DebugDrawCullingPlanes(cullingPlanes, color, transfo);
        }
        
        public static void DebugDrawCullingPlanes(Plane[] cullingPlanes, Color color, Matrix4x4 transfo)
        {
            int left  = (int) FrustumPlaneOrder.Left;
            int right = (int) FrustumPlaneOrder.Right;
            int down  = (int) FrustumPlaneOrder.Down;
            int up    = (int) FrustumPlaneOrder.Up;
            int near  = (int) FrustumPlaneOrder.Near;
            int far   = (int) FrustumPlaneOrder.Far;

            Func<int, int, int, Vector3> solveLinearEquation = (i, j, k) =>
            {
                Plane plane0 = cullingPlanes[i];
                Plane plane1 = cullingPlanes[j];
                Plane plane2 = cullingPlanes[k];

                Matrix4x4 A = Matrix4x4.identity;
                A.SetRow(0, plane0.normal);
                A.SetRow(1, plane1.normal);
                A.SetRow(2, plane2.normal);
                Vector4 B = new Vector4(-plane0.distance, -plane1.distance, -plane2.distance, 1);
                Vector4 x = A.inverse * B;

                return new Vector3(x[0], x[1], x[2]);
            };

            Vector3 p0 = solveLinearEquation(left, up, near);
            Vector3 p1 = solveLinearEquation(right, up, near);
            Vector3 p2 = solveLinearEquation(right, down, near);
            Vector3 p3 = solveLinearEquation(left, down, near);
            Vector3 p4 = solveLinearEquation(left, up, far);
            Vector3 p5 = solveLinearEquation(right, up, far);
            Vector3 p6 = solveLinearEquation(right, down, far);
            Vector3 p7 = solveLinearEquation(left, down, far);

            ShadowVolume volume = new ShadowVolume();
            volume.cornerPoints = new Vector3[8];
            volume.cornerPoints[0] = p0;
            volume.cornerPoints[1] = p1;
            volume.cornerPoints[2] = p2;
            volume.cornerPoints[3] = p3;
            volume.cornerPoints[4] = p4;
            volume.cornerPoints[5] = p5;
            volume.cornerPoints[6] = p6;
            volume.cornerPoints[7] = p7;

            volume.ApplyTransform(transfo);
            volume.DebugDraw(color);
            Debug.DrawRay((p0 + p1 + p2 + p3) /4, cullingPlanes[near].normal, Color.red);
            Debug.DrawRay((p4 + p5 + p6 + p7) /4, cullingPlanes[far].normal, Color.red);
            Debug.DrawRay((p0 + p4 + p7 + p3) /4, cullingPlanes[left].normal, Color.green);
            Debug.DrawRay((p1 + p5 + p6 + p2) /4, cullingPlanes[right].normal, Color.green);
            Debug.DrawRay((p3 + p7 + p6 + p2) /4, cullingPlanes[down].normal, Color.blue);
            Debug.DrawRay((p0 + p4 + p5 + p1) /4, cullingPlanes[up].normal, Color.blue);
        }
#endif
    }
}

