using UnityEngine;
using NUnit.Framework;
using System.IO;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools;

namespace UnityEditor.Scripting.Python.Tests.Regular
{
    public class BuildTest
    {
        private const BuildTargetGroup k_buildTargetGroup = BuildTargetGroup.Standalone;

        private const string k_temporaryFolderName = "_safe_to_delete_build";

        private string BuildFolder { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), k_temporaryFolderName); } }

        private string BuildTestScenePath { get { return $"Assets/{k_temporaryFolderName}"; } }

        [SetUp]
        public void Init()
        {
            // Create build folder
            Directory.CreateDirectory(BuildFolder);

            // Create temporary scene folder
            Directory.CreateDirectory(BuildTestScenePath);
        }

        [TearDown]
        public void Term()
        {
            // delete build folder
            if (Directory.Exists(BuildFolder))
            {
                Directory.Delete(BuildFolder, recursive: true);
            }

            // if the folder exists in the AssetDatabase, remove it
            // with AssetDatabase to avoid "Files not cleaned up after test" warnings.
            if (AssetDatabase.IsValidFolder(BuildTestScenePath))
            {
                AssetDatabase.DeleteAsset(BuildTestScenePath);
            }

            if (Directory.Exists(BuildTestScenePath))
            {
                Directory.Delete(BuildTestScenePath, recursive: true);
            }
        }

        [Test, RequirePlatformSupport(new BuildTarget[] { BuildTarget.StandaloneWindows64 })]
        public void TestBuildPlayer_StandaloneWindows64()
        {
            TestBuildPlayer(BuildTarget.StandaloneWindows64, "test.exe");
        }

        [Test, RequirePlatformSupport(new BuildTarget[] { BuildTarget.StandaloneOSX })]
        public void TestBuildPlayer_StandaloneOSX()
        {
            TestBuildPlayer(BuildTarget.StandaloneOSX, "test.app");
        }

        [Test, RequirePlatformSupport(new BuildTarget[] { BuildTarget.StandaloneLinux64 })]
        public void TestBuildPlayer_StandaloneLinux64()
        {
            TestBuildPlayer(BuildTarget.StandaloneLinux64, "test.x86_64");
        }

        void TestBuildPlayer(BuildTarget buildTarget, string buildName)
        {
            // create simple test scene
            var scene = SceneManagement.EditorSceneManager.NewScene(SceneManagement.NewSceneSetup.DefaultGameObjects, SceneManagement.NewSceneMode.Single);
            var scenePath = Path.Combine(BuildTestScenePath, "test.unity");
            SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.locationPathName = Path.Combine(BuildFolder, buildName);
            options.target = buildTarget;
            options.targetGroup = k_buildTargetGroup;
            options.scenes = new string[] { scenePath };

            var report = BuildPipeline.BuildPlayer(options);

            // Check that build completes without errors
            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded));
            Assert.That(report.summary.totalErrors, Is.EqualTo(0));
            Assert.That(report.summary.outputPath, Is.Not.Null.Or.Empty);
            Assert.That(report.summary.outputPath, Does.Exist);
        }
    }
}