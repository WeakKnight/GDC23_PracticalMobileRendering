using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DDXForUnity
{
    public static class DDXTextureUtils
    {
#if UNITY_EDITOR
        public static DDXTextureImporter Save(DDXTextureData rawData, string assetPath)
        {
            if (!assetPath.EndsWith(".ddx"))
                assetPath = assetPath + ".ddx";

            BinaryFormatter formatter = new BinaryFormatter();
            FileStream      stream    = new FileStream(assetPath, FileMode.Create);
            formatter.Serialize(new BufferedStream(stream), rawData);
            stream.Close();

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            DDXTextureImporter importer = (DDXTextureImporter) DDXTextureImporter.GetAtPath(assetPath);
            return importer;
        }
#endif

        public static DDXTextureData Load(string assetPath)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream      stream    = new FileStream(assetPath, FileMode.Open);
            DDXTextureData  rawData   = formatter.Deserialize(new BufferedStream(stream)) as DDXTextureData;
            stream.Close();

            return rawData;
        }

        public static DDXTextureData ExportTexture(RenderTexture srcRT, bool isHDRColor, bool isRGBM, bool flipVertical = false)
        {
            int arraySize = 1;
            int mipCount  = 1;
            if (srcRT.useMipMap)
            {
                mipCount = (int)Math.Log(Math.Max(srcRT.width, srcRT.height), 2) + 1;
            }

            DDXTextureData rawData = new DDXTextureData();

            if (srcRT.dimension == TextureDimension.Tex2D)
            {
                arraySize = 1;

                rawData.Dimension = TextureDimension.Tex2D;

                rawData.Width = srcRT.width;
                rawData.Height = srcRT.height;
                rawData.ArraySize = arraySize;
                rawData.NumOfMipLevels = mipCount;

                rawData.Format   = RenderTextureFmtToTextureFmt(srcRT.format);
            }
            else if (srcRT.dimension == TextureDimension.Tex2DArray)
            {
                arraySize = srcRT.volumeDepth;

                rawData.Dimension = TextureDimension.Tex2DArray;

                rawData.Width          = srcRT.width;
                rawData.Height         = srcRT.height;
                rawData.ArraySize      = arraySize;
                rawData.NumOfMipLevels = mipCount;

                rawData.Format = RenderTextureFmtToTextureFmt(srcRT.format);
            }
            else if (srcRT.dimension == TextureDimension.Cube)
            {
                arraySize = 6;

                rawData.Dimension = TextureDimension.Cube;

                rawData.Width = srcRT.width;
                rawData.Height = srcRT.height;
                rawData.ArraySize = arraySize;
                rawData.NumOfMipLevels = mipCount;

                rawData.Format = RenderTextureFmtToTextureFmt(srcRT.format);
            }
            else
            {
                Debug.Assert(false, "Texture Dimension not supported");
                return null;
            }

            byte[][] imagesData = new byte[arraySize * mipCount][];
            int totalSizeInByte = 0;
            for (int arrayIdx = 0; arrayIdx < arraySize; ++arrayIdx)
            {
                for (int mipLevel = 0; mipLevel < mipCount; ++mipLevel)
                {
                    Texture2D uncompressedTexture = GetPixelsData(srcRT, arrayIdx, mipLevel, flipVertical);

                    int elemIdx = arrayIdx * mipCount + mipLevel;
                    imagesData[elemIdx] = uncompressedTexture.GetRawTextureData();

                    totalSizeInByte += imagesData[elemIdx].Length;
                }
            }

            rawData.DataLayout = new DDXTextureData.Desc[arraySize * mipCount];
            rawData.RawData = new byte[totalSizeInByte];

            int dataOffset = 0;
            for (int arrayIdx = 0; arrayIdx < arraySize; ++arrayIdx)
            {
                for (int mipLevel = 0; mipLevel < mipCount; ++mipLevel)
                {
                    int elemIdx = arrayIdx * mipCount + mipLevel;
                    rawData.DataLayout[elemIdx].ArrayIndex = arrayIdx;
                    rawData.DataLayout[elemIdx].MipLevel   = mipLevel;
                    rawData.DataLayout[elemIdx].DataOffset = dataOffset;

                    imagesData[elemIdx].CopyTo(rawData.RawData, dataOffset);
                    dataOffset += imagesData[elemIdx].Length;
                }
            }

            rawData.Metadata        = new TextureMeta();
            rawData.Metadata.isHDR  = isHDRColor;
            rawData.Metadata.isSRGB = srcRT.sRGB;
            rawData.Metadata.isRGBM = isRGBM;
            if (rawData.Metadata.isRGBM)
            {
                rawData.Metadata.isSRGB = true;
                rawData.Metadata.isHDR= false;
            }
            rawData.Metadata.RGBMScale = 36;

            return rawData;
        }

        public static Texture ImportTexture(DDXTextureData rawData, bool cpuReadable = false)
        {
            if (rawData.Dimension == TextureDimension.Tex2D)
            {
                Texture2D tex = new Texture2D(rawData.Width, rawData.Height, rawData.Format, rawData.NumOfMipLevels > 1, !rawData.Metadata.isSRGB);
                foreach (var layout in rawData.DataLayout)
                {
                    tex.SetPixelData(rawData.RawData, layout.MipLevel, layout.DataOffset);
                }
                tex.Apply(false, !cpuReadable);
                return tex;
            }
            else if (rawData.Dimension == TextureDimension.Tex2DArray)
            {
                Texture2DArray tex = new Texture2DArray(rawData.Width, rawData.Height, rawData.ArraySize, rawData.Format, rawData.NumOfMipLevels > 1, !rawData.Metadata.isSRGB);
                foreach (var layout in rawData.DataLayout)
                {
                    tex.SetPixelData(rawData.RawData, layout.MipLevel, layout.ArrayIndex, layout.DataOffset);
                }
                tex.Apply(false, !cpuReadable);
                return tex;
            }
            else if (rawData.Dimension == TextureDimension.Cube)
            {
                Cubemap tex = new Cubemap(rawData.Width, rawData.Format, rawData.NumOfMipLevels > 1);
                foreach (var layout in rawData.DataLayout)
                {
                    tex.SetPixelData(rawData.RawData, layout.MipLevel, (CubemapFace)layout.ArrayIndex, layout.DataOffset);
                }
                tex.Apply(false, !cpuReadable);
                return tex;
            }
            else if (rawData.Dimension == TextureDimension.Tex3D)
            {
                Texture3D tex = new Texture3D(rawData.Width, rawData.Height, rawData.Depth, rawData.Format,
                    rawData.NumOfMipLevels > 1);
                foreach (var layout in rawData.DataLayout)
                {
                    tex.SetPixelData(rawData.RawData, layout.MipLevel, layout.DataOffset);
                }
                tex.Apply(false, !cpuReadable);
                return tex;
            }
            else
            {
                Debug.Assert(false, "Texture dimension not supported!!!");
            }
            return null;
        }

        private static TextureFormat RenderTextureFmtToTextureFmt(RenderTextureFormat fmt)
        {
            switch (fmt)
            {
                case RenderTextureFormat.ARGB32:    return TextureFormat.ARGB32;
                case RenderTextureFormat.Depth:     return TextureFormat.RFloat;
                case RenderTextureFormat.ARGBHalf:  return TextureFormat.RGBAHalf;
                case RenderTextureFormat.Shadowmap: return TextureFormat.RFloat;
                case RenderTextureFormat.RGB565:    return TextureFormat.RGB565;
                case RenderTextureFormat.ARGB4444:  return TextureFormat.ARGB4444;
                case RenderTextureFormat.ARGBFloat: return TextureFormat.RGBAFloat;
                case RenderTextureFormat.RGFloat:   return TextureFormat.RGFloat;
                case RenderTextureFormat.RGHalf:    return TextureFormat.RGHalf;
                case RenderTextureFormat.RFloat:    return TextureFormat.RFloat;
                case RenderTextureFormat.RHalf:     return TextureFormat.RHalf;
                case RenderTextureFormat.R8:        return TextureFormat.R8;
                case RenderTextureFormat.BGRA32:    return TextureFormat.BGRA32;
                case RenderTextureFormat.RGB111110Float:
                case RenderTextureFormat.RG32:
                case RenderTextureFormat.RGBAUShort:
                case RenderTextureFormat.RG16:
                case RenderTextureFormat.ARGB1555:
                case RenderTextureFormat.ARGB2101010:
                case RenderTextureFormat.DefaultHDR:
                case RenderTextureFormat.ARGB64:
                case RenderTextureFormat.ARGBInt:
                case RenderTextureFormat.RGInt:
                case RenderTextureFormat.RInt:
                case RenderTextureFormat.Default:
                default:
                    Debug.Assert(false);
                    return TextureFormat.RGBA32;
            }
        }

        private static Texture2D GetPixelsData(RenderTexture rt, int elementIdx, int mipLevel, bool flipVertical)
        {
            Debug.Assert(rt.dimension == TextureDimension.Tex2D ||
                         rt.dimension == TextureDimension.Tex2DArray ||
                         rt.dimension == TextureDimension.Cube);

            int width  = rt.width  >> mipLevel;
            int height = rt.height >> mipLevel;

            RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, rt.format, rt.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            tempRT.Create();

            Graphics.CopyTexture(rt, elementIdx, mipLevel, tempRT, 0, 0);

            RenderTexture.active = tempRT;
            Texture2D destTexture = new Texture2D(width, height, RenderTextureFmtToTextureFmt(rt.format), false);
            destTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(tempRT);

            if (!flipVertical)
            {
                return destTexture;
            }
            else
            {
                Texture2D flippedTexture = new Texture2D(width, height, destTexture.format, false);
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        flippedTexture.SetPixel(x, height - y - 1, destTexture.GetPixel(x, y));
                    }
                }
                flippedTexture.Apply(false, false);
                return flippedTexture;
            }
        }
    }
}