using UnityEngine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Python.Runtime;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("PythonRunnerTests")]

namespace UnityEditor.Scripting.Python
{
    /// <summary>
    /// Settings for the Python Scripting package.
    /// </summary>
    public sealed class PythonSettings : ScriptableObject
    {

        /// <summary>
        /// Current project directory, with a trailing slash
        /// </summary>
        static readonly string projectRoot = Regex.Replace(Directory.GetCurrentDirectory(), "\\\\", "/") + '/';
        const string PreferencesPath = "ProjectSettings/PythonSettings.asset";

        /// <summary>
        /// Location where Python will be installed, relative to the project path.
        /// </summary>
        public const string kDefaultPythonDirectory = "Library/PythonInstall";
#if UNITY_EDITOR_WIN
        public const string kDefaultPython = kDefaultPythonDirectory + "/python.exe";
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        public const string kDefaultPython = kDefaultPythonDirectory + "/bin/python" + PythonRunner.PythonMajorVersion;
#endif

        public static string kDefaultPythonFullPath => Path.GetFullPath(kDefaultPython);

        internal const string kPipRequirementsFile = "ProjectSettings/requirements.txt";

#if UNITY_EDITOR_WIN
        internal const string kSitePackagesRelativePath = kDefaultPythonDirectory + "/Lib/site-packages";
#else
        internal const string kSitePackagesRelativePath = kDefaultPythonDirectory + "/lib/python" + PythonRunner.PythonMajorVersion + "." + PythonRunner.PythonMinorVersion + "/site-packages";
#endif

        /// <summary>
        /// Returns the release version.
        ///
        /// Must be made to match the package.json or else things get confusing.
        /// </summary>
        public static string Version
        {
            get 
            {
                if (string.IsNullOrEmpty(_version))
                {
                    // Go read it from the package.json file
                    using (var reader = new StreamReader("Packages/com.unity.scripting.python/package.json"))
                    {
                        _version = "0.0.0";
                        // discard the first three lines
                        _ = reader.ReadLine();
                        _ = reader.ReadLine();
                        _ = reader.ReadLine();
                        // We want  to extract the version out of this string:
                        // `"version" : "3.0.0-pre.1",`
                        var regex = new Regex("\"version\"\\s*:\\s*\"([^\"]+)\",");
                        // and it's the 2nd captured group
                        var match = regex.Match(reader.ReadLine());
                        if (match.Groups.Count > 0)
                        {
                            _version = match.Groups[1].Value;
                        }
                    }
                }
                return _version; 
            }
        }

        static string _version = null;

        /// <summary>
        /// Version number of our custom
        /// <a href="https://github.com/Unity-Technologies/pythonnet">python.NET</a>
        /// forked library installed with Python Scripting.
        /// </summary>
        public static string PythonNetVersion
        {
            get { return System.Reflection.Assembly.GetAssembly(typeof(PythonEngine)).GetName().Version.ToString(); }
        }

        /////////
        /// User site-packages.
        /// Set via the serializedObject workflow.
        #pragma warning disable 0649
        [SerializeField]
        internal string [] m_sitePackages = new string[]{"Assets/site-packages"};
        #pragma warning restore 0649

        /// <summary>
        /// Set of additional site-packages paths used in your project.
        ///
        /// Example: add your studio scripts here.
        ///
        /// This is a copy; avoid calling SitePackages in a loop.
        /// </summary>
        /// <returns>A string array of the site-packages</returns>
        public static string [] GetSitePackages()
        {
            return (string[])Instance.m_sitePackages.Clone();
        }
        string [] m_originalSitePackages;

        internal static bool SitePackagesChanged
        {
            get
            {
                return !(Enumerable.SequenceEqual(GetSitePackages(), Instance.m_originalSitePackages));
            }
        }

        /// <summary>
        /// This class is a singleton. This returns the sole instance, loading
        /// it from the preferences if it hasn't been built yet.
        /// </summary>
        internal static PythonSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = CreateInstance<PythonSettings>();

                    // Try to load the preferences file. Use defaults otherwise.
                    try
                    {
                        var fileData = System.IO.File.ReadAllText(PreferencesPath);
                        EditorJsonUtility.FromJsonOverwrite(fileData, s_Instance);
                    }
                    catch
                    {
                    }

                    // Remember the original settings on startup.
                    s_Instance.m_originalSitePackages = GetSitePackages();
                }
                return s_Instance;
            }
        }
        static PythonSettings s_Instance;

        PythonSettings()
        {
            if (s_Instance != null)
            {
                throw new System.ArgumentException("second instance of PythonSettings being constructed");
            }
        }

        /// <summary>
        /// Save any changes to the preferences file.
        /// </summary>
        internal void Save()
        {
            if (s_Instance == null)
            {
                // Don't save, there's nothing to save.
                return;
            }

            var dirName = Path.GetDirectoryName(PreferencesPath);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            File.WriteAllText(PreferencesPath, EditorJsonUtility.ToJson(s_Instance, true));
        }
    }

    [CustomEditor(typeof(PythonSettings))]
    class PythonSettingsEditor : Editor
    {
        static class Styles
        {
            public static readonly GUIContent sitePackages = new GUIContent("Package Directories", "Directories where your custom scripts are stored. Added to your sys.path ahead of the system sys.path. They are added both to the in-process and out-of-process Python APIs. Relative paths are interpreted within the Unity virtual file system.");
        }

        internal static string ShortPythonVersion(string longPythonVersion)
        {
            // The long Python version is e.g.
            //  2.7.16 |Anaconda, Inc.| (default, Mar 14 2019, 16:24:02) \n[GCC 4.2.1 Compatible Clang 4.0.1 (tags/RELEASE_401/final)]
            // The short Python version for that is '2.7.16'
            //
            // Return the long Python version if it doesn't parse as a short version.
            if (string.IsNullOrEmpty(longPythonVersion))
            {
                return "";
            }

            var firstSpace = longPythonVersion.IndexOf(' ');
            if (firstSpace < 0)
            {
                return longPythonVersion;
            }
            return longPythonVersion.Substring(0, firstSpace);
        }

        public override void OnInspectorGUI()
        {
            try
            {
                OnInspectorGUICanThrow();
            }
            catch (System.Exception xcp)
            {
                Debug.LogException(xcp);
            }
            if (GUI.changed) {
                var settings = (PythonSettings)target;
                EditorUtility.SetDirty(settings);
                settings.Save();
            }
        }
        bool sitePackagesChangesPending = false;
        bool sitePackagesChangesApplied = false;
        /// <summary>
        /// Draw the editor UI in immediate mode.
        ///
        /// Called by OnInspectorGUI, which catches any exception we throw here.
        /// </summary>
        void OnInspectorGUICanThrow()
        {
            // Overall UI layout for now: information at the top.
            // Settings for in-process next (may also affect out-of-process Python).
            // Settings for out-of-process next.
            // TODO: nicer UI.

            var settings = (PythonSettings)target;

            // TODO: label + selectable label so users can copy-paste package version
            // (and the same for all versions below)
            EditorGUILayout.LabelField("Package Version: " + PythonSettings.Version);

            EditorGUILayout.LabelField(
                    new GUIContent("Python Version: " + ShortPythonVersion(PythonRunner.PythonVersion),
                        "Python Scripting is running Python version " + PythonRunner.PythonVersion));

            EditorGUILayout.LabelField(
                    new GUIContent("Python for .NET Version: " + PythonSettings.PythonNetVersion,
                        "Python Scripting is using Python for .NET version " + PythonSettings.PythonNetVersion));

            EditorGUILayout.Separator();

            //Site Packages section.
            EditorGUILayout.LabelField("Site Packages", EditorStyles.boldLabel);

            // The site packages array goes through the serializedObject code path
            // to offer the usual array modification workflow in the UI.
            // TODO: make this much prettier.
            var sitePackagesArray = serializedObject.FindProperty("m_sitePackages");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sitePackagesArray, Styles.sitePackages, true);
            if (EditorGUI.EndChangeCheck())
            {
                // site packages changes pending, show the button.
                sitePackagesChangesPending = true;
                serializedObject.ApplyModifiedProperties();
            }

            if (sitePackagesChangesPending)
            {
                if(GUILayout.Button("Apply site packages changes", GUILayout.Width(190)))
                {
                    PythonRunner.AddToSitePackages(PythonSettings.Instance.m_sitePackages);
                    // Even when modified once, we need to show the message.
                    sitePackagesChangesApplied = true;
                    sitePackagesChangesPending = false;
                }
            }

            if (PythonSettings.SitePackagesChanged && sitePackagesChangesApplied)
            {
                EditorGUILayout.HelpBox("The pacakges search path has been modified. A Unity restart may be required.", MessageType.Warning);
                
            }

            EditorGUILayout.Separator();

#if UNITY_EDITOR_WIN|| UNITY_EDITOR_OSX
            if (GUILayout.Button(new GUIContent("Launch Terminal", "Launches a terminal window with Unity Python in its PATH."), GUILayout.Width(125)))
            {
                PythonRunner.SpawnShell();
            }
#endif
        }
        [SettingsProvider]
        static SettingsProvider CreatePythonSettingsProvider()
        {
            return new AssetSettingsProvider("Project/Python Scripting", () => PythonSettings.Instance);
        }
    }
}
