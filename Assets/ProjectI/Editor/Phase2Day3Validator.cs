using System.Linq; // 목록 검색 기능 참조
using ProjectI.Player; // 플레이어 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 입력 액션 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase2Day3Validator // 3일차 플레이어와 테스트 맵 검증 도구
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 입력 액션 에셋 경로

        [MenuItem("Tools/Project I/Day 3/Validate")] // 수동 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 검증 실행
        {
            Validate(true); // 대화상자 포함 검증 실행
        }

        public static bool Validate(bool showDialog) // 3일차 전체 검증
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // 입력 액션 에셋 조회
            bool inputAssetExists = inputActions != null; // 입력 에셋 존재 결과 계산
            InputActionMap playerMap = inputActions == null ? null : inputActions.FindActionMap("Player", false); // Player 액션 맵 조회
            bool requiredActionsExist = playerMap != null && playerMap.FindAction("Move", false) != null && playerMap.FindAction("Look", false) != null && playerMap.FindAction("Sprint", false) != null; // 필수 액션 존재 결과 계산
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 대상 씬 열기
            GameObject[] roots = scene.GetRootGameObjects(); // 씬 루트 오브젝트 조회
            GameObject player = roots.FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = roots.FirstOrDefault(root => root.name == "===Day3 Test Map==="); // 테스트 맵 루트 조회
            bool playerComponentsExist = player != null && player.GetComponent<CharacterController>() != null && player.GetComponent<PlayerInputReader>() != null && player.GetComponent<PlayerMovement>() != null && player.GetComponent<PlayerStamina>() != null && player.GetComponent<PlayerLook>() != null; // 플레이어 필수 컴포넌트 검증
            bool playerCameraExists = player != null && player.GetComponentInChildren<Camera>(true) != null; // 플레이어 카메라 검증
            bool testZonesExist = mapRoot != null && mapRoot.transform.Find("01_SprintLane") != null && mapRoot.transform.Find("02_Slalom") != null && mapRoot.transform.Find("03_NarrowCorridor") != null && mapRoot.transform.Find("04_StairRamp") != null && mapRoot.transform.Find("05_CrouchGate_Future") != null && mapRoot.transform.Find("06_FallTest_Future") != null; // 테스트 구역 구성 검증
            int mainCameraCount = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.CompareTag("MainCamera")); // 메인 카메라 수 계산
            bool singleMainCamera = mainCameraCount == 1; // 메인 카메라 단일 여부 검증
            bool success = inputAssetExists && requiredActionsExist && playerComponentsExist && playerCameraExists && testZonesExist && singleMainCamera; // 전체 검증 결과 계산

            LogResult("Input Action Asset", inputAssetExists); // 입력 에셋 검증 결과 출력
            LogResult("Move / Look / Sprint Actions", requiredActionsExist); // 필수 액션 검증 결과 출력
            LogResult("Player Components", playerComponentsExist); // 플레이어 컴포넌트 검증 결과 출력
            LogResult("Player Main Camera", playerCameraExists); // 카메라 검증 결과 출력
            LogResult("Exploration Test Zones", testZonesExist); // 테스트 구역 검증 결과 출력
            LogResult("Single Main Camera", singleMainCamera); // 카메라 중복 검증 결과 출력

            if (showDialog) // 대화상자 표시 여부 확인
            {
                string message = success ? "Day 3 검증 성공" : "Day 3 검증 실패 - Console 확인"; // 검증 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static void LogResult(string label, bool success) // 개별 검증 결과 출력
        {
            if (success) // 성공 여부 확인
            {
                Debug.Log($"[Project I] PASS - Day 3 {label}"); // 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError($"[Project I] FAIL - Day 3 {label}"); // 실패 로그 출력
        }
    }
}
