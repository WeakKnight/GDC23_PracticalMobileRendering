#ifndef _COLOR_OUTPUT_CGINC_
#define _COLOR_OUTPUT_CGINC_

#include "../ShaderLib/GlobalConfig.cginc"
#include "GlobalVariables.cginc"

half4 finalColor(half4 color)
{
#if SHADEROPTIONS_PRE_EXPOSURE
    half4 EV = getExposureValue();
    return half4(color.rgb * EV.x, color.a);
#else
    return half4(color.rgb, color.a);
#endif
}

#endif
