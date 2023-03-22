Shader "Hidden/PostProcessUber"
{
    SubShader
    {


        Pass
        {
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
                #pragma vertex VSFullScreenTriangleTexCoord
                #pragma fragment Frag

                #pragma multi_compile _ _BLOOM

                //#pragma enable_d3d11_debug_symbols

                #include "PostProcessUber.cginc"
            ENDHLSL
        }
    }
}
