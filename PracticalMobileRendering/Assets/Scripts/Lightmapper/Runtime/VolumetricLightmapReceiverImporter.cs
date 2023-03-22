using PMRP;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class VolumetricLightmapReceiverImporter
{
    public class Context
    {
        public Context(ReceiverAsset pReceiverAsset, int pDynamicBakingSettingCount, int pVLMWidth, int pVLMHeight, int pVLMDepth, bool pSplitIntoFrameGroups = true)
        {
            receiverAsset = pReceiverAsset;
            dynamicBakingSettingCount = pDynamicBakingSettingCount;
            vlmWidth = pVLMWidth;
            vlmHeight = pVLMHeight;
            vlmDepth = pVLMDepth;
            splitIntoFrameGroups = pSplitIntoFrameGroups;

            usageMap = new Dictionary<Hash128, int>();
        }

        public ReceiverAsset receiverAsset;
        public int dynamicBakingSettingCount;
        public int vlmWidth;
        public int vlmHeight;
        public int vlmDepth;
        public bool splitIntoFrameGroups;

        public Dictionary<Hash128, int> usageMap;
    }

    public class Result
    {
        public Result()
        {
            oneLightData = new List<VolumetricLightmapReceiverData>();
            twoLightData = new List<VolumetricLightmapReceiverData>();
            threeLightData = new List<VolumetricLightmapReceiverData>();
        }

        public List<VolumetricLightmapReceiverData> oneLightData;
        public List<VolumetricLightmapReceiverData> twoLightData;
        public List<VolumetricLightmapReceiverData> threeLightData;
    }

    public static Result ImportVolumetricLightmapReceivers(VolumetricLightmapReceiverImporter.Context context)
    {
        CollectVLMReceiverUsage(context);
        return CreateVLMLightReceivers(context);
    }

    private static void CollectVLMReceiverUsage(VolumetricLightmapReceiverImporter.Context context)
    {
        for (int lightIndex = 0; lightIndex < context.receiverAsset.offsets.Count; lightIndex++)
        {
            int receiverOffset = (int)context.receiverAsset.offsets[lightIndex].x;
            int receiverCount = (int)context.receiverAsset.offsets[lightIndex].y;
            for (int receiverIndex = 0; receiverIndex < receiverCount; receiverIndex++)
            {
                Receiver receiver = context.receiverAsset.receivers[receiverIndex];

                int x = (int)receiver.posX;
                int y = (int)receiver.posY;
                int z = (int)receiver.posZ;

                if (receiver.irradiance.r <= 0 && receiver.irradiance.g <= 0 && receiver.irradiance.b <= 0)
                {
                    continue;
                }

                Hash128 locationKey = ConstructLocationKey(x, y, z);
                if (!context.usageMap.ContainsKey(locationKey))
                {
                    context.usageMap[locationKey] = (1 << lightIndex);
                }
                else
                {
                    context.usageMap[locationKey] = context.usageMap[locationKey] | (1 << lightIndex);
                }
            }
        }
    }

    private static Result CreateVLMLightReceivers(VolumetricLightmapReceiverImporter.Context context)
    {
        Result result = new Result();

        List<int> lightIndexEnumeration = new List<int>();
        for (int i = 0; i < context.dynamicBakingSettingCount; i++)
        {
            lightIndexEnumeration.Add(i);
        }

        Dictionary<Hash128, VolumetricLightmapReceiverData> oneLightReceiverAssets = new Dictionary<Hash128, VolumetricLightmapReceiverData>();
        Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>> oneLightReceivers = new Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>>();
        List<IEnumerable<int>> oneLightPossibleCombinations;
        if (context.dynamicBakingSettingCount >= 1)
        {
            oneLightPossibleCombinations = GetCombinations<int>(lightIndexEnumeration, 1).ToList();
        }
        else
        {
            oneLightPossibleCombinations = new List<IEnumerable<int>>();
        }

        Dictionary<Hash128, VolumetricLightmapReceiverData> twoLightReceiverAssets = new Dictionary<Hash128, VolumetricLightmapReceiverData>();
        Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>> twoLightReceivers = new Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>>();
        List<IEnumerable<int>> twoLightPossibleCombinations;
        if (context.dynamicBakingSettingCount >= 2)
        {
            twoLightPossibleCombinations = GetCombinations<int>(lightIndexEnumeration, 2).ToList();
        }
        else
        {
            twoLightPossibleCombinations = new List<IEnumerable<int>>();
        }

        Dictionary<Hash128, VolumetricLightmapReceiverData> threeLightReceiverAssets = new Dictionary<Hash128, VolumetricLightmapReceiverData>();
        Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>> threeLightReceivers = new Dictionary<Hash128, Dictionary<Hash128, PackedVolumetricLightmapReceiver>>();
        List<IEnumerable<int>> threeLightPossibleCombinations;
        if (context.dynamicBakingSettingCount >= 3)
        {
            threeLightPossibleCombinations = GetCombinations<int>(lightIndexEnumeration, 3).ToList();
        }
        else
        {
            threeLightPossibleCombinations = new List<IEnumerable<int>>();
        }

        // Initialize Vertex Dictionary
        List<int> frameGroups;
        if (context.splitIntoFrameGroups)
        {
            frameGroups = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        }
        else
        {
            frameGroups = new List<int> { VolumetricLightmapReceiverData.DefaultFrameGroup };
        }

        for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
        {
            int frameGroup = frameGroups[groupIndex];
            for (int i = 0; i < oneLightPossibleCombinations.Count; i++)
            {
                List<int> combination = oneLightPossibleCombinations[i].ToList();
                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                oneLightReceivers[receiverAssetKey] = new Dictionary<Hash128, PackedVolumetricLightmapReceiver>();
            }

            for (int i = 0; i < twoLightPossibleCombinations.Count; i++)
            {
                List<int> combination = twoLightPossibleCombinations[i].ToList();
                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                twoLightReceivers[receiverAssetKey] = new Dictionary<Hash128, PackedVolumetricLightmapReceiver>();
            }

            for (int i = 0; i < threeLightPossibleCombinations.Count; i++)
            {
                List<int> combination = threeLightPossibleCombinations[i].ToList();
                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                threeLightReceivers[receiverAssetKey] = new Dictionary<Hash128, PackedVolumetricLightmapReceiver>();
            }
        }

        // Collect Light Vertices
        for (int lightIndex = 0; lightIndex < context.receiverAsset.offsets.Count; lightIndex++)
        {
            int receiverOffset = (int)context.receiverAsset.offsets[lightIndex].x;
            int receiverCount = (int)context.receiverAsset.offsets[lightIndex].y;
            for (int receiverIndex = 0; receiverIndex < receiverCount; receiverIndex++)
            {
                Receiver receiver = context.receiverAsset.receivers[receiverOffset + receiverIndex];

                int x = (int)receiver.posX;
                int y = (int)receiver.posY;
                int z = (int)receiver.posZ;

                if (receiver.irradiance.r <= 0 && receiver.irradiance.g <= 0 && receiver.irradiance.b <= 0)
                {
                    continue;
                }

                int frameGroup = context.splitIntoFrameGroups ? GetFrameGroup(x, y, z) : VolumetricLightmapReceiverData.DefaultFrameGroup;

                Hash128 locationKey = ConstructLocationKey(x, y, z);
                if (context.usageMap.ContainsKey(locationKey))
                {
                    int usageFlag = context.usageMap[locationKey];
                    int bitCount = CountBits(usageFlag);
                    if (bitCount == 1)
                    {
                        List<int> combination = GetCombinationFromLightFlag(usageFlag, 1, context.dynamicBakingSettingCount);
                        Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);

                        if (!oneLightReceivers[receiverAssetKey].ContainsKey(locationKey))
                        {
                            oneLightReceivers[receiverAssetKey][locationKey] = new PackedVolumetricLightmapReceiver(context.vlmWidth - x + 1, y, z);
                        }

                        int irradianceIndex = combination.IndexOf(lightIndex);
                        if (irradianceIndex != -1)
                        {
                            var localReceiver = oneLightReceivers[receiverAssetKey][locationKey];
                            localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                            oneLightReceivers[receiverAssetKey][locationKey] = localReceiver;
                        }
                    }
                    else if (bitCount == 2)
                    {
                        List<int> combination = GetCombinationFromLightFlag(usageFlag, 2, context.dynamicBakingSettingCount);
                        Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);

                        if (!twoLightReceivers[receiverAssetKey].ContainsKey(locationKey))
                        {
                            twoLightReceivers[receiverAssetKey][locationKey] = new PackedVolumetricLightmapReceiver(context.vlmWidth - x + 1, y, z);
                        }

                        int irradianceIndex = combination.IndexOf(lightIndex);
                        if (irradianceIndex != -1)
                        {
                            var localReceiver = twoLightReceivers[receiverAssetKey][locationKey];
                            localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                            twoLightReceivers[receiverAssetKey][locationKey] = localReceiver;
                        }
                    }
                    else if (bitCount == 3)
                    {
                        List<int> combination = GetCombinationFromLightFlag(usageFlag, 3, context.dynamicBakingSettingCount);
                        Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);

                        if (!threeLightReceivers[receiverAssetKey].ContainsKey(locationKey))
                        {
                            threeLightReceivers[receiverAssetKey][locationKey] = new PackedVolumetricLightmapReceiver(context.vlmWidth - x + 1, y, z);
                        }

                        int irradianceIndex = combination.IndexOf(lightIndex);
                        if (irradianceIndex != -1)
                        {
                            var localReceiver = threeLightReceivers[receiverAssetKey][locationKey];
                            localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                            threeLightReceivers[receiverAssetKey][locationKey] = localReceiver;
                        }
                    }
                }
            }
        }

        //! Create Receiver Assets

        for (int i = 0; i < oneLightPossibleCombinations.Count; i++)
        {
            List<int> combination = oneLightPossibleCombinations[i].ToList();
            for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
            {
                int frameGroup = frameGroups[groupIndex];

                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                var receivers = oneLightReceivers[receiverAssetKey].Values.ToList();
                if (receivers.Count <= 0)
                {
                    continue;
                }

                var receiverAsset = new VolumetricLightmapReceiverData();
                receiverAsset.lightIndices = new List<int>(combination);
                receiverAsset.receivers = receivers;
                receiverAsset.frameGroup = frameGroup;
                oneLightReceiverAssets[receiverAssetKey] = receiverAsset;
            }
        }
        result.oneLightData = oneLightReceiverAssets.Values.ToList();

        for (int i = 0; i < twoLightPossibleCombinations.Count; i++)
        {
            List<int> combination = twoLightPossibleCombinations[i].ToList();
            for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
            {
                int frameGroup = frameGroups[groupIndex];

                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                var receivers = twoLightReceivers[receiverAssetKey].Values.ToList();
                if (receivers.Count <= 0)
                {
                    continue;
                }

                var receiverAsset = new VolumetricLightmapReceiverData();
                receiverAsset.lightIndices = new List<int>(combination);
                receiverAsset.receivers = receivers;
                receiverAsset.frameGroup = frameGroup;
                twoLightReceiverAssets[receiverAssetKey] = receiverAsset;
            }
        }
        result.twoLightData = twoLightReceiverAssets.Values.ToList();

        for (int i = 0; i < threeLightPossibleCombinations.Count; i++)
        {
            List<int> combination = threeLightPossibleCombinations[i].ToList();

            for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
            {
                int frameGroup = frameGroups[groupIndex];

                Hash128 receiverAssetKey = ConstructReceiverAssetKey(combination, frameGroup);
                var receivers = threeLightReceivers[receiverAssetKey].Values.ToList();
                if (receivers.Count <= 0)
                {
                    continue;
                }

                var receiverAsset = new VolumetricLightmapReceiverData();
                receiverAsset.lightIndices = new List<int>(combination);
                receiverAsset.receivers = receivers;
                receiverAsset.frameGroup = frameGroup;
                threeLightReceiverAssets[receiverAssetKey] = receiverAsset;
            }
        }
        result.threeLightData = threeLightReceiverAssets.Values.ToList();

        return result;
    }

    private static Hash128 ConstructLocationKey(int x, int y, int z)
    {
        return new Hash128((uint)x, (uint)y, (uint)z, 0);
    }

    private static Hash128 ConstructReceiverAssetKey(List<int> combination, int frameGroup)
    {
        int x = 0;
        int y = 0;
        int z = 0;
        if (combination.Count >= 1)
        {
            x = combination[0];
        }
        if (combination.Count >= 2)
        {
            y = combination[1];
        }
        if (combination.Count >= 3)
        {
            z = combination[2];
        }
        Debug.Assert(combination.Count <= 3);
        return new Hash128((uint)x, (uint)y, (uint)z, (uint)frameGroup);
    }

    private static int CountBits(int flag)
    {
        int count = 0;
        while (flag > 0)
        {
            count += flag & 1;
            flag >>= 1;
        }
        return count;
    }

    private static IEnumerable<IEnumerable<T>> GetCombinations<T>(IEnumerable<T> list, int length) where T : System.IComparable
    {
        if (length == 1)
        {
            return list.Select(t => new List<T> { t }.AsEnumerable());
        }

        return GetCombinations(list, length - 1)
            .SelectMany(t => list.Where(o => o.CompareTo(t.Last()) > 0),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }

    private static List<int> GetCombinationFromLightFlag(int flag, int length, int dynamicLightCount)
    {
        List<int> result = new List<int>();
        for (int lightIndex = 0; lightIndex < dynamicLightCount; lightIndex++)
        {
            if ((flag & (1 << lightIndex)) > 0)
            {
                result.Add(lightIndex);
            }

            if (result.Count >= length)
            {
                break;
            }
        }
        Debug.Assert(result.Count == length);
        return result;
    }

    private static int GetFrameGroup(int x, int y, int z)
    {
        int offset = ((z % 2 == 0) ? 0 : 4);
        if (x % 2 == 0 && y % 2 == 0)
        {
            return 0 + offset;
        }
        else if (x % 2 == 1 && y % 2 == 0)
        {
            return 1 + offset;
        }
        else if (x % 2 == 0 && y % 2 == 1)
        {
            return 2 + offset;
        }
        else if (x % 2 == 1 && y % 2 == 1)
        {
            return 3 + offset;
        }

        Debug.Assert(false);
        return VolumetricLightmapReceiverData.DefaultFrameGroup;
    }
}
