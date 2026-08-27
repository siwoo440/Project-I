using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Diagnostics // 진단 기능 네임스페이스
{
    [CreateAssetMenu(fileName = "ProjectDevelopmentSettings", menuName = "Project I/Development Settings")] // 개발 설정 생성 메뉴
    public sealed class ProjectDevelopmentSettings : ScriptableObject // 개발 전용 설정 데이터
    {
        [SerializeField] private bool enableVerboseLogs = true; // 상세 로그 사용 여부
        [SerializeField] private bool runValidationAfterSetup = true; // 자동 검증 사용 여부

        public bool EnableVerboseLogs => enableVerboseLogs; // 상세 로그 설정 제공
        public bool RunValidationAfterSetup => runValidationAfterSetup; // 자동 검증 설정 제공
    }
}
