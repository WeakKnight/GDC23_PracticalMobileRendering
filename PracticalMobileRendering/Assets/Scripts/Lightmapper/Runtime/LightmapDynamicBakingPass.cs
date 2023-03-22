using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    class LightmapDynamicBakingPass
    {
        struct FrameState
        {
            public int this[int index]
            {
                get
                {
                    return GetValue(index);
                }
                set
                {
                    SetValue(index, value);
                }
            }

            int GetValue(int index)
            {
                switch (index)
                {
                    case 0:
                        return item0;
                    case 1:
                        return item1;
                    case 2:
                        return item2;
                    case 3:
                        return item3;
                    default:
                        return 0;
                }
            }

            void SetValue(int index, int value)
            {
                switch (index)
                {
                    case 0:
                        item0 = value;
                        break;
                    case 1:
                        item1 = value;
                        break;
                    case 2:
                        item2 = value;
                        break;
                    case 3:
                        item3 = value;
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            public bool Contain(int frame)
            {
                return (GetValue(frame) == 1);
            }

            public bool Empty()
            {
                return (item0 == 0 && item1 == 0 && item2 == 0 && item3 == 0);
            }

            public int ActiveNum()
            {
                int res = 0;
                if (item0 == 1)
                {
                    res++;
                }
                if (item1 == 1)
                {
                    res++;
                }
                if (item2 == 1)
                {
                    res++;
                }
                if (item3 == 1)
                {
                    res++;
                }
                return res;
            }

            int item0;
            int item1;
            int item2;
            int item3;
        }

        struct FrameGroupAllocator
        {
            public FrameState AllocateFrames(float delta)
            {
                if (delta > 0.08f)
                {
                    return Pop(4);
                }
                else if (delta > 0.04f)
                {
                    return Pop(2);
                }
                else if (delta > 0.0f)
                {
                    return Pop(1);
                }
                else
                {
                    if (AllUpdated())
                    {
                        return Pop(0);
                    }
                    // Sync as fast as we can
                    else
                    {
                        return Pop(4);
                    }
                }
            }

            public FrameState AllocateAllFrames()
            {
                FrameState result = new FrameState();
                result[0] = 1;
                result[1] = 1;
                result[2] = 1;
                result[3] = 1;
                return result;
            }

            private FrameState Pop(int num)
            {
                FrameState result = new FrameState();

                int addedNum = 0;
                if (currentFrameState[0] == 0 && addedNum < num)
                {
                    currentFrameState[0] = 1;
                    result[0] = 1;
                    addedNum++;
                }
                if (currentFrameState[2] == 0 && addedNum < num)
                {
                    currentFrameState[2] = 1;
                    result[2] = 1;
                    addedNum++;
                }
                if (currentFrameState[1] == 0 && addedNum < num)
                {
                    currentFrameState[1] = 1;
                    result[1] = 1;
                    addedNum++;
                }
                if (currentFrameState[3] == 0 && addedNum < num)
                {
                    currentFrameState[3] = 1;
                    result[3] = 1;
                    addedNum++;
                }

                return result;
            }

            public bool AllUpdated()
            {
                return (currentFrameState[0] == 1 && currentFrameState[1] == 1 && currentFrameState[2] == 1 && currentFrameState[3] == 1);
            }

            public void Reset()
            {
                currentFrameState[0] = 0;
                currentFrameState[1] = 0;
                currentFrameState[2] = 0;
                currentFrameState[3] = 0;
            }

            public FrameState currentFrameState;
        }

        static class ShaderConstants
        {
            public static int SrcLightmap = Shader.PropertyToID("_SrcLightmap");
            public static int DstLightmap = Shader.PropertyToID("_DstLightmap");
            public static int Intensity0 = Shader.PropertyToID("_Intensity0");
            public static int Intensity1 = Shader.PropertyToID("_Intensity1");
            public static int Intensity2 = Shader.PropertyToID("_Intensity2");
            public static int LightmapReceivers = Shader.PropertyToID("_LightmapReceivers");
            public static int LightmapSize = Shader.PropertyToID("_LightmapSize");
            public static int ReceiverCount = Shader.PropertyToID("_ReceiverCount");
        }

        Material material;
        ComputeShader computeShader;
        int kernel;

        PrecomputedLightingManager mgr;
        FrameGroupAllocator frameGroupAllocator;
        CommandBuffer cmdBuffer;

        public LightmapDynamicBakingPass(PrecomputedLightingManager mgr)
        {
            this.mgr = mgr;
            if (PrecomputedLightingManager.UseComputeShaderForLightmapWithDynamicBaking)
            {
                computeShader = Resources.Load<ComputeShader>("Shaders/LightmapUpdateCompute");
                kernel = computeShader.FindKernel("main");
            }
            else
            {
                material = new Material(Shader.Find("PMRP/LightmapUpdate"));
            }
            frameGroupAllocator = new FrameGroupAllocator();
            cmdBuffer = new CommandBuffer();
            cmdBuffer.name = "DynamicLightingLightmapCmdBuffer";
        }

        public bool Begin()
        {
            if (mgr == null || mgr.lightmap == null || mgr.lightmapRT == null || mgr.dynamicBakings.Count <= 0)
            {
                return false;
            }

            float globalDelta = 0.0f;
            for (int i = 0; i < mgr.dynamicBakings.Count; i++)
            {
                globalDelta = Mathf.Max(globalDelta, mgr.dynamicBakings[i].delta);
            }

            if (globalDelta > 0 && frameGroupAllocator.AllUpdated())
            {
                frameGroupAllocator.Reset();
            }

            FrameState frames;
            if (UnityEngine.Application.isPlaying)
            {
                frames = frameGroupAllocator.AllocateFrames(globalDelta);
            }
            else
            {
                frames = frameGroupAllocator.AllocateAllFrames();
            }

            if (frames.Empty())
            {
                return false;
            }

            mgr.lightmap.filterMode = FilterMode.Point;
            mgr.lightmapRT.filterMode = FilterMode.Bilinear;

            cmdBuffer.BeginSample("Dynamic Baking Lightmap Update");

            float GetIntensity(LightmapReceiverData receiverData, int index)
            {
                if (index >= receiverData.lightIndices.Count)
                {
                    return 0.0f;
                }

                int lightIndex = receiverData.lightIndices[index];
                DynamicBaking dynamicBaking = mgr.dynamicBakings[lightIndex];
                return dynamicBaking.intensity;
            }

            void GraphicsPipelineImpl()
            {
                cmdBuffer.SetGlobalTexture(ShaderConstants.SrcLightmap, mgr.lightmap);
                cmdBuffer.SetRenderTarget(mgr.lightmapRT);
                cmdBuffer.SetViewport(new Rect(0.0f, 0.0f, mgr.lightmapRT.width, mgr.lightmapRT.height));

                void DrawReceiverData(LightmapReceiverData receiverData)
                {
                    if (!frames.Contain(receiverData.frameGroup) && receiverData.frameGroup != LightmapReceiverData.DefaultFrameGroup)
                    {
                        return;
                    }

                    if (receiverData.mesh == null)
                    {
                        return;
                    }

                    cmdBuffer.SetGlobalFloat(ShaderConstants.Intensity0, GetIntensity(receiverData, 0));
                    cmdBuffer.SetGlobalFloat(ShaderConstants.Intensity1, GetIntensity(receiverData, 1));
                    cmdBuffer.SetGlobalFloat(ShaderConstants.Intensity2, GetIntensity(receiverData, 2));
                    cmdBuffer.DrawMesh(receiverData.mesh, Matrix4x4.identity, material, 0, 0);
                }

                if (mgr.oneLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.oneLightLightmapReceiverDatas)
                    {
                        DrawReceiverData(receiverData);
                    }
                }

                if (mgr.twoLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.twoLightLightmapReceiverDatas)
                    {
                        DrawReceiverData(receiverData);
                    }
                }

                if (mgr.threeLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.threeLightLightmapReceiverDatas)
                    {
                        DrawReceiverData(receiverData);
                    }
                }
            }

            void ComputePipelineImpl()
            {
                cmdBuffer.SetComputeTextureParam(computeShader, kernel, ShaderConstants.SrcLightmap, mgr.lightmap);
                cmdBuffer.SetComputeTextureParam(computeShader, kernel, ShaderConstants.DstLightmap, mgr.lightmapRT);
                cmdBuffer.SetComputeIntParam(computeShader, ShaderConstants.LightmapSize, mgr.lightmapResolution);

                void DispatchReceiverData(LightmapReceiverData receiverData)
                {
                    if (!frames.Contain(receiverData.frameGroup) && receiverData.frameGroup != LightmapReceiverData.DefaultFrameGroup)
                    {
                        return;
                    }

                    if (receiverData.computeBuffer == null)
                    {
                        return;
                    }

                    cmdBuffer.SetComputeFloatParam(computeShader, ShaderConstants.Intensity0, GetIntensity(receiverData, 0));
                    cmdBuffer.SetComputeFloatParam(computeShader, ShaderConstants.Intensity1, GetIntensity(receiverData, 1));
                    cmdBuffer.SetComputeFloatParam(computeShader, ShaderConstants.Intensity2, GetIntensity(receiverData, 2));

                    cmdBuffer.SetComputeIntParam(computeShader, ShaderConstants.ReceiverCount, receiverData.receivers.Count);
                    cmdBuffer.SetComputeBufferParam(computeShader, kernel, ShaderConstants.LightmapReceivers, receiverData.computeBuffer);

                    cmdBuffer.DispatchCompute(computeShader, kernel, receiverData.receivers.Count / 64 + 1, 1, 1);
                }

                if (mgr.oneLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.oneLightLightmapReceiverDatas)
                    {
                        DispatchReceiverData(receiverData);
                    }
                }

                if (mgr.twoLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.twoLightLightmapReceiverDatas)
                    {
                        DispatchReceiverData(receiverData);
                    }
                }

                if (mgr.threeLightLightmapReceiverDatas != null)
                {
                    foreach (var receiverData in mgr.threeLightLightmapReceiverDatas)
                    {
                        DispatchReceiverData(receiverData);
                    }
                }
            }

            if (PrecomputedLightingManager.UseComputeShaderForLightmapWithDynamicBaking)
            {
                ComputePipelineImpl();
            }
            else
            {
                GraphicsPipelineImpl();
            }

            return true;
        }

        public void End()
        {
            cmdBuffer.EndSample("Dynamic Baking Lightmap Update");
            Graphics.ExecuteCommandBuffer(cmdBuffer);
            cmdBuffer.Clear();
        }
    }
}