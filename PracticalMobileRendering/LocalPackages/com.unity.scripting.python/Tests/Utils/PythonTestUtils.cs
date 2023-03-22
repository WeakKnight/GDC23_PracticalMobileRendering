using System;
using System.Collections;

using UnityEngine;
using Python.Runtime;
using NUnit.Framework;


namespace UnityEditor.Scripting.Python.Tests
{

    /// <summary>
    /// Implement waiting methods. We leverage the UnityTest attribute
    /// for our waits. Contrary to regular C#, we can nest `yield return` calls
    ///  and get the "intuitive" behaviour.
    /// </summary>
    public static class PythonTestUtils
    {
        /// <summary>
        /// Because UnityTests can only yield return null (or an IEnumerator
        /// yield returning null), we cannot use Unity's WaitForSeconds. Make
        /// our own version.
        /// If a condition function was given, if the condition never evaluated
        /// to True during the loop, raises a Assert.Fail
        /// </summary>
        /// <param name="waitTime">The interval of time to wait for, in seconds.</param>
        /// <param name="condition">A function returning a boolean. If the function returns true, exit early.</param>
        /// <returns></returns>
        public static IEnumerator WaitForSecondsDuringUnityTest(double waitTime, Func<bool> condition = null)
        {
            double initTime = EditorApplication.timeSinceStartup;
            double elapsedTime = 0.0;
            while ( elapsedTime < waitTime)
            {
                elapsedTime = EditorApplication.timeSinceStartup - initTime;
                if(condition != null && condition())
                {
                    yield break;
                }
                yield return null;
            }

            if(condition != null)
            {
                Assert.Fail("Condition in the loop never evaluated to True");
            }
        }

        /// <summary>
        /// Returns an IEnumerator to await the end of process. Asserts if the
        /// timeout is reached.
        /// </summary>
        /// <param name="process">The process to wait on. A python popen object</param>
        /// <param name="timeout">The maximum length of time for to wait the
        /// process to end, in seconds</param>
        /// <returns></returns>
        public static IEnumerator WaitForProcessEnd(System.Diagnostics.Process process, double timeout = 5.0)
        {
            double initTime = EditorApplication.timeSinceStartup;
            double elapsedTime = 0.0;
            while (elapsedTime < timeout)
            {
                elapsedTime = EditorApplication.timeSinceStartup - initTime;
                // popen.poll() returns None if process hasn't finished yet
                if(process.HasExited)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.That(elapsedTime, Is.LessThan(timeout));
        }
    }
}

