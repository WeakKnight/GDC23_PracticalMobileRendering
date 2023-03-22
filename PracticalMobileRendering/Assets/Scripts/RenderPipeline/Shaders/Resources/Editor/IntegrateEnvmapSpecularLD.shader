Shader "Hidden/IntegrateEnvmapSpecularLD"
{
    Properties
    {
        _MainTex ("Texture", any) = "" {}
        _SampleCount("Sample count", Range(64, 2048)) = 2048
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma only_renderers d3d11
            //#pragma enable_d3d11_debug_symbols

            #include "../../ShaderLib/GlobalConfig.cginc"
            #include "../../ShaderLib/ShaderUtilities.cginc"
            #include "../../ShaderLib/ImageBasedLighting.cginc"
            #include "../../ShaderLib/CubemapUtilities.cginc"


            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            uniform float4 _MainTex_ST;
            uniform float4 _MainTex_Dimensions;

            uniform float _MipmapLevel;
            uniform uint _SampleCount;

            uniform int _MIS;
            Texture2D<float> _ImportanceMap;

            uniform int _RenderToCubeface;
            uniform int _CubefaceIdx;
            uniform int _CubefaceSize;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir;
                if (_RenderToCubeface)
                {
                    // HACK: special handling for unity Texture coordinate
                    dir = TexelCoordToVect(_CubefaceIdx,
                                           floor(i.texcoord.x * _CubefaceSize),
                                           floor((1 - i.texcoord.y) * _CubefaceSize),
                                           _CubefaceSize,
                                           CP_FIXUP_NONE);
                }
                else
                {
                    dir = sphericalCrdToDir(i.texcoord);
                }

                float linearRoughness = mipmapLevelToLinearRoughness(_MipmapLevel);
                if (linearRoughness < 0.01)
                {
					// perfect mirror liked reflection	                
                    float2 uv = dirToSphericalCrd(dir);
					float3 Li = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
					return encodeIBLColor(Li);
                }

                float alpha = linearRoughnessToAlpha(linearRoughness);
                if (_MIS)
                    return integrateSpecularLdMIS(EnvironmentLight_(_MainTex, _ImportanceMap), _SampleCount, dir, dir, alpha);
                else
                    return integrateSpecularLd(_MainTex, sampler_MainTex, _MainTex_Dimensions.xyz, _SampleCount, dir, dir, alpha);
            }
            ENDHLSL

        }
    }
    Fallback Off
}
