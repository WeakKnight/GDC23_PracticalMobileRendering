Shader "Hidden/BuildEnvmapImportanceMap"
{
    Properties
    {
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex VSFullScreenTriangleTexCoord
            #pragma fragment frag
            #pragma only_renderers d3d11
            //#pragma enable_d3d11_debug_symbols

            #include "../../ShaderLib/GlobalConfig.cginc"
            #include "../../ShaderLib/DrawQuadVS.cginc"
            #include "../../ShaderLib/ColorSpace.cginc"
            #include "../../ShaderLib/Lights/EnvironmentLight.hlsl"

            Texture2D<float4> _Envmap;

            float4 frag(VsOutFullScreen vsOut) : SV_Target
            {
                return EnvironmentLight::importance(_Envmap, vsOut.texcoord);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
