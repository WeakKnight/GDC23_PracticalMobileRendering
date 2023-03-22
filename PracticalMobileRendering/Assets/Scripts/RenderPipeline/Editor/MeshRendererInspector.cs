using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PMRP
{
    [CustomEditor(typeof(MeshRenderer), true)]
    [CanEditMultipleObjects]
    public class MeshRendererInspector : Editor
    {
        //Unity's built-in editor
        Editor defaultEditor;
        //MeshRenderer meshRenderer;

        void OnEnable()
        {
            defaultEditor = CreateEditor(targets, Type.GetType("UnityEditor.MeshRendererEditor, UnityEditor"));
        }

        void OnDisable()
        {
            MethodInfo disableMethod = defaultEditor.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (disableMethod != null)
            {
                disableMethod.Invoke(defaultEditor, null);
            }
            DestroyImmediate(defaultEditor);
        }

        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();

            // Validation
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                var mr = target as MeshRenderer;
                if (mr != null)
                {
                    if (mr.gameObject.GetComponent<AdditionalRendererData>() == null)
                    {
                        mr.gameObject.AddComponent<AdditionalRendererData>();
                    }
                }

                if (mr is MeshRenderer && (GameObjectUtility.GetStaticEditorFlags(mr.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && mr.receiveGI == ReceiveGI.Lightmaps))
                {
                    mr.renderingLayerMask |= LiteForwardRenderer.LIGHTMAP_LAYER_MASK;
                    mr.renderingLayerMask &= ~LiteForwardRenderer.VLM_LAYER_MASK;
                }
                else
                {
                    mr.renderingLayerMask &= ~LiteForwardRenderer.LIGHTMAP_LAYER_MASK;
                    mr.renderingLayerMask |= LiteForwardRenderer.VLM_LAYER_MASK;
                }
            }
        }
    }
}


