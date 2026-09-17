using System; // 예외
using Steamworks; // Steamworks.NET
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net.Steam // Steam 연결 네임스페이스
{
    public sealed class SteamService : MonoBehaviour // 40일차: Steam 초기화·콜백 처리·이름 조회 (Steam 이 없으면 직접 IP 로 진행)
    {
        public const uint TestAppId = 480; // Steam 공용 시험 앱 (실제 앱 ID 를 받으면 교체)
        private static SteamService instance; // 실행 중인 서비스
        private static bool initializeAttempted; // 초기화 시도 여부

        public static bool Initialized { get; private set; } // Steam 사용 가능
        public static string FailureReason { get; private set; } = string.Empty; // 사용할 수 없는 이유
        public static AppId_t AppId => new AppId_t(TestAppId); // 앱 ID

        public static CSteamID LocalId => Initialized ? SteamUser.GetSteamID() : CSteamID.Nil; // 내 Steam ID
        public static string PersonaName => Initialized ? SteamFriends.GetPersonaName() : string.Empty; // 내 Steam 이름

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 에디터에서 플레이를 반복해도 다시 초기화
        private static void ResetStatics() // 정적 상태 초기화
        {
            initializeAttempted = false; // 다시 시도
            Initialized = false; // 사용 전
            FailureReason = string.Empty; // 정리
            SteamLobbyService.ResetStatics(); // 로비 콜백 정리
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 게임 시작 시
        private static void Boot() // Steam 연결과 친구 초대 처리 준비
        {
            if (!EnsureInitialized() || !SteamLobbyService.EnsureCallbacks()) // Steam 없음
            {
                return; // 직접 IP 만 사용
            }

            SteamLobbyService.JoinRequested -= RouteJoinRequest; // 중복 방지
            SteamLobbyService.JoinRequested += RouteJoinRequest; // 친구 목록 "게임 참가"·초대 수락
        }

        private static void RouteJoinRequest(CSteamID lobby) // 참가 요청 → 세션
        {
            NetworkSession.QueueSteamJoin(lobby); // 메뉴에서 참가 (게임 중이면 저장 후 메뉴로)
        }

        public static bool EnsureInitialized() // 필요할 때 한 번 초기화 (성공하면 true)
        {
            if (initializeAttempted) // 이미 시도
            {
                return Initialized; // 결과
            }

            initializeAttempted = true; // 기록

            try
            {
                if (!Packsize.Test()) // 플랫폼 구조 확인
                {
                    return Fail("Steamworks.NET 플랫폼 구성이 맞지 않습니다"); // 실패
                }

                if (!DllCheck.Test()) // steam_api 라이브러리 확인
                {
                    return Fail("steam_api 라이브러리 버전이 맞지 않습니다"); // 실패
                }

                if (!SteamAPI.Init()) // Steam 연결
                {
                    return Fail("Steam 이 실행 중이 아니거나 앱 ID 가 없습니다 (steam_appid.txt)"); // 실패
                }
            }
            catch (DllNotFoundException exception) // 라이브러리 없음
            {
                return Fail($"steam_api 라이브러리를 찾지 못했습니다: {exception.Message}"); // 실패
            }
            catch (Exception exception) // 기타
            {
                return Fail($"Steam 초기화 오류: {exception.Message}"); // 실패
            }

            Initialized = true; // 성공
            FailureReason = string.Empty; // 정리
            SteamNetworkingUtils.InitRelayNetworkAccess(); // Steam 중계망 준비 (포트 개방 없이 연결)
            GameObject runner = new GameObject("===ProjectI Steam==="); // 콜백 처리기
            DontDestroyOnLoad(runner); // 유지
            instance = runner.AddComponent<SteamService>(); // 등록
            Debug.Log($"[Project I] Steam 연결됨 / {PersonaName} ({LocalId.m_SteamID}) / 앱 {TestAppId}"); // 기록
            return true; // 성공
        }

        public static string NameOf(CSteamID id) // Steam 이름
        {
            if (!Initialized || !id.IsValid()) // 불가
            {
                return string.Empty; // 없음
            }

            return id == LocalId ? PersonaName : SteamFriends.GetFriendPersonaName(id); // 이름
        }

        private static bool Fail(string reason) // 실패 기록
        {
            Initialized = false; // 사용 불가
            FailureReason = reason; // 이유
            Debug.LogWarning($"[Project I] Steam 사용 불가 — {reason} · 직접 IP 연결만 사용합니다"); // 기록
            return false; // 실패
        }

        private void Update() // Steam 콜백 처리
        {
            if (Initialized) // 사용 중
            {
                SteamAPI.RunCallbacks(); // 콜백
            }
        }

        private void OnApplicationQuit() // 종료
        {
            if (!Initialized) // 사용 안 함
            {
                return; // 생략
            }

            SteamLobbyService.Leave(); // 로비 나가기
            SteamAPI.Shutdown(); // Steam 정리
            Initialized = false; // 기록
        }

        private void OnDestroy() // 파괴
        {
            if (instance == this) // 현재
            {
                instance = null; // 정리
            }
        }
    }
}
