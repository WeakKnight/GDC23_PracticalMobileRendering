using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace PMRP
{
    [EditorToolbarElement("PMRP/Lightmapper Pannel Button")]
    class LightmapperToolBarButton : EditorToolbarButton, IAccessContainerWindow
    {
        public LightmapperToolBarButton()
        {
            RegisterCallback<MouseUpEvent>(OnClick);
            text = "Lightmapper";
        }

        void OnClick(MouseUpEvent evt)
        {
            if (lightmapperEditor == null)
            {
                lightmapperEditor = ScriptableObject.CreateInstance<LightmapperEditor>();

                lightmapperEditor.ShowAsDropDown(
                    new Rect(containerWindow.position.x + contentContainer.worldBound.x,
                    containerWindow.position.y + contentContainer.worldBound.y,
                    contentContainer.worldBound.width,
                    contentContainer.worldBound.height),
                    new Vector2(320, 130));

                var borderColor = new Color(125.0f / 255.0f, 125.0f / 255.0f, 125.0f / 255.0f, 1.0f);
                var borderWidth = 1.05f;
                lightmapperEditor.rootVisualElement.style.borderLeftWidth = borderWidth;
                lightmapperEditor.rootVisualElement.style.borderLeftColor = borderColor;
                lightmapperEditor.rootVisualElement.style.borderRightWidth = borderWidth;
                lightmapperEditor.rootVisualElement.style.borderRightColor = borderColor;
                lightmapperEditor.rootVisualElement.style.borderTopWidth = borderWidth;
                lightmapperEditor.rootVisualElement.style.borderTopColor = borderColor;
                lightmapperEditor.rootVisualElement.style.borderBottomWidth = borderWidth;
                lightmapperEditor.rootVisualElement.style.borderBottomColor = borderColor;
            }
            else
            {
                lightmapperEditor.Close();
                lightmapperEditor = null;
            }
        }

        EditorWindow lightmapperEditor;

        public EditorWindow containerWindow { get; set; }
    }

    class LightmapperEditor : EditorWindow
    {
        void OnGUI()
        {
            var mgr = Object.FindAnyObjectByType<PrecomputedLightingManager>();
            if (mgr != null)
            {
                mgr.lightmapResolution = EditorGUILayout.IntField("Lightmap Resolution", mgr.lightmapResolution);
                mgr.lightmapTexelDensity = EditorGUILayout.FloatField("Lightmap Texel Density", mgr.lightmapTexelDensity);
                mgr.lightmapSampleCount = EditorGUILayout.IntField("Lightmap Sample Count", mgr.lightmapSampleCount);
                mgr.diffuseBoost = EditorGUILayout.FloatField("Diffuse Boost", mgr.diffuseBoost);
            }

            if (GUILayout.Button("Generate"))
            {
                Lightmapper.Execute();
            }

            if (GUILayout.Button("Reimport Receivers"))
            {
                Lightmapper.ReadLightmapReceiver();
                Lightmapper.ReadVolumetricLightmapReceiver();
                mgr?.Refresh();
            }
        }
    }

    [CustomEditor(typeof(PMRPToolBar))]
    public class PMRPToolBarEditor : UnityEditor.Editor, UnityEditor.Overlays.ICreateToolbar
    {
        public IEnumerable<string> toolbarElements
        {
            get
            {
                yield return "PMRP/Lightmapper Pannel Button";
            }
        }
    }

    public class PMRPToolBar : EditorTool
    {
        [InitializeOnLoadMethod]
        static void OnAfterAssembliesLoaded()
        {
            ToolManager.SetActiveTool<PMRPToolBar>();
        }
    }
}