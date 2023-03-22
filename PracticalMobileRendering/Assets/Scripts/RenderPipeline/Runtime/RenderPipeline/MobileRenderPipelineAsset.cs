using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    [CreateAssetMenu(menuName = "PMRP/Render Pipeline Asset")]
    public partial class MobileRenderPipelineAsset : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline()
        {
            return new MobileRenderPipeline();
        }

        public override Material defaultMaterial
        {
            get
            {
                Material m = new Material(Shader.Find("PMRP/DefaultLit"));
                m.name = "YARP-DefaultMaterial";
                m.SetFloat(MaterialUtils.k_PropertyVisibility, 0);
                m.DisableKeyword(MaterialUtils.PropertyNameToKeyword(MaterialUtils.k_PropertyVisibility));
                return m;
            }
        }

    }
}
