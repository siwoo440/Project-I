using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Diagnostics // 진단 기능 네임스페이스
{
    public static class ProjectLog // 프로젝트 공통 로그 도구
    {
        private const string SettingsResourceName = "ProjectDevelopmentSettings"; // 개발 설정 리소스 이름
        private static ProjectDevelopmentSettings settings; // 개발 설정 캐시

        public static void Log(string message) // 일반 로그 출력
        {
            if (!IsVerboseLoggingEnabled()) // 상세 로그 설정 확인
            {
                return; // 일반 로그 출력 생략
            }

            Debug.Log($"[Project I] {message}"); // 접두사 포함 로그 출력
        }

        public static void Warning(string message) // 경고 로그 출력
        {
            Debug.LogWarning($"[Project I] {message}"); // 접두사 포함 경고 출력
        }

        public static void Error(string message) // 오류 로그 출력
        {
            Debug.LogError($"[Project I] {message}"); // 접두사 포함 오류 출력
        }

        private static bool IsVerboseLoggingEnabled() // 상세 로그 설정 확인
        {
            if (settings == null) // 설정 캐시 확인
            {
                settings = Resources.Load<ProjectDevelopmentSettings>(SettingsResourceName); // 리소스 설정 불러오기
            }

            return settings == null || settings.EnableVerboseLogs; // 설정 없을 때 기본 활성화
        }
    }
}
