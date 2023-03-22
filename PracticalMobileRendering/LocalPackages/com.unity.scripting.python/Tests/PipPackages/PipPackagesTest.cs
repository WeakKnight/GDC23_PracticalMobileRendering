using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;

using NUnit.Framework;

using Python.Runtime;
using UnityEditor.Scripting.Python;
using UnityEditor.Scripting.Python.Packages;
using static UnityEditor.Scripting.Python.Tests.PythonTestUtils;

// These tests are slow:
// On each test run, the requirements/installed pip packages are cleaned
// in the Setup method.
namespace UnityEditor.Scripting.Python.Tests.Slow
{
    internal class PipTests : PipTestBase
    {

    internal static string TestsPath = Path.Combine(Path.GetFullPath("Packages/com.unity.scripting.python"),
                                                "Tests", "PipPackages");

    // simple test packages. 
    // Foo depends on toml>=0.10
    internal static string TestPackageFoo = Path.Combine(TestsPath, "TestPyPackageFoo").Replace("\\", "/");
    internal const string FooDependency = "tomli_w==0.4";
    // Bar depends on tomli-w==0.4
    internal static string TestPackageBar = Path.Combine(TestsPath, "TestPyPackageBar").Replace("\\", "/");
    internal const string BarDependency = "toml>=0.10";


    // the function unity_python.common.spawn_process.spawn_process_in_environment
    dynamic spawn;

    private bool m_pipRequirementsExists = false;
    private string m_origPipRequirementsContents;

    void ResetPackages()
    {
        File.WriteAllText(PythonSettings.kPipRequirementsFile, "");
        PipPackages.UpdatePackages(PythonSettings.kPipRequirementsFile);
    }

    [OneTimeSetUp]
    public void OneTimeInit()
    {
        if (File.Exists(PythonSettings.kPipRequirementsFile))
        {
            m_pipRequirementsExists = true;
            m_origPipRequirementsContents = File.ReadAllText(PythonSettings.kPipRequirementsFile);
        }
        ResetPackages();
    }

    [OneTimeTearDown]
    public void OneTimeTerm()
    {
        if (m_pipRequirementsExists)
        {
            // make sure original requirements are reset
            File.WriteAllText(PythonSettings.kPipRequirementsFile, m_origPipRequirementsContents);
            SessionState.SetBool(LoadPipRequirements.k_onStartup, true);
            LoadPipRequirements.LoadRequirements();
        }
        else
        {
            // delete file as it didn't exist before the tests ran.
            File.Delete(PythonSettings.kPipRequirementsFile);
        }
    }

    [SetUp]
    public void Setup()
    {
        PythonRunner.EnsureInitialized();
        ResetPackages();
    }

    /// <summary>
    /// Spawns a `python -m pip freeze` subprocess and returns its output
    /// </summary>
    /// <param="pythonInterpreter">Path to the Python interpreter on which we call pip freeze</param>
    /// <param="pythonPath">Override PYTHONPATH with the passed argument; no override if empty string</param>
    /// <returns>Standard output of the pip freeze subprocess</returns>
    internal static string CallPipFreeze(string pythonInterpreter = PythonSettings.kDefaultPython,
                                        string pythonPath = "")
    {
        PythonRunner.EnsureInitialized();
        var args = new List<string>();
        args.Add("-m");
        args.Add("pip");
        args.Add("freeze");

        var envOverride = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(pythonPath))
        {
            envOverride["PYTHONPATH"] = "";
        }

        using (var process = PythonRunner.SpawnPythonProcess(args, envOverride, redirectOutput:true))
        {
            process.WaitForExit();
            // (string output, string errors) = (res[0], res[1]);
            string errors = process.StandardError.ReadToEnd();
            if (errors != null && errors.Length > 0)
            {
                UnityEngine.Debug.LogError(errors);
            }

            return  process.StandardOutput.ReadToEnd();
        }
    }

    [UnityTest]
    public IEnumerator TestUpdatePackagesWithOneUpgrade()
    {
        using (var env = new PyTestVenv())
        {
            // Install toml 0.9.0 into the py venv
            // FIXME: we need to use `--use-deprecated=html5lib` otherwise we get a error about non-conform
            // html headers
            var args = new List<string>();
            args.Add("-m");
            args.Add("pip");
            args.Add("install");
            args.Add("--use-deprecated=html5lib");
            args.Add("toml==0.9.0");

            using (var process = PythonRunner.SpawnProcess(env.interpreter, args))
            {
                yield return WaitForProcessEnd(process, 20);
            }

            // Call UpdatePackages with a requirements.txt file containing only a requirement to toml 0.10.0 (and pip-tools)
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_1.txt");
            PipPackages.UpdatePackages(testRequirementFile, env.interpreter);

            // NOTE:
            // in this case, this not pip-tools diff/sync funcs that remove toml 9 for toml 10,
            // it is pip that sees another version of the same package exists and then uninstall
            // the current version before installing the required one

            // Check that the only package in the py venv is toml 0.10.0
            var output = CallPipFreeze(env.interpreter, env.pythonPath);
            Assert.That(output, Does.Contain("toml==0.10.0"));
        }
    }

    [UnityTest]
    public IEnumerator TestUpdatePackagesWithOneDowngrade()
    {
        using (var env = new PyTestVenv())
        {
            // Install toml 0.10.0 into the py venv
            // FIXME: we need to use `--use-deprecated=html5lib` otherwise we get a error about non-conform
            // html headers
            var args = new List<string>();
            args.Add("-m");
            args.Add("pip");
            args.Add("install");
            args.Add("--use-deprecated=html5lib");
            args.Add("toml==0.10.0");
            
            using (var process = PythonRunner.SpawnProcess(env.interpreter, args))
            {
                yield return WaitForProcessEnd(process, 20);
            }

            // Call UpdatePackages with a requirements.txt file containing only a requirement to toml 0.9.0 (and pip-tools)
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_2.txt");
            PipPackages.UpdatePackages(testRequirementFile, env.interpreter);

            // Check that the only package in the py venv is toml 0.9.0

            var output = CallPipFreeze(env.interpreter, env.pythonPath);
            Assert.That(output, Does.Contain("toml==0.9.0"));
        }
    }

    [Ignore("TODO: get this test to be reliable cross-platform by controlling pip (UT-3692)")]
    [UnityTest]
    public IEnumerator TestUpdatePackagesWithSeveralPackages()
    {
        using (var env = new PyTestVenv())
        {
            // Install several packages:
            // numpy & vg have no dependencies
            // UnityPy depends on Brotli colorama lz4 Pillow termcolor
            var args = new List<string>();
            args.Add("-m");
            args.Add("pip");
            args.Add("install");
            args.Add("--use-deprecated=html5lib");
            args.Add("numpy==1.17.5");
            args.Add("vg==1.6.0");
            args.Add("UnityPy==1.2.4.8");

            using (var process = PythonRunner.SpawnProcess(env.interpreter, args))
            {
                yield return WaitForProcessEnd(process, 60);
            }

            // Check installations went as expected, to ensure our test is properly set
            string output = CallPipFreeze(env.interpreter, env.pythonPath);
            // requested packages with specific versions
            Assert.That(output, Contains.Substring("numpy==1.17.5"));
            Assert.That(output, Contains.Substring("vg==1.6.0"));
            Assert.That(output, Contains.Substring("UnityPy==1.2.4.8"));
            // dependent packages, we don't know the version number
            Assert.That(output, Contains.Substring("Brotli"));
            Assert.That(output, Contains.Substring("colorama"));
            Assert.That(output, Contains.Substring("lz4"));
            Assert.That(output, Contains.Substring("Pillow"));
            Assert.That(output, Contains.Substring("termcolor"));
            // we should not have any more packages
            var newLineRegex = new Regex(@"\r\n|\n|\r");
            var lines = newLineRegex.Split(output);
            Assert.That(lines.Length, Is.EqualTo(9)); // 8 package lines + 1 empty line

            args = new List<string>();
            args.Add("-m");
            args.Add("pip");
            args.Add("install");
            args.Add("--use-deprecated=html5lib");
            args.Add("pip==21.2.4");
            args.Add("-U");

            var envOverride = new Dictionary<string, string>();
            envOverride["PYTHONPATH"] = env.pythonPath;

            using (var process = PythonRunner.SpawnProcess(env.interpreter, args, envOverride))
            {
                yield return WaitForProcessEnd(process, 60);
            }

            // Call UpdatePackages with a requirements.txt file containing:
            // numpy==1.18.2
            // vg==1.7.0
            // Brotli==1.0.7
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_3.txt");
            PipPackages.UpdatePackages(testRequirementFile, env.interpreter);

            var output2 = CallPipFreeze(env.interpreter, env.pythonPath);
            Assert.That(output2, Contains.Substring("numpy==1.18.2"));
            Assert.That(output2, Contains.Substring("vg==1.7.0"));
            Assert.That(output2, Contains.Substring("Brotli==1.0.7"));
            var lines2 = newLineRegex.Split(output2);
            Assert.That(lines2.Length, Is.EqualTo(4));
        }
    }

    [Test]
    public void TestIsInterestingWarning()
    {
        string unwantedWarningMsg1 = "Consider adding this directory to PATH or, if you prefer to suppress this warning, use --no-warn-script-location.";
        string unwantedWarningMsg2 = "WARNING: You are using pip version 20.0.2; however, version 20.1 is available.";
        string unwantedWarningMsg3 = "You should consider upgrading via the 'D:\\UnityProjects\\Python 3 - Copy\\Library\\PythonInstall\\python.exe -m pip install --upgrade pip' command.";

        string wantedWarningMsg = "Command \"python setup.py egg_info\" failed with error code 1 in C:\\Users\\foo\\AppData\\Local\\Temp\\pip-install-ws21otxr\\psycopg2\\";

        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg1), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg2), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg3), Is.False);

        Assert.That(PipPackages.IsInterestingWarning(wantedWarningMsg), Is.True);

        // try with null or empty message
        Assert.That(PipPackages.IsInterestingWarning(null), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(""), Is.False);
    }

        [Test]
        public void TestNoPipRequirements()
        {
            // check that everything works without a requirements.txt file.
            if (File.Exists(PythonSettings.kPipRequirementsFile)) {
                File.Delete(PythonSettings.kPipRequirementsFile);
            }
            Assert.That(PythonSettings.kPipRequirementsFile, Does.Not.Exist);

            var output = CallPipFreeze();

            // reset on startup variable to simulate Unity starting up
            SessionState.SetBool(LoadPipRequirements.k_onStartup, true);
            LoadPipRequirements.LoadRequirements();

            // output of pip freeze should be the same
            Assert.That(CallPipFreeze(), Is.EqualTo(output));
        }

        [Test]
        public void TestValidPipRequirements()
        {
            // add a package to the pip requirements
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_2.txt");
            Assert.That(testRequirementFile, Does.Exist);
            string fileContents = File.ReadAllText(testRequirementFile);

            // To avoid uninstalling any packages, if a requirements file already exists,
            // base the test requirements on this file.
            if (m_pipRequirementsExists)
            {
                // TODO: what to test if toml is already installed at the expected version?
                Assert.That(m_origPipRequirementsContents, Does.Not.Contain("toml"), "Is toml already in your requirements.txt file?");
                fileContents += "\n" + m_origPipRequirementsContents;
            }
            Assert.That(fileContents, Is.Not.Null.Or.Empty);

            File.WriteAllText(PythonSettings.kPipRequirementsFile, fileContents);

            // reset on startup variable to simulate Unity starting up
            SessionState.SetBool(LoadPipRequirements.k_onStartup, true);

            LoadPipRequirements.LoadRequirements();

            var packageUpdateRegex = new Regex("The Project's following Python packages have been updated:.*");
            LogAssert.Expect(LogType.Log, packageUpdateRegex);

            // Check that toml was updated to 0.9.0
            var output = CallPipFreeze();
            Assert.That(output, Does.Contain("toml==0.9.0"));

            // Test that the requirements are not reloaded again if they change (only loaded at startup)
            var newFileContents = m_pipRequirementsExists ? m_origPipRequirementsContents : "";
            File.WriteAllText(PythonSettings.kPipRequirementsFile, newFileContents);

            LoadPipRequirements.LoadRequirements();

            // toml should still be there as the requirements were not updated
            output = CallPipFreeze();
            Assert.That(output, Does.Contain("toml==0.9.0"));
        }
    
        // tests that subsequent calls to LoadPipRequirements does not uninstalls dependencies.
        [Test]
        public void TestCompiledRequirements ()
        {
            if (File.Exists(PythonSettings.kPipRequirementsFile))
            {
                File.Delete(PythonSettings.kPipRequirementsFile);
            }
            // tomli-w is canonicalized as tomlis_w by pip
            Assert.That(CallPipFreeze(), Does.Not.Contain("tomli_w"));

            File.WriteAllText(PythonSettings.kPipRequirementsFile, TestPackageFoo);
            PipPackages.UpdatePackages(PythonSettings.kPipRequirementsFile);
            // second call should be a no-op.
            PipPackages.UpdatePackages(PythonSettings.kPipRequirementsFile); 
            Assert.That(CallPipFreeze(), Does.Contain("tomli_w"));
        }

        [Test]
        public void TestAddPackage()
        {
            if (File.Exists(PythonSettings.kPipRequirementsFile))
            {
                File.Delete(PythonSettings.kPipRequirementsFile);
            }

            // CI uses a "@" in the package name to denote the commit ID.
            // pip freeze expresses a path as an URI with "@" encoded to %40
            string TestPackageFooUri = TestPackageFoo.Replace("@", "%40");
            string TestPackageBarUri = TestPackageBar.Replace("@", "%40");
            var currentPackages = CallPipFreeze();
            Assert.That(currentPackages, Does.Not.Contain(TestPackageFooUri));
            Assert.That(currentPackages, Does.Not.Contain(FooDependency));
            Assert.That(currentPackages, Does.Not.Contain(TestPackageBarUri));
            Assert.That(currentPackages, Does.Not.Contain(BarDependency));

            // Adding a package creates the requirement file.
            Assert.True(PipPackages.AddPackage(TestPackageFoo));
            Assert.That(PythonSettings.kPipRequirementsFile, Does.Exist);
            var newPackages = CallPipFreeze();
            Assert.That(newPackages, Does.Contain(TestPackageFooUri));
            
            // Dependencies are installed, but not explicited in the requirements file.
            Assert.That(newPackages, Does.Contain(FooDependency));
            var reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            Assert.That(reqfile, Does.Not.Contain(FooDependency));

            // Adding a new package has the expected effects
            Assert.True(PipPackages.AddPackage(TestPackageBar));
            reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            Assert.That(reqfile, Does.Contain(TestPackageFoo));
            Assert.That(reqfile, Does.Contain(TestPackageBar));
        }

        [Test]
        public void TestSamePackage ()
        {

            Assert.That(CallPipFreeze().ToLower(), Does.Not.Contain("tomli_w"));
            Assert.True(PipPackages.AddPackage("tomli_w"));
            LogAssert.Expect(LogType.Log, new Regex(@".*\s+Collecting tomli-w.*\s*.*\s+Installing collected packages: tomli-w\s+Successfully installed tomli-w.*[.*|\s]*"));
            LogAssert.Expect(LogType.Log, "Successfully installed Python package tomli_w.");
            
            // Adding the same package with equivalent name (see PEP426)
            Assert.True(PipPackages.AddPackage("TOMLI_w"));
            LogAssert.NoUnexpectedReceived();
            // once again, but spongebobified
            Assert.True(PipPackages.AddPackage("ToMlI_w"));
            LogAssert.NoUnexpectedReceived();
            
            var reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            // requirement added only once and is the same as the first added
            Assert.That(reqfile, Is.EqualTo("tomli_w"+System.Environment.NewLine));

            // same package may be added multiple times, as long as they have a different (compatible version)
            Assert.True(PipPackages.AddPackage("TOMLI_w>=0.4"));
            LogAssert.Expect(LogType.Log, "Successfully installed Python package TOMLI_w>=0.4.");
            reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            string doubleTomliwReq = "tomli_w"+System.Environment.NewLine+"TOMLI_w>=0.4"+System.Environment.NewLine;
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));
            
            // but incompatible versions gives an error
            // The pip-compile error check doesn't seem to want to work and always gives a 'Unexpected Log' error.
            // make it so that error logs doesn't fail the test.
            LogAssert.ignoreFailingMessages = true;
            Assert.False(PipPackages.AddPackage("TOMLI_w<0.4"));
            //var badPackageRegex = new Regex(@"Error while compiling requirements:\s*Could not find a version that matches TOMLI_w<0\.4,>=0\.4 \(from.*\sTried:.*\sThere are incompatible versions in the resolved dependencies:\s  TOMLI_w<0\.4 \(from.*\s  TOMLI_w>=0\.4 \(from.*\s  tomli_w \(from.*\s");
            // LogAssert.Expect(LogType.Error, PipPackages.errorHelper);
            // At least, this one does works.
            LogAssert.Expect(LogType.Error, "Failed to install Python package TOMLI_w<0.4.");
            reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            // finally, check that requirements file not modified
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));
        }

        [Test]
        public void TestMultipleSamePackage ()
        {

            Assert.That(CallPipFreeze().ToLower(), Does.Not.Contain("tomli_w"));
            var packagesToInstall = new string[] {"tomli_w", "TOMLI_w", "ToMlI_w", "TOMLI_w>=0.4"};
            Assert.True(PipPackages.AddPackages(packagesToInstall));
            LogAssert.Expect(LogType.Log, new Regex(@".*\s+Collecting tomli-w.*\s*.*\s+Installing collected packages: tomli-w\s*Successfully installed tomli-w.*[.*|\s]*"));
            LogAssert.Expect(LogType.Log, "Successfully installed Python packages [tomli_w,TOMLI_w>=0.4].");
            var reqfile = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            string doubleTomliwReq = "tomli_w"+System.Environment.NewLine+"TOMLI_w>=0.4"+System.Environment.NewLine;
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));
            
            // this is a no-op; req file not modified, no log emitted.
            Assert.True(PipPackages.AddPackages(packagesToInstall));
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));
            LogAssert.NoUnexpectedReceived();
            // for same reasons as above, error message checking fails.
            LogAssert.ignoreFailingMessages = true;
            Assert.False(PipPackages.AddPackage("TOMLI_w<0.4"));
            // At least, this log check does works.
            LogAssert.Expect(LogType.Error, "Failed to install Python package TOMLI_w<0.4.");
            // requirements file not modified
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));

            var contradictoryRequirements = new string[] {"tomli_w<0.4", "TOMLI_w>=0.4", "tomli_w"};
            Assert.False(PipPackages.AddPackages(contradictoryRequirements));
            LogAssert.Expect(LogType.Error, "Failed to install Python packages [tomli_w<0.4].");
            // requirements file not modified
            Assert.That(reqfile, Is.EqualTo(doubleTomliwReq));

        }
    }
}
