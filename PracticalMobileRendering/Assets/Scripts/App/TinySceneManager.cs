using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PMRP;


[ExecuteInEditMode]
[DisallowMultipleComponent]
public class TinySceneManager : MonoBehaviour
{
    private static TinySceneManager s_instance;

    private WorldData m_worldData;

    public static TinySceneManager GetInstance()
    {
        return s_instance;
    }

    public WorldData GetWorldData()
    {
        return m_worldData;
    }

    void OnEnable()
    {
        s_instance = this;
		m_worldData = new WorldData();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= OnUpdate;
        UnityEditor.EditorApplication.update += OnUpdate;
#endif

        SceneEventDelegate.OnLightChanged += SceneDescriptionChanged;

        UpdateWorldData();
    }

    void SceneDescriptionChanged()
    {
        UpdateWorldData();
    }

    void OnDisable()
    {
        s_instance = null;

		m_worldData = null;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= OnUpdate;
#endif
    }

    void Update()
    {
#if !UNITY_EDITOR
        OnUpdate();
#endif
    }

    void OnUpdate()
    {
        UpdateWorldData();
    }

    void UpdateWorldData()
    {
#if UNITY_EDITOR
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
            return;
#endif

        if (m_worldData == null)
            return;

        // Updating
        m_worldData.renderSettings = GameObject.FindFirstObjectByType<SRPRenderSettings>();

        m_worldData.envLight = null;
        m_worldData.sunLight = null;

        List<SRPLight> punctualLights = new List<SRPLight>();
        List<SRPLight> areaLights     = new List<SRPLight>();

        SRPLight[] allLights = GameObject.FindObjectsOfType<SRPLight>();
        foreach (var light in allLights)
        {
            if (!light.unityLight.isActiveAndEnabled)
                continue;

            if (light.IsPunctualLight())
            {
                if (light.Type == LightType.Directional)
                {
                    m_worldData.sunLight = light;
                }
                else
                {
                    punctualLights.Add(light);
                }
            }
            else
            {
                areaLights.Add(light);
            }
        }

        if (m_worldData.sunLight)
            punctualLights.Insert(0, m_worldData.sunLight);
        m_worldData.allPunctualLights = punctualLights.ToArray();
        m_worldData.allAreaLights     = areaLights.ToArray();

        var skyLights = GameObject.FindObjectsOfType<SRPEnvironmentLight>();
        if (skyLights != null && skyLights.Length > 0)
        {
            if (skyLights.Length > 1)
            {
                Debug.LogError(string.Format("Found {0} EnvironmentLight in scene", skyLights.Length));
            }
            m_worldData.envLight = skyLights[0];
        }
        else
        {
            m_worldData.envLight = null;
        }
    }
    
#if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/Rendering/Scene Manager", false, 10)]
	public static void Create(UnityEditor.MenuCommand menuCommand)
	{
		if (GameObject.FindObjectOfType<TinySceneManager>() != null)
		{
			UnityEditor.EditorUtility.DisplayDialog("Error",
				"Scene Manager Already Exist",
				"Ok");
			return;
		}

		// Create a custom game object
		GameObject go   = new GameObject("Tiny Scene Manager");
		var        comp = go.AddComponent<TinySceneManager>();

		// Ensure it gets reparented if this was a context click (otherwise does nothing)
		UnityEditor.GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

		// Register the creation in the undo system
		UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        UnityEditor.Selection.activeObject = go;
	}
#endif
}

