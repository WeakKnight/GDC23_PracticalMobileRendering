using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DDXForUnity
{
    [Serializable]
    public struct TextureMeta
    {
        public bool isHDR;
        public bool isSRGB;
        public bool isRGBM;
        public float RGBMScale;
    }

    // Internal storage class, should not be used outside of the plugin
    [Serializable]
    public class DDXTextureData
    {
        [Serializable]
        public struct Desc
        {
            public int ArrayIndex;
            public int MipLevel;
            public int DataOffset;
        }

        public TextureDimension Dimension;

        public int Width;
        public int Height;
        public int Depth;
        public int ArraySize;
        public int NumOfMipLevels;
        
        public TextureFormat Format;

        public TextureMeta Metadata;

        public Desc[] DataLayout;
        public byte[] RawData;
    }
}