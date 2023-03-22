using System;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    [Serializable]
    public class RectPacker
    {
        [Serializable]
        public class Item
        {
            /* 
             * input: 
             */
            /// <summary>
            /// unique id
            /// </summary>
            public int id;
            public Vector2Int size;
            public int minResolution = 0;

            /* 
             * output: 
             */
            /// <summary>
            /// Rect Container Index
            /// </summary>
            public int index;
            public Vector4 scaleAndOffset;
        };

        [Serializable]
        public class RectInt
        {
            public int X;
            public int Y;
            public int W;
            public int H;

            public RectInt() { }

            public RectInt(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        };

        [Serializable]
        public class Segment
        {
            public int StartPos;
            public int Length;

            public Segment()
            { }

            public Segment(int vStartPos, int vLength)
            {
                StartPos = vStartPos;
                Length = vLength;
            }
        };

        [Serializable]
        public class Row
        {
            public int Index;
            public int LongestSegment; // Represents either the longest free segment or the longest used segment, depending on how we're using this row

            public List<Segment> FreeSegments = new List<Segment>();
            public List<Segment> UsedSegments = new List<Segment>();
        };

        [Serializable]
        public class RectLayout
        {
            public RectLayout(int w, int h)
            {
                Init(w, h);
            }

            public RectLayout(Vector2Int size)
            {
                Init(size.x, size.y);
            }

            private void Init(int w, int h)
            {
                Width = w;
                Height = h;

                MinEmptyRect = new RectInt();
                MinEmptyRect.W = w;
                MinEmptyRect.H = h;

                Rows.Capacity = Height;

                for (int Y = 0; Y < Height; Y++)
                {
                    Row row = new Row();
                    row.Index = Y;

                    Rows.Add(row);
                }

                Segment freeSegment = new Segment();
                freeSegment.StartPos = 0;
                freeSegment.Length = Width;

                foreach (Row row in Rows)
                {
                    row.FreeSegments.Clear();
                    row.FreeSegments.Add(freeSegment);
                    row.LongestSegment = freeSegment.Length;

                    row.UsedSegments.Clear();
                }
            }

            public void FillRect(RectInt rect)   //TODO: move to Layout
            {
                int Top = rect.Y + rect.H;
                for (int RowIndex = rect.Y; RowIndex < Top; ++RowIndex)
                {
                    var FreeSegments = Rows[RowIndex].FreeSegments;
                    var UsedSegments = Rows[RowIndex].UsedSegments;

                    for (int i = 0; i < FreeSegments.Count; ++i)
                    {
                        var Segment = FreeSegments[i];
                        if (Segment.StartPos <= rect.X && Segment.StartPos + Segment.Length > rect.X && Segment.Length >= rect.W)
                        {
                            FreeSegments.RemoveAt(i);
                            UsedSegments.Add(new Segment(rect.X, rect.W));
                            if (rect.X - Segment.StartPos > 0)
                            {
                                FreeSegments.Add(new Segment(Segment.StartPos, rect.X - Segment.StartPos));
                            }
                            if (rect.X + rect.W < Segment.StartPos + Segment.Length)
                            {
                                FreeSegments.Add(new Segment(rect.X + rect.W, Segment.StartPos + Segment.Length - (rect.X + rect.W)));
                            }

                            break;
                        }
                    }

                    // Merge used segments
                    UsedSegments.Sort((Segment segA, Segment segB) => segA.StartPos.CompareTo(segB.StartPos));
                    for (int i = UsedSegments.Count - 1; i >= 1; --i)
                    {
                        if (UsedSegments[i - 1].StartPos + UsedSegments[i - 1].Length == UsedSegments[i].StartPos)
                        {
                            UsedSegments[i - 1].Length += UsedSegments[i].Length;
                            UsedSegments.RemoveAt(i);
                        }
                    }
                }
            }

            public List<Row> Rows = new List<Row>();
            public RectInt MinEmptyRect;
            public int Width;
            public int Height;
        }

        public int GetLightmapCount()
        {
            return layouts.Count;
        }

        public void ForcePackIntoSingleContainer(Vector2Int containerSize, int padding, List<Item> items)
        {
            float containerArea = containerSize.x * containerSize.y;
            float totalArea = 0.0f;
            Dictionary<Item, Vector2Int> initialSizeMap = new();
            foreach (var item in items)
            {
                initialSizeMap[item] = item.size;
                totalArea += (item.size.x * item.size.y);
            }

            float left = 0.0f;
            float right = containerArea / totalArea;
            const int iterNum = 32;
            for (int i = 0; i < iterNum; i++)
            {
                float mid = Mathf.Lerp(right, left, i / (iterNum - 1.0f));

                layouts.Clear();

                foreach (var item in items.ToArray())
                {
                    Vector2Int initialItemSize = initialSizeMap[item];
                    item.size = new Vector2Int((int)(Mathf.Clamp(mid * initialItemSize.x, Mathf.Max(8.0f, item.minResolution), containerSize.x)), (int)(Mathf.Clamp(mid * initialItemSize.y, Mathf.Max(8.0f, item.minResolution), containerSize.y)));
                    item.index = 0;
                }
                Pack(containerSize, padding, items);

                if (layouts.Count <= 1)
                {
                    return;
                }
            }

            if (layouts.Count > 1)
            {
                Debug.LogError("Lightmap Size is not big enough");
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="containerSize"></param>
        /// <param name="padding"></param>
        /// <param name="items"></param>
        /// <returns>Container Count</returns>
        public void Pack(Vector2Int containerSize, int padding, List<Item> items)
        {
            int lastLayoutIndex = 0;
            int lastRowIndex = 0;

            if (layouts.Count <= 0)
            {
                layouts.Add(new RectLayout(containerSize));
            }

            items.Sort((Item a, Item b) =>
            {
                if (a.size.x * a.size.y > b.size.x * b.size.y)
                {
                    return 1;
                }
                else if ((a.size.x * a.size.y == b.size.x * b.size.y))
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            });

            var rectPadding = new Vector2Int();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.size.x > containerSize.x || item.size.y > containerSize.y)
                {
                    Debug.LogError("Skip items which exceed the size of the container");
                    continue;
                }
                bool isFound = false;
                var dataRect = new RectInt();
                rectPadding.x = 2 * padding;
                rectPadding.y = 2 * padding;

                dataRect.W = item.size.x + rectPadding.x;
                dataRect.H = item.size.y + rectPadding.y;
                if (dataRect.W > containerSize.x)
                {
                    dataRect.W = containerSize.x;
                    rectPadding.x = dataRect.W - containerSize.x;
                }

                if (dataRect.H > containerSize.y)
                {
                    dataRect.H = containerSize.y;
                    rectPadding.y = dataRect.H - containerSize.y;
                }

                for (int k = lastLayoutIndex; k < layouts.Count; ++k)
                {
                    if (FindFreeRect(dataRect, layouts[k], k, ref lastRowIndex, ref lastLayoutIndex))
                    {
                        isFound = true;
                        item.index = k;
                        break;
                    }
                }
                if (!isFound)
                {
                    var newLayout = new RectLayout(containerSize);
                    FindFreeRect(dataRect, newLayout, layouts.Count - 1, ref lastRowIndex, ref lastLayoutIndex);
                    layouts.Add(newLayout);
                    item.index = layouts.Count - 1;
                }
                item.scaleAndOffset = new Vector4((float)(item.size.x) / containerSize.x, (float)(item.size.y) / containerSize.y,
                  (float)(dataRect.X + rectPadding.x * 0.5) / containerSize.x, (float)(dataRect.Y + rectPadding.y * 0.5) / containerSize.y);
            }
        }

        private static bool FindFreeRect(RectInt rect, RectLayout layout, int layoutIndex, ref int lastRowIndex, ref int lastLayoutIndex)
        {
            if (rect.W > layout.MinEmptyRect.W || rect.H > layout.MinEmptyRect.H)
                return false;

            var TmpRect = new RectInt();
            for (int RowIndex = 0; RowIndex <= layout.Height - rect.H; ++RowIndex)  //TODO: if we pack from larger to smaller, its unnecessary to start with 0
            {
                Row row = layout.Rows[RowIndex];
                for (int i = 0; i < row.FreeSegments.Count; ++i)
                {
                    if (row.FreeSegments[i].Length >= rect.W)
                    {
                        TmpRect.X = row.FreeSegments[i].StartPos;
                        TmpRect.Y = RowIndex;
                        TmpRect.W = rect.W;
                        TmpRect.H = rect.H;
                        if (IsFreeRect(TmpRect, layout))
                        {
                            FillRect(TmpRect, layout);
                            rect.X = TmpRect.X;
                            rect.Y = TmpRect.Y;
                            lastRowIndex = RowIndex;
                            lastLayoutIndex = layoutIndex;
                            return true;
                        }
                    }
                }
            }

            layout.MinEmptyRect.W = rect.W;
            layout.MinEmptyRect.H = rect.H;
            return false;
        }

        private static bool IsFreeRect(RectInt rect, RectLayout layout)
        {
            int Top = rect.Y + rect.H;
            for (int RowIndex = rect.Y; RowIndex < Top; ++RowIndex)
            {
                bool IsFindFreeSegment = false;
                var FreeSegmets = layout.Rows[RowIndex].FreeSegments;
                for (int i = 0; i < FreeSegmets.Count; ++i)
                {
                    if (FreeSegmets[i].StartPos <= rect.X && FreeSegmets[i].StartPos + FreeSegmets[i].Length > rect.X && FreeSegmets[i].Length >= rect.W)
                    {
                        IsFindFreeSegment = true;
                        break;
                    }
                }
                if (!IsFindFreeSegment)
                {
                    return false;
                }
            }
            return true;
        }

        private static void FillRect(RectInt rect, RectLayout layout)   //TODO: move to Layout
        {
            layout.FillRect(rect);
        }

        private readonly List<RectLayout> layouts = new List<RectLayout>();
    }
}
