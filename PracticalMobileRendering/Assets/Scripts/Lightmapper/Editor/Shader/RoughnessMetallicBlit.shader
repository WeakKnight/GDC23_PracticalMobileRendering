Shader "Hidden/RoughnessMetallicBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}


			sampler2D _MainTex;
			float _MetallicChannel;

			sampler2D _RoughnessTex;
			float _RoughnessChannel;
			float _IsSmoothness;

			float4 frag(v2f i) : SV_Target
			{
				float4 metallicColor = tex2D(_MainTex, i.uv);
				float metallic = metallicColor[(int)_MetallicChannel];

				float4 roughnessColor = tex2D(_RoughnessTex, i.uv);
				float roughness = roughnessColor[(int)_RoughnessChannel];
				// From the GLTF 2.0 spec
				// The metallic-roughness texture. The metalness values are sampled from the B channel. 
				// The roughness values are sampled from the G channel. These values are linear. 
				// If other channels are present (R or A), they are ignored for metallic-roughness calculations.
				//
				// Conversion Summary
				// R channel goes into G channel
				// G channel goes into B channel
				return float4(1.0f, _IsSmoothness > 0.5f? (1.0f - roughness): roughness, metallic, 1.0f);
			}
			ENDCG
		}
    }
}
