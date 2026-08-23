using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogPublicInformationTests
    {
        private const string CatalogTypeName =
            "DialogSystem.EditorTools.PublicInformation.DialogPublicInformationCatalog, DialogSystem.Editor";

        [Test]
        public void Catalog_DefinesBundledCopiesForEveryPublicInformationTopic()
        {
            var catalogType = RequireCatalogType();
            var entries = GetEntries(catalogType).Cast<object>().ToArray();

            Assert.That(entries, Has.Length.GreaterThanOrEqualTo(6));
            Assert.That(entries.Select(GetStringProperty("Id")), Is.Unique);

            foreach (var entry in entries)
            {
                var onlineUrl = GetStringProperty("OnlineUrl")(entry);
                var bundledAssetPath = GetStringProperty("BundledAssetPath")(entry);
                var bundledActionLabel = GetStringProperty("BundledActionLabel")(entry);

                Assert.That(onlineUrl, Does.StartWith("https://"));
                Assert.That(bundledActionLabel, Is.EqualTo("View Bundled Copy"));
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(bundledAssetPath), Is.Not.Null,
                    $"Missing bundled TextAsset for {bundledAssetPath}.");
            }
        }

        [Test]
        public void Resolve_UsesOnlineContentWhenRequestSucceeds()
        {
            var result = Resolve("documentation", true, "Online documentation", string.Empty);

            Assert.That(GetBoolProperty(result, "IsBundled"), Is.False);
            Assert.That(GetStringProperty(result, "Content"), Is.EqualTo("Online documentation"));
        }

        [TestCase(false, "", "Connection failed")]
        [TestCase(false, "", "Request timed out")]
        [TestCase(true, "", "Empty response")]
        public void Resolve_UsesBundledTextForFailureTimeoutAndEmptyResponses(
            bool onlineSucceeded,
            string onlineContent,
            string failureReason)
        {
            var result = Resolve("documentation", onlineSucceeded, onlineContent, failureReason);

            Assert.That(GetBoolProperty(result, "IsBundled"), Is.True);
            Assert.That(GetStringProperty(result, "Content"), Does.Contain("Dialogue Graph System"));
            Assert.That(GetStringProperty(result, "StatusMessage"), Does.Contain("Bundled"));
        }

        [Test]
        public void AiEntry_UsesExactComingSoonCopyAndOneStableLearnMoreTarget()
        {
            var catalogType = RequireCatalogType();
            var title = GetStaticString(catalogType, "AiComingSoonTitle");
            var learnMoreUrl = GetStaticString(catalogType, "AiLearnMoreUrl");
            var aiEntry = InvokeStatic(catalogType, "Get", "ai-extension");

            Assert.That(title, Is.EqualTo("AI Extension 2.0 — Coming Soon"));
            Assert.That(GetStringProperty(aiEntry, "Title"), Is.EqualTo(title));
            Assert.That(GetStringProperty(aiEntry, "OnlineUrl"), Is.EqualTo(learnMoreUrl));
            Assert.That(learnMoreUrl, Is.EqualTo("https://bekaforge.com/dgs-docs/ai-extension.html"));

            var editorSourceRoot = Path.GetFullPath("Assets/DialogGraphSystem/Scripts/Editor");
            var source = string.Join("\n", Directory.GetFiles(editorSourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            Assert.That(source, Does.Not.Contain("assetstore.unity.com"));
            Assert.That(source, Does.Not.Contain("Open Extension URL"));
        }

        [Test]
        public void MissingAiExtensionPanel_ExposesLearnMoreAndBundledCopy()
        {
            var panelType = Type.GetType(
                "DialogSystem.EditorTools.Windows.DialogGraphAiFloatingPanel, DialogSystem.Editor",
                throwOnError: true);
            var panel = (VisualElement)Activator.CreateInstance(
                panelType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { null, null },
                culture: null);

            var labels = panel.Query<Label>().ToList().Select(label => label.text).ToArray();
            var buttons = panel.Query<Button>().ToList().Select(button => button.text).ToArray();

            Assert.That(labels, Does.Contain("AI Extension 2.0 — Coming Soon"));
            Assert.That(buttons, Does.Contain("Learn More"));
            Assert.That(buttons, Does.Contain("View Bundled Copy"));
        }

        [Test]
        public void PackageDocumentation_UsesCoreOnlyAiBoundaryAndStablePublicLinks()
        {
            var readme = File.ReadAllText(Path.GetFullPath("Assets/DialogGraphSystem/README.md"));
            var troubleshooting = File.ReadAllText(Path.GetFullPath("Assets/DialogGraphSystem/Documentation/TROUBLESHOOTING.md"));
            var packageJson = File.ReadAllText(Path.GetFullPath("Assets/DialogGraphSystem/package.json"));
            var welcomeSource = File.ReadAllText(Path.GetFullPath(
                "Assets/DialogGraphSystem/Scripts/Editor/Onboarding/DialogWelcomeBrandingEditorService.cs"));
            var combined = readme + troubleshooting + welcomeSource;

            Assert.That(combined, Does.Contain("AI Extension 2.0 — Coming Soon"));
            Assert.That(combined, Does.Not.Contain("Import `Assets/DialogSystemAIExtension`"));
            Assert.That(combined, Does.Not.Contain("configure a provider"));
            Assert.That(combined, Does.Not.Contain("preview-first AI"));
            Assert.That(packageJson, Does.Contain("https://bekaforge.com/dgs-docs/"));
            Assert.That(packageJson, Does.Contain("https://bekaforge.com/dgs-docs/changelog.html"));
            Assert.That(packageJson, Does.Contain("https://bekaforge.com/dgs-docs/license.html"));
        }

        [Test]
        public void UpgradeDocumentation_ExplainsLegacyVendorCleanupWithoutDeletingUserContent()
        {
            var guide = File.ReadAllText(Path.GetFullPath(
                "Assets/DialogGraphSystem/Documentation/UPGRADE_GUIDE.md"));
            var changelog = File.ReadAllText(Path.GetFullPath("Assets/DialogGraphSystem/CHANGELOG.md"));

            Assert.That(guide, Does.Contain("Assets/DialogGraphSystem/Tests"));
            Assert.That(guide, Does.Contain("Assets/DialogGraphSystem/Assets.csc.rsp"));
            Assert.That(guide, Does.Contain("DialogNodeRewriteService.cs"));
            Assert.That(guide, Does.Contain("Do not delete user-owned graphs, definitions, exports, or localization tables"));
            Assert.That(changelog, Does.Contain("importing `3.0.0` over `2.4.1`"));
        }

        [Test]
        public void LicenseDocumentation_LinksToThePackagedThirdPartyNotice()
        {
            var license = File.ReadAllText(Path.GetFullPath("Assets/DialogGraphSystem/LICENSE.md"));
            var bundledLicense = File.ReadAllText(Path.GetFullPath(
                "Assets/DialogGraphSystem/Documentation/Bundled/LICENSE.txt"));
            const string noticePath = "Assets/DialogGraphSystem/Third-Party Notices.txt";

            Assert.That(File.Exists(Path.GetFullPath(noticePath)), Is.True);
            Assert.That(license, Does.Contain("[Third-Party Notices.txt](Third-Party%20Notices.txt)"));
            Assert.That(license, Does.Not.Contain("Third-Party%20Notices.md"));
            Assert.That(bundledLicense, Does.Contain("Third-Party Notices.txt"));
            Assert.That(bundledLicense, Does.Not.Contain("Third-Party Notices.md"));
        }

        private static Type RequireCatalogType()
        {
            var type = Type.GetType(CatalogTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null, "The online/offline public-information catalog must exist.");
            return type;
        }

        private static IEnumerable GetEntries(Type catalogType)
        {
            var property = catalogType.GetProperty("All", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (IEnumerable)property.GetValue(null);
        }

        private static object Resolve(string topicId, bool onlineSucceeded, string onlineContent, string failureReason)
        {
            return InvokeStatic(RequireCatalogType(), "Resolve", topicId, onlineSucceeded, onlineContent, failureReason);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing {type.FullName}.{methodName}.");
            return method.Invoke(null, arguments);
        }

        private static string GetStaticString(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing {type.FullName}.{fieldName}.");
            return (string)field.GetValue(null);
        }

        private static Func<object, string> GetStringProperty(string propertyName)
        {
            return value => GetStringProperty(value, propertyName);
        }

        private static string GetStringProperty(object value, string propertyName)
        {
            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Missing {value.GetType().FullName}.{propertyName}.");
            return (string)property.GetValue(value);
        }

        private static bool GetBoolProperty(object value, string propertyName)
        {
            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(value);
        }
    }
}
