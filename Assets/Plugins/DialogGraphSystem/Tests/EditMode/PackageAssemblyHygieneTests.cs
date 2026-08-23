using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class PackageAssemblyHygieneTests
    {
        private const string AiExtensionCompileReadyDefine = "BEKA_FORGE_DGS_AI_EXTENSION_COMPILE_READY_V1";
        private const string AiExtensionTestsDefine = "BEKA_FORGE_DGS_AI_EXTENSION_TESTS";
        private const string LegacyProviderNamespace = "DialogSystemAIExtension.Runtime.llm";
        private static readonly string[] LegacyProviderScriptGuids =
        {
            "ea6bbe07c963f024baaff61b827534cc",
            "6abae77ed047fc54dba93d41abd94b90",
            "26f98775e525a73498a178b166ab5b11",
            "89851c2ed3801844abad97e9d46b0a20",
            "8fc2dac5fc4ab004b9411edb5e173d38"
        };

        // Manual import scenario checklist:
        // - Import DialogGraphSystem alone: main runtime/editor asmdefs compile without AI extension references.
        // - Import AiProviders alone: runtime asmdef compiles without DialogSystem references.
        // - Import DialogSystemAIExtension alone: runtime DTO/settings asmdef compiles without DialogSystem or AiProviders.
        // - Import DialogSystemAIExtension after both dependencies: the dependency warning assembly sets
        //   BEKA_FORGE_DGS_AI_EXTENSION_COMPILE_READY_V1 and the editor asmdef may reference DialogSystem + AiProviders.
        // - AI extension tests are internal-only and additionally gated by
        //   BEKA_FORGE_DGS_AI_EXTENSION_TESTS so consumer imports do not compile them by default.
        // - Remove DialogSystem or AiProviders: AI extension editor assembly should be excluded by define constraints.

        [Test]
        public void MainDialogSystemAssemblies_DoNotReferenceOptionalAiAssemblies()
        {
            var forbiddenReferences = new[]
            {
                "DialogSystemAIExtension.Runtime",
                "DialogSystemAIExtension.Editor",
                "DialogSystemAIExtension.DependencyWarning.Editor",
                "AiProviders.Runtime",
                "AiProviders.Editor"
            };

            AssertAsmdefDoesNotReference("Assets/DialogGraphSystem/Scripts/Runtime/DialogSystem.Runtime.asmdef", forbiddenReferences);
            AssertAsmdefDoesNotReference("Assets/DialogGraphSystem/Scripts/Editor/DialogSystem.Editor.asmdef", forbiddenReferences);
        }

        [Test]
        public void AiProvidersRuntime_DoesNotReferenceDialogSystem()
        {
            AssertAsmdefDoesNotReference(
                "Assets/AiProviders/Runtime/AiProviders.Runtime.asmdef",
                new[] { "DialogSystem.Runtime", "DialogSystem.Editor" });
        }

        [Test]
        public void AiExtensionRuntime_DoesNotReferenceOptionalDependencies()
        {
            AssertAsmdefDoesNotReference(
                "Assets/DialogSystemAIExtension/Runtime/DialogSystemAIExtension.Runtime.asmdef",
                new[] { "DialogSystem.Runtime", "DialogSystem.Editor", "AiProviders.Runtime", "AiProviders.Editor" });
        }

        [Test]
        public void AiExtensionEditor_IsGatedByCompileReadyDefine()
        {
            AssertAsmdefHasDefineConstraint(
                "Assets/DialogSystemAIExtension/Editor/DialogSystemAIExtension.Editor.asmdef",
                AiExtensionCompileReadyDefine);
        }

        [Test]
        public void AiExtensionTests_AreGatedByCompileReadyDefine()
        {
            AssertAsmdefHasDefineConstraint(
                "Assets/DialogSystemAIExtension/Tests/DialogSystemAIExtension.Tests.asmdef",
                AiExtensionCompileReadyDefine);

            AssertAsmdefHasDefineConstraint(
                "Assets/DialogSystemAIExtension/Tests/DialogSystemAIExtension.Tests.asmdef",
                AiExtensionTestsDefine);
        }

        [Test]
        public void RuntimeSources_DoNotUseUnityEditorOutsideUnityEditorGuards()
        {
            var violations = new List<string>();

            foreach (var asmdefPath in RuntimeAsmdefPaths())
            {
                var asmdefDirectory = Path.GetDirectoryName(ProjectPath(asmdefPath));
                if (!Directory.Exists(asmdefDirectory))
                {
                    continue;
                }

                foreach (var scriptPath in Directory.EnumerateFiles(asmdefDirectory, "*.cs", SearchOption.AllDirectories))
                {
                    violations.AddRange(FindUnguardedUnityEditorUsages(scriptPath));
                }
            }

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void RuntimeSources_UseCentralizedUnityObjectFinder()
        {
            var compatibilityHelper = ProjectPath("Assets/DialogGraphSystem/Scripts/Runtime/Utils/DialogRuntimeUnityCompatibility.cs");
            var forbiddenApis = new[] { "FindFirstObjectByType", "FindAnyObjectByType", "FindObjectOfType" };
            var runtimeRoot = ProjectPath("Assets/DialogGraphSystem/Scripts/Runtime");

            var violations = Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFullPath(path), compatibilityHelper, StringComparison.OrdinalIgnoreCase))
                .SelectMany(path =>
                {
                    var lines = File.ReadAllLines(path);
                    return lines
                        .Select((line, index) => new { line, index })
                        .Where(entry => forbiddenApis.Any(api => entry.line.Contains(api)))
                        .Select(entry => $"{ProjectRelativePath(path)}:{entry.index + 1}: {entry.line.Trim()}");
                })
                .OrderBy(path => path)
                .ToArray();

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void AiExtensionRuntime_DoesNotContainLegacyLlmProviderNamespace()
        {
            var runtimePath = ProjectPath("Assets/DialogSystemAIExtension/Runtime");
            if (!Directory.Exists(runtimePath))
            {
                Assert.Pass("AI Extension runtime is not installed in this Core-only project.");
            }

            var violations = Directory.EnumerateFiles(runtimePath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(LegacyProviderNamespace))
                .Select(ProjectRelativePath)
                .OrderBy(path => path)
                .ToArray();

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void AiExtensionProviderAssets_DoNotReferenceLegacyProviderScripts()
        {
            var providerAssetRoots = new[]
            {
                "Assets/DialogSystemAIExtension/ProviderAssets",
                "Assets/DialogSystemAIExtension/Resources",
                "Assets/DialogSystemAIExtension/Samples"
            };

            var violations = providerAssetRoots
                .Select(ProjectPath)
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*.asset", SearchOption.AllDirectories))
                .Where(path => LegacyProviderScriptGuids.Any(guid => File.ReadAllText(path).Contains(guid)))
                .Select(ProjectRelativePath)
                .OrderBy(path => path)
                .ToArray();

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        private static IEnumerable<string> RuntimeAsmdefPaths()
        {
            yield return "Assets/DialogGraphSystem/Scripts/Runtime/DialogSystem.Runtime.asmdef";
            yield return "Assets/AiProviders/Runtime/AiProviders.Runtime.asmdef";
            yield return "Assets/DialogSystemAIExtension/Runtime/DialogSystemAIExtension.Runtime.asmdef";
        }

        private static void AssertAsmdefDoesNotReference(string asmdefPath, string[] forbiddenReferences)
        {
            if (!File.Exists(ProjectPath(asmdefPath)))
            {
                Assert.Pass($"Optional assembly '{asmdefPath}' is not installed.");
            }

            var asmdef = ReadAsmdef(asmdefPath);
            var references = asmdef.references ?? Array.Empty<string>();
            var violations = references
                .Where(reference => forbiddenReferences.Contains(reference))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(violations, Is.Empty, $"{asmdef.name} must not reference: {string.Join(", ", violations)}");
        }

        private static void AssertAsmdefHasDefineConstraint(string asmdefPath, string define)
        {
            if (!File.Exists(ProjectPath(asmdefPath)))
            {
                Assert.Pass($"Optional assembly '{asmdefPath}' is not installed.");
            }

            var asmdef = ReadAsmdef(asmdefPath);
            Assert.That(
                asmdef.defineConstraints ?? Array.Empty<string>(),
                Does.Contain(define),
                $"{asmdef.name} must be gated by {define}.");
        }

        private static AssemblyDefinition ReadAsmdef(string asmdefPath)
        {
            return JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(ProjectPath(asmdefPath)));
        }

        private static IEnumerable<string> FindUnguardedUnityEditorUsages(string scriptPath)
        {
            var guardStack = new Stack<bool>();
            var lines = File.ReadAllLines(scriptPath);

            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].Trim();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    guardStack.Push(IsUnityEditorCondition(trimmed.Substring(4)));
                    continue;
                }

                if (trimmed.StartsWith("#elif ", StringComparison.Ordinal))
                {
                    if (guardStack.Count > 0)
                    {
                        guardStack.Pop();
                    }

                    guardStack.Push(IsUnityEditorCondition(trimmed.Substring(6)));
                    continue;
                }

                if (trimmed.StartsWith("#else", StringComparison.Ordinal))
                {
                    if (guardStack.Count > 0)
                    {
                        guardStack.Push(!guardStack.Pop());
                    }

                    continue;
                }

                if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (guardStack.Count > 0)
                    {
                        guardStack.Pop();
                    }

                    continue;
                }

                if (lines[index].Contains("UnityEditor") && !guardStack.Any(guarded => guarded))
                {
                    yield return $"{ProjectRelativePath(scriptPath)}:{index + 1}: {trimmed}";
                }
            }
        }

        private static bool IsUnityEditorCondition(string condition)
        {
            return condition.Contains("UNITY_EDITOR") && !condition.Contains("!UNITY_EDITOR");
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectRelativePath(string fullPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return fullPath.Substring(projectRoot.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
        }

        [Serializable]
        private sealed class AssemblyDefinition
        {
#pragma warning disable CS0649
            public string name;
            public string[] references;
            public string[] defineConstraints;
#pragma warning restore CS0649
        }

        [Test]
        public void DialogSystemEditorAssembly_DoesNotReferenceVectorGraphics()
        {
            AssertAsmdefDoesNotReference(
                "Assets/DialogGraphSystem/Scripts/Editor/DialogSystem.Editor.asmdef",
                new[] { "Unity.VectorGraphics" });
        }

        [Test]
        public void DialogSystemEditorSources_DoNotImportVectorGraphicsNamespace()
        {
            var editorRoot = ProjectPath("Assets/DialogGraphSystem/Scripts/Editor");
            var violations = Directory.EnumerateFiles(editorRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    return text.Contains("using Unity.VectorGraphics") ||
                           text.Contains("Unity.VectorGraphics.");
                })
                .Select(ProjectRelativePath)
                .OrderBy(p => p)
                .ToArray();

            Assert.That(violations, Is.Empty,
                "No DialogSystem editor source should import Unity.VectorGraphics namespace." +
                (violations.Length > 0
                    ? Environment.NewLine + "Violations:" + Environment.NewLine + string.Join(Environment.NewLine, violations)
                    : ""));
        }

    }
}
