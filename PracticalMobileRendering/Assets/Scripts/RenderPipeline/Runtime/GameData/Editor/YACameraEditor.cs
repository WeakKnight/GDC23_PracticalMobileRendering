using UnityEditor;
using UnityEngine;

namespace PMRP
{
    [CustomEditor(typeof(SRPCamera))]
    public class YACameraEditor : Editor
    {
        private SRPCamera m_camera;

        void OnEnable()
        {
            m_camera = (SRPCamera) target;
        }

        void OnDisable()
        {
        }

        void OnRendererTypeGUI()
        {
            EditorGUILayout.LabelField("Rendering");

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string[] allRenderers = SRPCamera.AllRendererTypes();

                int selectedIdx = -1;
                int defaultIdx  = 0;

                for (int i = 0; i < allRenderers.Length; ++i)
                {
                    if (allRenderers[i] == m_camera.RendererType)
                        selectedIdx = i;

                    if (allRenderers[i] == ConstVars.DefaultRendererType)
                        defaultIdx = i;
                }

                // renderer not found, assign default value
                if (selectedIdx < 0)
                    selectedIdx = defaultIdx;

                int newIdx = EditorGUILayout.Popup("Type", selectedIdx, allRenderers);
                if (newIdx != selectedIdx)
                    m_camera.RendererType = allRenderers[newIdx];

                if (check.changed)
                {
                    EditorUtility.SetDirty(target);
                }
            }

            GUILayout.Space(12);
        }

        void OnCameraOptionsGUI()
        {
            EditorGUILayout.LabelField("Camera Options");
            base.OnInspectorGUI();

            GUILayout.Space(12);

            if (GUILayout.Button("Capture Screenshot"))
            {
                string path = EditorUtility.SaveFilePanel("Capture Screenshot", "", "Screenshot", "exr");
                if (!string.IsNullOrEmpty(path))
                {
                    m_camera.CaptureScreenshot = true;
                    m_camera.ScreenshotFilename = path;
                }
            }
            GUILayout.Space(12);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            OnRendererTypeGUI();
            OnCameraOptionsGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}