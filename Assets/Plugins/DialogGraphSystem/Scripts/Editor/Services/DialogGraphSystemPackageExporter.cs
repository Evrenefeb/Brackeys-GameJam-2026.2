using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor.Services
{
    /// <summary>
    /// Exports the core Dialogue Graph System package from its package root only.
    /// This avoids pulling Package Manager source files or optional add-ons into the export.
    /// </summary>
    public static class DialogGraphSystemPackageExporter
    {
        private const string PackageRootAssetPath = "Assets/DialogGraphSystem";
        private const string PackageMetadataAssetPath = "Assets/DialogGraphSystem/package.json";
        private const string BatchExportPathArgument = "-dgsExportPath";
        private static readonly string[] ApprovedExportAssetPaths =
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

        #pragma warning disable CS0649
        [Serializable]
        private sealed class PackageMetadata
        {
            public string version;
        }
        #pragma warning restore CS0649

        public static void ValidateCorePackageSourceMenu()
        {
            ValidateCorePackageSource();
            Debug.Log(GetValidationSummary());
        }

        public static void ExportCorePackageInteractive()
        {
            ValidateCorePackageSource();

            var defaultFileName = GetDefaultArtifactFileName();
            var outputPath = EditorUtility.SaveFilePanel(
                "Export Dialogue Graph System Core Package",
                Directory.GetCurrentDirectory(),
                defaultFileName,
                "unitypackage");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            ExportCorePackage(outputPath);
        }

        public static void ExportCorePackageBatch()
        {
            ValidateCorePackageSource();

            var outputPath = GetBatchExportPath();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    GetDefaultArtifactFileName());
            }

            ExportCorePackage(outputPath);
            Debug.Log(GetValidationSummary());
        }

        private static void ExportCorePackage(string outputPath)
        {
            var normalizedOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(normalizedOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Export output directory could not be resolved.");
            }

            Directory.CreateDirectory(outputDirectory);

            AssetDatabase.ExportPackage(
                GetExportAssetPaths(),
                normalizedOutputPath,
                ExportPackageOptions.Recurse);

            var manifestPath = WriteManifest(normalizedOutputPath);
            Debug.Log(
                $"[DialogGraphSystemPackageExporter] Exported core package to '{normalizedOutputPath}'. " +
                $"Exact manifest: '{manifestPath}'.");
        }

        private static void ValidateCorePackageSource()
        {
            if (!AssetDatabase.IsValidFolder(PackageRootAssetPath))
            {
                throw new InvalidOperationException($"Package root '{PackageRootAssetPath}' was not found.");
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(PackageMetadataAssetPath) == null)
            {
                throw new InvalidOperationException($"Package metadata '{PackageMetadataAssetPath}' was not found.");
            }

            var runtimeSourceDirectory = GetAbsolutePath("Assets/DialogGraphSystem/Scripts/Runtime");
            if (!Directory.Exists(runtimeSourceDirectory))
            {
                throw new InvalidOperationException("Runtime source directory is missing.");
            }

            var runtimeFileCount = Directory.GetFiles(runtimeSourceDirectory, "*.cs", SearchOption.AllDirectories).Length;
            if (runtimeFileCount == 0)
            {
                throw new InvalidOperationException("No runtime C# files were found under the core package root.");
            }

            foreach (var assetPath in ApprovedExportAssetPaths)
            {
                var absolutePath = GetAbsolutePath(assetPath);
                if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
                {
                    throw new InvalidOperationException($"Approved package asset '{assetPath}' was not found.");
                }
            }
        }

        private static string GetValidationSummary()
        {
            var runtimeSourceDirectory = GetAbsolutePath("Assets/DialogGraphSystem/Scripts/Runtime");
            var runtimeFileCount = Directory.GetFiles(runtimeSourceDirectory, "*.cs", SearchOption.AllDirectories).Length;
            var manifestFileCount = GetManifestAssetPaths().Length;

            return $"[DialogGraphSystemPackageExporter] Validation passed. Export root: '{PackageRootAssetPath}'. " +
                   $"Approved roots/files: {ApprovedExportAssetPaths.Length}. Runtime scripts: {runtimeFileCount}. " +
                   $"Manifest files: {manifestFileCount}.";
        }

        private static string GetDefaultArtifactFileName()
        {
            return $"DialogueGraphSystem_{GetPackageVersionOrFallback()}.unitypackage";
        }

        private static string GetPackageVersionOrFallback()
        {
            var metadataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(PackageMetadataAssetPath);
            if (metadataAsset != null)
            {
                var metadata = JsonUtility.FromJson<PackageMetadata>(metadataAsset.text);
                if (!string.IsNullOrWhiteSpace(metadata?.version))
                {
                    return metadata.version.Trim();
                }
            }

            return "0.0.0";
        }

        private static string GetBatchExportPath()
        {
            var commandLineArguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < commandLineArguments.Length - 1; index++)
            {
                if (string.Equals(commandLineArguments[index], BatchExportPathArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return commandLineArguments[index + 1];
                }
            }

            return string.Empty;
        }

        private static string[] GetExportAssetPaths()
        {
            return ApprovedExportAssetPaths.ToArray();
        }

        private static string[] GetManifestAssetPaths()
        {
            var manifestPaths = new List<string>();
            foreach (var approvedPath in ApprovedExportAssetPaths)
            {
                var absolutePath = GetAbsolutePath(approvedPath);
                if (File.Exists(absolutePath))
                {
                    manifestPaths.Add(approvedPath);
                    continue;
                }

                if (!Directory.Exists(absolutePath))
                {
                    throw new InvalidOperationException($"Approved package asset '{approvedPath}' was not found.");
                }

                manifestPaths.AddRange(Directory
                    .GetFiles(absolutePath, "*", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .Select(path => ToAssetPath(path)));
            }

            return manifestPaths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string WriteManifest(string packageOutputPath)
        {
            var outputDirectory = Path.GetDirectoryName(packageOutputPath);
            var artifactName = Path.GetFileName(packageOutputPath);
            var artifactStem = Path.GetFileNameWithoutExtension(packageOutputPath);
            var manifestPath = Path.Combine(outputDirectory, $"{artifactStem}.manifest.txt");
            var manifestAssetPaths = GetManifestAssetPaths();
            var lines = new List<string>
            {
                "# Dialogue Graph System Core package manifest",
                $"# Artifact: {artifactName}",
                $"# Version: {GetPackageVersionOrFallback()}",
                $"# File count: {manifestAssetPaths.Length}"
            };
            lines.AddRange(manifestAssetPaths);
            File.WriteAllLines(manifestPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return Path.GetFullPath(manifestPath);
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Project root could not be resolved.");
            }

            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string ToAssetPath(string absolutePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Project root could not be resolved.");
            }

            var normalizedProjectRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedAbsolutePath = Path.GetFullPath(absolutePath);
            if (!normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Asset path '{absolutePath}' is outside the Unity project.");
            }

            return normalizedAbsolutePath.Substring(normalizedProjectRoot.Length).Replace("\\", "/");
        }
    }
}
