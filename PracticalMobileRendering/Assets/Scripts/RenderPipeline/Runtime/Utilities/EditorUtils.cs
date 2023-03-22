#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace PMRP
{
    public static class EditorUtils
	{
        public enum ETextureExportFormat
        {
            PNG,
            EXR,
        }

        public static void ExportRenderTextureToFile(RenderTexture rt, ETextureExportFormat fmt, string absolutePath)
        {
            var activeRT = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = activeRT;

            string fileExtension = "";
            if (fmt == ETextureExportFormat.PNG)
                fileExtension = ".png";
            else if (fmt == ETextureExportFormat.EXR)
                fileExtension = ".exr";

            if (!absolutePath.EndsWith(fileExtension))
            {
                absolutePath += fileExtension;
            }

            if (fmt == ETextureExportFormat.PNG)
                File.WriteAllBytes(absolutePath, tex.EncodeToPNG());
            else if (fmt == ETextureExportFormat.EXR)
                File.WriteAllBytes(absolutePath, tex.EncodeToEXR());
        }

        public static TextureImporter SaveRenderTextureToFile(RenderTexture rt, ETextureExportFormat fmt, string absolutePath)
        {
            if (Path.IsPathRooted(absolutePath))
            {
                if (!PathUtils.RelativeToUnityProject(absolutePath, out absolutePath))
                    return null;
            }

            var activeRT = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormatUtil.RenderTextureFmtToTextureFmt(rt.format), false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = activeRT;

            string fileExtension = "";
            if (fmt == ETextureExportFormat.PNG)
                fileExtension = ".png";
            else if (fmt == ETextureExportFormat.EXR)
                fileExtension = ".exr";

            if (!absolutePath.EndsWith(fileExtension))
            {
                absolutePath += fileExtension;
            }

            if (fmt == ETextureExportFormat.PNG)
                File.WriteAllBytes(absolutePath, tex.EncodeToPNG());
            else if (fmt == ETextureExportFormat.EXR)
                File.WriteAllBytes(absolutePath, tex.EncodeToEXR());

            AssetDatabase.ImportAsset(absolutePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter) TextureImporter.GetAtPath(absolutePath);

            importer.sRGBTexture   = rt.sRGB;
            importer.wrapMode      = rt.wrapMode;
            importer.filterMode    = rt.filterMode;
            importer.mipmapEnabled = rt.useMipMap;
            importer.SaveAndReimport();
            return importer;
        }

        public static T CreateGameObjectWithComponent<T>(MenuCommand menuCommand, string name) where T : Component
        {
            return CreateGameObjectWithComponent<T>(menuCommand.context as GameObject, name);
        }

        public static T CreateGameObjectWithComponent<T>(GameObject parentObject, string name) where T : Component
        {
            // Create a custom game object
            GameObject go = new GameObject(name);
            var comp = go.AddComponent<T>();

            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(go, parentObject);

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            return comp;
        }

        public static List<MeshRenderer> CollectRenderersContributeToGI()
        {
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>(Object.FindObjectsByType(typeof(MeshRenderer), FindObjectsSortMode.None) as MeshRenderer[]);
            return meshRenderers.FindAll(x => GameObjectUtility.GetStaticEditorFlags(x.gameObject).HasFlag(StaticEditorFlags.ContributeGI));
        }

        public static List<MeshRenderer> CollectLightmappedRenderers()
        {
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>(Object.FindObjectsByType(typeof(MeshRenderer), FindObjectsSortMode.None) as MeshRenderer[]);
            return meshRenderers.FindAll(x => (GameObjectUtility.GetStaticEditorFlags(x.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && x.receiveGI == ReceiveGI.Lightmaps));
        }

        public static List<Light> CollectBakedLights()
        {
            List<Light> lights = new List<Light>(Object.FindObjectsByType(typeof(Light), FindObjectsSortMode.None) as Light[]);
            return lights.FindAll(x => x.lightmapBakeType != LightmapBakeType.Realtime);
        }

        public static string GetWorkingDirectory()
        {
            string libPath = Directory.GetParent(Application.dataPath).FullName + "/Library";
            string workingDirectory = libPath + "/PMRP";

            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
                Debug.Log("Create the Working Directory");
            }

            return workingDirectory;
        }

        public static void BeginRenderDocCapture(EditorWindow window)
        {
            if (RenderDoc.IsLoaded())
            {
                if (window != null)
                {
                    RenderDoc.BeginCaptureRenderDoc(window);
                }
            }
        }

        public static void EndRenderDocCapture(EditorWindow window)
        {
            if (RenderDoc.IsLoaded())
            {
                if (window != null)
                {
                    RenderDoc.EndCaptureRenderDoc(window);
                }
            }
        }

        public static Texture2D CubeToEquirectangular(Texture cubeMap, Color intensity, int encodingMode = 0, Vector4 encodingVector = new Vector4())
        {
            var blitSkyBox = new Material(Shader.Find("Hidden/CubeToEquirectangular"));

            blitSkyBox.SetFloat("_EncodingMode", encodingMode);
            blitSkyBox.SetVector("_EncodingFactor", encodingVector);
            blitSkyBox.SetColor("_Intensity", intensity);

            var destRenderTexture = RenderTexture.GetTemporary(1024, 1024, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(cubeMap, destRenderTexture, blitSkyBox);

            var preActiveRT = RenderTexture.active;
            RenderTexture.active = destRenderTexture;

            var equirectangular = new Texture2D(destRenderTexture.width, destRenderTexture.height, TextureFormat.RGBAFloat, false, true);
            equirectangular.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
            equirectangular.Apply();

            RenderTexture.active = preActiveRT;
            RenderTexture.ReleaseTemporary(destRenderTexture);

            return equirectangular;
        }

        public static float AreaOfTrinagle(Vector3 n1, Vector3 n2, Vector3 n3)
        {
            float res = Mathf.Pow(((n2.x * n1.y) - (n3.x * n1.y) - (n1.x * n2.y) + (n3.x * n2.y) + (n1.x * n3.y) - (n2.x * n3.y)), 2.0f);
            res += Mathf.Pow(((n2.x * n1.z) - (n3.x * n1.z) - (n1.x * n2.z) + (n3.x * n2.z) + (n1.x * n3.z) - (n2.x * n3.z)), 2.0f);
            res += Mathf.Pow(((n2.y * n1.z) - (n3.y * n1.z) - (n1.y * n2.z) + (n3.y * n2.z) + (n1.y * n3.z) - (n2.y * n3.z)), 2.0f);
            return Mathf.Sqrt(res) * 0.5f;
        }

        public static float ComputeLightmapResolution(Mesh mesh, MeshRenderer meshRenderer, float texelPerUnit)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter != null && modelImporter.secondaryUVMinLightmapResolution > 0.0f)
            {
                return Mathf.Clamp(meshRenderer.scaleInLightmap * (meshRenderer.bounds.size.magnitude / mesh.bounds.size.magnitude) * modelImporter.secondaryUVMinLightmapResolution, 16.0f, 512.0f);
            }
            else
            {
                var transform = meshRenderer.transform;
                var bSize = meshRenderer.bounds.size;

                float surfaceArea = 0.0f;
                // thin
                if (mesh.isReadable && (bSize.x < 0.001f || bSize.y < 0.001f || bSize.z < 0.001f))
                {
                    for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                    {
                        int[] triIndices = mesh.GetTriangles(subMeshIndex);
                        Debug.Assert(triIndices.Length % 3 == 0);
                        for (int triIndex = 0; triIndex < triIndices.Length / 3; triIndex++)
                        {
                            int aIndex = triIndices[triIndex];
                            int bIndex = triIndices[triIndex + 1];
                            int cIndex = triIndices[triIndex + 2];
                            Vector3 a = transform.TransformPoint(mesh.vertices[aIndex]);
                            Vector3 b = transform.TransformPoint(mesh.vertices[bIndex]);
                            Vector3 c = transform.TransformPoint(mesh.vertices[cIndex]);

                            float triArea = AreaOfTrinagle(a, b, c);
                            surfaceArea = surfaceArea + triArea;
                        }
                    }
                }
                else
                {
                    surfaceArea = 2.0f * bSize.x * bSize.y + 2.0f * bSize.x * bSize.z + 2.0f * bSize.z * bSize.y;
                }

                return Mathf.Clamp(Mathf.Sqrt(texelPerUnit * texelPerUnit * surfaceArea), 16.0f, 512.0f);
            }
        }
	}
}

#endif
