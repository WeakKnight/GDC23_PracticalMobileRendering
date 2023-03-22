using UnityEngine;

namespace PMRP
{
    public static class GlobalResources
    {
        private static Texture2D s_SpecOcclusionLut3D;

        public static void Reset()
        {
            s_SpecOcclusionLut3D = null;
        }

        public static Texture2D GetSpecularOcclusionLut3D()
        {
            if (s_SpecOcclusionLut3D == null)
            {
                s_SpecOcclusionLut3D = Resources.Load("Textures/SpecularOcclusionLut3D") as Texture2D;
            }
            return s_SpecOcclusionLut3D;
        }
    }
}