using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    public static class ConstVars
    {
        public const string ShaderLibrary = "Assets/Scripts/RenderPipeline/Shaders/";

        public const string PythonRootFolder = "Assets/Scripts/RenderPipeline/Editor/PythonScripts/";

        public static readonly string DefaultRendererType = LiteForwardRenderer.name;

        public static readonly int ReflectionProbeTextureSize = 128;
    }
}
