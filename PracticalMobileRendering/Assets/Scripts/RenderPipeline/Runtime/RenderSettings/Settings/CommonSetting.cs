using System;
using UnityEngine;

namespace PMRP
{
    public class CommonSetting : BaseRenderSetting
    {
        [Range(0.25f, 2.0f)]
        public float PrimaryScreenPercentage = 1.0f;

        [Range(-8.0f, 8.0f)]
        public float FixedExposure = 0.0f;

        public YAViewMode ViewMode = YAViewMode.Lit;

        public YALightingComponent LightingComponents = (YALightingComponent)(-1);
    }
}