Shader "Hidden/CubeToEquirectangular"
{
    Properties
    {
		[NoScaleOffset] _MainTex ("Texture", Cube) = "grey" {}
    }
    SubShader
    {
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float3 equirectUVToDirection(float2 uv)
			{
				float azimuth = (uv.x + 0.25) * (2 * 3.14159265);
				float elevation = (0.5 - uv.y) * 3.14159265;
				float cosElevation = cos(elevation);

				return float3(cos(azimuth) * cosElevation, sin(elevation), sin(azimuth) * cosElevation);
			}

			samplerCUBE _MainTex;
			// 0, No Encoding, 1, RGBM Encoding
			float _EncodingMode;
			float4 _EncodingFactor;
			float4 _Intensity;

			int GetEncodingMode()
			{
				return (int)_EncodingMode;
			}

			static const half k_RGBMRange = 6.0;

			half3 DecodeRGBM(half4 rgbm)
			{
				half3 color = rgbm.xyz * (rgbm.w * k_RGBMRange);
				color *= color;
				return color;
			}

			float3 DecodeHDRValue(float4 hdr)
			{
				int encodingMode = GetEncodingMode();
				if (encodingMode == 0)
				{
					return hdr;
				}
				else if (encodingMode == 1)
				{
					half alpha = _EncodingFactor.w * (hdr.a - 1.0) + 1.0;
					return (_EncodingFactor.x * pow(alpha, _EncodingFactor.y)) * hdr.rgb;
				}
				else if (encodingMode == 2)
				{
					return DecodeRGBM(hdr);
				}
				else
				{
					return hdr;
				}
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				#if UNITY_UV_STARTS_AT_TOP
				uv = float2(uv.x, 1.0f - uv.y);
				#endif

				float3 dir = equirectUVToDirection(uv);
				return float4(_Intensity.xyz * DecodeHDRValue(texCUBE(_MainTex, dir)), 1.0f);
			}
			ENDCG
		}
    }
}
