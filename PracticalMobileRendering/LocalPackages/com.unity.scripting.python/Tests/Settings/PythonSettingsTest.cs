using System.IO;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Text.RegularExpressions;

namespace UnityEditor.Scripting.Python.Tests.Regular
{
    public class PythonSettingsEditorTest : PipTestBase 
    {
        [Test]
        public void TestShortPythonVersion()
        {
            // test null or empty
            Assert.That(PythonSettingsEditor.ShortPythonVersion(null), Is.Empty);
            Assert.That(PythonSettingsEditor.ShortPythonVersion(""), Is.Empty);

            // test invalid
            var longVersion = "invalidString";
            Assert.That(PythonSettingsEditor.ShortPythonVersion(longVersion), Is.EqualTo(longVersion));

            // test valid
            longVersion = "2.7.16 |Anaconda, Inc.| (default, Mar 14 2019, 16:24:02) \n[GCC 4.2.1 Compatible Clang 4.0.1 (tags/RELEASE_401/final)]";
            Assert.That(PythonSettingsEditor.ShortPythonVersion(longVersion), Is.EqualTo("2.7.16"));
        }

        [Test]
        public void TestSitePackagesChanged()
        {
            if (PythonSettings.SitePackagesChanged)
            {
                Assert.Ignore("Unable to test site-packages changes since they're already changed");
            }

            var settings = PythonSettings.Instance;

            string [] sitePackages = settings.m_sitePackages;

            PythonSettings.Instance.m_sitePackages = new string [] {"asjkfjas"};
            Assert.That(PythonSettings.SitePackagesChanged);

            PythonSettings.Instance.m_sitePackages = sitePackages;
            Assert.That(!PythonSettings.SitePackagesChanged);
        }

        [Test]
        public void TestSave()
        {
            // Just check it doesn't throw
            PythonSettings.Instance.Save();
        }

        [UnityTest]
        public IEnumerator TestSettingsWindow()
        {
            // This test OnGui etc. If there's error messages there's a problem.
            var window = SettingsService.OpenProjectSettings("Project/Python Scripting");
            yield return null;
            window.Repaint();
            yield return null;
            window.Close();
        }
    }
}