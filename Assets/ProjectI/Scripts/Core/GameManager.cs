using ProjectI.Diagnostics; // 프로젝트 로그 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public sealed class GameManager : MonoBehaviour, IProjectService // 전체 게임 상태 관리자
    {
        public static GameManager Instance { get; private set; } // 전역 관리자 참조
        public GameState CurrentState { get; private set; } = GameState.Boot; // 현재 게임 상태

        private void Awake() // 객체 초기 진입
        {
            if (Instance != null && Instance != this) // 기존 관리자 중복 확인
            {
                Destroy(gameObject); // 중복 관리자 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            ProjectServices.Register<GameManager>(this); // 서비스 저장소 등록
            Initialize(); // 관리자 초기화 실행
        }

        private void OnDestroy() // 객체 제거 시점
        {
            if (Instance != this) // 현재 관리자 여부 확인
            {
                return; // 다른 객체 제거 무시
            }

            Shutdown(); // 관리자 종료 처리
            ProjectServices.Unregister<GameManager>(); // 서비스 저장소 해제
            Instance = null; // 전역 참조 초기화
        }

        public void Initialize() // 관리자 초기화
        {
            SetState(GameState.Boot); // 초기 부트 상태 설정
            ProjectLog.Log("GameManager 초기화 완료"); // 초기화 로그 출력
        }

        public void Shutdown() // 관리자 종료
        {
            ProjectLog.Log("GameManager 종료"); // 종료 로그 출력
        }

        public void SetState(GameState state) // 게임 상태 변경
        {
            CurrentState = state; // 현재 상태 저장
            GameEvents.PublishGameStateChanged(state); // 상태 변경 이벤트 발행
            ProjectLog.Log($"게임 상태 변경: {state}"); // 상태 변경 로그 출력
        }
    }
}
