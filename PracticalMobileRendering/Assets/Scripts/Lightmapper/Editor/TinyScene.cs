using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    namespace TinyScene
    {
        using ImageIndex = System.Int32;
        using MeshIndex = System.Int32;
        using GeometryIndex = System.Int32;
        using MaterialIndex = System.Int32;

        [Serializable]
        public class Geometry
        {
            public int indexStart;
            public int indexCount;
        }

        [Serializable]
        public class Mesh
        {
            public Vector3[] positions;
            public Vector3[] normals;
            public Vector4[] tangents;
            public Vector2[] uv0s;
            public Vector2[] uv1s;
            public int[] indices;

            // array of geometry index
            public List<GeometryIndex> geometries = new();
        }

        [Serializable]
        public class Material
        {
            // image index
            public ImageIndex baseColorTexture = -1;
            public Vector4 baseColorFactor;
            // image index
            public ImageIndex metalRoughnessTexture = -1;
            public float metallic;
            public float roughness;
        }

        [Serializable]
        public class Image
        {
            public int width;
            public int height;
            public int byteSize;
            public byte[] data;
        }

        [Serializable]
        public enum LightType
        {
            Point = 1,
            Spot = 2,
            Directional = 3,
        }

        [Serializable, Flags]
        public enum LightFlags
        {
            None = 0,
            DirectLighting = 1 << 0,
            IndirectLighting = 1 << 1,
            DynamicBaking = 1 << 2,
        }

        [Serializable]
        public class Light
        {
            public LightType type;
            public LightFlags flags;
            public Vector3 position;
            // Quat
            public Vector4 rotation;
            public Vector3 intensity;

            public float range;
            public float indirectScale;
            public float innerAngle;
            public float outerAngle;
            public float radius;
            public float angle;
        }

        [Serializable, Flags]
        public enum InstanceFlags
        {
            None = 0,
            Lightmapped = 1 << 1,
            VolumetricLightmapped = 1 << 2,
            ContributeToGI = 1 << 3,
        }

        [Serializable]
        public class Instance
        {
            public Matrix4x4 tranformation;
            // mesh index
            public MeshIndex mesh = -1;
            public List<MaterialIndex> materials = new();
            public InstanceFlags flags = InstanceFlags.None;
            public int lightmapIndex;
            public Vector4 lightmapScaleAndOffset;
        }

        [Serializable]
        public class Scene
        {
            public string name;

            [SerializeField]
            public List<Geometry> geometries = new();

            [SerializeField]
            public List<Mesh> meshes = new();

            [SerializeField]
            public List<Image> images = new();

            [SerializeField]
            public List<Material> materials = new();

            [SerializeField]
            public List<Instance> instances = new();

            [SerializeField]
            public List<Light> lights = new();

            [SerializeField]
            public ImageIndex environmentMap = -1;

            // runtime only
            private Dictionary<UnityEngine.Mesh, Mesh> meshCache = new();
            private Dictionary<UnityEngine.Mesh, MeshIndex> meshIndexMap = new();

            public static Scene Create(List<MeshRenderer> renderers, List<UnityEngine.Light> lights, Texture2D envmap)
            {
                Scene scene = new();

                foreach (var renderer in renderers)
                {
                    scene.AddInstance(renderer);
                }

                foreach (var light in lights)
                {
                    scene.AddLight(light);
                }

                scene.AddEnvmap(envmap);

                return scene;
            }

            public void WriteToBinary(string path)
            {
                using (var stream = File.Open(path, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                    {
                        writer.Write(geometries.Count);
                        writer.Write(meshes.Count);
                        writer.Write(images.Count);
                        writer.Write(materials.Count);
                        writer.Write(instances.Count);
                        writer.Write(lights.Count);

                        foreach (var geometry in geometries)
                        {
                            writer.Write(geometry.indexStart);
                            writer.Write(geometry.indexCount);
                        }

                        foreach (var mesh in meshes)
                        {
                            int vertexCount = mesh.positions.Length;
                            int indexCount = mesh.indices.Length;
                            int geometryCount = mesh.geometries.Count;

                            writer.Write(vertexCount);
                            writer.Write(indexCount);
                            writer.Write(geometryCount);

                            for (int i = 0; i < vertexCount; i++)
                            {
                                writer.Write(coordinateSpaceConversionScale.x * mesh.positions[i][0]);
                                writer.Write(coordinateSpaceConversionScale.y * mesh.positions[i][1]);
                                writer.Write(coordinateSpaceConversionScale.z * mesh.positions[i][2]);
                            }

                            for (int i = 0; i < vertexCount; i++)
                            {
                                writer.Write(coordinateSpaceConversionScale.x * mesh.normals[i][0]);
                                writer.Write(coordinateSpaceConversionScale.y * mesh.normals[i][1]);
                                writer.Write(coordinateSpaceConversionScale.z * mesh.normals[i][2]);
                            }

                            for (int i = 0; i < vertexCount; i++)
                            {
                                writer.Write(tangentSpaceConversionScale.x * mesh.tangents[i][0]);
                                writer.Write(tangentSpaceConversionScale.y * mesh.tangents[i][1]);
                                writer.Write(tangentSpaceConversionScale.z * mesh.tangents[i][2]);
                                writer.Write(tangentSpaceConversionScale.w * mesh.tangents[i][3]);
                            }

                            for (int i = 0; i < vertexCount; i++)
                            {
                                writer.Write(mesh.uv0s[i][0]);
                                writer.Write(1.0f - mesh.uv0s[i][1]);
                            }

                            for (int i = 0; i < vertexCount; i++)
                            {
                                writer.Write(mesh.uv1s[i][0]);
                                writer.Write(mesh.uv1s[i][1]);
                            }

                            for (int i = 0; i < (indexCount / 3); i++)
                            {
                                // flip face
                                writer.Write(mesh.indices[i * 3 + 2]);
                                writer.Write(mesh.indices[i * 3 + 1]);
                                writer.Write(mesh.indices[i * 3]);
                            }

                            for (int i = 0; i < geometryCount; i++)
                            {
                                writer.Write(mesh.geometries[i]);
                            }
                        }

                        foreach (var image in images)
                        {
                            writer.Write(image.width);
                            writer.Write(image.height);
                            writer.Write(image.byteSize);
                            writer.Write(image.data);
                        }

                        foreach (var material in materials)
                        {
                            writer.Write(material.baseColorTexture);
                            writer.Write(material.baseColorFactor.x);
                            writer.Write(material.baseColorFactor.y);
                            writer.Write(material.baseColorFactor.z);
                            writer.Write(material.baseColorFactor.w);
                            writer.Write(material.metalRoughnessTexture);
                            writer.Write(material.metallic);
                            writer.Write(material.roughness);
                        }

                        foreach (var instance in instances)
                        {
                            Vector4 c0 = instance.tranformation.GetColumn(0);
                            Vector4 c1 = instance.tranformation.GetColumn(1);
                            Vector4 c2 = instance.tranformation.GetColumn(2);
                            Vector4 c3 = instance.tranformation.GetColumn(3);
                            writer.Write(c0.x);
                            writer.Write(c0.y);
                            writer.Write(c0.z);
                            writer.Write(c0.w);
                            writer.Write(c1.x);
                            writer.Write(c1.y);
                            writer.Write(c1.z);
                            writer.Write(c1.w);
                            writer.Write(c2.x);
                            writer.Write(c2.y);
                            writer.Write(c2.z);
                            writer.Write(c2.w);
                            writer.Write(c3.x);
                            writer.Write(c3.y);
                            writer.Write(c3.z);
                            writer.Write(c3.w);

                            writer.Write(instance.mesh);

                            writer.Write(instance.materials.Count);
                            foreach (var material in instance.materials)
                            {
                                writer.Write(material);
                            }

                            writer.Write((int)instance.flags);

                            writer.Write(instance.lightmapIndex);

                            writer.Write(instance.lightmapScaleAndOffset.x);
                            writer.Write(instance.lightmapScaleAndOffset.y);
                            writer.Write(instance.lightmapScaleAndOffset.z);
                            writer.Write(instance.lightmapScaleAndOffset.w);
                        }

                        foreach (var light in lights)
                        {
                            writer.Write((int)light.type);
                            writer.Write((int)light.flags);

                            writer.Write(light.position.x);
                            writer.Write(light.position.y);
                            writer.Write(light.position.z);

                            writer.Write(light.rotation.x);
                            writer.Write(light.rotation.y);
                            writer.Write(light.rotation.z);
                            writer.Write(light.rotation.w);

                            writer.Write(light.intensity.x);
                            writer.Write(light.intensity.y);
                            writer.Write(light.intensity.z);

                            writer.Write(light.range);
                            writer.Write(light.indirectScale);
                            writer.Write(light.innerAngle);
                            writer.Write(light.outerAngle);
                            writer.Write(light.radius);
                            writer.Write(light.angle);
                        }

                        writer.Write(environmentMap);
                    }
                }
            }

            public MeshIndex AddMesh(UnityEngine.Mesh unityMesh)
            {
                if (unityMesh == null)
                {
                    return -1;
                }

                if (meshCache.ContainsKey(unityMesh))
                {
                    return meshIndexMap[unityMesh];
                }

                Mesh mesh = new();

                List<int> totalIndicies = new();
                for (int i = 0; i < unityMesh.subMeshCount; i++)
                {
                    Geometry geometry = new Geometry();
                    SubMeshDescriptor subMeshDesc = unityMesh.GetSubMesh(i);
                    geometry.indexStart = subMeshDesc.indexStart;
                    geometry.indexCount = subMeshDesc.indexCount;

                    mesh.geometries.Add(geometries.Count);
                    geometries.Add(geometry);

                    totalIndicies.AddRange(unityMesh.GetIndices(i));
                }

                mesh.positions = unityMesh.vertices;
                mesh.normals = unityMesh.normals;
                mesh.tangents = unityMesh.tangents;
                mesh.uv0s = unityMesh.uv;
                mesh.uv1s = unityMesh.uv2;
                mesh.indices = totalIndicies.ToArray();

                meshIndexMap[unityMesh] = meshes.Count;
                meshCache[unityMesh] = mesh;
                meshes.Add(mesh);

                return meshIndexMap[unityMesh];
            }

            public void AddInstance(MeshRenderer renderer)
            {
                var unityMesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (unityMesh == null)
                {
                    return;
                }

                Instance instance = new();
                instance.tranformation = FilterMatrix(renderer.transform.localToWorldMatrix);
                instance.mesh = AddMesh(unityMesh);
                foreach (var material in renderer.sharedMaterials)
                {
                    instance.materials.Add(AddMaterial(unityMesh, material));
                }

                if (renderer.receiveGI == ReceiveGI.Lightmaps)
                {
                    instance.flags |= InstanceFlags.Lightmapped;
                }
                else if (renderer.receiveGI == ReceiveGI.LightProbes)
                {
                    instance.flags |= InstanceFlags.VolumetricLightmapped;
                }

                if (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                {
                    instance.flags |= InstanceFlags.ContributeToGI;
                }

                AdditionalRendererData rendererData = RuntimeUtils.GetAdditionalRendererData(renderer);
                if (renderer.receiveGI == ReceiveGI.Lightmaps)
                {
                    instance.lightmapIndex = 0;
                    instance.lightmapScaleAndOffset = rendererData.scaleAndOffset;
                }
                else
                {
                    instance.lightmapIndex = -1;
                }

                instances.Add(instance);
            }

            Dictionary<Hash128, ImageIndex> imageCache = new();

            public ImageIndex AddImage(Texture2D tex, bool encodeToExr = false)
            {
                if (tex == null)
                {
                    return -1;
                }

                if (imageCache.ContainsKey(tex.imageContentsHash))
                {
                    return imageCache[tex.imageContentsHash];
                }

                var srcTex = tex;

                var image = new Image();
                if (encodeToExr)
                {
                    image.data = srcTex.EncodeToEXR();
                }
                else
                {
                    image.data = srcTex.EncodeToPNG();
                }
                image.byteSize = image.data.Length; // RGBA
                image.width = srcTex.width;
                image.height = srcTex.height;

                var imageIndex = images.Count;
                images.Add(image);
                return imageIndex;
            }

            static UnityEngine.Material _roughnessMetallicBlitMaterial;

            Dictionary<Hash128, Texture2D> TextureCache = new();

            public MaterialIndex AddMaterial(UnityEngine.Mesh unityMesh, UnityEngine.Material unityMaterial)
            {
                const int MaxOutputTextureSize = 512;

                Texture2D BlitTexture(Texture2D srcTex, bool isSRGB)
                {
                    Hash128 hash128 = new();
                    hash128.Append(srcTex.imageContentsHash.ToString());
                    hash128.Append(isSRGB ? 1u : 0u);

                    if (TextureCache.ContainsKey(hash128))
                    {
                        return TextureCache[hash128];
                    }

                    int outputSize = Mathf.Min(srcTex.width, MaxOutputTextureSize);

                    var renderTexture = RenderTexture.GetTemporary(outputSize, outputSize, 0, RenderTextureFormat.ARGB32, isSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                    Graphics.Blit(srcTex, renderTexture);

                    var preActiveRT = RenderTexture.active;
                    RenderTexture.active = renderTexture;

                    var newTexture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true);
                    newTexture.ReadPixels(new Rect(0, 0, outputSize, outputSize), 0, 0);
                    newTexture.Apply();
                    newTexture.imageContentsHash = srcTex.imageContentsHash;

                    RenderTexture.active = preActiveRT;
                    RenderTexture.ReleaseTemporary(renderTexture);

                    TextureCache[hash128] = newTexture;

                    return newTexture;
                }

                Texture2D BlitRoughnessMetallicTexture(Texture2D metallicTexture, int metallicChannel, Texture2D roughnessTexture, int roughnessChannel, bool isSmoothness)
                {
                    Hash128 hash128 = new();
                    hash128.Append(metallicTexture.imageContentsHash.ToString());
                    hash128.Append(roughnessTexture.imageContentsHash.ToString());
                    hash128.Append(metallicChannel);
                    hash128.Append(roughnessChannel);
                    hash128.Append(isSmoothness ? 1u : 0u);

                    if (TextureCache.ContainsKey(hash128))
                    {
                        return TextureCache[hash128];
                    }

                    int outputSize = Mathf.Min(metallicTexture.width, MaxOutputTextureSize);

                    if (_roughnessMetallicBlitMaterial == null)
                    {
                        _roughnessMetallicBlitMaterial = new UnityEngine.Material(Shader.Find("Hidden/RoughnessMetallicBlit"));
                    }

                    var destRenderTexture = RenderTexture.GetTemporary(outputSize, outputSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

                    _roughnessMetallicBlitMaterial.SetFloat("_MetallicChannel", metallicChannel);
                    _roughnessMetallicBlitMaterial.SetTexture("_RoughnessTex", roughnessTexture ?? Texture2D.whiteTexture);
                    _roughnessMetallicBlitMaterial.SetFloat("_RoughnessChannel", roughnessChannel);
                    _roughnessMetallicBlitMaterial.SetFloat("_IsSmoothness", isSmoothness ? 1.0f : 0.0f);
                    Graphics.Blit(metallicTexture ?? Texture2D.whiteTexture, destRenderTexture, _roughnessMetallicBlitMaterial);

                    var preActiveRT = RenderTexture.active;
                    RenderTexture.active = destRenderTexture;

                    var exportTexture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, true);
                    exportTexture.ReadPixels(new Rect(0, 0, outputSize, outputSize), 0, 0);
                    exportTexture.Apply();

                    var imageContentsHash = new Hash128();
                    if (metallicTexture)
                    {
                        imageContentsHash.Append(metallicTexture.imageContentsHash.ToString());
                    }
                    if (roughnessTexture)
                    {
                        imageContentsHash.Append(roughnessTexture.imageContentsHash.ToString());
                    }
                    exportTexture.imageContentsHash = imageContentsHash;

                    RenderTexture.active = preActiveRT;

                    RenderTexture.ReleaseTemporary(destRenderTexture);

                    TextureCache[hash128] = exportTexture;

                    return exportTexture;
                }

                bool IsStandardPBRMaterial(UnityEngine.Material mat)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        return true;
                    }

                    return false;
                }

                // Standard Material Export
                if (IsStandardPBRMaterial(unityMaterial))
                {
                    Material material = new Material();
                    Color color = unityMaterial.GetColor("_BaseColor");
                    material.baseColorFactor = new Color(color.r, color.g, color.b, color.a);
                    Texture2D mainTexture = unityMaterial.GetTexture("_BaseColorMap") as Texture2D;
                    if (mainTexture)
                    {
                        material.baseColorTexture = AddImage(BlitTexture(mainTexture, true));
                    }

                    Texture2D metallicTexture = unityMaterial.GetTexture("_MaskMap") as Texture2D;
                    Texture2D roughnessTexture = metallicTexture;

                    int GetSwizzleChannel(Vector4 swizzle)
                    {
                        if (swizzle.x > 0.0f)
                        {
                            return 0;
                        }
                        else if (swizzle.y > 0.0f)
                        {
                            return 1;
                        }
                        else if (swizzle.z > 0.0f)
                        {
                            return 2;
                        }
                        else if (swizzle.w > 0.0f)
                        {
                            return 3;
                        }

                        return 0;
                    }

                    int metallicChannel = GetSwizzleChannel(unityMaterial.GetVector("_MetallicChSwizzle"));
                    int roughnessTextureChannel = GetSwizzleChannel(unityMaterial.GetVector("_RoughnessChSwizzle"));

                    if (metallicTexture != null || roughnessTexture != null)
                    {
                        material.metalRoughnessTexture = AddImage(BlitRoughnessMetallicTexture(metallicTexture, metallicChannel, roughnessTexture, roughnessTextureChannel, false));
                    }
                    else
                    {
                        material.metalRoughnessTexture = -1;
                    }

                    if (material.metalRoughnessTexture != -1)
                    {
                        material.roughness = 1.0f;
                        material.metallic = 1.0f;
                    }
                    else
                    {
                        material.roughness = 1.0f;
                        material.metallic = 0.0f;
                    }

                    var materialIndex = materials.Count;
                    materials.Add(material);
                    return materialIndex;
                }
                else
                {
                    int passIndex = unityMaterial.FindPass("Meta");
                    if (passIndex == -1)
                    {
                        passIndex = unityMaterial.FindPass("META");
                    }

                    // Meta Pass Workflow
                    if (passIndex != -1)
                    {
                    }
                    // Fallback
                    else
                    {
                    }
                }

                return -1;
            }

            public void AddLight(UnityEngine.Light unityLight)
            {
                Light light = new Light();

                if (unityLight.type == UnityEngine.LightType.Point)
                {
                    light.type = LightType.Point;
                }
                else if (unityLight.type == UnityEngine.LightType.Spot)
                {
                    light.type = LightType.Spot;
                }
                else if (unityLight.type == UnityEngine.LightType.Directional)
                {
                    light.type = LightType.Directional;
                }

                if (unityLight.lightmapBakeType == LightmapBakeType.Mixed)
                {
                    light.flags |= LightFlags.IndirectLighting;
                }
                else if (unityLight.lightmapBakeType == LightmapBakeType.Baked)
                {
                    light.flags |= LightFlags.DirectLighting;
                    light.flags |= LightFlags.IndirectLighting;
                }

                if (unityLight.GetComponent<DynamicBaking>() != null)
                {
                    light.flags |= LightFlags.DynamicBaking;
                }

                light.position = FilterPosition(unityLight.transform.position);
                light.rotation = FilterRotation(unityLight.transform.rotation);

                light.range = unityLight.range;

                light.indirectScale = unityLight.bounceIntensity;
                light.intensity = new Vector3(unityLight.intensity * unityLight.color.r,
                    unityLight.intensity * unityLight.color.g,
                    unityLight.intensity * unityLight.color.b);

                light.angle = Mathf.Max(unityLight.shadowAngle, 1.0f);
                light.radius = unityLight.shadowRadius;
                light.innerAngle = unityLight.innerSpotAngle / 2.0f;
                light.outerAngle = unityLight.spotAngle / 2.0f;

                lights.Add(light);
            }

            public void AddEnvmap(Texture2D envmap)
            {
                environmentMap = AddImage(envmap, true);
            }

            private readonly Vector3 coordinateSpaceConversionScale = new Vector3(-1.0f, 1.0f, 1.0f);
            private readonly Vector4 tangentSpaceConversionScale = new Vector4(-1, 1, 1, -1);
            private Vector3 FilterPosition(Vector3 pos)
            {
                return new Vector3(coordinateSpaceConversionScale.x * pos.x, coordinateSpaceConversionScale.y * pos.y, coordinateSpaceConversionScale.z * pos.z);
            }

            private Vector4 FilterRotation(Quaternion quat)
            {
                bool handednessFlip = coordinateSpaceConversionScale.x * coordinateSpaceConversionScale.y * coordinateSpaceConversionScale.z < 0.0f;

                Vector3 origAxis = new Vector3(quat.x, quat.y, quat.z);
                float axisFlipScale = handednessFlip ? -1.0f : 1.0f;
                Vector3 newAxis = axisFlipScale * Vector3.Scale(origAxis, coordinateSpaceConversionScale);

                return new Vector4(newAxis.x, newAxis.y, newAxis.z, quat.w);
            }

            private Matrix4x4 FilterMatrix(Matrix4x4 matrix)
            {
                Matrix4x4 convert = Matrix4x4.Scale(coordinateSpaceConversionScale);
                return convert * matrix * convert;
            }
        }
    }
}
