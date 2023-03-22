using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Python.Runtime;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Scripting.Python.Tests")]

namespace UnityEditor.Scripting.Python.Packages
{
    public class PipPackages
    {

        private static readonly string PipPath = Path.GetFullPath("Packages/com.unity.scripting.python/Editor/PipPackages");
        private static readonly string updatePackagesScript = Path.Combine(PipPath, "update_packages.py");
        private static readonly string compiledRequirementsPath = $"{Directory.GetCurrentDirectory()}/Temp/compiled_requirements.txt";

        static void ProgressBarHelper (Process process, string title, string info)
        {
                float progress = 0.25f;
                bool reverse = false;
                while (!process.HasExited)
                {
                    if (!reverse)
                    {
                        // progress bar "grows"
                        progress += 0.01f;
                    }
                    else
                    {
                        // progress bar shrinks
                        progress -= 0.01f;
                    }
                    if (progress > 0.85f)
                    {
                        // we've reached the "max growth", now shrink
                        reverse = true;
                    }
                    else if (progress < 0.15f)
                    {
                        // we've reached the "max shrinkage", now grow.
                        reverse = false;
                    }
                    EditorUtility.DisplayProgressBar(title, info, progress);
                    
                    // sleep for about a frame
                    Thread.Sleep(17/*ms*/);
                }
                EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Compiles the full requirements (dependencies included) of a given
        /// requirements file.
        /// 
        /// Returns the process' retcode.
        /// </summary>
        static int CompileRequirements(string requirementsFile, string pythonInterpreter)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                var args = new List<string>();
                args.Add("-m");
                args.Add("piptools");
                args.Add("compile");
                args.Add("-o");
                args.Add($"\"{compiledRequirementsPath}\"");
                args.Add($"\"{requirementsFile}\"");

                using (var process = PythonRunner.SpawnPythonProcess(args, redirectOutput:true))
                {

                    ProgressBarHelper(process, "Compiling requirements", "Pip requirements compilation in progress");
                    process.WaitForExit();
                    // get the retcode after process has finished
                    int retcode = process.ExitCode;
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    // inform the user only on failure, the pip install will inform
                    // the user of the installed packages
                    if(retcode != 0)
                    {
                        var strbuilder = new StringBuilder();
                        strbuilder.AppendLine("Error while compiling requirements:");
                        foreach (var line in Regex.Split(errors, "\r\n|\n|\r"))
                        {
                            if (!line.StartsWith("#"))
                            {
                                strbuilder.AppendLine(line);
                            }
                        }
                        Debug.LogError(strbuilder.ToString());
                    }
                    return retcode;
                }
            }
        }

        /// <summary>
        /// Execute the Python script `update_packages.py`: using a package requirements.txt file, it will install
        /// needed packages and uninstall unused ones
        /// </summary>
        /// <param="requirementsFile">Path to the requirements.txt file</param>
        /// <param="pythonInterpreter">Path to the Python interpreter on wich we run the update packages script</param>
        /// <returns>Standard output of the script</returns>
        private static string UpdatePackagesHelper(string requirementsFile,
                                                    string pythonInterpreter)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                var args = new List<string>();
                args.Add($"\"{updatePackagesScript}\"");
                args.Add($"\"{compiledRequirementsPath}\"");
                // Only take packages in our site-packages, don't pick up the ones installed on the system.
                args.Add($"\"{Path.GetFullPath(PythonSettings.kSitePackagesRelativePath)}\"");

                using (var process = PythonRunner.SpawnPythonProcess(args, redirectOutput:true))
                {
                    ProgressBarHelper(process, "Updating required pip packages", "This could take a few minutes");

                    process.WaitForExit();
                    // get the retcode after process has finished
                    int retcode = process.ExitCode;
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(errors))
                    {
                        var pipWarningStringBuilder = new StringBuilder();
                        // Split errors lines to filter them individually
                        foreach (var line in Regex.Split(errors, "\r\n|\n|\r"))
                        {
                            if (IsInterestingWarning(line))
                            {
                                pipWarningStringBuilder.AppendLine(line);
                            }
                        }

                        if (pipWarningStringBuilder.Length > 0)
                        {
                            Debug.LogError(pipWarningStringBuilder.ToString());
                        }
                    }
                    return output;
                }
            }
        }

        internal static string UpdatePackages(string requirementsFile,
                                              string pythonInterpreter = PythonSettings.kDefaultPython)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                // As piptools sync is made to work only with requirements that have been
                // gemerated by piptools compile, use their workflow: compile the
                // user-supplied requirements, which may or may not contain dependents.
                if (CompileRequirements(requirementsFile, pythonInterpreter) != 0)
                {
                    return string.Empty;
                }
                return UpdatePackagesHelper(requirementsFile, pythonInterpreter);
            }
        }

        /// <summary>
        /// Returns true if the warning is interesting. Use this to filter output from pip.
        /// Certain common warnings from pip are uninteresting.
        /// Returns false if the warning is empty.
        /// </summary>
        internal static bool IsInterestingWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false; // message is not a pip warning, but there is no point to display it
            }
            
            const string notOnPath = @"WARNING:.+ installed in.+ which is not on PATH\.";
            const string considerAddingToPath = "Consider adding this directory to PATH";
            const string newPipVersionAvailable = @"WARNING: You are using pip version \d+\.\d+(.\d+)?;.+version \d+\.\d+(.\d+)? is available\.";
            const string considerPipUpgrade = @"You should consider upgrading via the.+-m pip install --upgrade pip' command\.";
            const string outOfTreeDeprecation = @"DEPRECATION: A future pip version will change local packages to be built in-place without first copying to a temporary directory.";
            const string outOfTreeDeprecation2 = @"\w*pip 21.3 will remove support for this functionality. You can find discussion regarding this at https://github.com/pypa/pip/issues/7555.\w*";
            
            string[] patternsToFilterOut = {notOnPath, considerAddingToPath, newPipVersionAvailable, considerPipUpgrade, outOfTreeDeprecation, outOfTreeDeprecation2};

            int anyMatch = patternsToFilterOut.Select(pattern => Regex.IsMatch(message, pattern))
                .Where(match => match).Count();

            return anyMatch == 0;
        }

        static string GetRequirements ()
        {
            if (!File.Exists(PythonSettings.kPipRequirementsFile))
            {
                return string.Empty;
            }
            // TODO: prevent reading an excessively large file?
            var contents =  File.ReadAllText(PythonSettings.kPipRequirementsFile);
            return contents;
        }

        /// <summary>
        /// Adds python pacakges via pip. Also adds the packages to the project's 
        /// requirements.txt file if the same pacakge, at the same version is
        /// not already present and installs and/or updates the packages.
        /// If already present, no operations are performed; it is safe to
        /// add the same package multiple times.
        /// 
        /// This function has a side effect of removing installed pip packages
        /// that are not specified in the requirements.txt file or in the computed
        /// requirements (as a dependency)
        /// </summary>
        /// <param name="_packages">An enumerable of the packages to add</param>
        /// <returns>Returns true if the packages are successfully or already installed
        /// returns false on failure. </returns>
        public static bool AddPackages(IEnumerable<string> packages)
        {
            var packagesToAdd = new List<string>();
            var curReqs = GetRequirements();
            // https://peps.python.org/pep-0426/#name
            // All comparisons of distribution names MUST be case insensitive, and MUST consider hyphens and underscores to be equivalent
            // As pip tools cannonicalize hyphens to underscores and all letters to lowercase, we'll do the same.
            System.Func<string, string> pep426transform = (string input) => {
                var output = new StringBuilder(input.Length);
                foreach(var @char in input)
                {
                    if (@char == '-')
                    {
                        output.Append('_');
                    }
                    else{
                        output.Append(char.ToLower(@char))  ;
                    }
                }
                return output.ToString();
            };
            var curReqSet = new System.Collections.Generic.HashSet<string>(Regex.Split(curReqs, "\r\n|\n|\r").Select(pep426transform));

            foreach(var package in packages)
            {
                var package426 = pep426transform(package);
                if (!curReqSet.Contains(package426))
                {
                    packagesToAdd.Add(package); // write to file as the user inputted the name
                    curReqSet.Add(package426); // but add the transformed name to the set.
                }
            }
            if (packagesToAdd.Count() == 0)
            {
                // nothing to do.
                return true;
            }

            // If there are packages to add, create a temporary requirements file.

            string tempReqPath = Path.GetDirectoryName(Application.dataPath) + "/Temp/temp_requirements.txt";
            if(File.Exists(tempReqPath))
            {
                try
                {
                    File.Delete(tempReqPath);
                }
                catch
                {
                    throw;
                }
            }
            // a FileStream has no WriteLine method, StreamWriter has
            using (var tempReqFile = new StreamWriter(File.Create(tempReqPath)))
            {
                tempReqFile.Write(curReqs);
                foreach (var package in packagesToAdd)
                {
                    tempReqFile.WriteLine(package);
                }
            }

            string packagesString = string.Empty;
            if (packages.Count() > 1)
            {
                packagesString = "Python packages [" + string.Join(",", packagesToAdd) + "]";
            }
            else 
            {
                packagesString = $"Python package {packagesToAdd.First()}";
            }

            var res = CompileRequirements(tempReqPath, PythonSettings.kDefaultPython);
            if (res == 0)
            {
                if(File.Exists(PythonSettings.kPipRequirementsFile))
                {
                    // failure to move the file means failure of this
                    File.Delete(PythonSettings.kPipRequirementsFile);
                }
                File.Move(tempReqPath, PythonSettings.kPipRequirementsFile);
                var output = UpdatePackagesHelper(PythonSettings.kPipRequirementsFile, PythonSettings.kDefaultPython);
                Debug.Log("The Project's following Python packages have been installed/updated:\n" + output);
            }
            else
            {
                
                Debug.LogError($"Failed to install {packagesString}.");
                return false;
            }

            Debug.Log($"Successfully installed {packagesString}.");
            return true;
        }

        /// <summary>
        /// Convenience function to install a single package. 
        /// Wraps `AddPackages`.
        /// </summary>
        /// <param name="package">The package to add</param>
        /// <returns>Returns true if the package is successfully or already installed
        /// returns false on failure.</returns>
        public static bool AddPackage(string package)
        {
            return AddPackages(new string[] {package});
        }
    }
}
