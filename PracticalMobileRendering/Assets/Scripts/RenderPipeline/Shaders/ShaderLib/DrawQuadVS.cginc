#ifndef _DRAW_QUAD_VS_CGINC_
#define _DRAW_QUAD_VS_CGINC_

#include "GlobalConfig.cginc"


struct VsOutFullScreen {
    float4 vertex : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

struct VsInFullScreen {
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
};

VsOutFullScreen VSFullScreenTriangleTexCoord(VsInFullScreen v)
{
    VsOutFullScreen o;
    o.texcoord = v.texcoord.xy;
    o.vertex = v.vertex;

#if UNITY_REVERSED_Z
	o.vertex.z = 0;
#else
	o.vertex.z = 1;
#endif
    return o;
}

#endif
