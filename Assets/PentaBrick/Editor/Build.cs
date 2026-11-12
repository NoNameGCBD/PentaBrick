using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PentaBrick.EditorTools
{
    /// <summary>
    /// 메뉴 PentaBrick > 빌드. 결과는 Builds/ 아래(git 제외).
    /// 명령줄: Unity -batchmode -quit -projectPath . -executeMethod PentaBrick.EditorTools.Build.All
    /// </summary>
    public static class Build
    {
        [MenuItem("PentaBrick/빌드/WebGL")]
        public static void WebGL()
        {
            // 압축(Brotli) 파일을 서버 설정 없이도 열 수 있게: 로컬 서버·GitHub Pages는 Content-Encoding 헤더를 붙여 주지 않는다.
            PlayerSettings.WebGL.decompressionFallback = true;
            Run(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("PentaBrick/빌드/macOS")]
        public static void MacOS()
        {
            // 발표 컴퓨터가 인텔 맥일 수도 있으므로 인텔·애플 실리콘 둘 다(Universal)
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Run(BuildTarget.StandaloneOSX, "Builds/macOS/PentaBrick.app");
        }

        [MenuItem("PentaBrick/빌드/Windows")]
        public static void Windows() => Run(BuildTarget.StandaloneWindows64, "Builds/Windows/PentaBrick.exe");

        [MenuItem("PentaBrick/빌드/전부")]
        public static void All()
        {
            WebGL();
            MacOS();
            Windows();
        }

        private static void Run(BuildTarget target, string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = target,
                locationPathName = path,
            });
            var summary = report.summary;
            Debug.Log($"PentaBrick 빌드 {target}: {summary.result}, {summary.totalSize / 1024 / 1024}MB, {summary.totalTime:mm\\:ss} → {path}");
            // 명령줄에서 실패가 종료 코드로 드러나도록 예외를 던진다.
            if (summary.result != BuildResult.Succeeded) throw new Exception($"{target} 빌드 실패: 오류 {summary.totalErrors}개");
        }
    }
}
