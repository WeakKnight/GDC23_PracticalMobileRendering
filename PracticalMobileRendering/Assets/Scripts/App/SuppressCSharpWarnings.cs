#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

internal static class SuppressCSharpWarnings
{
    [InitializeOnLoadMethod]
    private static void SuppressWarnings()
    {
        string[] ignoreWarnings = new[]
        {
            "/nowarn:0168",    // The variable 'var' is declared but never used
            "/nowarn:0162",     // Unreachable code detected
            "/nowarn:0618"
        };

        BuildTargetGroup[] buildTargets = new[]
        {
            BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS, EditorUserBuildSettings.selectedBuildTargetGroup
        };

        foreach (var target in buildTargets)
        {
            if (target == BuildTargetGroup.Unknown)
                continue;

            List<string> args = PlayerSettings.GetAdditionalCompilerArgumentsForGroup(target).ToList();
            bool hasChanges = false;

            foreach (var ignoreWarning in ignoreWarnings)
            {
                if (args.Contains(ignoreWarning) == false)
                {
                    args.Add(ignoreWarning);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                PlayerSettings.SetAdditionalCompilerArgumentsForGroup(target, args.ToArray());
            }
        }
    }
}

#endif