using System.IO;
using UnityEngine;
using NUnit.Framework;

namespace UnityEditor.Scripting.Python.Tests
{
    /// <summary>
    /// Base class for tests requiring setup/cleanup such as deleting temporary files.
    /// </summary>
    public class PipTestBase
    {
        const string k_temporaryFolderPrefix = "_safe_to_delete";
        protected string TemporaryFolder { get { return Path.Combine(Application.dataPath, k_temporaryFolderPrefix); } }

        [SetUp]
        public virtual void Init()
        {
            Directory.CreateDirectory(TemporaryFolder);
        }

         [TearDown]
         public virtual void Term()
         {
            // if the folder exists in the AssetDatabase, remove it
            // with AssetDatabase to avoid "Files not cleaned up after test" warnings.
            var tempFolderRelPath = "Assets/" + k_temporaryFolderPrefix;
            if (AssetDatabase.IsValidFolder(tempFolderRelPath))
            {
                AssetDatabase.DeleteAsset(tempFolderRelPath);
            }

            if (Directory.Exists(TemporaryFolder))
             {
                 Directory.Delete(TemporaryFolder, recursive: true);
             }
        }
    }
}