using System;
using System.IO;
using UnityEngine;


namespace PMRP
{
    public static class PathUtils
    {
        public static bool CreateDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static string MakePreferred(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string GetFullPath(string path)
        {
            return MakePreferred(Path.GetFullPath(path));
        }

#if UNITY_EDITOR
        public static bool IsRemapped(string origanlPath, out string remappedPath)
        {
            origanlPath = MakePreferred(origanlPath);
            int index = origanlPath.IndexOf("/LocalPackages/", StringComparison.Ordinal);
            if (index >= 0)
            {
                index += "/Local".Length;
                remappedPath = origanlPath.Substring(index, origanlPath.Length - index);
                return true;
            }

            remappedPath = null;
            return false;
        }

        public static string GetActiveSceneDirectory()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                return "";
            else
                return MakePreferred(Path.GetDirectoryName(scene.path));
        }

        public static bool RelativeToUnityProject(string absolutePath, out string relativePath)
        {
            if (!Path.IsPathRooted(absolutePath))
            {
                absolutePath = Path.GetFullPath(absolutePath);
            }

            absolutePath = MakePreferred(absolutePath);

            string projectFolder = Directory.GetParent(Application.dataPath).FullName;
            projectFolder = MakePreferred(projectFolder);

            if (absolutePath.StartsWith(projectFolder))
            {
                relativePath = absolutePath.Substring(projectFolder.Length + 1);
                return true;
            }

            if (IsRemapped(absolutePath, out relativePath))
            {
                return true;
            }

            relativePath = null;
            return false;
        }

        public static string RelativeToUnityProject(string absolutePath)
        {
            string temp;
            if (!RelativeToUnityProject(absolutePath, out temp))
            {
                Debug.Assert(false);
            }
            return temp;
        }
#endif
    }
}