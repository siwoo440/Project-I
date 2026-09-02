using ProjectI.Diagnostics; // 프로젝트 로그 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 관리 기능 참조

namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public sealed class SceneFlowManager : MonoBehaviour, IProjectService // 씬 전환 관리자
    {
        public const string BootSceneName = "Boot"; // 부트 씬 이름
        public const string MainMenuSceneName = "MainMenu"; // 메인 메뉴 씬 이름
        public const string WagonPersistentSceneName = "00_WagonPersistent"; // 마차 영구 씬 이름
        public const string ExplorationOfficeSceneName = WagonPersistentSceneName; // 기존 사무소 진입 API를 영구 마차 씬으로 연결

        private void Awake() // 객체 초기 진입
        {
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            ProjectServices.Register<SceneFlowManager>(this); // 서비스 저장소 등록
            Initialize(); // 서비스 초기화 실행
        }

        private void OnDestroy() // 객체 제거 시점
        {
            Shutdown(); // 서비스 종료 처리
            ProjectServices.Unregister<SceneFlowManager>(); // 서비스 저장소 해제
        }

        public void Initialize() // 서비스 초기화
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // 중복 씬 이벤트 해제
            SceneManager.sceneLoaded += HandleSceneLoaded; // 씬 이벤트 등록
            ProjectLog.Log("SceneFlowManager 초기화 완료"); // 초기화 로그 출력
        }

        public void Shutdown() // 서비스 종료
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // 씬 이벤트 해제
        }

        public void LoadBoot() // 부트 씬 이동
        {
            LoadScene(BootSceneName, GameState.Boot); // 부트 씬 요청
        }

        public void LoadMainMenu() // 메인 메뉴 이동
        {
            LoadScene(MainMenuSceneName, GameState.MainMenu); // 메인 메뉴 요청
        }

        public void LoadExplorationOffice() // 탐사 사무소 이동
        {
            LoadScene(WagonPersistentSceneName, GameState.ExplorationOffice); // 영구 마차 씬 진입 후 Office 맵을 Additive 로드
        }

        private void LoadScene(string sceneName, GameState state) // 공통 씬 이동
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) // 빌드 씬 등록 확인
            {
                ProjectLog.Error($"Build Settings에 씬이 없습니다: {sceneName}"); // 누락 씬 오류 출력
                return; // 씬 이동 중단
            }

            GameManager.Instance?.SetState(state); // 게임 상태 선반영
            ProjectLog.Log($"씬 이동 요청: {sceneName}"); // 씬 이동 로그 출력
            SceneManager.LoadScene(sceneName); // 영구 마차 씬 또는 메뉴 계열 씬 로드
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) // 씬 로드 완료 처리
        {
            GameEvents.PublishSceneChanged(scene.name); // 씬 변경 이벤트 발행
            ProjectLog.Log($"씬 로드 완료: {scene.name} / Mode={mode}"); // 씬 완료 로그 출력
        }
    }
}
