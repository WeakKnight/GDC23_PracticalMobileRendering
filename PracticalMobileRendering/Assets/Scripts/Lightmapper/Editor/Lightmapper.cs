using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PMRP
{
    public static class Lightmapper
    {
        public class Settings
        {
            public float lightmapTexelDensity = 10.0f;
            public int lightmapResolution = 1024;
        }

        public static async void Execute()
        {
            var precomputedLightingManager = UnityEngine.GameObject.FindAnyObjectByType<PrecomputedLightingManager>();
            if (precomputedLightingManager == null)
            {
                var go = new GameObject();
                go.name = "Precomputed Lighting Manager";
                precomputedLightingManager = go.AddComponent<PrecomputedLightingManager>();
            }

            var renderersContributeToGI = EditorUtils.CollectRenderersContributeToGI();
            var lightmappedRenderers = EditorUtils.CollectLightmappedRenderers();
            precomputedLightingManager.lightmappedRenderers = lightmappedRenderers;

            void SetupRenderingLayerMask()
            {
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (var renderer in renderers)
                {
                    if (renderer is MeshRenderer && (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && (renderer as MeshRenderer).receiveGI == ReceiveGI.Lightmaps))
                    {
                        renderer.renderingLayerMask |= LiteForwardRenderer.LIGHTMAP_LAYER_MASK;
                        renderer.renderingLayerMask &= ~LiteForwardRenderer.VLM_LAYER_MASK;
                    }
                    else
                    {
                        renderer.renderingLayerMask &= ~LiteForwardRenderer.LIGHTMAP_LAYER_MASK;
                        renderer.renderingLayerMask |= LiteForwardRenderer.VLM_LAYER_MASK;
                    }
                }
            }
            SetupRenderingLayerMask();

            foreach (var renderer in lightmappedRenderers)
            {
                float itemResolution = EditorUtils.ComputeLightmapResolution(renderer.GetComponent<MeshFilter>().sharedMesh, renderer, precomputedLightingManager.lightmapTexelDensity);
                var additionalData = RuntimeUtils.GetAdditionalRendererData(renderer);
                additionalData.size = new Vector2Int((int)itemResolution, (int)itemResolution);
            }

            var rectPackerItems = new List<RectPacker.Item>();
            int rectPackerItemId = 0;
            var rectPackerItemMap = new Dictionary<int, MeshRenderer>();

            foreach (var renderer in lightmappedRenderers)
            {
                var additionalData = RuntimeUtils.GetAdditionalRendererData(renderer);

                var rectPackerItem = new RectPacker.Item();
                rectPackerItem.id = rectPackerItemId;
                rectPackerItem.size = additionalData.size;

                string path = AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>().sharedMesh);
                ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                if (modelImporter != null && modelImporter.secondaryUVMinLightmapResolution > 0.0f)
                {
                    rectPackerItem.minResolution = (int)modelImporter.secondaryUVMinLightmapResolution;
                }

                rectPackerItems.Add(rectPackerItem);

                rectPackerItemMap[rectPackerItemId] = renderer;

                rectPackerItemId++;
            }

            RectPacker rectPacker = new RectPacker();
            rectPacker.ForcePackIntoSingleContainer(new Vector2Int(precomputedLightingManager.lightmapResolution, precomputedLightingManager.lightmapResolution), 2, rectPackerItems);

            precomputedLightingManager.lightmapNum = 0;

            foreach (var item in rectPackerItems)
            {
                MeshRenderer meshRenderer = rectPackerItemMap[item.id];
                var additionalData = RuntimeUtils.GetAdditionalRendererData(meshRenderer);
                additionalData.scaleAndOffset = item.scaleAndOffset;

                precomputedLightingManager.lightmapNum = (item.index + 1) > precomputedLightingManager.lightmapNum ? (item.index + 1) : precomputedLightingManager.lightmapNum;
            }

            var combinedRenderers = new HashSet<MeshRenderer>();
            combinedRenderers.UnionWith(renderersContributeToGI);
            combinedRenderers.UnionWith(lightmappedRenderers);

            var lights = EditorUtils.CollectBakedLights();
            precomputedLightingManager.dynamicLights = lights.FindAll(light => light.GetComponent<DynamicBaking>() != null);
            precomputedLightingManager.dynamicBakings = new();

            foreach (var dynamicLight in precomputedLightingManager.dynamicLights)
            {
                precomputedLightingManager.dynamicBakings.Add(dynamicLight.GetComponent<DynamicBaking>());
            }

            precomputedLightingManager.oneLightLightmapReceiverDatas = null;
            precomputedLightingManager.twoLightLightmapReceiverDatas = null;
            precomputedLightingManager.threeLightLightmapReceiverDatas = null;
            precomputedLightingManager.oneLightVolumetricLightmapReceiverDatas = null;
            precomputedLightingManager.twoLightVolumetricLightmapReceiverDatas = null;
            precomputedLightingManager.threeLightVolumetricLightmapReceiverDatas = null;

            precomputedLightingManager.Clear();

            Texture2D envmap = Texture2D.blackTexture;
            SRPEnvironmentLight environmentLight = UnityEngine.Object.FindAnyObjectByType<SRPEnvironmentLight>();
            if (environmentLight != null)
            {
                envmap = EditorUtils.CubeToEquirectangular(environmentLight.EnvmapGgxFiltered, environmentLight.Intensity, 2);
            }
            // Export Scene
            TinyScene.Scene scene = TinyScene.Scene.Create(combinedRenderers.ToList(), lights, envmap);
            string scenePath = EditorUtils.GetWorkingDirectory() + "/" + SceneManager.GetActiveScene().name + ".tinyscene";
            scene.WriteToBinary(scenePath);

            await IPC.Open();

            await IPC.SetDiffuseBoost(precomputedLightingManager.diffuseBoost);

            await IPC.GenerateLightmap(lightmappedRenderers.Count > 0);
            await IPC.SetLightmapSize(precomputedLightingManager.lightmapResolution);
            await IPC.SetLightmapSampleCount(precomputedLightingManager.lightmapSampleCount);

            var volumetricLightmapVolume = UnityEngine.Object.FindAnyObjectByType<VolumetricLightmapVolume>();
            await IPC.GenerateVolumetricLightmap(volumetricLightmapVolume != null);
            if (volumetricLightmapVolume != null)
            {
                Vector3 minP = volumetricLightmapVolume.GetBoundingBoxMin();
                Vector3 maxP = volumetricLightmapVolume.GetBoundingBoxMax();
                await IPC.SetVLMBoundingBoxMin(new Vector3(-maxP.x, minP.y, minP.z));
                await IPC.SetVLMBoundingBoxMax(new Vector3(-minP.x, maxP.y, maxP.z));
                await IPC.SetVLMCellSize(volumetricLightmapVolume.cellSize);
                await IPC.SetVLMLocalToWorldMatrix(volumetricLightmapVolume.GetLocalToWorldMatrix());
                await IPC.SetVLMWorldToLocalMatrix(volumetricLightmapVolume.GetWorldToLocalMatrix());
            }
            await IPC.LoadScene(scenePath);
            await IPC.WaitToFinish();

            ReadResult();

            foreach (var light in lights)
            {
                if (light.lightmapBakeType != LightmapBakeType.Realtime)
                {
                    var bakingOutput = new LightBakingOutput();
                    if (light.lightmapBakeType == LightmapBakeType.Mixed)
                    {
                        bakingOutput.mixedLightingMode = MixedLightingMode.IndirectOnly;
                    }
                    else if (light.lightmapBakeType == LightmapBakeType.Baked)
                    {
                        bakingOutput.mixedLightingMode = MixedLightingMode.Subtractive;
                    }
                    bakingOutput.isBaked = true;
                    bakingOutput.lightmapBakeType = light.lightmapBakeType;
                    light.bakingOutput = bakingOutput;
                }
            }

            precomputedLightingManager.Refresh();
        }

        public static void ReadResult()
        {
            var precomputedLightingManager = UnityEngine.GameObject.FindAnyObjectByType<PrecomputedLightingManager>();
            var workingDirectory = EditorUtils.GetWorkingDirectory();

            void ReadLightmap()
            {
                string assetPath = "Assets/Resources/PrecomputedLighting/" + SceneManager.GetActiveScene().name + "_Lightmap_0.exr";

                if (File.Exists(assetPath))
                {
                    File.Delete(assetPath);
                }
                File.Copy(workingDirectory + "/resTex0.exr", assetPath);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                TextureImporterPlatformSettings windowsSettings = new();
                windowsSettings.name = "Standalone";
                windowsSettings.format = TextureImporterFormat.BC6H;
                windowsSettings.overridden = true;
                importer.SetPlatformTextureSettings(windowsSettings);

                TextureImporterPlatformSettings androidSettings = new();
                androidSettings.name = "Android";
                androidSettings.format = TextureImporterFormat.ASTC_HDR_4x4;
                androidSettings.overridden = true;
                importer.SetPlatformTextureSettings(androidSettings);

                importer.SaveAndReimport();

                Texture2D lightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                precomputedLightingManager.lightmap = lightmap;
            }

            void ReadVolumetricLightmap()
            {
                string dataPath = workingDirectory + "/vlm.bin";
                if (!File.Exists(dataPath))
                {
                    return;
                }

                var volumetricLightmapVolume = UnityEngine.Object.FindAnyObjectByType<VolumetricLightmapVolume>();
                var actualGridSize = volumetricLightmapVolume.GetGridSize();
                UnityEngine.Debug.Log("Actual VLM Grid Size: " + actualGridSize.ToString());
                byte[] bytes = File.ReadAllBytes(dataPath);
                if (bytes.Length != actualGridSize.x * actualGridSize.y * actualGridSize.z * 6 * 4 * 4)
                {
                    return;
                }

                var floatArray = new float[bytes.Length / 4];
                System.Buffer.BlockCopy(bytes, 0, floatArray, 0, bytes.Length);

                var result = new Texture3D(actualGridSize.x, actualGridSize.y, actualGridSize.z * 6, TextureFormat.RGBA32, false);
                result.filterMode = FilterMode.Bilinear;
                result.wrapMode = TextureWrapMode.Clamp;

                Color32[] vlmColors = new Color32[result.width * result.height * result.depth];
                for (int z = 0; z < result.depth; z++)
                {
                    for (int y = 0; y < result.height; y++)
                    {
                        for (int x = 0; x < result.width; x++)
                        {
                            int srcAddress = ((x * result.height * result.depth) + (y * result.depth) + z) * 4;
                            int targetAddress = x + (y * result.width) + (z * (result.width * result.height));

                            vlmColors[targetAddress] = RuntimeUtils.EncodeRGBM(new Color(floatArray[srcAddress + 0], floatArray[srcAddress + 1], floatArray[srcAddress + 2], 1.0f));
                        }
                    }
                }
                result.SetPixels32(vlmColors);

                result.Apply();

                string assetPath = "Assets/Resources/PrecomputedLighting/" + SceneManager.GetActiveScene().name + "_VLM.asset";
                AssetDatabase.CreateAsset(result, assetPath);

                precomputedLightingManager.volumetricLightmap = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Texture3D)) as Texture3D;
            }

            ReadLightmap();

            ReadLightmapReceiver();

            ReadVolumetricLightmap();

            ReadVolumetricLightmapReceiver();
        }

        public static void ReadLightmapReceiver()
        {
            var precomputedLightingManager = UnityEngine.Object.FindAnyObjectByType<PrecomputedLightingManager>();
            if (precomputedLightingManager == null || precomputedLightingManager.dynamicLights.Count <= 0)
            {
                return;
            }

            var workingDirectory = EditorUtils.GetWorkingDirectory();
            string dataPath = workingDirectory + "/Packed_DLSReceivers.bin";
            if (!File.Exists(dataPath))
            {
                UnityEngine.Debug.LogError(dataPath + " does not exist");
                return;
            }

            ReceiverAsset receiverAsset = ScriptableObject.CreateInstance<ReceiverAsset>();

            using (var stream = File.Open(dataPath, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    uint offsetCount = reader.ReadUInt32();
                    uint receiverCount = reader.ReadUInt32();

                    for (int i = 0; i < offsetCount; i++)
                    {
                        uint x = reader.ReadUInt32();
                        uint y = reader.ReadUInt32();
                        receiverAsset.offsets.Add(new Vector2Int((int)x, (int)y));
                    }

                    for (int i = 0; i < receiverCount; i++)
                    {
                        Receiver receiver = new Receiver();
                        receiver.posX = reader.ReadUInt32();
                        receiver.posY = reader.ReadUInt32();
                        receiver.posZ = reader.ReadUInt32();
                        receiver.irradiance = RuntimeUtils.UnpackR8G8B8A8ToColor(reader.ReadUInt32());
                        receiver.direction = reader.ReadUInt32();
                        receiverAsset.receivers.Add(receiver);
                    }
                }
            }

            string assetPath = "Assets/Resources/PrecomputedLighting/" + SceneManager.GetActiveScene().name + "_Lightmap_Receiver.asset";
            AssetDatabase.CreateAsset(receiverAsset, assetPath);

            precomputedLightingManager.lightmapReceiverGUID = AssetDatabase.AssetPathToGUID(assetPath);
            precomputedLightingManager.ImportLightmapReceivers();
        }

        public static void ReadVolumetricLightmapReceiver()
        {
            var precomputedLightingManager = UnityEngine.Object.FindAnyObjectByType<PrecomputedLightingManager>();
            if (precomputedLightingManager == null || precomputedLightingManager.dynamicLights.Count <= 0)
            {
                return;
            }

            var workingDirectory = EditorUtils.GetWorkingDirectory();
            string dataPath = workingDirectory + "/Packed_VLMDLSReceivers.bin";
            if (!File.Exists(dataPath))
            {
                UnityEngine.Debug.LogError(dataPath + " does not exist");
                return;
            }

            ReceiverAsset receiverAsset = ScriptableObject.CreateInstance<ReceiverAsset>();

            using (var stream = File.Open(dataPath, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    uint offsetCount = reader.ReadUInt32();
                    uint receiverCount = reader.ReadUInt32();

                    for (int i = 0; i < offsetCount; i++)
                    {
                        uint x = reader.ReadUInt32();
                        uint y = reader.ReadUInt32();
                        receiverAsset.offsets.Add(new Vector2Int((int)x, (int)y));
                    }

                    for (int i = 0; i < receiverCount; i++)
                    {
                        Receiver receiver = new Receiver();
                        receiver.posX = reader.ReadUInt32();
                        receiver.posY = reader.ReadUInt32();
                        receiver.posZ = reader.ReadUInt32();
                        receiver.irradiance = RuntimeUtils.UnpackR8G8B8A8ToColor(reader.ReadUInt32());
                        receiver.direction = reader.ReadUInt32();
                        receiverAsset.receivers.Add(receiver);
                    }
                }
            }

            string assetPath = "Assets/Resources/PrecomputedLighting/" + SceneManager.GetActiveScene().name + "_VolumetricLightmap_Receiver.asset";
            AssetDatabase.CreateAsset(receiverAsset, assetPath);

            precomputedLightingManager.volumetricLightmapReceiverGUID = AssetDatabase.AssetPathToGUID(assetPath);
            precomputedLightingManager.ImportVolumetricLightmapReceivers();
        }

        public class IPC
        {
            public static async Task GenerateLightmap(bool generate)
            {
                await UberCall("generateLightmap", generate ? "1" : "0");
            }

            public static async Task SetDiffuseBoost(float val)
            {
                await UberCall("setDiffuseBoost", val.ToString());
            }

            public static async Task SetLightmapSize(int lightmapSize)
            {
                await UberCall("setLightmapSize", lightmapSize.ToString());
            }

            public static async Task SetLightmapSampleCount(int sampleCount)
            {
                await UberCall("setLightmapSampleCount", sampleCount.ToString());
            }

            public static async Task GenerateVolumetricLightmap(bool generate)
            {
                await UberCall("generateVolumetricLightmap", generate ? "1" : "0");
            }

            public static async Task SetVLMBoundingBoxMin(Vector3 boundingBoxMin)
            {
                await UberCall("setVLMBoundingBoxMin", boundingBoxMin.x.ToString(), boundingBoxMin.y.ToString(), boundingBoxMin.z.ToString());
            }

            public static async Task SetVLMBoundingBoxMax(Vector3 boundingBoxMax)
            {
                await UberCall("setVLMBoundingBoxMax", boundingBoxMax.x.ToString(), boundingBoxMax.y.ToString(), boundingBoxMax.z.ToString());
            }

            public static async Task SetVLMCellSize(float cellSize)
            {
                await UberCall("setVLMCellSize", cellSize.ToString());
            }

            public static async Task SetVLMLocalToWorldMatrix(Matrix4x4 mat)
            {
                Matrix4x4 convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                Matrix4x4 flippedMat = convert * mat * convert;

                string matStr = "";
                Vector4 c0 = flippedMat.GetColumn(0);
                Vector4 c1 = flippedMat.GetColumn(1);
                Vector4 c2 = flippedMat.GetColumn(2);
                Vector4 c3 = flippedMat.GetColumn(3);
                matStr += (c0.x.ToString() + ",");
                matStr += (c0.y.ToString() + ",");
                matStr += (c0.z.ToString() + ",");
                matStr += (c0.w.ToString() + ",");
                matStr += (c1.x.ToString() + ",");
                matStr += (c1.y.ToString() + ",");
                matStr += (c1.z.ToString() + ",");
                matStr += (c1.w.ToString() + ",");
                matStr += (c2.x.ToString() + ",");
                matStr += (c2.y.ToString() + ",");
                matStr += (c2.z.ToString() + ",");
                matStr += (c2.w.ToString() + ",");
                matStr += (c3.x.ToString() + ",");
                matStr += (c3.y.ToString() + ",");
                matStr += (c3.z.ToString() + ",");
                matStr += (c3.w.ToString());
                await UberCall("setVLMLocalToWorldMatrix", matStr);
            }

            public static async Task SetVLMWorldToLocalMatrix(Matrix4x4 mat)
            {
                Matrix4x4 convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                Matrix4x4 flippedMat = convert * mat * convert;

                string matStr = "";
                Vector4 c0 = flippedMat.GetColumn(0);
                Vector4 c1 = flippedMat.GetColumn(1);
                Vector4 c2 = flippedMat.GetColumn(2);
                Vector4 c3 = flippedMat.GetColumn(3);
                matStr += (c0.x.ToString() + ",");
                matStr += (c0.y.ToString() + ",");
                matStr += (c0.z.ToString() + ",");
                matStr += (c0.w.ToString() + ",");
                matStr += (c1.x.ToString() + ",");
                matStr += (c1.y.ToString() + ",");
                matStr += (c1.z.ToString() + ",");
                matStr += (c1.w.ToString() + ",");
                matStr += (c2.x.ToString() + ",");
                matStr += (c2.y.ToString() + ",");
                matStr += (c2.z.ToString() + ",");
                matStr += (c2.w.ToString() + ",");
                matStr += (c3.x.ToString() + ",");
                matStr += (c3.y.ToString() + ",");
                matStr += (c3.z.ToString() + ",");
                matStr += (c3.w.ToString());

                await UberCall("setVLMWorldToLocalMatrix", matStr);
            }

            public static async Task LoadScene(string scenePath)
            {
                await UberCall("loadScene", scenePath);
            }

            public static async Task Open()
            {
                bool IsPortAvailable(int port)
                {
                    // Get the list of active TCP connections
                    var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();

                    // Check if any connection is using the specified port
                    foreach (var tcpConnection in tcpConnections)
                    {
                        if (tcpConnection.Port == port)
                        {
                            return false;
                        }
                    }

                    return true;
                }

                int FindValidPort()
                {
                    for (int portCandidate = 20000; portCandidate < 30000; portCandidate++)
                    {
                        if (IsPortAvailable(portCandidate))
                        {
                            return portCandidate;
                        }
                    }

                    return -1;
                }

                string workingDirectory = EditorUtils.GetWorkingDirectory();
                string lightmapperDirPath = Path.GetFullPath(Application.dataPath + @"\Scripts\Lightmapper\Bin~");

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = lightmapperDirPath + @"\runner.exe";
                processStartInfo.WorkingDirectory = workingDirectory;
                processStartInfo.UseShellExecute = true;
                var date = DateTime.Now;
                Port = FindValidPort();
                if (Port == -1)
                {
                    UnityEngine.Debug.LogError("Can not find valid port");
                    return;
                }
                else
                {
                    UnityEngine.Debug.Log("Running lightmapper at port: " + Port.ToString());
                }
                processStartInfo.Arguments = "--log " + workingDirectory + "/logs/" + SceneManager.GetActiveScene().name + "_" + date.Month.ToString() + "_" + date.Day.ToString() + "_" + date.Hour.ToString() + ".txt" + " --port=" + Port.ToString();
                Process.Start(processStartInfo);

                await IsRunning();
            }

            public static async Task<bool> WaitToFinish()
            {
                bool succeeded = false;
                while (!succeeded)
                {
                    // do work
                    try
                    {
                        succeeded = await IsFinished();
                    }
                    catch
                    {
                    }

                    await Task.Delay(2000);
                }
                return succeeded;
            }

            static async Task<byte[]> GetBytesAsync(string url)
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                using (var response = await request.GetResponseAsync())
                using (var content = new MemoryStream())
                using (var responseStream = response.GetResponseStream())
                {
                    await responseStream.CopyToAsync(content);
                    return content.ToArray();
                }
            }

            static async Task<string> GetStringAsync(string url)
            {
                var bytes = await GetBytesAsync(url);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }

            static async Task UberCall(string name, string arg0 = "", string arg1 = "", string arg2 = "", string arg3 = "")
            {
                await GetStringAsync(string.Format("http://127.0.0.1:{0}/uber_api?name={1}&arg0={2}&arg1={3}&arg2={4}&arg3={5}", Port, name, arg0, arg1, arg2, arg3));
            }

            static async Task IsRunning()
            {
                bool succeeded = false;
                while (!succeeded)
                {
                    try
                    {
                        var content = await GetStringAsync("http://127.0.0.1:" + Port.ToString() + "/running");
                        var result = int.Parse(content);
                        if (result == 1)
                        {
                            succeeded = true;
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(2000);
                }
            }

            static async Task<bool> IsFinished()
            {
                var content = await GetStringAsync("http://127.0.0.1:" + Port.ToString() + "/baking_state");
                var result = int.Parse(content);
                return result == 1;
            }

            private static int Port;
        }
    }
}
