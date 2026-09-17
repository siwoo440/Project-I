using ProjectI.Diagnostics; // 프로젝트 로그 참조
using ProjectI.UI; // 일시정지 창
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public static class ProjectBootstrap // 런타임 핵심 객체 생성기
    {
        private const string RootObjectName = "===ProjectI Core==="; // 핵심 루트 객체 이름

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // 씬 시작 전 자동 실행
        private static void CreateProjectRoot() // 핵심 루트 생성
        {
            if (GameObject.Find(RootObjectName) != null) // 기존 루트 확인
            {
                return; // 중복 생성 방지
            }

            ProjectServices.Clear(); // 이전 서비스 정보 초기화
            GameEvents.Clear(); // 이전 이벤트 정보 초기화
            GameObject rootObject = new GameObject(RootObjectName); // 핵심 루트 객체 생성
            Object.DontDestroyOnLoad(rootObject); // 씬 전환 유지 설정
            rootObject.AddComponent<GameManager>(); // 게임 관리자 추가
            rootObject.AddComponent<SceneFlowManager>(); // 씬 관리자 추가
            rootObject.AddComponent<PauseMenu>(); // 게임 중 Esc 창 (플레이어가 있을 때만 열림)
            rootObject.AddComponent<GameHud>(); // 정식 HUD (플레이어가 있을 때만 표시)
            ProjectLog.Log("Project I 런타임 부트스트랩 완료"); // 부트스트랩 로그 출력
        }
    }
}
