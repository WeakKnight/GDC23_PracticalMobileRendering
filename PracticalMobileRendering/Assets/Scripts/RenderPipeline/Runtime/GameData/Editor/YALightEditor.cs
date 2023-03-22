using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public static class YALightEditorHelper
    {
        public static void CreatePunctualLight(MenuCommand menuCommand, LightType type, string name)
        {
            GameObject light = new GameObject(name);

            // Ensure it gets reparented if this was a context click (otherwise does nothing)
            GameObjectUtility.SetParentAndAlign(light, menuCommand.context as GameObject);

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(light, "Create " + light.name);
            Selection.activeObject = light;

            // Setup all components
            var unityLight = light.AddComponent<Light>();
            unityLight.type = type;

            var aLight = light.AddComponent<SRPLight>();
        }
    }

#if UNITY_2020_1_OR_NEWER
    [CustomEditorForRenderPipeline(typeof(SRPLight), typeof(MobileRenderPipelineAsset))]
    public class DummyLightEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }
    }

    [CustomEditorForRenderPipeline(typeof(Light), typeof(MobileRenderPipelineAsset))]
    public class UnityLightEditor : LightEditor
    {
        private SRPLight          m_light;
        private SerializedObject m_serializedLight = null;

        private SerializedProperty m_twoSided                 = null;
        private SerializedProperty m_useInverseSquaredFalloff = null;
        private SerializedProperty m_lightFalloffExponent     = null;

        private SerializedProperty m_shadowMapResolution  = null;
        private SerializedProperty m_shadowDepthSlopeBias = null;
        private SerializedProperty m_shadowNormalBias     = null;
        private SerializedProperty m_shadowDepthBiasClamp = null;

        private SerializedProperty m_overrideShadowBoundingSphere = null;
        private SerializedProperty m_shadowSplits               = null;

        private SerializedProperty m_csmNumOfSplits           = null;
        private SerializedProperty m_csmLambda                  = null;
        private SerializedProperty m_csmDistanceFade            = null;
        private SerializedProperty m_csmNearPlane               = null;
        private SerializedProperty m_csmFarPlane                = null;
        private SerializedProperty m_csmZOffest                 = null;

        private SerializedProperty m_lightmapping = null;

        protected override void OnEnable()
        {
            this.settings.OnEnable();

            Light unityLight = (Light)target;

            m_light = unityLight.GetComponent<SRPLight>();
            if (!m_light)
            {
                m_light = unityLight.gameObject.AddComponent<SRPLight>();
            }

            m_serializedLight = new SerializedObject(m_light);

            m_twoSided                 = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.TwoSided));
            m_useInverseSquaredFalloff = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.UseInverseSquaredFalloff));
            m_lightFalloffExponent     = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.LightFalloffExponent));

            m_shadowMapResolution  = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.ShadowMapResolution));
            m_shadowDepthSlopeBias = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.DepthSlopeBias));
            m_shadowNormalBias     = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.NormalBias));
            m_shadowDepthBiasClamp = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.DepthBiasClamp));

            m_overrideShadowBoundingSphere = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.OverrideShadowBoundingSphere));
            m_shadowSplits                 = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.ShadowSplits));

            m_csmNumOfSplits  = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.NumOfSplits));
            m_csmLambda       = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.PSSMLambda));
            m_csmDistanceFade = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.DirectionalShadowDistanceFade));
            m_csmNearPlane    = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.NearPlane));
            m_csmFarPlane     = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.FarPlane));
            m_csmZOffest      = m_serializedLight.FindProperty(CSharpUtils.FullPropertyName(() => m_light.ShadowSetting.ZOffset));

            m_lightmapping = serializedObject.FindProperty("m_Lightmapping");

            // base.OnEnable();
        }

        void OnDisable()
        {
        }

        void OnLightmappingItemSelected(object userData)
        {
            m_lightmapping.intValue = (int)userData;
            serializedObject.ApplyModifiedProperties();
        }

        void DrawLightmapping()
        {
            GUIContent   LightmappingMode       = EditorGUIUtility.TrTextContent("Mode", "Specifies the light mode used to determine if and how a light will be baked. Possible modes are Baked, Mixed, and Realtime.");
            int[]        LightmapBakeTypeValues = {(int) LightmapBakeType.Realtime, (int) LightmapBakeType.Mixed, (int) LightmapBakeType.Baked};
            GUIContent[] LightmapBakeTypeTitles = {EditorGUIUtility.TrTextContent("Realtime"), EditorGUIUtility.TrTextContent("Mixed"), EditorGUIUtility.TrTextContent("Baked")};
            bool         lightmappingTypeIsSame = !m_lightmapping.hasMultipleDifferentValues;

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, LightmappingMode, m_lightmapping);
            rect = EditorGUI.PrefixLabel(rect, LightmappingMode);

            int index = Math.Max(0, Array.IndexOf(LightmapBakeTypeValues, m_lightmapping.intValue));

            if (EditorGUI.DropdownButton(rect, LightmapBakeTypeTitles[index], FocusType.Passive))
            {
                var menu = new GenericMenu();

                for (int i = 0; i < LightmapBakeTypeValues.Length; i++)
                {
                    int value = LightmapBakeTypeValues[i];
                    bool selected = (lightmappingTypeIsSame && (value == m_lightmapping.intValue));

                    menu.AddItem(LightmapBakeTypeTitles[i], selected, OnLightmappingItemSelected, value);
                }
                menu.DropDown(rect);
            }
            EditorGUI.EndProperty();
        }

        private void OnInspectorGUIInternal()
        {
            this.settings.Update();
            m_serializedLight.Update();

            this.settings.DrawLightType();

            if (!m_light.IsPunctualLight())
            {
                EditorGUILayout.PropertyField(m_twoSided);
            }

            if (this.settings.light.type != LightType.Directional)
            {
                this.settings.DrawRange();
            }

            if (this.settings.light.type == LightType.Spot)
            {
                this.settings.DrawInnerAndOuterSpotAngle();
            }


            this.settings.DrawColor();
            this.settings.DrawIntensity();

            if (this.settings.light.type == LightType.Point ||
                this.settings.light.type == LightType.Spot)
            {
                EditorGUILayout.PropertyField(m_useInverseSquaredFalloff);
                using (new EditorGUI.DisabledScope(m_useInverseSquaredFalloff.boolValue))
                    EditorGUILayout.PropertyField(m_lightFalloffExponent);
            }

            this.settings.DrawBounceIntensity();
            DrawLightmapping();

            this.settings.DrawCookie();
            if (this.settings.light.type == LightType.Directional)
                this.settings.DrawCookieSize();

            this.settings.DrawRenderMode();
            this.settings.DrawCullingMask();
            // if (EditorGUILayout.BeginFadeGroup(1))
            //     this.settings.DrawCookieSize();
            // EditorGUILayout.EndFadeGroup();
            this.settings.DrawHalo();
            this.settings.DrawFlare();

            this.settings.DrawShadowsType();
            if (this.settings.light.shadows != LightShadows.None)
            {
                if (this.settings.light.type == LightType.Directional)
                    this.settings.DrawBakedShadowAngle();

                if (this.settings.light.type == LightType.Point ||
                    this.settings.light.type == LightType.Spot)
                    this.settings.DrawBakedShadowRadius();

                if (m_light.IsPunctualLight())
                {
                    EditorGUILayout.PropertyField(m_shadowMapResolution);

                    if (this.settings.light.type == LightType.Directional || 
                        this.settings.light.type == LightType.Spot)
                    {
                        EditorGUILayout.PropertyField(m_overrideShadowBoundingSphere);
                        EditorGUILayout.PropertyField(m_shadowSplits);
                    }

                    if (this.settings.light.type == LightType.Directional)
                    {
                        EditorGUILayout.PropertyField(m_csmNumOfSplits);
                        EditorGUILayout.PropertyField(m_csmLambda);
                        EditorGUILayout.PropertyField(m_csmDistanceFade);
                        EditorGUILayout.PropertyField(m_csmNearPlane);
                        EditorGUILayout.PropertyField(m_csmFarPlane);
                        EditorGUILayout.PropertyField(m_csmZOffest);
                    }

                    EditorGUILayout.PropertyField(m_shadowDepthSlopeBias);
                    EditorGUILayout.PropertyField(m_shadowNormalBias);
                    EditorGUILayout.PropertyField(m_shadowDepthBiasClamp);
                }
            }

            this.settings.ApplyModifiedProperties();
            m_serializedLight.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                OnInspectorGUIInternal();
            }
        }
    }
#endif
}

