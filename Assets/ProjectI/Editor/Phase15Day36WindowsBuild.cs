using System; // 시간
using System.IO; // 경로·보고서
using System.Linq; // 조회
using System.Text; // 보고
using UnityEditor; // 에디터 기능
using UnityEditor.Build.Reporting; // 빌드 결과
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase15Day36WindowsBuild // Windows 싱글 알파 빌드 (36일차)
    {
        public const string AlphaVersion = "0.43.0-alpha"; // 알파 버전 표시
        private const string OutputFolder = "Builds/Windows"; // 출력 폴더 (git 제외)
        private const string ExecutableName = "ProjectI.exe"; // 실행 파일
        private const string TextMaterialPath = "Assets/ProjectI/Resources/Text/TextMeshDepth.mat"; // 빌드에 필요한 글자 재질

        [MenuItem("Project I/Build/Windows Alpha")] // 메뉴
        public static void BuildFromMenu() // 에디터에서 빌드
        {
            if (!EditorUtility.DisplayDialog("Windows 알파 빌드", $"{OutputFolder}/{ExecutableName} 로 빌드합니다.\n몇 분 걸릴 수 있습니다.", "빌드", "취소")) // 확인
            {
                return; // 취소
            }

            BuildReport report = Build(); // 빌드

            if (report != null && report.summary.result == BuildResult.Succeeded) // 성공
            {
                EditorUtility.RevealInFinder(Path.Combine(ProjectRoot(), OutputFolder, ExecutableName)); // 폴더 열기
            }
        }

        [MenuItem("Project I/Build/Open Build Folder")] // 메뉴
        public static void OpenFolder() // 출력 폴더 열기
        {
            string folder = Path.Combine(ProjectRoot(), OutputFolder); // 폴더
            Directory.CreateDirectory(folder); // 보장
            EditorUtility.RevealInFinder(folder); // 열기
        }

        public static void BuildFromCommandLine() // 배치 모드 진입점 (-executeMethod)
        {
            BuildReport report = Build(); // 빌드
            EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1); // 종료 코드
        }

        public static BuildReport Build() // 빌드 실행
        {
            StringBuilder problems = new StringBuilder(); // 사전 확인
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(); // 빌드 씬

            if (scenes.Length == 0 || !scenes[0].EndsWith("/Boot.unity", StringComparison.Ordinal)) // 첫 씬
            {
                problems.AppendLine("첫 씬이 Boot 가 아닙니다 — Project I > Day 35 > Build Main Menu Scene 을 실행하세요."); // 안내
            }

            foreach (string scene in scenes.Where(scene => !File.Exists(Path.Combine(ProjectRoot(), scene)))) // 없는 씬
            {
                problems.AppendLine($"씬 파일 없음: {scene}"); // 안내
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath) == null) // 글자 재질
            {
                problems.AppendLine($"글자 재질 없음: {TextMaterialPath}"); // 안내
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)) // 빌드 모듈
            {
                problems.AppendLine("Windows 빌드 모듈이 설치되어 있지 않습니다 (Unity Hub > Installs > Add modules)."); // 안내
            }

            if (problems.Length > 0) // 문제
            {
                Debug.LogError($"[Project I] 36일차 Windows 빌드 중단\n{problems}"); // 오류
                return null; // 중단
            }

            ApplyPlayerSettings(); // 버전·창 설정
            string output = Path.Combine(ProjectRoot(), OutputFolder, ExecutableName); // 실행 파일 경로
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? OutputFolder); // 폴더
            BuildPlayerOptions options = new BuildPlayerOptions // 옵션
            {
                scenes = scenes, // 씬
                locationPathName = output, // 출력
                target = BuildTarget.StandaloneWindows64, // Windows 64비트
                targetGroup = BuildTargetGroup.Standalone, // PC
                options = BuildOptions.None, // 일반 빌드
            };

            BuildReport report = BuildPipeline.BuildPlayer(options); // 빌드
            CopySteamAppId(output); // 40일차: 시험용 Steam 앱 ID (Steam 스토어 배포 시 제외)
            string text = Describe(report, scenes); // 보고
            File.WriteAllText(Path.Combine(ProjectRoot(), OutputFolder, "build_report.txt"), text, Encoding.UTF8); // 보고서 저장

            if (report.summary.result == BuildResult.Succeeded) // 성공
            {
                Debug.Log(text); // 보고
            }
            else
            {
                Debug.LogError(text); // 실패 보고
            }

            return report; // 반환
        }

        private static void CopySteamAppId(string executablePath) // 실행 파일 옆에 steam_appid.txt 복사 (Steam 클라이언트 밖에서 실행해도 Steam 연결)
        {
            string source = Path.Combine(ProjectRoot(), "steam_appid.txt"); // 프로젝트 루트 파일

            if (File.Exists(source)) // 있음
            {
                File.Copy(source, Path.Combine(Path.GetDirectoryName(executablePath) ?? OutputFolder, "steam_appid.txt"), true); // 복사
            }
        }

        private static void ApplyPlayerSettings() // 알파 빌드 기본 설정
        {
            PlayerSettings.productName = "Project I"; // 이름
            PlayerSettings.bundleVersion = AlphaVersion; // 버전 (메인 메뉴 왼쪽 아래 표시)
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; // 전체 화면 창
            PlayerSettings.defaultScreenWidth = 1920; // 창 모드 기본 폭
            PlayerSettings.defaultScreenHeight = 1080; // 창 모드 기본 높이
            PlayerSettings.resizableWindow = true; // 창 크기 조절
            PlayerSettings.runInBackground = true; // 다른 창을 봐도 진행 (협동 대비)
            AssetDatabase.SaveAssets(); // 저장
        }

        private static string Describe(BuildReport report, string[] scenes) // 보고서
        {
            BuildSummary summary = report.summary; // 요약
            StringBuilder builder = new StringBuilder($"[Project I] 36일차 Windows 알파 빌드 {(summary.result == BuildResult.Succeeded ? "완료" : "실패")}"); // 제목
            builder.AppendLine(); // 줄
            builder.AppendLine($"결과 {summary.result} · 버전 {PlayerSettings.bundleVersion} · 시간 {summary.totalTime:hh\\:mm\\:ss}"); // 요약
            builder.AppendLine($"출력 {summary.outputPath}"); // 경로
            builder.AppendLine($"크기 {summary.totalSize / (1024f * 1024f):0.0} MB · 오류 {summary.totalErrors} · 경고 {summary.totalWarnings}"); // 크기
            builder.AppendLine($"씬 {string.Join(" → ", scenes.Select(Path.GetFileNameWithoutExtension))}"); // 씬

            foreach (BuildStep step in report.steps) // 단계별 오류
            {
                foreach (BuildStepMessage message in step.messages.Where(item => item.type == LogType.Error || item.type == LogType.Exception)) // 오류
                {
                    builder.AppendLine($"  오류 [{step.name}] {message.content}"); // 기록
                }
            }

            return builder.ToString(); // 반환
        }

        private static string ProjectRoot() // 프로젝트 폴더
        {
            return Path.GetDirectoryName(Application.dataPath) ?? "."; // Assets 의 상위
        }
    }
}
