using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    // Follow D3D convention: https://docs.microsoft.com/en-us/windows/win32/direct3d9/cubic-environment-mapping
    // Please keep in-sync with CubemapUtilities.cginc
    public static class CubemapUtilities
    {
        public static readonly Vector3[] Forward = new Vector3[]
        {
            new Vector3(1, 0, 0),  // +X
            new Vector3(-1, 0, 0), // -X
            new Vector3(0, 1, 0),  // +Y
            new Vector3(0, -1, 0), // -Y
            new Vector3(0, 0, 1),  // +Z
            new Vector3(0, 0, -1), // -Z
        };

        public static readonly Vector3[] Up = new Vector3[]
        {
            new Vector3(0, 1, 0),  // +X
            new Vector3(0, 1, 0),  // -X
            new Vector3(0, 0, -1), // +Y
            new Vector3(0, 0, 1),  // -Y
            new Vector3(0, 1, 0),  // +Z
            new Vector3(0, 1, 0),  // -Z
        };
    }
}