using System.IO;
using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using Python.Runtime;

namespace UnityEditor.Scripting.Python.Samples
{
    /// <summary>
    /// This is a simplified example of how to create a menu item to launch a
    /// PySide2 dialog.
    ///
    /// See also PySideExample.py in the same directory.
    ///
    /// This example shows:
    /// * two ways to call Python code from C#
    /// * how to send Unity events to Python from C#
    /// * how to restart from a domain reload event
    /// </summary>
    public class PySideExample
    {
        const string kStateName = "com.unity.scripting.python.samples.pyside";

        /// <summary>
        /// Hack to get the current file's directory
        /// </summary>
        /// <param name="fileName">Leave it blank to the current file's directory</param>
        /// <returns></returns>
        private static string __DIR__([System.Runtime.CompilerServices.CallerFilePath] string fileName = "")
        {
            return Path.GetDirectoryName(fileName);
        }

        /// <summary>
        /// Menu to launch the client
        /// </summary>
        [MenuItem("Python/Examples/PySide Example")]
        public static void OnMenuClick()
        {
            CreateOrReinitialize();
        }

       static void CreateOrReinitialize()
       {
            // You can manually add the sample directory to your sys.path in
            // the Python Settings under site-packages. Or you can do it
            // programmatically like so.
            string dir = __DIR__();
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                if ((int)sys.path.count(dir) == 0)
                {
                    sys.path.append(dir);
                }
            }

            // Now that we've set up the path correctly, we can import the
            // Python side of this example as a module:
            PythonRunner.RunString(@"
                    import PySideExample

                    PySideExample.create_or_reinitialize()
                    ");

            // We can't register events in Python directly, so register them
            // here in C#:
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.update += OnUpdate;

            //
            // A domain reload happens when you change C# code or when you
            // launch into play mode (unless you selected the option not to
            // reload then).
            //
            // When it happens, your C# state is entirely reinitialized. The
            // Python state, however, remains as it was.
            //
            // To store information about what happened in the previous domain,
            // Unity provides the SessionState. Alternately we could have
            // stored the data in a variable in Python.
            //
            SessionState.SetBool(kStateName, true);
        }

        /// <summary>
        /// Reconnect to the PySide UI upon a domain reload, if we created it
        /// in a previous domain.
        ///
        /// This is also called when Unity starts, in which case we won't have
        /// previously created the PySide UI.
        /// </summary>
        [InitializeOnLoadMethod]
        static void OnDomainLoad()
        {
            if (SessionState.GetBool(kStateName, false))
            {
                CreateOrReinitialize();
            }
        }

        static void OnHierarchyChanged()
        {
            // This is the simplest way to call Python code.
            PythonRunner.RunString(@"
                    import PySideExample
                    PySideExample.update_camera_list()
                    ");
        }

        static void OnUpdate()
        {
            // This is another way to call Python, handy if you want to mix and match
            // languages. Best practice: don't store references to objects from Python
            // longer than you need to -- let them be garbage collected.
            //
            // If you have unexplained crashes when running in this mode, often
            // it's because you forgot to take the GIL.
            using (Py.GIL())
            {
                dynamic module = Py.Import("PySideExample");
                module.on_update();
            }
        }
    }
}
