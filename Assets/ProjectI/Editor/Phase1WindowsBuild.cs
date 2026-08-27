using System.IO; // 파일 경로 기능 참조
using System.Linq; // 목록 변환 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.Build.Reporting; // 빌드 결과 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase1WindowsBuild // Windows 개발 빌드 도구
    {
        private const string OutputDirectory = "Builds/Windows"; // 빌드 출력 폴더
        private const string OutputPath = OutputDirectory + "/ProjectI.exe"; // 실행 파일 출력 경로

        [MenuItem("Tools/Project I/Build/Windows Development")] // Windows 개발 빌드 메뉴 등록
        public static void BuildWindowsDevelopment() // Windows 개발 빌드 실행
        {
            if (!Phase1Validator.Validate(false)) // Phase 1 사전 검증
            {
                Debug.LogError("[Project I] Phase 1 검증 실패로 빌드를 중단합니다."); // 검증 실패 오류 출력
                return; // 빌드 실행 중단
            }

            Directory.CreateDirectory(OutputDirectory); // 빌드 출력 폴더 생성
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(); // 활성 빌드 씬 수집
            BuildPlayerOptions options = new BuildPlayerOptions(); // 빌드 옵션 생성
            options.scenes = scenes; // 빌드 씬 지정
            options.locationPathName = OutputPath; // 빌드 출력 경로 지정
            options.target = BuildTarget.StandaloneWindows64; // Windows 64비트 대상 지정
            options.options = BuildOptions.Development | BuildOptions.AllowDebugging; // 개발 및 디버깅 빌드 설정
            BuildReport report = BuildPipeline.BuildPlayer(options); // Windows 빌드 실행

            if (report.summary.result == BuildResult.Succeeded) // 빌드 성공 여부 확인
            {
                Debug.Log($"[Project I] Windows Development 빌드 성공: {OutputPath}"); // 빌드 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError($"[Project I] Windows Development 빌드 실패: {report.summary.result}"); // 빌드 실패 로그 출력
        }
    }
}
