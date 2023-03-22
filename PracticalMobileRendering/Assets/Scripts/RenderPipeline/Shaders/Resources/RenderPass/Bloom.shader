Shader "Hidden/BloomPyramid"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PREFILTER"

            HLSLPROGRAM
                #pragma vertex VSFullScreenTriangleTexCoord
                #pragma fragment FragPrefilter
                //#pragma enable_d3d11_debug_symbols
                #pragma multi_compile __ _HALF_SIZED_PREFILTER

                #include "Bloom.cginc"
            ENDHLSL
        }

        Pass
        {
            Name "DOWNSAMPLE"

            HLSLPROGRAM
                #pragma vertex VSFullScreenTriangleTexCoord
                #pragma fragment FragDownsample
                //#pragma enable_d3d11_debug_symbols
        
                #include "Bloom.cginc"
            ENDHLSL
        }

        Pass
        {
            Name "UPSAMPLE"

            HLSLPROGRAM
                #pragma vertex VSFullScreenTriangleTexCoord
                #pragma fragment FragUpsample
                //#pragma enable_d3d11_debug_symbols

                #include "Bloom.cginc"
            ENDHLSL
        }

    }
}
