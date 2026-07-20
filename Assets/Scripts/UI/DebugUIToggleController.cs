using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // 새 Input System의 키보드 입력 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DebugUIToggleController : MonoBehaviour // 기존 OnGUI 디버그 화면의 표시 상태 관리
    {
        [Header("시작 표시 상태")] // Scene 시작 시 디버그 UI 표시 여부 구분
        [SerializeField] bool playerInfoVisibleAtStart = false; // 플레이어 관련 디버그 UI 시작 표시 여부
        [SerializeField] bool inventoryInfoVisibleAtStart = false; // 인벤토리 관련 디버그 UI 시작 표시 여부
        [SerializeField] bool spawnInfoVisibleAtStart = false; // 자동 스폰 관련 디버그 UI 시작 표시 여부
        [SerializeField] bool campaignInfoVisibleAtStart = false; // 캠페인 관련 디버그 UI 시작 표시 여부

        public static bool PlayerInfoVisible { get; private set; } // 플레이어 관련 디버그 UI 표시 상태
        public static bool InventoryInfoVisible { get; private set; } // 인벤토리 관련 디버그 UI 표시 상태
        public static bool SpawnInfoVisible { get; private set; } // 자동 스폰 관련 디버그 UI 표시 상태
        public static bool CampaignInfoVisible { get; private set; } // 캠페인 관련 디버그 UI 표시 상태

        void Awake() // Inspector 시작 설정으로 모든 디버그 UI 상태 초기화
        {
            PlayerInfoVisible = playerInfoVisibleAtStart; // 플레이어 정보 시작 상태 적용
            InventoryInfoVisible = inventoryInfoVisibleAtStart; // 인벤토리 정보 시작 상태 적용
            SpawnInfoVisible = spawnInfoVisibleAtStart; // 스폰 정보 시작 상태 적용
            CampaignInfoVisible = campaignInfoVisibleAtStart; // 캠페인 정보 시작 상태 적용
        }

        void Update() // 기능키 입력으로 각 디버그 UI 표시 상태 전환
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null) // 키보드 연결 여부 확인
            {
                return; // 단축키 처리 중단
            }

            if (keyboard.f1Key.wasPressedThisFrame) // F1 입력 여부 확인
            {
                PlayerInfoVisible = !PlayerInfoVisible; // 플레이어 정보 표시 상태 전환
                LogState("플레이어 정보", PlayerInfoVisible); // 변경 상태를 Console에 출력
            }

            if (keyboard.f2Key.wasPressedThisFrame) // F2 입력 여부 확인
            {
                InventoryInfoVisible = !InventoryInfoVisible; // 인벤토리 정보 표시 상태 전환
                LogState("인벤토리 및 마차 정보", InventoryInfoVisible); // 변경 상태를 Console에 출력
            }

            if (keyboard.f4Key.wasPressedThisFrame) // F4 입력 여부 확인
            {
                SpawnInfoVisible = !SpawnInfoVisible; // 자동 스폰 정보 표시 상태 전환
                LogState("자동 스폰 정보", SpawnInfoVisible); // 변경 상태를 Console에 출력
            }

            if (keyboard.f7Key.wasPressedThisFrame) // F7 입력 여부 확인
            {
                CampaignInfoVisible = !CampaignInfoVisible; // 캠페인 정보 표시 상태 전환
                LogState("캠페인 정보", CampaignInfoVisible); // 변경 상태를 Console에 출력
            }
        }

        void LogState(string uiName, bool visible) // 변경된 디버그 UI 표시 상태를 Console에 출력
        {
            string stateText = visible ? "표시" : "숨김"; // 현재 표시 상태 문구 계산
            Debug.Log($"[DebugUI] {uiName} {stateText}"); // 디버그 UI 상태 출력
        }
    }
}