using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public class VolumetricLightmapDynamicBakingPass : IDisposable
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
                    case 4:
                        return item4;
                    case 5:
                        return item5;
                    case 6:
                        return item6;
                    case 7:
                        return item7;
                    default:
                        Debug.Assert(false);
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
                    case 4:
                        item4 = value;
                        break;
                    case 5:
                        item5 = value;
                        break;
                    case 6:
                        item6 = value;
                        break;
                    case 7:
                        item7 = value;
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
                return (item0 == 0 && item1 == 0 && item2 == 0 && item3 == 0 && item4 == 0 && item5 == 0 && item6 == 0 && item7 == 0);
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
                if (item4 == 1)
                {
                    res++;
                }
                if (item5 == 1)
                {
                    res++;
                }
                if (item6 == 1)
                {
                    res++;
                }
                if (item7 == 1)
                {
                    res++;
                }
                return res;
            }

            int item0;
            int item1;
            int item2;
            int item3;
            int item4;
            int item5;
            int item6;
            int item7;
        }

        struct FrameGroupAllocator
        {
            public FrameState AllocateFrames(float delta)
            {
                if (delta > 0.2f)
                {
                    return Pop(8);
                }
                else if (delta > 0.1f)
                {
                    return Pop(4);
                }
                else if (delta > 0.0f)
                {
                    return Pop(2);
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
                        return Pop(8);
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
                result[4] = 1;
                result[5] = 1;
                result[6] = 1;
                result[7] = 1;
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
                if (currentFrameState[4] == 0 && addedNum < num)
                {
                    currentFrameState[4] = 1;
                    result[4] = 1;
                    addedNum++;
                }
                if (currentFrameState[6] == 0 && addedNum < num)
                {
                    currentFrameState[6] = 1;
                    result[6] = 1;
                    addedNum++;
                }
                if (currentFrameState[5] == 0 && addedNum < num)
                {
                    currentFrameState[5] = 1;
                    result[5] = 1;
                    addedNum++;
                }
                if (currentFrameState[7] == 0 && addedNum < num)
                {
                    currentFrameState[7] = 1;
                    result[7] = 1;
                    addedNum++;
                }

                return result;
            }

            public bool AllUpdated()
            {
                return (currentFrameState[0] == 1 && currentFrameState[1] == 1 && currentFrameState[2] == 1 && currentFrameState[3] == 1 && currentFrameState[4] == 1 && currentFrameState[5] == 1 && currentFrameState[6] == 1 && currentFrameState[7] == 1);
            }

            public void Reset()
            {
                currentFrameState[0] = 0;
                currentFrameState[1] = 0;
                currentFrameState[2] = 0;
                currentFrameState[3] = 0;
                currentFrameState[4] = 0;
                currentFrameState[5] = 0;
                currentFrameState[6] = 0;
                currentFrameState[7] = 0;
            }

            public FrameState currentFrameState;
        }

        private PrecomputedLightingManager mgr;

        CommandBuffer mCmdBuffer;
        ComputeShader mShader;

        int mKernel;

        List<ComputeBuffer> mOneLightComputeBuffers;
        List<ComputeBuffer> mTwoLightComputeBuffers;
        List<ComputeBuffer> mThreeLightComputeBuffers;

        FrameGroupAllocator mFrameGroupAllocator;

        public VolumetricLightmapDynamicBakingPass(PrecomputedLightingManager mgr)
        {
            this.mgr = mgr;

            mCmdBuffer = new CommandBuffer();
            mCmdBuffer.name = "VolumetricLightmapDynamicBakingPass";

            mShader = Resources.Load<ComputeShader>("Shaders/VolumetricLightmapDynamicBakingPass");
            mKernel = mShader.FindKernel("main");

            mOneLightComputeBuffers = new List<ComputeBuffer>();
            for (int i = 0; i < mgr.oneLightVolumetricLightmapReceiverDatas.Count; i++)
            {
                var receiverData = mgr.oneLightVolumetricLightmapReceiverDatas[i];
                ComputeBuffer computeBuffer = new ComputeBuffer(receiverData.receivers.Count, 16);
                computeBuffer.SetData(receiverData.receivers);
                mOneLightComputeBuffers.Add(computeBuffer);
            }

            mTwoLightComputeBuffers = new List<ComputeBuffer>();
            for (int i = 0; i < mgr.twoLightVolumetricLightmapReceiverDatas.Count; i++)
            {
                var receiverData = mgr.twoLightVolumetricLightmapReceiverDatas[i];
                ComputeBuffer computeBuffer = new ComputeBuffer(receiverData.receivers.Count, 16);
                computeBuffer.SetData(receiverData.receivers);
                mTwoLightComputeBuffers.Add(computeBuffer);
            }

            mThreeLightComputeBuffers = new List<ComputeBuffer>();
            for (int i = 0; i < mgr.threeLightVolumetricLightmapReceiverDatas.Count; i++)
            {
                var receiverData = mgr.threeLightVolumetricLightmapReceiverDatas[i];
                ComputeBuffer computeBuffer = new ComputeBuffer(receiverData.receivers.Count, 16);
                computeBuffer.SetData(receiverData.receivers);
                mThreeLightComputeBuffers.Add(computeBuffer);
            }

            mFrameGroupAllocator = new FrameGroupAllocator();
        }

        public bool Begin()
        {
            if (mgr == null || mgr.GetVolumetricLightmap() == null || mgr.dynamicBakings.Count <= 0)
            {
                return false;
            }

            float deltaRatio = 0.0f;
            for (int i = 0; i < mgr.dynamicBakings.Count; i++)
            {
                DynamicBaking dynamicBaking = mgr.dynamicBakings[i];
                deltaRatio = Mathf.Max(deltaRatio, dynamicBaking.delta);
            }

            if (deltaRatio > 0 && mFrameGroupAllocator.AllUpdated())
            {
                mFrameGroupAllocator.Reset();
            }

            FrameState frames;
            if (Application.isPlaying)
            {
                frames = mFrameGroupAllocator.AllocateFrames(deltaRatio);
            }
            else
            {
                frames = mFrameGroupAllocator.AllocateAllFrames();
            }

            if (frames.Empty())
            {
                return false;
            }

            mCmdBuffer.BeginSample("Dynamic Baking Volumetric Lightmap Update");

            mCmdBuffer.SetComputeTextureParam(mShader, mKernel, "_SrcVLM", mgr.volumetricLightmap);
            mCmdBuffer.SetComputeTextureParam(mShader, mKernel, "_DstVLM", mgr.volumetricLightmapRT);

            float GetIntensity(VolumetricLightmapReceiverData receiverData, int index)
            {
                if (index >= receiverData.lightIndices.Count)
                {
                    return 0.0f;
                }

                int lightIndex = receiverData.lightIndices[index];
                DynamicBaking dynamicBaking = mgr.dynamicBakings[lightIndex];
                return dynamicBaking.intensity;
            }

            for (int i = 0; i < mOneLightComputeBuffers.Count; i++)
            {
                VolumetricLightmapReceiverData asset = mgr.oneLightVolumetricLightmapReceiverDatas[i];
                if (!frames.Contain(asset.frameGroup) && asset.frameGroup != VolumetricLightmapReceiverData.DefaultFrameGroup)
                {
                    continue;
                }

                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity0", GetIntensity(asset, 0));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity1", GetIntensity(asset, 1));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity2", GetIntensity(asset, 2));

                mCmdBuffer.SetComputeIntParam(mShader, "_ReceiverCount", asset.receivers.Count);
                mCmdBuffer.SetComputeBufferParam(mShader, mKernel, "_Receivers", mOneLightComputeBuffers[i]);

                mCmdBuffer.DispatchCompute(mShader, mKernel, asset.receivers.Count / 8 + 1, 1, 1);
            }

            for (int i = 0; i < mTwoLightComputeBuffers.Count; i++)
            {
                VolumetricLightmapReceiverData asset = mgr.twoLightVolumetricLightmapReceiverDatas[i];
                if (!frames.Contain(asset.frameGroup) && asset.frameGroup != VolumetricLightmapReceiverData.DefaultFrameGroup)
                {
                    continue;
                }

                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity0", GetIntensity(asset, 0));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity1", GetIntensity(asset, 1));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity2", GetIntensity(asset, 2));

                mCmdBuffer.SetComputeIntParam(mShader, "_ReceiverCount", asset.receivers.Count);
                mCmdBuffer.SetComputeBufferParam(mShader, mKernel, "_Receivers", mTwoLightComputeBuffers[i]);

                mCmdBuffer.DispatchCompute(mShader, mKernel, asset.receivers.Count / 8 + 1, 1, 1);
            }

            for (int i = 0; i < mThreeLightComputeBuffers.Count; i++)
            {
                VolumetricLightmapReceiverData asset = mgr.threeLightVolumetricLightmapReceiverDatas[i];
                if (!frames.Contain(asset.frameGroup) && asset.frameGroup != VolumetricLightmapReceiverData.DefaultFrameGroup)
                {
                    continue;
                }

                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity0", GetIntensity(asset, 0));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity1", GetIntensity(asset, 1));
                mCmdBuffer.SetComputeFloatParam(mShader, "_Intensity2", GetIntensity(asset, 2));

                mCmdBuffer.SetComputeIntParam(mShader, "_ReceiverCount", asset.receivers.Count);
                mCmdBuffer.SetComputeBufferParam(mShader, mKernel, "_Receivers", mThreeLightComputeBuffers[i]);

                mCmdBuffer.DispatchCompute(mShader, mKernel, asset.receivers.Count / 8 + 1, 1, 1);
            }

            return true;
        }

        public void End()
        {
            mCmdBuffer.EndSample("Dynamic Baking Volumetric Lightmap Update");
            Graphics.ExecuteCommandBuffer(mCmdBuffer);
            mCmdBuffer.Clear();
        }

        public void Dispose()
        {
            if (mOneLightComputeBuffers != null)
            {
                for (int i = 0; i < mOneLightComputeBuffers.Count; i++)
                {
                    var computeBuffer = mOneLightComputeBuffers[i];
                    if (computeBuffer != null)
                    {
                        computeBuffer.Dispose();
                    }
                }

                mOneLightComputeBuffers = null;
            }

            if (mTwoLightComputeBuffers != null)
            {
                for (int i = 0; i < mTwoLightComputeBuffers.Count; i++)
                {
                    var computeBuffer = mTwoLightComputeBuffers[i];
                    if (computeBuffer != null)
                    {
                        computeBuffer.Dispose();
                    }
                }

                mTwoLightComputeBuffers = null;
            }

            if (mThreeLightComputeBuffers != null)
            {
                for (int i = 0; i < mThreeLightComputeBuffers.Count; i++)
                {
                    var computeBuffer = mThreeLightComputeBuffers[i];
                    if (computeBuffer != null)
                    {
                        computeBuffer.Dispose();
                    }
                }

                mThreeLightComputeBuffers = null;
            }

            if (mCmdBuffer != null)
            {
                mCmdBuffer.Dispose();
            }
        }
    }
}
