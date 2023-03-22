using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    public static class SceneEventDelegate
    {
        public static Action OnLightChanged         = () => { };
        public static Action OnMaterialChanged      = () => { };
        public static Action OnPrecomputedGIChanged = () => { };
        public static Action OnSceneChanged         = () => { };

        static SceneEventDelegate()
        {
            UnityEditor.Undo.postprocessModifications -= OnPostprocessModifications;
            UnityEditor.Undo.postprocessModifications += OnPostprocessModifications;
        }

        static UnityEditor.UndoPropertyModification[] OnPostprocessModifications(UnityEditor.UndoPropertyModification[] allModifications)
        {
            List<Light>               lights               = new List<Light>();
            List<MeshRenderer>        meshRenderers        = new List<MeshRenderer>();
            List<SkinnedMeshRenderer> skinnedMeshRenderers = new List<SkinnedMeshRenderer>();

            bool lightTransformChanged = false;
            bool meshTransformChanged = false;

            foreach (var modification in allModifications)
            {
                if (modification.currentValue.target is GameObject)
                {
                    if (modification.currentValue.propertyPath == "m_IsActive")
                    {
                        Debug.Log("Scene Changed");
                        OnSceneChanged();
                        break;
                    }
                }
                else if (modification.currentValue.target is Transform transfo)
                {
                    if (!lightTransformChanged)
                    {
                        transfo.gameObject.GetComponentsInChildren(lights);

                        if (lights.Count > 0)
                            lightTransformChanged = true;
                    }

                    if (!meshTransformChanged)
                    {
                        transfo.gameObject.GetComponentsInChildren(meshRenderers);
                        transfo.gameObject.GetComponentsInChildren(skinnedMeshRenderers);

                        if (meshRenderers.Count > 0 || skinnedMeshRenderers.Count > 0)
                            meshTransformChanged = true;
                    }
                }
                else if (modification.currentValue.target is SRPLight ||
                         modification.currentValue.target is Light)
                {
                    Debug.Log("Light Changed");
                    OnLightChanged();
                    break;
                }
                else if (modification.currentValue.target is MeshRenderer ||
                         modification.currentValue.target is SkinnedMeshRenderer)
                {
                    Debug.Log("Mesh Changed");
                    OnSceneChanged();
                    break;
                }
                else if (modification.currentValue.target is SRPEnvironmentLight)
                {
                    Debug.Log("Scene Setting Changed");
                    OnSceneChanged();
                    break;
                }
                else if (modification.currentValue.target is Material)
                {
                    Debug.Log("Material Changed");
                    OnMaterialChanged();
                    break;
                }
            }

            if (lightTransformChanged)
            {
                OnLightChanged();
            }

            if (meshTransformChanged)
            {
                Debug.Log("Mesh Transform Changed");
                OnSceneChanged();
            }

            return allModifications;
        }
    }
#else
    public static class SceneEventDelegate
    {
        public static Action OnLightChanged         = () => { };
        public static Action OnMaterialChanged      = () => { };
        public static Action OnSceneChanged         = () => { };
        public static Action OnPrecomputedGIChanged = () => { };
    }
#endif
}
