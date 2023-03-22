using UnityEngine;
using Python.Runtime;
using System.IO;
using System.Diagnostics;

using UnityEditor.Scripting.Python;

namespace UnityEditor.Scripting.Python.Tests
{
    /// <summary>
    ///   Create a temp python virtual env
    /// </summary>
    public class PyTestVenv : System.IDisposable
    {
    public string path {get;}
    public string interpreter {get;}
    public string pythonPath {get;}

    public PyTestVenv()
    {
        // Create a temporary Python virtual environment by spawning a subprocess

        path = Path.Combine(Path.GetTempPath(), "py_venv_test");

        var args = new System.Collections.Generic.List<string>();
        args.Add("-m");
        args.Add("venv");
        args.Add($"\"{path}\"");
        using (var proc = PythonRunner.SpawnPythonProcess(args))
        {
            proc.WaitForExit();
        }
#if UNITY_EDITOR_WIN
        pythonPath = Path.Combine(path, "Lib", "site-packages");
        interpreter = Path.Combine(path, "Scripts", "python.exe");
#else
        pythonPath = Path.Combine(path, "lib", "site-packages", $"python{PythonRunner.PythonMajorVersion}.{PythonRunner.PythonMinorVersion}", "site-packages");
        interpreter = Path.Combine(path, "bin", $"python{PythonRunner.PythonMajorVersion}");
#endif
        // Install pip-tools into the py venv
        // FIXME: we need to use `--use-deprecated=legacy-resolver` otherwise we get a error about non-conform
        // html headers
        
        args = new System.Collections.Generic.List<string>();
        args.Add("-m");
        args.Add("pip");
        args.Add("install");
        args.Add("--use-deprecated=legacy-resolver");
        args.Add("pip-tools");

        using (var proc = PythonRunner.SpawnPythonProcess(args))
        {
            proc.WaitForExit();
        }
    }

    public void Dispose()
    {
        // remove temp python virtual env folder from filesystem
        try {
        System.IO.Directory.Delete(path, true);
        }
        catch (System.Exception exc)
        {
        UnityEngine.Debug.Log($"Deletion of the temporary Python virtual environment at {path} failed. Reason: {exc.Message}");
        }

    }
    }
}

