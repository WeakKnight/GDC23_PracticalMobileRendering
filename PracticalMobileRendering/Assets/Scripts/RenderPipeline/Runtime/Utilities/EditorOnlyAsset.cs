using System;
using UnityEngine;


namespace PMRP
{
    // An utility class for editor only asset serialization, it will save GUID explicitly
    // so that Unity has no information about the object reference relationship.

    [Serializable]
    public class EditorOnlyAsset<T> where T : UnityEngine.Object
    {
        [SerializeField] private string m_guid;

#if UNITY_EDITOR
        private T m_object;
        public T Object
        {
            get
            {
                if (m_object != null)
                {
                    return m_object;
                }
                else if (!string.IsNullOrEmpty(m_guid))
                {
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(m_guid);
                    m_object = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);

                    if (m_object == null)
                    {
                        Debug.LogError(string.Format("Failed to load asset with GUID {0}, at path {1}", m_guid, assetPath));
                    }

                    return m_object;
                }

                return null;
            }

            set
            {
                m_object = value;

                if (m_object != null)
                {
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(m_object);
                    m_guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                }
                else
                {
                    m_guid = String.Empty;
                }
            }
        }
#endif
    }
}
