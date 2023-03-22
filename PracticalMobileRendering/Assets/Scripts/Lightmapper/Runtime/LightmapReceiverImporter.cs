using PMRP;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace PMRP
{
    public static class LightmapReceiverImporter
    {
        public class Context
        {
            public Context(ReceiverAsset pReceiverAsset, int pDynamicBakingSettingCount, int pLightmapWidth, int pLightmapHeight, bool pSplitIntoFrameGroups = true)
            {
                receiverAsset = pReceiverAsset;
                dynamicBakingSettingCount = pDynamicBakingSettingCount;
                lightmapWidth = pLightmapWidth;
                lightmapHeight = pLightmapHeight;
                splitIntoFrameGroups = pSplitIntoFrameGroups;

                usageMap = new int[lightmapWidth * lightmapHeight];
            }

            public ReceiverAsset receiverAsset;
            public int dynamicBakingSettingCount;
            public int lightmapWidth;
            public int lightmapHeight;
            public bool splitIntoFrameGroups;

            public int[] usageMap;
        }

        public class Result
        {
            public Result()
            {
                oneLightData = new List<LightmapReceiverData>();
                twoLightData = new List<LightmapReceiverData>();
                threeLightData = new List<LightmapReceiverData>();
            }

            public List<LightmapReceiverData> oneLightData;
            public List<LightmapReceiverData> twoLightData;
            public List<LightmapReceiverData> threeLightData;
        }

        public static Result ImportLightmapReceiverMeshes(Context context)
        {
            void CollectReceiverUsage()
            {
                for (int lightIndex = 0; lightIndex < context.receiverAsset.offsets.Count; lightIndex++)
                {
                    int receiverOffset = (int)context.receiverAsset.offsets[lightIndex].x;
                    int receiverCount = (int)context.receiverAsset.offsets[lightIndex].y;

                    for (int receiverIndex = 0; receiverIndex < receiverCount; receiverIndex++)
                    {
                        Receiver receiver = context.receiverAsset.receivers[receiverOffset + receiverIndex];
                        int x = (int)receiver.posX;
                        int y = (int)receiver.posY;

                        if (receiver.irradiance.r <= 0 && receiver.irradiance.g <= 0 && receiver.irradiance.b <= 0)
                        {
                            continue;
                        }

                        int locationKey = x + y * context.lightmapWidth;
                        context.usageMap[locationKey] = context.usageMap[locationKey] | (1 << lightIndex);
                    }
                }
            }
            CollectReceiverUsage();

            Result CreateReceiverData()
            {
                IEnumerable<IEnumerable<T>> GetCombinations<T>(IEnumerable<T> list, int length) where T : System.IComparable
                {
                    if (length == 1)
                    {
                        return list.Select(t => new List<T> { t }.AsEnumerable());
                    }

                    return GetCombinations(list, length - 1)
                        .SelectMany(t => list.Where(o => o.CompareTo(t.Last()) > 0),
                            (t1, t2) => t1.Concat(new T[] { t2 }));
                }

                Hash128 ConstructReceiverMeshKey(List<int> combination, int frameGroup)
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
                    return new Hash128((uint)x, (uint)y, (uint)z, (uint)frameGroup);
                }

                int GetFrameGroup(int x, int y, int z = 0)
                {
                    int offset = z == 0 ? 0 : 4;

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

                    return LightmapReceiverData.DefaultFrameGroup;
                }

                int CountBits(int flag)
                {
                    int count = 0;
                    while (flag > 0)
                    {
                        count += flag & 1;
                        flag >>= 1;
                    }
                    return count;
                }

                List<int> GetCombinationFromUsageFlag(int flag, int length)
                {
                    List<int> result = new List<int>();
                    for (int lightIndex = 0; lightIndex < context.dynamicBakingSettingCount; lightIndex++)
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

                Result result = new Result();

                List<int> lightIndexEnumeration = new List<int>();
                for (int i = 0; i < context.dynamicBakingSettingCount; i++)
                {
                    lightIndexEnumeration.Add(i);
                }

                Dictionary<Hash128, LightmapReceiverData> oneLightReceiverMeshes = new Dictionary<Hash128, LightmapReceiverData>();
                Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>> oneLightReceiverVertices = new Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>>();
                List<IEnumerable<int>> oneLightPossibleCombinations;
                if (context.dynamicBakingSettingCount >= 1)
                {
                    oneLightPossibleCombinations = GetCombinations<int>(lightIndexEnumeration, 1).ToList();
                }
                else
                {
                    oneLightPossibleCombinations = new List<IEnumerable<int>>();
                }

                Dictionary<Hash128, LightmapReceiverData> twoLightReceiverMeshes = new Dictionary<Hash128, LightmapReceiverData>();
                Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>> twoLightReceiverVertices = new Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>>();
                List<IEnumerable<int>> twoLightPossibleCombinations;
                if (context.dynamicBakingSettingCount >= 2)
                {
                    twoLightPossibleCombinations = GetCombinations<int>(lightIndexEnumeration, 2).ToList();
                }
                else
                {
                    twoLightPossibleCombinations = new List<IEnumerable<int>>();
                }

                Dictionary<Hash128, LightmapReceiverData> threeLightReceiverMeshes = new Dictionary<Hash128, LightmapReceiverData>();
                Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>> threeLightReceiverVertices = new Dictionary<Hash128, Dictionary<int, PackedLightmapReceiver>>();
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
                    frameGroups = new List<int> { 0, 1, 2, 3 };
                }
                else
                {
                    frameGroups = new List<int> { LightmapReceiverData.DefaultFrameGroup };
                }

                for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
                {
                    int frameGroup = frameGroups[groupIndex];
                    for (int i = 0; i < oneLightPossibleCombinations.Count; i++)
                    {
                        List<int> combination = oneLightPossibleCombinations[i].ToList();
                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        oneLightReceiverMeshes[receiverMeshKey] = new LightmapReceiverData();
                        oneLightReceiverMeshes[receiverMeshKey].lightIndices = combination;
                        oneLightReceiverVertices[receiverMeshKey] = new Dictionary<int, PackedLightmapReceiver>();
                    }

                    for (int i = 0; i < twoLightPossibleCombinations.Count; i++)
                    {
                        List<int> combination = twoLightPossibleCombinations[i].ToList();
                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        twoLightReceiverMeshes[receiverMeshKey] = new LightmapReceiverData();
                        twoLightReceiverMeshes[receiverMeshKey].lightIndices = combination;
                        twoLightReceiverVertices[receiverMeshKey] = new Dictionary<int, PackedLightmapReceiver>();
                    }

                    for (int i = 0; i < threeLightPossibleCombinations.Count; i++)
                    {
                        List<int> combination = threeLightPossibleCombinations[i].ToList();
                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        threeLightReceiverMeshes[receiverMeshKey] = new LightmapReceiverData();
                        threeLightReceiverMeshes[receiverMeshKey].lightIndices = combination;
                        threeLightReceiverVertices[receiverMeshKey] = new Dictionary<int, PackedLightmapReceiver>();
                    }
                }

                // Collect Vertices
                for (int lightIndex = 0; lightIndex < context.receiverAsset.offsets.Count; lightIndex++)
                {
                    int receiverOffset = context.receiverAsset.offsets[lightIndex].x;
                    int receiverCount = context.receiverAsset.offsets[lightIndex].y;
                    for (int receiverIndex = 0; receiverIndex < receiverCount; receiverIndex++)
                    {
                        Receiver receiver = context.receiverAsset.receivers[receiverOffset + receiverIndex];
                        int x = (int)receiver.posX;
                        int y = (int)receiver.posY;

                        if (receiver.irradiance.r <= 0 && receiver.irradiance.g <= 0 && receiver.irradiance.b <= 0)
                        {
                            continue;
                        }

                        int frameGroup = context.splitIntoFrameGroups ? GetFrameGroup(x, y) : LightmapReceiverData.DefaultFrameGroup;

                        int locationKey = x + y * context.lightmapWidth;
                        if (context.usageMap[locationKey] > 0)
                        {
                            int usageFlag = context.usageMap[locationKey];
                            int bitCount = CountBits(usageFlag);
                            if (bitCount == 1)
                            {
                                List<int> combination;
                                Hash128 receiverMeshKey;
                                combination = GetCombinationFromUsageFlag(usageFlag, 1);
                                receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);

                                if (!oneLightReceiverVertices[receiverMeshKey].ContainsKey(locationKey))
                                {
                                    oneLightReceiverVertices[receiverMeshKey][locationKey] = new PackedLightmapReceiver(x, y, context.lightmapWidth, context.lightmapHeight);
                                }

                                int irradianceIndex = combination.IndexOf(lightIndex);
                                PackedLightmapReceiver localReceiver = oneLightReceiverVertices[receiverMeshKey][locationKey];
                                localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                                oneLightReceiverVertices[receiverMeshKey][locationKey] = localReceiver;
                            }
                            else if (bitCount == 2)
                            {
                                List<int> combination;
                                Hash128 receiverMeshKey;
                                combination = GetCombinationFromUsageFlag(usageFlag, 2);
                                receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);

                                if (!twoLightReceiverVertices[receiverMeshKey].ContainsKey(locationKey))
                                {
                                    twoLightReceiverVertices[receiverMeshKey][locationKey] = new PackedLightmapReceiver(x, y, context.lightmapWidth, context.lightmapHeight);
                                }

                                int irradianceIndex = combination.IndexOf(lightIndex);
                                PackedLightmapReceiver localReceiver = twoLightReceiverVertices[receiverMeshKey][locationKey];
                                localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                                twoLightReceiverVertices[receiverMeshKey][locationKey] = localReceiver;
                            }
                            else if (bitCount == 3)
                            {
                                List<int> combination;
                                Hash128 receiverMeshKey;
                                combination = GetCombinationFromUsageFlag(usageFlag, 3);
                                receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);

                                if (!threeLightReceiverVertices[receiverMeshKey].ContainsKey(locationKey))
                                {
                                    threeLightReceiverVertices[receiverMeshKey][locationKey] = new PackedLightmapReceiver(x, y, context.lightmapWidth, context.lightmapHeight);
                                }

                                int irradianceIndex = combination.IndexOf(lightIndex);
                                PackedLightmapReceiver localReceiver = threeLightReceiverVertices[receiverMeshKey][locationKey];
                                localReceiver.SetIrradiance(irradianceIndex, receiver.irradiance);
                                threeLightReceiverVertices[receiverMeshKey][locationKey] = localReceiver;
                            }
                        }
                    }
                }

                for (int i = 0; i < oneLightPossibleCombinations.Count; i++)
                {
                    List<int> combination = oneLightPossibleCombinations[i].ToList();
                    List<PackedLightmapReceiver> oneLightVertices = new List<PackedLightmapReceiver>();
                    for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
                    {
                        int frameGroup = frameGroups[groupIndex];

                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        var vertices = oneLightReceiverVertices[receiverMeshKey].Values.ToList();
                        if (vertices.Count <= 0)
                        {
                            continue;
                        }

                        var receiverMesh = new LightmapReceiverData();
                        receiverMesh.lightIndices = new List<int>(combination);
                        receiverMesh.receivers = vertices;
                        receiverMesh.frameGroup = frameGroup;
                        oneLightReceiverMeshes[receiverMeshKey] = receiverMesh;

                        oneLightVertices.AddRange(vertices);
                    }
                }
                result.oneLightData = oneLightReceiverMeshes.Values.ToList();

                for (int i = 0; i < twoLightPossibleCombinations.Count; i++)
                {
                    List<int> combination = twoLightPossibleCombinations[i].ToList();
                    List<PackedLightmapReceiver> twoLightVertices = new List<PackedLightmapReceiver>();

                    for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
                    {
                        int frameGroup = frameGroups[groupIndex];

                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        var vertices = twoLightReceiverVertices[receiverMeshKey].Values.ToList();
                        if (vertices.Count <= 0)
                        {
                            continue;
                        }

                        var receiverMesh = new LightmapReceiverData();
                        receiverMesh.lightIndices = new List<int>(combination);
                        receiverMesh.receivers = vertices;
                        receiverMesh.frameGroup = frameGroup;
                        twoLightReceiverMeshes[receiverMeshKey] = receiverMesh;

                        twoLightVertices.AddRange(vertices);
                    }
                }
                result.twoLightData = twoLightReceiverMeshes.Values.ToList();

                for (int i = 0; i < threeLightPossibleCombinations.Count; i++)
                {
                    List<int> combination = threeLightPossibleCombinations[i].ToList();
                    List<PackedLightmapReceiver> threeLightVertices = new List<PackedLightmapReceiver>();

                    for (int groupIndex = 0; groupIndex < frameGroups.Count; groupIndex++)
                    {
                        int frameGroup = frameGroups[groupIndex];

                        Hash128 receiverMeshKey = ConstructReceiverMeshKey(combination, frameGroup);
                        var vertices = threeLightReceiverVertices[receiverMeshKey].Values.ToList();
                        if (vertices.Count <= 0)
                        {
                            continue;
                        }

                        var receiverMesh = new LightmapReceiverData();
                        receiverMesh.lightIndices = new List<int>(combination);
                        receiverMesh.receivers = vertices;
                        receiverMesh.frameGroup = frameGroup;
                        threeLightReceiverMeshes[receiverMeshKey] = receiverMesh;

                        threeLightVertices.AddRange(vertices);
                    }
                }
                result.threeLightData = threeLightReceiverMeshes.Values.ToList();

                return result;
            }

            return CreateReceiverData();
        }
    }
}