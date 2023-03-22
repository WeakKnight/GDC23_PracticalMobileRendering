#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.AssetImporters;

namespace DDXForUnity
{
    [CustomEditor(typeof(DDXTextureImporter))]
    public class DDXTextureImporterInspector : ScriptedImporterEditor
    {
        SerializedProperty m_Aniso;
        SerializedProperty m_FilterMode;
        SerializedProperty m_WrapMode;
        SerializedProperty m_sourceFormat;
        SerializedProperty m_textureDimension;
        SerializedProperty m_readable;

        public override void OnEnable()
        {
            base.OnEnable();

            m_Aniso = serializedObject.FindProperty("anisoLevel");
            m_FilterMode = serializedObject.FindProperty("filterMode");
            m_WrapMode = serializedObject.FindProperty("wrapMode");
            m_sourceFormat = serializedObject.FindProperty("sourceFormat");
            m_textureDimension = serializedObject.FindProperty("textureDimension");
            m_readable = serializedObject.FindProperty("readable");
        }

        public void OnImporterPlatformSettingGUI(TextureImporterPlatformSettings platformSettings)
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                platformSettings.format = (TextureImporterFormat)EditorGUILayout.EnumPopup("Format", platformSettings.format);
                platformSettings.textureCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", platformSettings.textureCompression);

                if (check.changed)
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(m_sourceFormat);
                EditorGUILayout.PropertyField(m_textureDimension);
            }

            EditorGUILayout.PropertyField(m_readable);
            EditorGUILayout.PropertyField(m_WrapMode);
            EditorGUILayout.PropertyField(m_FilterMode);
            EditorGUILayout.PropertyField(m_Aniso);

            DDXTextureImporter importer = target as DDXTextureImporter;

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Default Importer Settings");
            var defaultImportSetting = importer.GetPlatformTextureSettings(DDXTextureImporter.s_DefaultPlatformName);
            OnImporterPlatformSettingGUI(defaultImportSetting);
            EditorGUILayout.Separator();

            BuildTargetGroup buildTargetGroup = EditorGUILayout.BeginBuildTargetSelectionGrouping();
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string targetGroupName = DDXTextureImporter.GetBuildTargetGroupName(buildTargetGroup);

                var targetPlatformSetting = importer.GetPlatformTextureSettings(buildTargetGroup);

                targetPlatformSetting.overridden = EditorGUILayout.ToggleLeft("Overriden For " + targetGroupName, targetPlatformSetting.overridden);

                using (new EditorGUI.DisabledScope(!targetPlatformSetting.overridden))
                {
                    OnImporterPlatformSettingGUI(targetPlatformSetting);
                }

                if (check.changed)
                {
                    EditorUtility.SetDirty(target);

                    if (targetPlatformSetting.overridden)
                    {
                        importer.SetPlatformTextureSettings(targetPlatformSetting);
                    }
                    else
                    {
                        importer.ClearPlatformTextureSettings(targetGroupName);
                    }
                }
            }
            EditorGUILayout.EndBuildTargetSelectionGrouping();

            serializedObject.ApplyModifiedProperties();

            ApplyRevertGUI();
        }
    }
}

#endif