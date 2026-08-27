using System.Linq; // 시험 아이템 유형 집계 기능 참조
using ProjectI.Items; // 인벤토리와 아이템 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase2Day6Validator // Day 6 빠른 슬롯과 인벤토리 구성 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로

        [MenuItem("Tools/Project I/Day 6/Validate")] // 수동 Day 6 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 검증 실행
        }

        public static bool Validate(bool showDialog) // Day 6 전체 구성 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "===Day3 Test Map==="); // 공용 테스트 맵 조회
            PlayerInventory inventory = player == null ? null : player.GetComponent<PlayerInventory>(); // 플레이어 인벤토리 조회
            PlayerCarryController carryController = player == null ? null : player.GetComponent<PlayerCarryController>(); // 화면 운반 기능 조회
            QuickSlotHud hud = player == null ? null : player.GetComponent<QuickSlotHud>(); // 빠른 슬롯 HUD 조회
            bool componentsPass = inventory != null && carryController != null && hud != null; // Day 6 핵심 컴포넌트 존재 검증
            bool sixSlotsPass = inventory != null && inventory.SlotCount == 6; // 빠른 슬롯 정확히 6칸 검증
            bool storagePass = player != null && player.transform.Find("InventoryStorage") != null; // 숨김 보관 루트 검증
            Transform testZone = mapRoot == null ? null : mapRoot.transform.Find("09_InventoryTest"); // Day 6 시험 구역 조회
            bool zonePass = testZone != null; // 시험 구역 존재 검증
            WorldItem[] testItems = testZone == null ? new WorldItem[0] : testZone.GetComponentsInChildren<WorldItem>(true); // Day 6 시험 아이템 조회
            int oneHandCount = testItems.Count(item => item.CarryType == CarryType.OneHand); // 한손 시험 아이템 수 계산
            int twoHandCount = testItems.Count(item => item.CarryType == CarryType.TwoHand); // 양손 시험 아이템 수 계산
            bool itemCountPass = testItems.Length >= 7 && oneHandCount >= 6 && twoHandCount >= 1; // 6칸 채우기와 양손 잠금 시험 수량 검증
            bool usablePass = testZone != null && testZone.GetComponentsInChildren<TestUsableItem>(true).Length >= 7; // 좌클릭 사용 시험 기능 검증
            bool rulesPass = ValidateRules(); // 슬롯 원형 이동과 양손 잠금 순수 규칙 검증
            bool success = componentsPass && sixSlotsPass && storagePass && zonePass && itemCountPass && usablePass && rulesPass; // 전체 검증 결과 계산

            LogResult("Inventory / Carry / HUD Components", componentsPass); // 핵심 컴포넌트 결과 출력
            LogResult("Quick Slots = 6", sixSlotsPass); // 6칸 슬롯 결과 출력
            LogResult("Inventory Storage Root", storagePass); // 숨김 보관 루트 결과 출력
            LogResult("09 Inventory Test Zone", zonePass); // Day 6 시험 구역 결과 출력
            LogResult("6 OneHand + 1 TwoHand Items", itemCountPass); // 시험 아이템 구성 결과 출력
            LogResult("Usable Item Tests", usablePass); // 좌클릭 Use 시험 기능 결과 출력
            LogResult("Quick Slot Rules", rulesPass); // 순수 슬롯 규칙 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 6 빠른 슬롯·인벤토리 구성 검증 완료" : "[Project I] FAIL - Day 6 검증 항목을 확인하세요."); // 전체 검증 결과 출력

            if (showDialog) // 수동 실행 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 6", success ? "Day 6 검증 PASS" : "Day 6 검증 FAIL - Console을 확인하세요.", "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateRules() // Unity 오브젝트와 무관한 빠른 슬롯 핵심 규칙 검증
        {
            bool wrapPrevious = QuickSlotRules.WrapIndex(-1, 6) == 5; // 1번에서 이전 이동 시 6번으로 순환 검증
            bool wrapNext = QuickSlotRules.WrapIndex(6, 6) == 0; // 6번에서 다음 이동 시 1번으로 순환 검증
            bool oneHandUnlocked = !QuickSlotRules.IsSelectionLocked(CarryType.OneHand, true); // 한손 운반 중 슬롯 자유 전환 검증
            bool twoHandLocked = QuickSlotRules.IsSelectionLocked(CarryType.TwoHand, true); // 양손 실제 운반 중 슬롯 잠금 검증
            bool droppedUnlocked = !QuickSlotRules.IsSelectionLocked(CarryType.TwoHand, false); // 양손 내려놓은 뒤 잠금 해제 검증
            return wrapPrevious && wrapNext && oneHandUnlocked && twoHandLocked && droppedUnlocked; // 모든 순수 규칙 결과 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 Console 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 6 {label}"); // PASS 또는 FAIL 문구 출력
        }
    }
}
