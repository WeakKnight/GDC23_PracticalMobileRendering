using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    [Serializable]
    public struct Receiver
    {
        public uint posX;
        public uint posY;
        public uint posZ;
        /* RGBA8888, RGBM Encoding */
        public Color32 irradiance;
        /* Octahedron Encoding */
        public uint direction;

        public float Luminance()
        {
            return RuntimeUtils.Luminance(RuntimeUtils.DecodeRGBM(irradiance));
        }
    }

    [Serializable]
    public struct PackedLightmapReceiver
    {
        public PackedLightmapReceiver(int x, int y, int width, int height)
        {
            uvAndIrradiance01 = new Vector3();
            irradiance2 = new Color32();
            SetUV(new Vector2(x / (float)width + (0.5f / width), y / (float)height + (0.5f / height)));
        }

        public void SetUV(Vector2 uv)
        {
            uvAndIrradiance01.x = RuntimeUtils.AsFloat(RuntimeUtils.PackR16G14ToUINT(uv));
        }

        public void SetIrradiance(int index, Color32 irradiance)
        {
            if (index == 0)
            {
                uvAndIrradiance01.y = RuntimeUtils.AsFloat(RuntimeUtils.SafelyPackColor32ToUInt(irradiance));
            }
            else if (index == 1)
            {
                uvAndIrradiance01.z = RuntimeUtils.AsFloat(RuntimeUtils.SafelyPackColor32ToUInt(irradiance));
            }
            else if (index == 2)
            {
                irradiance2 = irradiance;
            }
            else
            {
                Debug.LogAssertion("Should Not Happen");
            }
        }

        [SerializeField]
        public Vector3 uvAndIrradiance01;
        [SerializeField]
        public Color32 irradiance2;
    }

    [Serializable]
    public struct PackedVolumetricLightmapReceiver
    {
        public PackedVolumetricLightmapReceiver(int x, int y, int z)
        {
            xyz = RuntimeUtils.PackR10G11B11ToUInt(new Vector3Int(x, y, z));
            irradiance0 = 0u;
            irradiance1 = 0u;
            irradiance2 = 0u;
        }

        public void SetIrradiance(int index, Color32 irradiance)
        {
            if (index == 0)
            {
                irradiance0 = RuntimeUtils.SafelyPackColor32ToUInt(irradiance);
            }
            else if (index == 1)
            {
                irradiance1 = RuntimeUtils.SafelyPackColor32ToUInt(irradiance);
            }
            else if (index == 2)
            {
                irradiance2 = RuntimeUtils.SafelyPackColor32ToUInt(irradiance);
            }
            else
            {
                Debug.LogAssertion("Should Not Happen");
            }
        }

        public uint xyz;
        public uint irradiance0;
        public uint irradiance1;
        public uint irradiance2;
    }

    [Serializable]
    public class VolumetricLightmapReceiverData
    {
        public const int DefaultFrameGroup = 65535;

        [SerializeField]
        public List<PackedVolumetricLightmapReceiver> receivers;
        [SerializeField]
        public List<int> lightIndices;
        [SerializeField]
        public int frameGroup = DefaultFrameGroup;

        [NonSerialized]
        public ComputeBuffer computeBuffer;
    }

    [Serializable]
    public class LightmapReceiverData
    {
        public const int DefaultFrameGroup = 65535;

        [SerializeField]
        public List<PackedLightmapReceiver> receivers;

        [SerializeField]
        public List<int> lightIndices;

        [SerializeField]
        public int frameGroup = DefaultFrameGroup;

        [NonSerialized]
        public Mesh mesh;

        [NonSerialized]
        public ComputeBuffer computeBuffer;

        public void Release()
        {
            if (computeBuffer != null)
            {
                computeBuffer.Release();
                computeBuffer = null;
            }
        }

        public void CreateReceiverBuffer()
        {
            if (receivers == null || receivers.Count <= 0)
            {
                return;
            }

            void CreateForComputePipeline()
            {
                if (computeBuffer != null)
                {
                    computeBuffer.Release();
                    computeBuffer = null;
                }

                computeBuffer = new ComputeBuffer(receivers.Count, 16);
                computeBuffer.SetData(receivers.ToArray());
            }

            void CreateForGraphicsPipeline()
            {
                mesh = new Mesh();
                mesh.name = "Receiver Mesh";
                mesh.indexFormat = receivers.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

                List<Vector3> meshVertices = new List<Vector3>(receivers.Count);
                List<Color32> meshColors = new List<Color32>(receivers.Count);
                for (int i = 0; i < receivers.Count; i++)
                {
                    meshVertices.Add(receivers[i].uvAndIrradiance01);
                    meshColors.Add(receivers[i].irradiance2);
                }
                mesh.SetVertices(meshVertices);
                mesh.SetColors(meshColors);

                int[] indices = new int[receivers.Count];
                for (int vertexIndex = 0; vertexIndex < receivers.Count; vertexIndex++)
                {
                    indices[vertexIndex] = vertexIndex;
                }

                mesh.SetIndices(indices, MeshTopology.Points, 0, false /* bounding calculation is meaningless for us, just assian an empty bound */);
                mesh.bounds = new Bounds();
            }

            if (PrecomputedLightingManager.UseComputeShaderForLightmapWithDynamicBaking)
            {
                CreateForComputePipeline();
            }
            else
            {
                CreateForGraphicsPipeline();
            }
        }
    }
}
