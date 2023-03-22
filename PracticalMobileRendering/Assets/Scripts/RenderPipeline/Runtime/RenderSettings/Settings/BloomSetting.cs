using UnityEngine;

namespace PMRP
{
    public class BloomSetting : BaseRenderSetting
    {
        public bool Enable = true;

        [Range(0.01f, 4.0f)]
        public float BloomThreshold = 0.9f;

        [Range(0.0f, 1.0f)]
        public float BloomSoftKnee = 0.5f;

        [Range(0.0f, 4.0f)]
        public float BloomIntensity = 0.0f;

        [Range(0.0f, 1.0f)]
        public float BloomDiffusion = 0.7f;

        [Range(0.1f, 2.0f)]
        public float BloomFireflyRemovalStrength = 1.0f;

        public float BloomClamp = 65472f;

        public Color BloomTint = Color.white;

        public bool IsBloomActivated()
        {
            return Enable && BloomIntensity > 0;
        }

        public bool BloomHalfSizeDownsample = true;

        public bool BloomQuaterSizeUpsample = true;
    }
}