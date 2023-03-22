#ifndef _QUASI_RANDOM_CGINC_
#define _QUASI_RANDOM_CGINC_

#include "GlobalConfig.cginc"

// Van der Corput radical inverse in base 2
float _radicalInverseBase2(uint i)
{
    i = (i << 16) | (i >> 16);
    i = (i & 0x55555555) << 1 | (i & 0xAAAAAAAA) >> 1;
    i = (i & 0x33333333) << 2 | (i & 0xCCCCCCCC) >> 2;
    i = (i & 0x0F0F0F0F) << 4 | (i & 0xF0F0F0F0) >> 4;
    i = (i & 0x00FF00FF) << 8 | (i & 0xFF00FF00) >> 8;
    return float(i) * 2.3283064365386963e-10f;
}

float2 hammersley2D(uint i, uint N)
{
    return float2(float(i) / float(N), _radicalInverseBase2(i));
}

// QMC sequence from http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
// Working well for low dimension with high sample count (tested 4 dimensions) https://github.com/anderslanglands/notebooks/blob/master/r2_sequence_01.ipynb
static float2 __R2_alpha = float2(0.7548776662466927, 0.5698402909980532);
static float4 __R4_alpha = float4(0.8566748838545029, 0.733891856627126, 0.6287067210378086, 0.53859725722361);

// Be careful with numerical precision, as n increased it's recommend to use nextSampleR2Sequence instead
float2 nthSampleR2Sequence(int n)
{
#if 0
    return frac(0.5 + __R2_alpha * (n + 1));
#else
    // MAD form
    return frac((0.5 + __R2_alpha) + __R2_alpha * n);
#endif
}

float2 nextSampleR2Sequence(float2 s)
{
    return frac(__R2_alpha + s);
}

float4 nthSampleR4Sequence(int n)
{
#if 0
    return frac(0.5 + __R4_alpha * (n + 1));
#else
    // MAD form
    return frac((0.5 + __R4_alpha) + __R4_alpha * n);
#endif
}

float4 nextSampleR4Sequence(float4 s)
{
    return frac(__R4_alpha + s);
}

#endif
