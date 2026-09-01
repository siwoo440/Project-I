using System.Linq; // 씬 루트와 아이템 목록 검색 기능 참조
using ProjectI.Items; // 기존 빠른 슬롯 아이템 기능 참조
using ProjectI.Player; // 플레이어 입력 기능 참조
using UnityEditor; // 검증 메뉴와 Missing Script 확인 기능 참조
using UnityEditor.SceneManagement; // 대상 씬 열기 기능 참조
using UnityEngine; // 유니티 오브젝트 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    public static class Phase5Day20Validator // 기존 WorldItem·PlayerInventory 재사용 구조 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string RootName = "===Day20 Existing Item Test==="; // 새 Day20 시험장 루트 이름
        private const string ReadyMarkerName = "===Day20 Existing Item Ready v2==="; // 새 Day20 완료 마커 이름
        private const string LegacyRootName = "===Day20 Item Recoverable System==="; // 이전 회수품 시험장 루트 이름
        private const string LegacyReadyMarkerName = "===Day20 Item Recoverable Ready==="; // 이전 회수품 완료 마커 이름

        [MenuItem("Tools/Project I/Day 20/Validate Existing Item Carry Fix")] // 수동 Day20 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(true); // 대화상자를 포함한 검증 실행
            Debug.Log(success ? "[Project I] Day20 기존 빠른 슬롯 운반 구조 검증 PASS" : "[Project I] Day20 기존 빠른 슬롯 운반 구조 검증 FAIL"); // 최종 검증 로그 출력
        }

        public static bool Validate(bool showDialog) // Day20 정적 구조 전체 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            bool success = true; // 전체 검증 결과 초기화
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName); // 새 Day20 시험장 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ReadyMarkerName); // 새 완료 마커 조회
            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 플레이어 입력 래퍼 조회
            GameObject player = inputReader == null ? null : inputReader.gameObject; // 기존 플레이어 루트 조회
            PlayerInventory inventory = player == null ? null : player.GetComponent<PlayerInventory>(); // 기존 빠른 슬롯 인벤토리 조회
            PlayerCarryController carryController = player == null ? null : player.GetComponent<PlayerCarryController>(); // 기존 화면 운반 제어기 조회
            QuickSlotHud quickSlotHud = player == null ? null : player.GetComponent<QuickSlotHud>(); // 기존 하단 빠른 슬롯 HUD 조회
            Camera playerCamera = player == null ? null : player.GetComponentInChildren<Camera>(true); // 기존 1인칭 카메라 조회
            success &= Check(root != null, "새 Day20 기존 아이템 시험장 존재"); // 새 시험장 존재 검증
            success &= Check(marker != null, "새 Day20 완료 마커 존재"); // 새 완료 마커 존재 검증
            success &= Check(scene.GetRootGameObjects().All(item => item.name != LegacyRootName), "이전 별도 회수품 시험장 제거"); // 이전 시험장 제거 검증
            success &= Check(scene.GetRootGameObjects().All(item => item.name != LegacyReadyMarkerName), "이전 회수품 완료 마커 제거"); // 이전 마커 제거 검증
            success &= Check(player != null && inventory != null && carryController != null, "기존 PlayerInventory·PlayerCarryController 유지"); // 기존 아이템 구조 유지 검증
            success &= Check(quickSlotHud != null, "기존 하단 빠른 슬롯 HUD 유지"); // 슬롯 HUD 존재 검증

            if (player != null) // 플레이어 존재 시 추가 구조 검증
            {
                success &= Check(player.GetComponents<MonoBehaviour>().All(component => component == null || component.GetType().Name != "PlayerRecoverableCarrier"), "이전 PlayerRecoverableCarrier 제거"); // 이전 직접 운반 컴포넌트 제거 검증
            }

            if (playerCamera != null) // 플레이어 카메라 존재 시 CarryPoint 검증
            {
                Transform oneHandPoint = playerCamera.transform.Find("OneHandCarryPoint"); // 기존 한손 CarryPoint 조회
                Transform twoHandPoint = playerCamera.transform.Find("TwoHandCarryPoint"); // 기존 양손 CarryPoint 조회
                success &= Check(oneHandPoint != null && oneHandPoint.localPosition.x > 0.2f, "한손 CarryPoint가 화면 오른쪽에 위치"); // 오른손 배치 검증
                success &= Check(twoHandPoint != null && Mathf.Abs(twoHandPoint.localPosition.x) < 0.05f, "양손 CarryPoint가 화면 중앙에 위치"); // 양손 중앙 배치 검증
                success &= Check(playerCamera.transform.Find("RecoverableOneHandCarryPoint") == null, "이전 왼손 회수품 CarryPoint 제거"); // 이전 왼손 포인트 제거 검증
                success &= Check(playerCamera.transform.Find("RecoverableTwoHandCarryPoint") == null, "이전 회수품 양손 CarryPoint 제거"); // 이전 양손 포인트 제거 검증
            }

            if (root != null) // 시험장 존재 시 아이템 구성 검증
            {
                WorldItem[] items = root.GetComponentsInChildren<WorldItem>(true); // 새 시험장의 기존 WorldItem 목록 조회
                success &= Check(items.Length == 4, "테스트 보물 4개가 기존 WorldItem으로 구성"); // 보물 개수와 구조 검증
                success &= Check(items.Count(item => item.CarryType == CarryType.OneHand) == 2, "한손 보물 2개 구성"); // 한손 보물 개수 검증
                success &= Check(items.Count(item => item.CarryType == CarryType.TwoHand) == 2, "양손 보물 2개 구성"); // 양손 보물 개수 검증
                success &= Check(items.Any(item => item.DisplayName == "은 동전" && item.CarryType == CarryType.OneHand), "은 동전이 한손 빠른 슬롯 아이템"); // 은 동전 구성 검증
                success &= Check(items.Any(item => item.DisplayName == "장인의 금속 장식" && item.CarryType == CarryType.OneHand), "금속 장식이 한손 빠른 슬롯 아이템"); // 금속 장식 구성 검증
                success &= Check(items.Any(item => item.DisplayName == "왕관" && item.CarryType == CarryType.TwoHand), "왕관이 양손 빠른 슬롯 아이템"); // 왕관 구성 검증
                success &= Check(items.Any(item => item.DisplayName == "신들의 조각상" && item.CarryType == CarryType.TwoHand), "조각상이 양손 빠른 슬롯 아이템"); // 조각상 구성 검증
            }

            if (showDialog) // 수동 검증 결과 대화상자 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day20 기존 빠른 슬롯 운반 구조 검증 PASS" : "Day20 검증 FAIL - Console을 확인하세요.", "확인"); // 검증 결과 대화상자 출력
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool Check(bool condition, string label) // 개별 검증 결과 로그 출력
        {
            if (condition) // 검증 통과 여부 확인
            {
                Debug.Log("[Project I][Day20] PASS - " + label); // 통과 로그 출력
                return true; // 성공 반환
            }

            Debug.LogError("[Project I][Day20] FAIL - " + label); // 실패 로그 출력
            return false; // 실패 반환
        }
    }
}
