#ifndef _LIGHT_LOOP_CGINC_
#define _LIGHT_LOOP_CGINC_

#include "../../ShaderLib/GlobalConfig.cginc"
#include "../../ShaderLib/TypeDecl.cginc"
#include "../../ShaderLib/LightUtil.cginc"

LightData getDirectionalLight(in Intersection its);

void getPunctualLightIndexRange(in Intersection its, out int start, out int end);
LightData getPunctualLight(in Intersection its, in int lightIdx);

#if SHADEROPTIONS_LIGHT_LOOP_IMPL == YALIGHTLOOP_SIMPLE
    #include "SimpleLightLoop.cginc"
#else
    #error unknown light loop implementation
#endif

#endif
