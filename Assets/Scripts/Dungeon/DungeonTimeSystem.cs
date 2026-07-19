using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// 던전의 게임시간과 현실 제한시간을 관리.
    /// 게임시간을 6시부터 24시까지 진행하고 마감 시 마차 거리로 탈출 성공과 유기 실패를 판정.
    /// 유기 실패 시 플레이어의 모든 소지품을 현재 위치에 드랍하고 게임 진행을 정지.
    /// </summary>
    public class DungeonTimeSystem : MonoBehaviour // 던전 제한시간 관리 컴포넌트
    {
        [Header("시간 설정")] // 던전 시간 설정 구분
        [SerializeField] float realDurationSeconds = 18f * 60f; // 던전 제한시간 18분
        [SerializeField] float startHour = 6f; // 던전 시작 게임시간
        [SerializeField] float endHour = 24f; // 던전 종료 게임시간
        [SerializeField] float wagonProximity = 5f; // 마차 탈출 인정 거리

        float elapsed; // 현재까지 흐른 현실시간
        bool deadlineFired; // 제한시간 처리 완료 여부
        bool failed; // 제한시간 유기 실패 여부
        Wagon wagon; // 던전 탈출용 마차
        InventorySystem playerInventory; // 플레이어 소지품 인벤토리

        public float Progress => realDurationSeconds > 0f ? Mathf.Clamp01(elapsed / realDurationSeconds) : 1f; // 제한시간 진행 비율
        public float GameHour => Mathf.Lerp(startHour, endHour, Progress); // 현재 던전 게임시간
        public float RemainingSeconds => Mathf.Max(0f, realDurationSeconds - elapsed); // 남은 현실시간
        public bool IsLocked => Progress >= 1f; // 탈출 봉쇄 여부
        public bool HasFailed => failed; // 외부 수직 슬라이스 검증에서 유기 실패 여부 확인
        public float ElapsedSeconds => elapsed; // 실제 던전 진행시간 반환
        public event System.Action Failed; // 제한시간 유기 실패 이벤트

        void Start() // 시간과 필수 참조 초기화
        {
            Time.timeScale = 1f; // 이전 종료 상태의 시간 정지 해제
            wagon = FindFirstObjectByType<Wagon>(); // 현재 씬의 마차 검색
            playerInventory = FindFirstObjectByType<InventorySystem>(); // 현재 씬의 플레이어 인벤토리 검색
        }

        void Update() // 매 프레임 제한시간 진행
        {
            if (deadlineFired) // 제한시간 처리 완료 여부 확인
            {
                return; // 중복 제한시간 처리 방지
            }

            elapsed += Time.deltaTime; // 경과시간 누적

            if (Progress >= 1f) // 제한시간 종료 여부 확인
            {
                deadlineFired = true; // 제한시간 처리 완료 상태 저장
                OnDeadline(); // 제한시간 종료 결과 처리
            }
        }

        void OnDeadline() // 제한시간 종료 시 탈출 또는 유기 판정
        {
            if (wagon != null && wagon.HasLeft) // 기존 탈출 성공 여부 확인
            {
                return; // 탈출 후 유기 처리 방지
            }

            bool isNearWagon = wagon != null && playerInventory != null && Vector3.Distance(playerInventory.transform.position, wagon.transform.position) <= wagonProximity; // 마차 근처 여부 계산

            if (isNearWagon) // 마차 자동 탈출 조건 확인
            {
                Debug.Log("[Time] 시간 초과 — 마차에서 탈출 성공"); // 자동 탈출 결과 출력
                wagon.Leave(playerInventory); // 플레이어 소지품 확보 후 탈출 처리
                return; // 유기 실패 처리 방지
            }

            failed = true; // 제한시간 유기 상태 활성화
            Failed?.Invoke(); // 결과 매니저에 유기 실패 전달

            if (playerInventory != null) // 플레이어 인벤토리 존재 여부 확인
            {
                playerInventory.DropAll(playerInventory.transform.position); // 유기 위치에 모든 소지품 드랍
            }

            Debug.Log("[Time] 24:00 도달 — 탈출 봉쇄(유기, 탈출 실패)"); // 제한시간 실패 결과 출력
            Time.timeScale = 0f; // 실패 후 게임 진행 정지
        }

        void OnGUI() // 던전 시간과 실패 화면 표시
        {
            int gameHour = Mathf.FloorToInt(GameHour); // 현재 게임시간의 시 계산
            int gameMinute = Mathf.FloorToInt((GameHour - gameHour) * 60f); // 현재 게임시간의 분 계산
            int remainingSeconds = Mathf.CeilToInt(RemainingSeconds); // 남은 현실시간의 정수 초 계산
            GUIStyle timeStyle = new GUIStyle(GUI.skin.label); // 시간 표시 스타일 생성

            timeStyle.fontSize = 14; // 시간 글자 크기 설정
            timeStyle.alignment = TextAnchor.UpperRight; // 시간 오른쪽 정렬 설정

            GUI.Label(new Rect(Screen.width - 230f, 6f, 220f, 22f), $"던전 시간 {gameHour:00}:{gameMinute:00}   (남은 {remainingSeconds / 60:00}:{remainingSeconds % 60:00})", timeStyle); // 현재 시간과 남은 시간 표시

            if (failed && (RunResultManager.Instance == null || !RunResultManager.Instance.HasResult)) 
                // 중앙 결과 화면이 없을 때만 기존 실패 화면 표시
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 실패 제목 스타일 생성
                GUIStyle messageStyle = new GUIStyle(GUI.skin.label); // 실패 안내 스타일 생성

                titleStyle.fontSize = 24; // 실패 제목 글자 크기 설정
                titleStyle.alignment = TextAnchor.MiddleCenter; // 실패 제목 중앙 정렬 설정
                messageStyle.fontSize = 16; // 실패 안내 글자 크기 설정
                messageStyle.alignment = TextAnchor.MiddleCenter; // 실패 안내 중앙 정렬 설정

                GUI.Label(new Rect(0f, Screen.height / 2f - 40f, Screen.width, 40f), "제한 시간 초과 — 탈출 실패 (유기)", titleStyle); // 유기 실패 제목 표시
                GUI.Label(new Rect(0f, Screen.height / 2f + 4f, Screen.width, 30f), "(플레이 정지 후 다시 시작하세요)", messageStyle); // 재시작 안내 표시
            }
        }
    }
}