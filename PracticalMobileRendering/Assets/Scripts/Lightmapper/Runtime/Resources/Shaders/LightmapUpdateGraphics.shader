Shader "PMRP/LightmapUpdate"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            //#pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            
            float UnpackR8ToUFLOAT(uint r)
            {
                const uint mask = (1U << 8) - 1U;
                return (float)(r & mask) / (float)mask;
            }

            float4 UnpackR8G8B8A8ToUFLOAT(uint rgba)
            {
                float r = UnpackR8ToUFLOAT(rgba);
                float g = UnpackR8ToUFLOAT(rgba >> 8);
                float b = UnpackR8ToUFLOAT(rgba >> 16);
                float a = UnpackR8ToUFLOAT(rgba >> 24);
                return float4(r, g, b, a);
            }

            float2 UnpackUINTToR16G14(uint xy)
            {
                const uint mask16Bit = (1u << 16u) - 1u;
                const uint mask14Bit = (1u << 14u) - 1u;
                uint x = xy & mask16Bit;
                uint y = xy >> 14 & mask16Bit;
                return float2(x / (float)mask16Bit, y / (float)mask14Bit);
            }

            float3 RGBMDecode(float4 rgbm)
            {
                float3 result = 6.0f * rgbm.rgb * rgbm.a;
                return result * result;
            }

            struct ReceiverVertex
            {
                float3 xyz : POSITION;
                fixed4 col : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD;
                float3 dstIrradiance0 : COLOR0;
                float3 dstIrradiance1 : COLOR1;
                float3 dstIrradiance2 : COLOR2;
            };

            float _Intensity0;
            float _Intensity1;
            float _Intensity2;

            sampler2D _SrcLightmap;

            v2f vert (ReceiverVertex v)
            {
                float2 xy = UnpackUINTToR16G14(asuint(v.xyz.x));
                v2f o;
                o.vertex = float4(xy * 2.0f - 1.0f, 0.0f, 1.0f);
#if !UNITY_UV_STARTS_AT_TOP
                o.vertex.y = -o.vertex.y;
#endif
                o.uv = xy;
                o.dstIrradiance0 = RGBMDecode(UnpackR8G8B8A8ToUFLOAT(asuint(v.xyz.y)));
                o.dstIrradiance1 = RGBMDecode(UnpackR8G8B8A8ToUFLOAT(asuint(v.xyz.z)));
                o.dstIrradiance2 = RGBMDecode(v.col);
                return o;
            }

            float3 frag (v2f i) : SV_Target
            {
                float3 currentVal = tex2D(_SrcLightmap, float2(i.uv.x, 1.0f - i.uv.y)).xyz;
                float3 newVal = currentVal + _Intensity0 * i.dstIrradiance0 + _Intensity1 * i.dstIrradiance1 + _Intensity2 * i.dstIrradiance2;
                return newVal;
            }

            ENDCG
        }
    }
}
