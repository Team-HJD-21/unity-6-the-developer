// 현재 Build Settings의 실제 게임 Scene으로 Windows 개발 빌드를 생성합니다.
// 별도 최소 테스트 Scene으로 통합 검증 범위를 축소하지 않습니다.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class CoreNetworkBuildPipeline
    {
        public static void BuildWindowsDevelopment()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != "Assets/Scenes/Gameplay/Initial_Stage.unity")
                throw new InvalidOperationException("The actual Initial_Stage must be the first enabled build Scene.");
            const string output = "Builds/CoreNetworkValidation/TheDeveloper.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Network validation build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"CORE_NETWORK_BUILD_SUCCEEDED {output}; bytes={report.summary.totalSize}");
        }
    }
}
