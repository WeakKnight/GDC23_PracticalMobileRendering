using UnityEngine;
using System.IO;

namespace UnityEditor.Scripting.Python.Packages
{
    internal class LoadPipRequirements
    {
        private const string k_sessionSettingPrefix = "PythonForUnity_";
        private const string k_sessionPipRequirements = k_sessionSettingPrefix + "PipRequirements";
        
        internal const string k_onStartup = k_sessionSettingPrefix + "PipRequirementsStartupCheck";

        private const string k_packagesUptoDateMessage = "Everything up-to-date";

        internal static void LoadRequirements()
        {
            var startupCheck = SessionState.GetBool(k_onStartup, true);
            if (!startupCheck)
            {
                return;
            }

            // Load the requirements as we just opened Unity and haven't loaded them yet

            // make sure that this is not called again on domain reload
            SessionState.SetBool(k_onStartup, false);

            // check that the requirements file exists
            if (!File.Exists(PythonSettings.kPipRequirementsFile))
            {
                return;
            }

            string output = Packages.PipPackages.UpdatePackages(PythonSettings.kPipRequirementsFile);
            if (!string.IsNullOrEmpty(output) && !output.Trim().Equals(k_packagesUptoDateMessage))
            {
                Debug.Log("The Project's following Python packages have been updated:\n" + output);
            }

            // save the contents of the requirements file for the session so that we can notify
            // the user if they need to restart Unity to update their pip packages.
            var pipRequirementsContents = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            SessionState.SetString(k_sessionPipRequirements, pipRequirementsContents);
        }
    }
}