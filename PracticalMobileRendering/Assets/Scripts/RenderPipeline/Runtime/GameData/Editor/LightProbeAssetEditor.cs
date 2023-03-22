using UnityEngine;
using UnityEditor;

namespace PMRP
{
    [CustomEditor(typeof(LightProbeAsset))]
    public class LightProbeAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            LightProbeAsset probeAsset = (LightProbeAsset) target;

            probeAsset.PanoramicMap     = EditorGUILayout.ObjectField("Panoramic Map", probeAsset.PanoramicMap, typeof(Texture2D), false) as Texture2D;

            if (probeAsset.PanoramicMap && GUILayout.Button("Save"))
            {
                var path = AssetDatabase.GetAssetPath(probeAsset);
                probeAsset.PreFilter(path);
                probeAsset.Save(path);
            }
			
            if (GUI.changed)
            {
                EditorUtility.SetDirty(probeAsset);
            }
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            base.OnPreviewGUI(r, background);
            LightProbeAsset asset = (LightProbeAsset)target;
            GUI.Label(r, asset.PanoramicMap);
        }
    }
}