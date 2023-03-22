Shader "Hidden/IntegrateSpecularOcclusion"
{
    Properties
    {
    }

    SubShader
    {
        Pass
        {
            Name "LUT3D"
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex VSFullScreenTriangleTexCoord
            #pragma fragment frag
            #pragma only_renderers d3d11
            //#pragma enable_d3d11_debug_symbols

            #include "../../ShaderLib/GlobalConfig.cginc"
            #include "../../ShaderLib/DrawQuadVS.cginc"
            #include "../../ShaderLib/Occlusion.cginc"
            #include "../../ShaderLib/DrawQuadVS.cginc"

            float _TexcoordZ;

            float4 frag (VsOutFullScreen i) : SV_Target
            {
                float3 texcoord = float3(i.texcoord.xy, _TexcoordZ);
                return integrateConeBrdf3D(texcoord);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
