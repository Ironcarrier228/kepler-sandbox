using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kepler.Editor
{
    public static class LinuxPlayerBuild
    {
        // Called in a fresh batch process with -buildTarget Linux64.
        public static void Run()
        {
            const BuildTarget target = BuildTarget.StandaloneLinux64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Linux Standalone (Mono) support is missing from this Editor.");

            FlightChecks.BuildAndVerify();
            string directory = Environment.GetEnvironmentVariable("KEPLER_PLAYER_DIR");
            if (string.IsNullOrEmpty(directory))
                throw new BuildFailedException("Use scripts/build-linux.sh to set the player output directory.");
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SpaceBuilder.ScenePath },
                locationPathName = Path.Combine(directory, "Kepler.x86_64"),
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Linux player: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"Built Linux player: {report.summary.outputPath}");
        }
    }
}
