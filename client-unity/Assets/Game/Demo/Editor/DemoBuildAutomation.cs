using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BiomeRivals.Demo.Editor
{
    public static class DemoBuildAutomation
    {
        public const string OutputPath = "Builds/DemoPreview/BiomeRivalsDemo.exe";

        public static void BuildWindowsFromCommandLine()
            => BuildWindows(BuildOptions.Development);

        // A compile/isolation audit build, not approval to distribute Minecraft assets.
        public static void BuildWindowsNonDevelopmentFromCommandLine()
            => BuildWindows(BuildOptions.None);

        private static void BuildWindows(BuildOptions buildOptions)
        {
            var absoluteOutput = ResolveOutputPath(Environment.GetCommandLineArgs());
            var artSource = Path.Combine(Application.dataPath, "Generated", "MinecraftCardIcons");
            var icons = ValidateCardArtSource(artSource);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput) ?? throw new InvalidOperationException("Build output directory is invalid."));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoSceneBuilder.ScenePath },
                locationPathName = absoluteOutput,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Demo build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            CopyPlayerCardArt(artSource, absoluteOutput, icons);
            WriteBuildManifest(absoluteOutput, report.summary.totalSize, report.summary.options, icons);
            Debug.Log($"Demo Windows build succeeded: {absoluteOutput} ({report.summary.totalSize} bytes)");
        }

        private static void WriteBuildManifest(string playerOutputPath, ulong buildSize, BuildOptions buildOptions, CardIconEntry[] icons)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                              throw new InvalidOperationException("Unity project root could not be resolved.");
            var inputRoots = new[] { "Assets", "ProjectSettings", "Packages" };
            var inputFiles = inputRoots
                .Select(root => Path.Combine(projectRoot, root))
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var inputs = new List<DemoBuildInput>(inputFiles.Length);
            using (var sha256 = SHA256.Create())
            {
                foreach (var inputFile in inputFiles)
                {
                    var relativePath = inputFile.Substring(projectRoot.Length + 1)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    inputs.Add(new DemoBuildInput
                    {
                        path = relativePath,
                        sha256 = ToHex(sha256.ComputeHash(File.ReadAllBytes(inputFile)))
                    });
                }
            }

            var manifest = new DemoPlayerBuildManifest
            {
                schemaVersion = 1,
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                developmentBuild = (buildOptions & BuildOptions.Development) != 0,
                buildOptions = buildOptions.ToString(),
                buildSizeBytes = buildSize,
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                packagedCardArt = icons.Select(icon => new DemoBuildInput { path = "MinecraftCardIcons/" + icon.outputFile, sha256 = icon.sha256 }).ToArray(),
                inputs = inputs.ToArray()
            };
            var manifestPath = playerOutputPath + ".build-manifest.json";
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            Debug.Log($"Demo build manifest written: {manifestPath} ({inputs.Count} source files)");
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        [Serializable]
        private sealed class DemoBuildInput
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        private sealed class DemoPlayerBuildManifest
        {
            public int schemaVersion;
            public string unityVersion;
            public string applicationVersion;
            public string buildTarget;
            public bool developmentBuild;
            public string buildOptions;
            public ulong buildSizeBytes;
            public string builtAtUtc;
            public DemoBuildInput[] inputs;
            public DemoBuildInput[] packagedCardArt;
        }

        [Serializable] private sealed class CardIconEntry { public string id, cardId, outputFile, sha256; }
        [Serializable] private sealed class CardIconDocument { public int schemaVersion; public string redistributionPolicy; public CardIconEntry[] entries; }

        private static CardIconEntry[] ValidateCardArtSource(string source)
        {
            var provenancePath = Path.Combine(source, "asset-provenance.local.json");
            if (!File.Exists(provenancePath)) throw new BuildFailedException("Required Minecraft card art provenance is missing: " + provenancePath);
            var provenance = JsonUtility.FromJson<CardIconDocument>(File.ReadAllText(provenancePath));
            var names = Resources.Load<TextAsset>("CardContent/card-name-registry.zh-CN.v1");
            if (names == null) throw new BuildFailedException("Card art name registry resource is missing.");
            var registry = JsonUtility.FromJson<CardIconDocument>(names.text);
            if (registry?.schemaVersion != 1 || registry.entries == null || registry.entries.Length == 0 ||
                provenance?.schemaVersion != 1 || provenance.entries == null || provenance.redistributionPolicy != "DO_NOT_COMMIT_EXTRACTED_ASSETS")
                throw new BuildFailedException("Card art registry/provenance is invalid; local extracted assets must not be committed or published.");
            var expected = new HashSet<string>(registry.entries.Select(entry => entry.id), StringComparer.Ordinal);
            if (expected.Count != registry.entries.Length || expected.Any(id => !DemoCardArtProvider.IsSafeCardId(id)))
                throw new BuildFailedException("Card art name registry contains unsafe or duplicate IDs.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            foreach (var icon in provenance.entries)
            {
                if (icon == null || !expected.Contains(icon.cardId) || !seen.Add(icon.cardId) || icon.outputFile != icon.cardId + ".png" ||
                    icon.sha256 == null || icon.sha256.Length != 64 || icon.sha256.Any(c => !Uri.IsHexDigit(c)))
                    throw new BuildFailedException("Card art provenance contains an unknown/duplicate/unsafe entry.");
                var path = Path.Combine(source, icon.outputFile);
                if (!File.Exists(path)) throw new BuildFailedException("Registered Minecraft card art is missing: " + icon.cardId);
                if (!string.Equals(ToHex(sha.ComputeHash(File.ReadAllBytes(path))), icon.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Registered Minecraft card art hash mismatch: " + icon.cardId);
            }
            if (!seen.SetEquals(expected) || Directory.GetFiles(source, "*.png").Length != expected.Count)
                throw new BuildFailedException("Card art source does not exactly cover the registered cards.");
            return provenance.entries;
        }

        private static void CopyPlayerCardArt(string source, string playerOutputPath, CardIconEntry[] icons)
        {
            var destination = Path.Combine(Path.GetDirectoryName(playerOutputPath) ?? string.Empty, "MinecraftCardIcons");
            Directory.CreateDirectory(destination);
            foreach (var icon in icons)
                File.Copy(Path.Combine(source, icon.outputFile), Path.Combine(destination, icon.outputFile), true);

            Debug.Log($"Copied {icons.Length} validated local card icons beside the Player (local-only, do not publish): {destination}");
        }

        private static string ResolveOutputPath(string[] arguments)
        {
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], "-buildOutput", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(arguments[i + 1]))
                    return Path.GetFullPath(arguments[i + 1]);
            }

            return Path.GetFullPath(OutputPath);
        }
    }
}
