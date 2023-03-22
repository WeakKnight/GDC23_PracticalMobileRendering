using System;
using UnityEngine;

namespace PMRP
{
    public static class RuntimeUtils
    {
        public static AdditionalRendererData GetAdditionalRendererData(MeshRenderer renderer)
        {
            var result = renderer.GetComponent<AdditionalRendererData>();
            if (result == null)
            {
                result = renderer.gameObject.AddComponent<AdditionalRendererData>();
            }
            return result;
        }

        public static UInt2 UnpackUINTToR16UG14U(uint xy)
        {
            const uint mask16Bit = (1U << 16) - 1U;
            const uint mask14Bit = (1U << 14) - 1U;
            uint x = xy & mask16Bit;
            uint y = xy >> 16 & mask14Bit;
            return new UInt2(x, y);
        }

        public static Vector2 UnpackUINTToR16G14(uint xy)
        {
            const uint mask16Bit = (1u << 16) - 1u;
            const uint mask14Bit = (1u << 14) - 1u;
            uint x = xy & mask16Bit;
            uint y = xy >> 14 & mask16Bit;
            return new Vector2(x / (float)mask16Bit, y / (float)mask14Bit);
        }

        public static uint PackR16G14ToUINT(Vector2 xy)
        {
            const uint mask16Bit = (1U << 16) - 1U;
            const uint mask14Bit = (1U << 14) - 1U;
            uint xx = (uint)(xy.x * mask16Bit) & mask16Bit;
            uint yy = (uint)(xy.y * mask14Bit) >> 2 & mask16Bit;
            uint x = xx;
            uint y = yy << 16;
            return x | y;
        }

        public static uint PackR10G11B11ToUInt(Vector3Int xyz)
        {
            uint x = (uint)xyz.x << 22;
            uint y = (uint)xyz.y << 11;
            uint z = (uint)xyz.z;
            return x | y | z;
        }

        public static uint PackR16UG16UToUINT(UInt2 xy)
        {
            uint x = xy.x;
            uint y = xy.y << 16;
            return x | y;
        }

        public static float UnpackR8ToUFLOAT(uint r)
        {
            const uint mask = (1U << 8) - 1U;
            return (float)(r & mask) / (float)mask;
        }

        public static Vector4 UnpackR8G8B8A8ToUFLOAT(uint rgba)
        {
            float r = UnpackR8ToUFLOAT(rgba);
            float g = UnpackR8ToUFLOAT(rgba >> 8);
            float b = UnpackR8ToUFLOAT(rgba >> 16);
            float a = UnpackR8ToUFLOAT(rgba >> 24);
            return new Vector4(r, g, b, a);
        }

        public static Color UnpackR8G8B8A8ToColor(uint rgba)
        {
            Vector4 val = UnpackR8G8B8A8ToUFLOAT(rgba);
            return new Color(val.x, val.y, val.z, val.w);
        }

        public static uint SafelyPackColor32ToUInt(Color32 col)
        {
            uint x = col.r;
            uint y = (uint)col.g << 8;
            uint z = (uint)col.b << 16;
            uint w = (uint)col.a << 24;

            // Avoid NaN
            if ((col.a == 127 || col.a == 255) && col.b > 127)
            {
                w = (col.a - 1u) << 24;
            }

            return x | y | z | w;
        }

        public static Color DecodeRGBM(Color rgbm)
        {
            return new Color(Mathf.Pow(6.0f * rgbm.r * rgbm.a, 2.0f), Mathf.Pow(6.0f * rgbm.g * rgbm.a, 2.0f), Mathf.Pow(6.0f * rgbm.b * rgbm.a, 2.0f));
        }

        public static Color32 EncodeRGBM(Color col)
        {
            Vector3 result = new Vector3(Mathf.Sqrt(col.r) / 6.0f, Mathf.Sqrt(col.g) / 6.0f, Mathf.Sqrt(col.b) / 6.0f);
            float m = Mathf.Clamp01(Mathf.Max(Mathf.Max(result.x, result.y), result.z));
            m = Mathf.Ceil(m * 255.0f) / 255.0f;
            result = new Vector3(result.x / m, result.y / m, result.z / m);
            return new Color(result.x, result.y, result.z, m);
        }

        public static float Luminance(Color col)
        {
            return col.r * 0.2126f + col.g + 0.7152f + col.b * 0.0722f;
        }

        public static float AsFloat(uint val)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
        }

        public static uint AsUInt(float val)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
        }
    }
}
