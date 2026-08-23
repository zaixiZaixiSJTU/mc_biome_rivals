using System;
using System.IO;
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
        {
            var absoluteOutput = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput) ?? throw new InvalidOperationException("Build output directory is invalid."));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoSceneBuilder.ScenePath },
                locationPathName = absoluteOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Demo build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"Demo Windows build succeeded: {absoluteOutput} ({report.summary.totalSize} bytes)");
        }
    }
}
