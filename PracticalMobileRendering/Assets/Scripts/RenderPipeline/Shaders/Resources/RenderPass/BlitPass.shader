Shader "Hidden/BlitPass" {
	Properties
	{
        _SrcBlend("SrcBlend", Int) = 1
        _DstBlend("DstBlend", Int) = 0
        _BlendOp("BlendOp", Int) = 0
		_SrcTex ("Texture", any) = "" {}
	}
	
    HLSLINCLUDE	
    
	#pragma exclude_renderers d3d9 d3d11_9x

    #include "../../ShaderLib/GlobalConfig.cginc"
    #include "../../ShaderLib/ColorSpace.cginc"
    #include "../../ShaderLib/DrawQuadVS.cginc"

	#define ColorEncoding_None				0
	#define ColorEncoding_RGBM				1
	#define ColorEncoding_DepthOnly			2
	#define ColorEncoding_LinearToGamma		3
	#define ColorEncoding_GammaToLinear		4

    TEXTURE2D(_SrcTex);
    SAMPLER(sampler_SrcTex);
    float4 _SrcTex_ST;

    float4 _UvScaleOffset;
    float4 _Swizzle;
	float4 _ColorScale;
    float4 _ColorOffset;
	int _Encoding;

    ENDHLSL
	
	SubShader
	{ 
		Pass
		{
		    Name "BLIT"

 			ZTest Always
			Cull Off
			ZWrite Off
			Blend [_SrcBlend] [_DstBlend]
			BlendOp [_BlendOp]

			HLSLPROGRAM
			#pragma vertex VSFullScreenTriangleTexCoord
			#pragma fragment frag
			//#pragma enable_d3d11_debug_symbols

			half4 frag (VsOutFullScreen vsOut) : SV_Target
			{
				float2 uv = vsOut.texcoord * _UvScaleOffset.xy + _UvScaleOffset.zw;
				float4 src = SAMPLE_TEXTURE2D(_SrcTex, sampler_SrcTex, uv);

				float4 dst;
				for (int i = 0; i < 4; ++i)
				{
					dst[i] = src[(int)_Swizzle[i]];
				}
				float4 result = dst * _ColorScale + _ColorOffset;

				if (_Encoding == ColorEncoding_RGBM)
				    return encodeRGBM(result.rgb);
				else if (_Encoding == ColorEncoding_LinearToGamma)
				    return float4(LinearToGammaSpace(result.rgb), result.a);
				else if (_Encoding == ColorEncoding_GammaToLinear)
					return float4(GammaToLinearSpace(result.rgb), result.a);
				else
				    return result;
			}
			ENDHLSL 
		}
		
		Pass {
		    Name "BLIT_DEPTH"

			ZTest Always
			Cull Off
			ZWrite On
 			ColorMask 0

			HLSLPROGRAM
			#pragma vertex VSFullScreenTriangleTexCoord
			#pragma fragment frag
			//#pragma enable_d3d11_debug_symbols

			float frag (VsOutFullScreen vsOut) : SV_Depth
			{
				float2 uv = vsOut.texcoord * _UvScaleOffset.xy + _UvScaleOffset.zw;
				float4 src = SAMPLE_TEXTURE2D(_SrcTex, sampler_SrcTex, uv);

				float4 dst;
				for (int i = 0; i < 4; ++i)
				{
					dst[i] = src[(int)_Swizzle[i]];
				}
				float4 result = dst * _ColorScale + _ColorOffset;

                return result.x;
			}
			ENDHLSL 

		}
	}
	Fallback Off 
}
