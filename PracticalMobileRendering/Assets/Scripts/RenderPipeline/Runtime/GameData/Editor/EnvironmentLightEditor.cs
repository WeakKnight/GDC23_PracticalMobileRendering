using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;


namespace PMRP
{
    [CustomEditor(typeof(SRPEnvironmentLight))]
    public class EnvironmentLightEditor : Editor
    {
        private LightProbeAsset m_lightProbeAsset;

        private SerializedProperty m_intensity;
        private SerializedProperty m_angularRotation;
        private SerializedProperty m_IBLNormalization;
        private SerializedProperty m_envmapGgxFiltered;
        private SerializedProperty m_irradianceSH9Coeffs;

        private void OnEnable()
        {
            SRPEnvironmentLight envmap = (SRPEnvironmentLight) target;

            m_lightProbeAsset = envmap.lightProbeAsset;

            m_intensity = serializedObject.FindProperty(nameof(envmap.Intensity));
            m_angularRotation = serializedObject.FindProperty(nameof(envmap.RotationAngle));
            m_IBLNormalization = serializedObject.FindProperty(nameof(envmap.IBLNormalization));
            m_envmapGgxFiltered = serializedObject.FindProperty(nameof(envmap.EnvmapGgxFiltered));
            m_irradianceSH9Coeffs = serializedObject.FindProperty(nameof(envmap.IrradianceSH9Coeffs));
        }

        void PrefilterAndSave(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                string filepath = AssetDatabase.GetAssetPath(m_lightProbeAsset.PanoramicMap);
                filepath = Path.GetFileNameWithoutExtension(filepath);
                path     = EditorUtility.SaveFilePanel("Save File", 
                                                       PathUtils.GetActiveSceneDirectory(), 
                                                       filepath, 
                                                       "asset");
            }

            if (string.IsNullOrEmpty(path))
                return;

            m_lightProbeAsset.PreFilter(path);
            m_lightProbeAsset.Save(path);

            SRPEnvironmentLight envmap = (SRPEnvironmentLight)target;
            envmap.lightProbeAsset = m_lightProbeAsset;

            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(envmap.gameObject.scene);
            }
        }

        void OnInspectorGuiInternal()
        {
            SRPEnvironmentLight envmap = (SRPEnvironmentLight) target;

            GUILayout.Label("Environment Light Settings:");
            EditorGUILayout.PropertyField(m_intensity);
            EditorGUILayout.PropertyField(m_angularRotation);
            EditorGUILayout.PropertyField(m_IBLNormalization);
            EditorGUILayout.Space();

            GUILayout.Label("Assets:");

            var selectedAsset = EditorGUILayout.ObjectField("Cooked Asset", m_lightProbeAsset, typeof(LightProbeAsset), true) as LightProbeAsset;
            if (selectedAsset != m_lightProbeAsset)
            {
                m_lightProbeAsset      = selectedAsset;
                envmap.lightProbeAsset = selectedAsset;
            }

            if (m_lightProbeAsset != null)
            {
                m_lightProbeAsset.PanoramicMap = EditorGUILayout.ObjectField("Sky Texture",
                                                                             m_lightProbeAsset.PanoramicMap,
                                                                             typeof(Texture2D),
                                                                             false) as Texture2D;

                if (m_lightProbeAsset.PanoramicMap && GUILayout.Button("Save"))
                {
                    PrefilterAndSave(AssetDatabase.GetAssetPath(m_lightProbeAsset));
                }
            }

            if (GUILayout.Button("New"))
            {
                m_lightProbeAsset = ScriptableObject.CreateInstance<LightProbeAsset>();
                // Do not update envmap since it's asset is not serialized yet
            }
        }

        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();

            SRPEnvironmentLight envmap = (SRPEnvironmentLight) target;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                OnInspectorGuiInternal();

                if (check.changed)
                {
                    SceneEventDelegate.OnSceneChanged();

                    EditorUtility.SetDirty(envmap);
                    if (envmap.lightProbeAsset)
                        EditorUtility.SetDirty(envmap.lightProbeAsset);

                    EditorSceneManager.MarkSceneDirty(envmap.gameObject.scene);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            base.OnPreviewGUI(r, background);
            EditorGUILayout.PropertyField(m_envmapGgxFiltered);
            EditorGUILayout.PropertyField(m_irradianceSH9Coeffs, true);
        }
    }
}