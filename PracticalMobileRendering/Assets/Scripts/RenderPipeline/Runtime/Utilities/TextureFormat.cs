using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public static class TextureFormatUtil
    {
        public static TextureFormat RenderTextureFmtToTextureFmt(RenderTextureFormat fmt)
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

        public static bool IsCompressedASTCTextureFormat(TextureFormat format)
        {
#if UNITY_2019_1_OR_NEWER
            return (format >= TextureFormat.ASTC_4x4 && format <= TextureFormat.ASTC_12x12) || (format >= TextureFormat.ASTC_HDR_4x4 && format <= TextureFormat.ASTC_HDR_12x12);
#else
            return (format >= TextureFormat.ASTC_RGB_4x4 && format <= TextureFormat.ASTC_RGBA_12x12) ||
                   (format >= TextureFormat.ASTC_RGB_3x3x3 && format <= TextureFormat.ASTC_RGBA_3x3x3);
#endif
        }
    }
}