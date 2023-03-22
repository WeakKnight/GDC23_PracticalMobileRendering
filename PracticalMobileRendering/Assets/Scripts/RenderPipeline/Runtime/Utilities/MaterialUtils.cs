using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace UnityEngine.Rendering
{
    // HACK: put it int UnityEngine.Rendering namespace otherwise ShaderGUI will not treat it as enum pop somehow
    public enum YABlendMode
    {
        Off         = 0,
        Lerp        = 1,
        AlphaAdd    = 2,
        SoftAdd     = 3,
        Add         = 4,
        Multiply    = 5,
        Premultiply = 6,

        Customization = 11
    }
}

namespace PMRP
{
    [Flags]
    public enum VertexInputAttribute
    {
        Position = 0x01 << 0,
        Normal = 0x01 << 1,
        Tangent = 0x01 << 2,
        Color = 0x01 << 3,
        TexCoord0 = 0x01 << 4,
        TexCoord1 = 0x01 << 5,
        TexCoord2 = 0x01 << 6,
        TexCoord3 = 0x01 << 7,
    }

    public static class MaterialUtils
    {
        public enum SurfaceType
        {
            Opaque,
            Transparent,
            TransparentCutout,
        }

        public const string k_PropertySrcBlend         = "_SrcBlend";
        public const string k_PropertyDstBlend         = "_DstBlend";
        public const string k_PropertyZWrite           = "_ZWrite";
        public const string k_PropertyZTestMode        = "_ZTestMode";
        public const string k_PropertyBlendMode        = "_BlendMode";
        public const string k_PropertyVisibility       = "_Visibility";
        public const string k_PropertyAlphaTest        = "_AlphaTest";
        public const string k_PropertyAlphaToMask      = "_AlphaToMask";
        public const string k_PropertyCull             = "_Cull";
        public const string k_PropertyEmissiveColor    = "_EmissiveColor";
        public const string k_PropertyEmissiveMap      = "_EmissiveMap";
        public const string k_PropertyTwoSided         = "_TwoSided";


        public static string PropertyNameToKeyword(string name)
        {
            return name.ToUpper() + "_ON";
        }

        public static void ToggleShaderKeyword(string keyword, bool toggle)
        {
            if (toggle)
                Shader.EnableKeyword(keyword);
            else
                Shader.DisableKeyword(keyword);
        }

        public static void ToggleShaderKeyword(Material mat, string keyword, bool toggle)
        {
            if (toggle)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);
        }

        public static void GetSrcDstBlend(YABlendMode mode, ref BlendMode src,ref BlendMode dst)
        {
             switch (mode)
                {
                    case YABlendMode.Off:
                        src = BlendMode.One;
                        dst = BlendMode.Zero;
                        break;

                    case YABlendMode.Lerp:
                        src = BlendMode.SrcAlpha;
                        dst = BlendMode.OneMinusSrcAlpha;
                        break;

                    case YABlendMode.Add:
                        src = BlendMode.One;
                        dst = BlendMode.One;
                        break;

                    case YABlendMode.SoftAdd:
                        src = BlendMode.OneMinusDstColor;
                        dst = BlendMode.One;
                        break;

                    case YABlendMode.Multiply:
                        src = BlendMode.DstColor;
                        dst = BlendMode.Zero;
                        break;

                    case YABlendMode.Premultiply:
                        src = BlendMode.One;
                        dst = BlendMode.OneMinusSrcAlpha;
                        break;
                }
        }

        public static bool HasProperty(Shader shader, string name)
        {
            return shader.FindPropertyIndex(name) >= 0;
        }

        public static void ToggleShaderKeyword(ComputeShader shader, string keyword, bool toggle)
        {
            if (toggle)
                shader.EnableKeyword(keyword);
            else
                shader.DisableKeyword(keyword);
        }
    }
}
