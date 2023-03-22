#ifndef _INTERFACE_BSDF_CGINC_
#define _INTERFACE_BSDF_CGINC_

#include "../TypeDecl.cginc"

#define BSDF_FLAGS_UNSET (0)
#define BSDF_FLAGS_REFLECTION (1)
#define BSDF_FLAGS_TRANSMISSION (2)
#define BSDF_FLAGS_DIFFUSE (4)
#define BSDF_FLAGS_GLOSSY (8)
#define BSDF_FLAGS_SPECULAR (16)
#define BSDF_FLAGS_DIFFUSE_REFLECTION           (BSDF_FLAGS_DIFFUSE | BSDF_FLAGS_REFLECTION)
#define BSDF_FLAGS_DIFFUSE_TRANSMISSION         (BSDF_FLAGS_DIFFUSE | BSDF_FLAGS_TRANSMISSION)
#define BSDF_FLAGS_GLOSSY_REFLECTION            (BSDF_FLAGS_GLOSSY | BSDF_FLAGS_REFLECTION)
#define BSDF_FLAGS_GLOSSY_TRANSMISSION          (BSDF_FLAGS_GLOSSY | BSDF_FLAGS_TRANSMISSION)
#define BSDF_FLAGS_SPECULAR_REFLECTION          (BSDF_FLAGS_SPECULAR | BSDF_FLAGS_REFLECTION)
#define BSDF_FLAGS_SPECULAR_TRANSMISSION        (BSDF_FLAGS_SPECULAR | BSDF_FLAGS_TRANSMISSION)

bool BSDFFlagsIsSpecular(uint flags)
{
    return flags & BSDF_FLAGS_SPECULAR;
}

bool BSDFFlagsHasNonSpecularComponent(uint flags)
{
    return (flags & BSDF_FLAGS_DIFFUSE) || (flags & BSDF_FLAGS_GLOSSY);
}

/**
 * \brief Convenience data structure used to pass multiple
 * parameters to the evaluation and sampling routines in \ref BSDF
 */
struct BSDFSamplingRecord {
    /// Outgoing direction (in the local frame)
    float3 wo;

    /// Incident direction (in the local frame)
    float3 wi;

    /// Relative refractive index in the sampled direction
    // float eta;

    int bsdfFlags;

    bool sampleBssrdf;
};

/// Create a new record for sampling the BSDF
BSDFSamplingRecord BSDFSamplingRecord_(in float3 wo_)
{
    BSDFSamplingRecord rec;
    rec.wo = wo_;
    rec.sampleBssrdf = false;
    return rec;
}

/// Create a new record for querying the BSDF
BSDFSamplingRecord BSDFSamplingRecord_(in float3 wo_, in float3 wi_)
{
    BSDFSamplingRecord rec;
    rec.wi = wi_;
    rec.wo = wo_;
    rec.sampleBssrdf = false;
    return rec;
}

/**
 * \brief Superclass of all bidirectional scattering distribution functions
 */
interface BSDF
{
    uint BSDFFlags();

    /**
     * \brief Sample the BSDF and return the associated bsdf value and pdf 
     *
     * \param bRec    A BSDF query record
     * \param sample  A uniformly distributed sample on \f$[0,1]^2\f$
     *
     * \return The BSDF and solid angle pdf value.. A zero value means that sampling failed.
     */
    float3 sample(inout BSDFSamplingRecord bRec, in float u1, in float2 u2, out float bsdfPdf);

    /**
     * \brief Evaluate the BSDF for a pair of directions and measure
     * specified in \code bRec
     *
     * \param bRec
     *     A record with detailed information on the BSDF query
     * \return
     *     The BSDF value, evaluated for each color channel
     */
    float3 eval(in BSDFSamplingRecord bRec);

    /**
     * \brief Compute the probability of sampling \c bRec.wo
     * (conditioned on \c bRec.wi).
     *
     * This method provides access to the probability density that
     * is realized by the \ref sample() method.
     *
     * \param bRec
     *     A record with detailed information on the BSDF query
     *
     * \return
     *     A probability/density value expressed with respect
     *     to the specified measure
     */
    float pdf(in BSDFSamplingRecord bRec);

    void regularize();

    float3 getEmission();
};

#endif