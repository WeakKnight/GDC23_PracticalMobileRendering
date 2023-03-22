using System;
using UnityEngine;

namespace PMRP
{
    public class DynamicBaking : MonoBehaviour
    {
        public float cullingRatioForLightmap = 0.6f;
        public float conservativeFactorForLightmap = 0.5f;

        public float cullingRatioForVolumetricLightmap = 0.6f;
        public float conservativeFactorForVolumetricLightmap = 0.5f;

        public EmissiveSync emissiveSync;

        [Range(0.0f, 2.0f)]
        public float intensity = 0.0f;

        [NonSerialized]
        public float delta = 0.0f;
        [NonSerialized]
        public float prevIntensity = -1.0f;

        private void OnEnable()
        {
            delta = 0.0f;
            prevIntensity = 0.0f;
        }
    }
}