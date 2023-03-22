using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Scripting.Python;
using Python.Runtime;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.TestTools;

namespace UnityEditor.Scripting.Python.Tests.Regular
{
    internal class PythonRunnerTests
    {

        private static string TestsPath = Path.Combine(Path.GetFullPath("Packages/com.unity.scripting.python"), "Tests", "PythonRunner");
        private static Regex PythonExceptionRegex = new Regex(@"Python\.Runtime\.PythonException");

        [Test]
        public void TestPythonException()
        {
            var PIEtype = typeof(PythonInstallException);
            Assert.Throws(PIEtype,
                    () => throw new PythonInstallException());
            Assert.Throws(PIEtype,
                    () => throw new PythonInstallException("yo"));
            Assert.Throws(PIEtype,
                    () => throw new PythonInstallException("yo", new System.NullReferenceException()));

            PythonInstallException xcp;

            xcp = new PythonInstallException();
            Assert.That(xcp.Message, Does.StartWith("Python Scripting"));

            xcp = new PythonInstallException("yo");
            Assert.That(xcp.Message, Does.StartWith("Python Scripting"));

            xcp = new PythonInstallException("yo", new System.NullReferenceException());
            Assert.That(xcp.Message, Does.StartWith("Python Scripting"));
        }


        [Test]
        public void TestRunString()
        {
            // check with null and empty string
            PythonRunner.RunString(null);
            PythonRunner.RunString("");

            // Something valid
            string goName = "Bob";
            PythonRunner.RunString($"import UnityEngine;obj = UnityEngine.GameObject();obj.name = '{goName}'");
            var obj = GameObject.Find(goName);
            Assert.That(obj, Is.Not.Null);

            // Same code, with obvious error
            Assert.Throws<PythonException>( () =>
                {
                    PythonRunner.RunString($"import UnityEngineobj = UnityEngine.GameObject();obj.name = '{goName}'");
                } );

            // Testing scopeName parameter
            string scopeName = "__main__";
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, scopeName);
            PythonRunner.RunString("import UnityEngine; UnityEngine.Debug.Log(__name__)", scopeName);

            scopeName = "unity_python";
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, scopeName);
            PythonRunner.RunString("import UnityEngine; UnityEngine.Debug.Log(__name__)", scopeName);

            // No NameError with list comprehension when setting __name__
            scopeName = "__main__";
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, scopeName);
            PythonRunner.RunString("import UnityEngine;items=[1,2,3];[x for x in items if isinstance(x, UnityEngine.GameObject)];UnityEngine.Debug.Log(__name__)",
                                   scopeName);
        }

        [Test]
        public void TestRunFile()
        {
            string validFileName = Path.Combine(TestsPath, "testPythonFile.py");
            string fileWithErrorsName = Path.Combine(TestsPath, "testPythonFileWithError.py");
            string nonExistantFile = Path.Combine(TestsPath, "doesNotExist.py");
            string notAPythonFile = Path.Combine(TestsPath, "notAPythonFile.txt");

            // null file
            Assert.Throws<ArgumentNullException>( () =>
                {
                    PythonRunner.RunFile(null);
                } );

            // does not exist
            Assert.Throws<FileNotFoundException>( () =>
                {
                    PythonRunner.RunFile(nonExistantFile);
                } );

            // not a python file. Throws syntax error. File must not be empty
            Assert.Throws<PythonException>( () =>
                {
                    PythonRunner.RunFile(notAPythonFile);
                } );

            // Indentation error
            Assert.Throws<PythonException>( () =>
                {
                    PythonRunner.RunFile(fileWithErrorsName);
                } );

            // finally, a good, valid, file
            // Also testing scopeName parameter
            string scopeName = "__main__";
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, scopeName);            
            PythonRunner.RunFile(validFileName, scopeName);
            // should create a game object named Alice
            var go = GameObject.Find("Alice");
            Assert.That(go, Is.Not.Null);
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void TestPackageVersionStrings()
        {
            Assert.That(PythonSettings.Version, Does.Match(@"\d+\.\d+\.\d+(\-(pre|exp)\.\d+)?"));
            Assert.That(PythonSettings.PythonNetVersion, Does.Match(@"[\d.\d.\d.\d]"));
        }

        /// <summary>
        /// Tests that the "Library/ScriptAssemblies" folder has been added to the
        /// sys.path and that the DLLs from the folder can be loaded.
        /// </summary>
        [Test]
        public void TestScriptAssembliesInSysPath ()
        {
            PythonRunner.EnsureInitialized();
            
            using (Py.GIL())
            {
                dynamic sysmod = Py.Import("sys");
                PyList syspath = sysmod.path;
                bool found = false;
                foreach (var path in syspath)
                {
                    if (path.ToString() == Path.GetFullPath("Library/ScriptAssemblies").Replace("\\", "/"))
                    {
                        found = true;
                        break;
                    }
                }
                Assert.True(found);

                // Next, try to load PythonRunner from python
                dynamic clrmod = Py.Import("clr");
                clrmod.AddReference("Unity.Scripting.Python.Editor");
                dynamic pythonRunnerMod = Py.Import("UnityEditor.Scripting.Python");
                string version = pythonRunnerMod.PythonRunner.PythonVersion;
                Assert.That(version, Is.EqualTo(PythonRunner.PythonVersion));
            }
        }

        // Helper for TestProjectAssemblyReferencesAdded.
        // Tries to import System and run a command with PythonRunner.RunString().
        // Called once before and after domain reload.
        private void TestImportSystemHelper()
        {
            var importSystem = "import System;System.IO.File.Exists('test.txt')";
            PythonRunner.RunString(importSystem);

            // System has an extra m, so this should throw an exception
            var importSystemInvalid = "import Systemm;System.IO.File.Exists('test.txt')";
            Assert.Throws<PythonException>(() =>
            {
                PythonRunner.RunString(importSystemInvalid);
            });
        }

        [UnityTest]
        public IEnumerator TestProjectAssemblyReferencesAdded()
        {
            // Test that project assemblies are automatically added,
            // and that it is not necessary to add them through clr.AddReference().
            TestImportSystemHelper();

            // start and stop playmode to force a domain reload
            Assert.False(Application.isPlaying);
            yield return new EnterPlayMode();
            Assert.True(Application.isPlaying);
            yield return new ExitPlayMode();
            Assert.False(Application.isPlaying);

            TestImportSystemHelper();
        }

        [Test]
        public void TestUndoRedirectStdout()
        {
            // open python console
            PythonConsoleWindow.ShowWindow();

            var prevContents = PythonConsoleWindow.s_window.m_outputContents;
            PythonConsoleWindow.s_window.m_outputContents = "";

            var msg = "hello world";
            var pythonCmd = string.Format("print ('{0}')", msg);
            PythonRunner.RunString(pythonCmd);
            var output = PythonConsoleWindow.s_window.m_outputContents;

            Assert.That(output, Is.EqualTo(msg + "\n"));

            PythonRunner.UndoRedirectStdout();


            PythonConsoleWindow.s_window.m_outputContents = "";

            // stdout should no longer be redirected
            PythonRunner.RunString(pythonCmd);
            output = PythonConsoleWindow.s_window.m_outputContents;

            Assert.That(output, Is.Null.Or.Empty);

            PythonConsoleWindow.s_window.m_outputContents = prevContents;
            PythonConsoleWindow.s_window.Close();

            // redo stdout redirection
            PythonRunner.RedirectStdout();
        }

        [Test]
        public void TestIsPythonLibraryLoaded()
        {
            PythonRunner.EnsureInitialized();
            var result = PythonRunner.IsPythonLibraryLoaded();
            Assert.That(result, Is.True);
        }

        [Test]
        public void TestConvertVersionToTuple()
        {
            var invalidVersions = new string[]
            {
                "2.0",
                "a.0.0",
                "1.b.0",
                "3.4.d-pre.1",
                "1.9.0-preview.2",
                "1.2.0-pre.f"
            };
            foreach (var version in invalidVersions)
            {
                Assert.Throws<PythonInstallException>(() =>
                {
                    PythonRunner.ConvertVersionToTuple(version);
                    Debug.LogError($"Unexpected valid version '{version}'");
                });
            }

            var prereleaseVersion = "1.1.1-pre.4";
            var result = PythonRunner.ConvertVersionToTuple(prereleaseVersion);
            Assert.That(result, Is.EqualTo((1, 1, 1, PythonRunner.BinariesPackageReleaseType.kPreRelease, 4)));

            var expVersion = "2.4.6-exp.2";
            result = PythonRunner.ConvertVersionToTuple(expVersion);
            Assert.That(result, Is.EqualTo((2, 4, 6, PythonRunner.BinariesPackageReleaseType.kExperimental, 2)));

            var preVersion = "2.1.7-pre.1";
            result = PythonRunner.ConvertVersionToTuple(preVersion);
            Assert.That(result, Is.EqualTo((2, 1, 7, PythonRunner.BinariesPackageReleaseType.kPreRelease, 1)));

            var releaseVersion = "3.5.7";
            result = PythonRunner.ConvertVersionToTuple(releaseVersion);
            Assert.That(result, Is.EqualTo((3, 5, 7, PythonRunner.BinariesPackageReleaseType.kRelease, 0)));
        }

        [Test]
        public void TestCanInstallPythonCheck()
        {
            // In order to have this function return true, python would need to not be installed
            // or loaded. However, this would require restarting Unity which is out of scope for this test.
            // Instead test all the cases where the function should return false.

            // With 0 versionStatus correct package is already installed, nothing to do
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: 0, localPackage: false), Is.False);
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: 0, localPackage: true), Is.False);

            // With versionStatus < 0, needsDowngrade, but don't do so immediately
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: -1, localPackage: false), Is.False);
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: -1, localPackage: true), Is.False);

            // With versionStatus > 0, needs upgrade but don't do so immediately
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: 1, localPackage: false), Is.False);
            Assert.That(PythonRunner.CanInstallPythonCheck(versionStatus: 1, localPackage: true), Is.False);
        }

#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
        [Test]
        public void TestSpawnShell()
        {
            PythonRunner.SpawnShell();
        }
#endif

        [Test]
        public void TestLaunchProcess()
        {
            using (Process proc = PythonRunner.SpawnProcess(PythonSettings.kDefaultPythonFullPath, new List<string> {"-c", @"import sys;print('test')"}))
            {
                Assert.That(proc, Is.Not.Null);
            }

#if UNITY_EDITOR_WIN
            string pip = Path.GetFullPath(PythonSettings.kDefaultPythonDirectory) + "/Scripts/pip.bat";
#else
            string pip = Path.GetFullPath(PythonSettings.kDefaultPythonDirectory) + "/bin/pip";
#endif
            using (Process proc = PythonRunner.SpawnProcess(pip, new List<string> {"--version"} ))
            {
                Assert.That(proc, Is.Not.Null);
                proc.WaitForExit(10000);
                Assert.That(proc.ExitCode, Is.EqualTo(0));
            }
        }

        [Test]
        public void TestLaunchPythonProcess()
        {
            using (Process proc = PythonRunner.SpawnPythonProcess(showWindow: true))
            {
                Assert.That(proc, Is.Not.Null);
                // This starts the interpreter in interactive mode. Kill it so it doesn't
                // waste a handle.
                proc.Kill();
            }

            using (Process proc = PythonRunner.SpawnPythonProcess(new List<string> {"-c", "\"import sys;print('test')\""}))
            {
                Assert.That(proc, Is.Not.Null);
                proc.WaitForExit(10000);
                Assert.That(proc.ExitCode, Is.EqualTo(0));
            }

            using (Process proc = PythonRunner.SpawnPythonProcess(new List<string> {"-m", "pip", "--version"}))
            {
                Assert.That(proc, Is.Not.Null);
                proc.WaitForExit(10000);
                Assert.That(proc.ExitCode, Is.EqualTo(0));
            }
        }
    }
}

