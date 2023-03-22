Shader "Hidden/Skydome"
{
    Properties
    {
    }
    SubShader {
        Pass {
            ZTest LEqual Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex VSFullScreenTriangleTexCoord
            #pragma fragment frag
            //#pragma enable_d3d11_debug_symbols

            #include "../../ShaderLib/GlobalConfig.cginc"
			#include "../../ShadingSystem/GlobalVariables.cginc"
			#include "../../ShadingSystem/ColorOutput.cginc"
            #include "../../ShaderLib/ShaderUtilities.cginc"
            #include "../../ShaderLib/DrawQuadVS.cginc"
            #include "../../ShaderLib/ColorSpace.cginc"

            TEXTURE2D(_Envmap);
            SAMPLER(sampler_Envmap);

            float4x4 _InvViewProjMatrix;
            float3 _CameraPositionW;
            float3 _Intensity;
            float2 _OffsetUV;

            half3 _FogColor_F;
            half3 _FogColor_H;
            half4 _SunColor;
			half3 _CamForward;
            
            float _ScatteringExponent;
            float _FogHeight;
            float _HeightFallOff;
            float _GlobalDensity;
            

            fixed4 frag(VsOutFullScreen i) : SV_Target
            {
                float3 posW = computePositionFromDepth(UNITY_NEAR_CLIP_VALUE, i.texcoord, _InvViewProjMatrix);

                float3 dir = normalize(posW - _CameraPositionW);
                float2 uv = dirToSphericalCrd(dir) - _OffsetUV;

                float4 skyColor = SAMPLE_TEXTURE2D_LOD(_Envmap, sampler_Envmap, uv, 0);
#if SHADEROPTIONS_UNITY_PROJECT_GAMMA_COLOR_SPACE
                skyColor.rgb = GammaToLinearSpace(skyColor.rgb);
#endif
				//skyColor.rgb = decodeRGBM(skyColor) * _Intensity;
                skyColor.rgb = skyColor.rgb * _Intensity;
				//skyColor.rgb = evalDepthAndHeightFog(posW, skyColor.rgb);

				//return skyColor;
				float3 worldLitDir=WorldSpaceLightDir(i.vertex);
            	
				float Inscatter_lerp=saturate(pow(saturate(dot(dir,worldLitDir)) ,_ScatteringExponent)*1.5);
            	
                float lookup=saturate(dot(_CamForward ,float3(0,1,0)));
            	lookup*=lookup;
            	lookup*=lookup;
            	
            	float3 _FogColor=lerp(_FogColor_F,_FogColor_H,lookup);
				//return float4(_FogColor,1);
            	
            	float3 viewdir = normalize(posW - _WorldSpaceCameraPos);
                float skyline=1-saturate(dot(viewdir,float3(0,1,0)));

            	float h=_WorldSpaceCameraPos.y-_FogHeight;
            	float FogDensity= saturate( exp2(-h*_HeightFallOff));
            	
            	skyline=pow(skyline,4/(FogDensity));


				float Fog_lerp=max(skyline,FogDensity*0.66)*_GlobalDensity;          		

				_SunColor.rgb=lerp(_FogColor,_SunColor.rgb,_SunColor.a);
            	float3 fCol=lerp(_FogColor,_SunColor.rgb,Inscatter_lerp);

            	//return float4(fCol,1);
            	skyColor.rgb=lerp(skyColor.rgb,fCol, Fog_lerp);
            	
                return finalColor(half4(skyColor.rgb, 1));

            }
            ENDHLSL
        }
    }
    Fallback Off
}