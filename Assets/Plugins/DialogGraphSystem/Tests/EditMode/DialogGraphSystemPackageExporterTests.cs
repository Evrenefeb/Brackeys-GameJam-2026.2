using System;
using System.Linq;
using System.Reflection;
using DialogSystem.Editor.Services;
using NUnit.Framework;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphSystemPackageExporterTests
    {
        private static readonly string[] ExpectedExportRoots =
        {
            "Assets/DialogGraphSystem/CHANGELOG.md",
            "Assets/DialogGraphSystem/Definitions",
            "Assets/DialogGraphSystem/DemoScenes",
            "Assets/DialogGraphSystem/Documentation",
            "Assets/DialogGraphSystem/Editor Default Resources",
            "Assets/DialogGraphSystem/Graphs",
            "Assets/DialogGraphSystem/LICENSE.md",
            "Assets/DialogGraphSystem/README.md",
            "Assets/DialogGraphSystem/Resources",
            "Assets/DialogGraphSystem/Scripts",
            "Assets/DialogGraphSystem/Third-Party Notices.txt",
            "Assets/DialogGraphSystem/package.json"
        };

        [Test]
        public void ExportRoots_MatchApprovedCoreAllowlistExactly()
        {
            var actual = InvokePrivate<string[]>("GetExportAssetPaths");

            Assert.That(actual, Is.EqualTo(ExpectedExportRoots));
            Assert.That(actual, Has.None.Contains("Tests"));
            Assert.That(actual, Has.None.Contains("Assets.csc.rsp"));
            Assert.That(actual, Has.None.Contains("/Localization"));
            Assert.That(actual, Has.None.Contains("DialogSystemAIExtension"));
            Assert.That(actual, Has.None.Contains("AiProviders"));
        }

        [Test]
        public void DefaultArtifactFileName_IsExactReleaseName()
        {
            Assert.That(
                InvokePrivate<string>("GetDefaultArtifactFileName"),
                Is.EqualTo("DialogueGraphSystem_3.0.0.unitypackage"));
        }

        [Test]
        public void ManifestPaths_AreSortedFilesWithinApprovedRoots()
        {
            var paths = InvokePrivate<string[]>("GetManifestAssetPaths");

            Assert.That(paths, Is.Not.Empty);
            Assert.That(paths, Is.EqualTo(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray()));
            Assert.That(paths, Has.None.EndsWith(".meta"));
            Assert.That(paths, Has.None.Contains("/Tests/"));
            Assert.That(paths, Has.None.Contains("Assets.csc.rsp"));
            Assert.That(paths.Any(path => path.StartsWith(
                "Assets/DialogGraphSystem/Localization/",
                StringComparison.Ordinal)), Is.False);
            Assert.That(paths.All(path => ExpectedExportRoots.Any(root =>
                string.Equals(path, root, StringComparison.Ordinal) ||
                path.StartsWith(root + "/", StringComparison.Ordinal))), Is.True);
        }

        private static T InvokePrivate<T>(string methodName)
        {
            var method = typeof(DialogGraphSystemPackageExporter).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing exporter method {methodName}.");
            return (T)method.Invoke(null, Array.Empty<object>());
        }
    }
}
