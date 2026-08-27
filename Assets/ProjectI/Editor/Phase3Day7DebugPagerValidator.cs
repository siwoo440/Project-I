using System.Linq; // Input Binding과 씬 루트 검색 기능 참조
using ProjectI.Brightness; // 밝기 디버그 페이지 기능 참조
using ProjectI.Diagnostics; // 공통 디버그 페이지 관리자 기능 참조
using ProjectI.Player; // 플레이어 디버그 페이지 기능 참조
using UnityEditor; // 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Input Action 검증 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조
using UnityEngine.UI; // 공통 디버그 Canvas UI 검증 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day7DebugPagerValidator // F1 통합 디버그 창과 자동 페이지 등록 구조 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 검증 대상 Input Action Asset 경로

        [MenuItem("Tools/Project I/Day 7/Validate F1 Debug Pager")] // 수동 통합 디버그 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(true); // 전체 검증 실행

            if (!success) // 실패 여부 확인
            {
                Debug.LogError("[Project I] F1 Debug Pager 검증 실패 - 위 FAIL 항목을 확인하세요."); // 실패 안내 출력
            }
        }

        public static bool Validate(bool showDialog) // F1·좌우 화살표·공통 페이지 공급자·Canvas 구조 검증
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // Input Action Asset 로드
            InputActionMap map = actions == null ? null : actions.FindActionMap(GameplayInputActions.Map, false); // Player 액션 맵 조회
            bool f1Pass = HasBinding(map, GameplayInputActions.DebugToggle, "<Keyboard>/f1"); // F1 DebugToggle 기본 바인딩 검증
            bool previousPass = HasBinding(map, GameplayInputActions.DebugPreviousPage, "<Keyboard>/leftArrow"); // 왼쪽 화살표 이전 페이지 기본 바인딩 검증
            bool nextPass = HasBinding(map, GameplayInputActions.DebugNextPage, "<Keyboard>/rightArrow"); // 오른쪽 화살표 다음 페이지 기본 바인딩 검증
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject debugCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "DebugPageCanvas"); // 새 공통 디버그 Canvas 조회
            GameObject oldBrightnessCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "BrightnessDebugCanvas"); // 제거되어야 할 기존 밝기 전용 Canvas 조회
            bool playerPagePass = player != null && player.GetComponent<PlayerDebugHud>() != null && player.GetComponent<PlayerDebugHud>() is DebugPageProvider; // 플레이어 DebugPageProvider 검증
            bool brightnessPagePass = player != null && player.GetComponent<BrightnessDebugHud>() != null && player.GetComponent<BrightnessDebugHud>() is DebugPageProvider; // 밝기 DebugPageProvider 검증
            DebugPageManager manager = debugCanvas == null ? null : debugCanvas.GetComponent<DebugPageManager>(); // 공통 페이지 관리자 조회
            bool canvasPass = debugCanvas != null && debugCanvas.GetComponent<Canvas>() != null && manager != null; // 단일 Canvas와 관리자 존재 검증
            bool buttonsPass = debugCanvas != null && debugCanvas.GetComponentsInChildren<Button>(true).Length >= 2; // 좌우 화면 Button 두 개 이상 검증
            bool oldCanvasRemovedPass = oldBrightnessCanvas == null; // 별도 밝기 Canvas 제거 검증
            bool success = f1Pass && previousPass && nextPass && playerPagePass && brightnessPagePass && canvasPass && buttonsPass && oldCanvasRemovedPass; // 전체 검증 결과 계산

            LogResult("F1 DebugToggle", f1Pass); // F1 액션 결과 출력
            LogResult("LeftArrow Previous Page", previousPass); // 이전 페이지 액션 결과 출력
            LogResult("RightArrow Next Page", nextPass); // 다음 페이지 액션 결과 출력
            LogResult("Player Debug Page Provider", playerPagePass); // 플레이어 페이지 결과 출력
            LogResult("Brightness Debug Page Provider", brightnessPagePass); // 밝기 페이지 결과 출력
            LogResult("Single Debug Canvas + Manager", canvasPass); // 공통 Canvas 결과 출력
            LogResult("Previous / Next UI Buttons", buttonsPass); // 화면 화살표 Button 결과 출력
            LogResult("Old Brightness Canvas Removed", oldCanvasRemovedPass); // 기존 별도 Canvas 제거 결과 출력
            Debug.Log(success ? "[Project I] PASS - F1 Debug Pager / 자동 페이지 목록 구조 검증 완료" : "[Project I] FAIL - F1 Debug Pager 검증 항목을 확인하세요."); // 전체 결과 출력

            if (showDialog) // 수동 검증 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 7", success ? "F1 Debug Pager 검증 PASS" : "F1 Debug Pager 검증 FAIL - Console을 확인하세요.", "확인"); // 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool HasBinding(InputActionMap map, string actionName, string bindingPath) // 지정 액션과 기본 바인딩 존재 여부 확인
        {
            InputAction action = map == null ? null : map.FindAction(actionName, false); // 대상 액션 조회
            return action != null && action.bindings.Any(binding => binding.path == bindingPath); // 지정 기본 경로 존재 결과 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 7 {label}"); // PASS 또는 FAIL 로그 출력
        }
    }
}
