using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // 선택한 Dungeon Scene 이동 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonSelectionManager : MonoBehaviour // 마을에서 선택한 던전 경로를 Scene 간 보관
    {
        public static DungeonSelectionManager Instance { get; private set; } // 현재 활성 던전 선택 매니저 접근점

        [Header("선택 가능한 경로")] // Inspector 던전 경로 목록 구분
        [SerializeField] DungeonRouteData[] routes; // 마차에서 선택할 던전 경로 목록
        [SerializeField] int defaultRouteIndex; // 새 캠페인의 기본 던전 경로 번호

        int selectedRouteIndex; // 현재 선택한 던전 경로 번호

        public int RouteCount => routes != null ? routes.Length : 0; // 등록된 던전 경로 개수 반환

        public DungeonRouteData SelectedRoute // 현재 선택한 던전 경로 반환
        {
            get
            {
                if (routes == null || routes.Length == 0) // 던전 경로 등록 여부 확인
                {
                    return null; // 선택 가능한 경로 없음 반환
                }

                int safeIndex = Mathf.Clamp(selectedRouteIndex, 0, routes.Length - 1); // 선택 번호를 배열 범위로 제한
                return routes[safeIndex]; // 현재 선택된 던전 경로 반환
            }
        }

        void Awake() // 던전 선택 매니저 싱글톤과 기본 경로 초기화
        {
            if (Instance != null && Instance != this) // 기존 던전 선택 매니저 존재 여부 확인
            {
                Destroy(gameObject); // 중복 던전 선택 매니저 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 던전 선택 매니저로 저장
            transform.SetParent(null); // DontDestroyOnLoad 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 선택한 경로 유지

            if (routes == null || routes.Length == 0) // 던전 경로 목록 등록 여부 확인
            {
                Debug.LogError("[DungeonSelection] Dungeon Routes가 비어 있습니다."); // 경로 누락 오류 출력
                selectedRouteIndex = 0; // 선택 번호 기본값 적용
                return; // 경로 초기화 중단
            }

            selectedRouteIndex = Mathf.Clamp(defaultRouteIndex, 0, routes.Length - 1); // 기본 경로 번호를 안전 범위로 제한
            Debug.Log($"[DungeonSelection] 기본 경로 — {SelectedRoute.DisplayName}"); // 기본 경로 결과 출력
        }

        void OnDestroy() // 던전 선택 매니저 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 선택 매니저인지 확인
            {
                Instance = null; // 전역 선택 매니저 참조 초기화
            }
        }

        public DungeonRouteData GetRoute(int index) // 지정된 번호의 던전 경로 반환
        {
            if (routes == null || index < 0 || index >= routes.Length) // 경로 번호 유효 여부 확인
            {
                return null; // 유효하지 않은 경로 없음 반환
            }

            return routes[index]; // 지정된 던전 경로 반환
        }

        public bool SelectRoute(int index) // 지정된 번호의 던전 경로 선택
        {
            DungeonRouteData route = GetRoute(index); // 선택할 던전 경로 가져오기

            if (route == null) // 던전 경로 존재 여부 확인
            {
                Debug.LogWarning($"[DungeonSelection] 유효하지 않은 경로 번호: {index}"); // 잘못된 경로 선택 경고 출력
                return false; // 던전 경로 선택 실패 반환
            }

            selectedRouteIndex = index; // 현재 선택 경로 번호 저장
            Debug.Log($"[DungeonSelection] 경로 선택 — {route.DisplayName}"); // 선택 결과 출력
            return true; // 던전 경로 선택 성공 반환
        }

        public bool LoadSelectedDungeon() // 캠페인과 Scene을 확인한 뒤 선택한 던전으로 출발
        {
            DungeonRouteData route = SelectedRoute; // 현재 선택한 던전 경로 가져오기

            if (route == null) // 선택된 던전 경로 존재 여부 확인
            {
                Debug.LogError("[DungeonSelection] 선택된 던전 경로가 없습니다."); // 경로 누락 오류 출력
                return false; // 던전 출발 실패 반환
            }

            CampaignManager campaignManager = CampaignManager.Instance; // 현재 캠페인 매니저 가져오기

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                Debug.LogError("[DungeonSelection] CampaignManager가 없습니다."); // 캠페인 매니저 누락 오류 출력
                return false; // 던전 출발 실패 반환
            }

            if (!campaignManager.CanStartNextRun) // 현재 캠페인에서 출발 가능한지 확인
            {
                Debug.LogWarning($"[DungeonSelection] 출발 불가 — {campaignManager.GetDeadlineMessage()}"); // 캠페인 상태에 맞는 출발 차단 사유 출력
                return false; // 던전 출발 실패 반환
            }

            if (string.IsNullOrWhiteSpace(route.SceneName)) // 선택 경로의 Scene 이름 입력 여부 확인
            {
                Debug.LogError($"[DungeonSelection] {route.DisplayName}의 Scene 이름이 비어 있습니다."); // Scene 이름 누락 오류 출력
                return false; // 던전 출발 실패 반환
            }

            if (!Application.CanStreamedLevelBeLoaded(route.SceneName)) // Build Profile에 Scene이 등록됐는지 확인
            {
                Debug.LogError($"[DungeonSelection] Build Profile에서 {route.SceneName} Scene을 찾을 수 없습니다."); // Scene 등록 누락 오류 출력
                return false; // 던전 출발 실패 반환
            }

            Time.timeScale = 1f; // Scene 이동 전에 게임시간 정상화
            Debug.Log($"[DungeonSelection] {route.DisplayName}으로 출발합니다."); // 던전 출발 결과 출력
            SceneManager.LoadScene(route.SceneName); // 선택된 Dungeon Scene 로드
            return true; // 던전 출발 성공 반환
        }
    }
}