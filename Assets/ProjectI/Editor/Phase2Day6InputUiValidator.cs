using System.Linq; // Input Binding과 UI 검색 기능 참조
using ProjectI.Items; // 빠른 슬롯 기능 참조
using ProjectI.Player; // 입력 기능 참조
using UnityEditor; // 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Input Action 검증 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조
using UnityEngine.UI; // Canvas UI Text 검증 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase2Day6InputUiValidator // Day 6 재바인딩 입력과 Canvas UI 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 검증 대상 Input Action 경로

        [MenuItem("Tools/Project I/Day 6/Validate Rebindable Input + Canvas UI")] // 수동 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(); // 전체 검증 실행
            EditorUtility.DisplayDialog("Project I Day 6", success ? "Input / Canvas UI 검증 PASS" : "Input / Canvas UI 검증 FAIL - Console을 확인하세요.", "확인"); // 결과 대화상자 표시
        }

        public static bool Validate() // 입력 액션·Q 버리기·Canvas UI 구조 검증
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // Input Action Asset 로드
            InputActionMap map = actions == null ? null : actions.FindActionMap(GameplayInputActions.Map, false); // Player 액션 맵 조회
            bool dropPass = HasBinding(map, GameplayInputActions.Drop, "<Keyboard>/q"); // Drop 기본 Q 바인딩 검증
            bool usePass = HasBinding(map, GameplayInputActions.Use, "<Mouse>/leftButton"); // Use 좌클릭 바인딩 검증
            bool pausePass = HasBinding(map, GameplayInputActions.Pause, "<Keyboard>/escape"); // Pause ESC 바인딩 검증
            bool scrollPass = HasBinding(map, GameplayInputActions.SlotScroll, "<Mouse>/scroll/y"); // 슬롯 휠 바인딩 검증
            bool slotsPass = true; // 빠른 슬롯 1~6 액션 검증 초기값

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 슬롯 액션 전체 순회
            {
                slotsPass &= HasBinding(map, GameplayInputActions.QuickSlot(index), $"<Keyboard>/{index + 1}"); // 각 숫자키 1~6 바인딩 검증
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            bool rebindPass = player != null && player.GetComponent<InputRebindService>() != null; // 재바인딩 서비스 존재 검증
            GameObject canvasRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "PlayerHUDCanvas"); // Canvas 루트 조회
            bool canvasPass = canvasRoot != null && canvasRoot.GetComponent<Canvas>() != null; // 실제 Canvas 컴포넌트 존재 검증
            Transform panel = canvasRoot == null ? null : canvasRoot.transform.Find("QuickSlotPanel"); // 빠른 슬롯 패널 조회
            bool sixSlotPass = panel != null && Enumerable.Range(1, PlayerInventory.Capacity).All(number => panel.Find($"Slot_{number}") != null); // 슬롯 6칸 존재 검증
            bool numberTopLeftPass = ValidateNumberAnchors(panel); // 각 슬롯 숫자의 왼쪽 위 앵커 검증
            bool success = dropPass && usePass && pausePass && scrollPass && slotsPass && rebindPass && canvasPass && sixSlotPass && numberTopLeftPass; // 전체 결과 계산

            Log("Drop = Q", dropPass); // Drop Q 결과 출력
            Log("Use InputAction", usePass); // Use 액션 결과 출력
            Log("Pause InputAction", pausePass); // Pause 액션 결과 출력
            Log("Slot Scroll InputAction", scrollPass); // 휠 액션 결과 출력
            Log("Slot 1~6 InputActions", slotsPass); // 숫자키 액션 결과 출력
            Log("InputRebindService", rebindPass); // 재바인딩 서비스 결과 출력
            Log("Canvas HUD", canvasPass); // Canvas 결과 출력
            Log("Six Canvas Slots", sixSlotPass); // 슬롯 수 결과 출력
            Log("Slot Numbers Top Left", numberTopLeftPass); // 숫자 위치 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 6 Rebindable Input + Canvas UI" : "[Project I] FAIL - Day 6 Rebindable Input + Canvas UI"); // 전체 결과 출력
            return success; // 전체 검증 결과 반환
        }

        private static bool HasBinding(InputActionMap map, string actionName, string bindingPath) // 지정 액션과 바인딩 존재 여부 확인
        {
            InputAction action = map == null ? null : map.FindAction(actionName, false); // 대상 액션 조회
            return action != null && action.bindings.Any(binding => binding.path == bindingPath); // 지정 기본 바인딩 존재 결과 반환
        }

        private static bool ValidateNumberAnchors(Transform panel) // 슬롯 숫자가 각 칸 왼쪽 위에 배치되었는지 검증
        {
            if (panel == null) // 패널 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            for (int number = 1; number <= PlayerInventory.Capacity; number++) // 1번부터 6번 슬롯 순회
            {
                Transform numberTransform = panel.Find($"Slot_{number}/Number"); // 현재 슬롯 숫자 Text 조회
                RectTransform rect = numberTransform == null ? null : numberTransform.GetComponent<RectTransform>(); // 숫자 RectTransform 조회
                Text text = numberTransform == null ? null : numberTransform.GetComponent<Text>(); // 숫자 Text 조회

                if (rect == null || text == null) // 숫자 UI 구성 누락 확인
                {
                    return false; // 검증 실패 반환
                }

                if (rect.anchorMin != new Vector2(0f, 1f) || rect.anchorMax != new Vector2(0f, 1f) || rect.pivot != new Vector2(0f, 1f)) // 왼쪽 위 고정 앵커 여부 확인
                {
                    return false; // 숫자가 왼쪽 위에 고정되지 않았으면 실패 반환
                }

                if (text.alignment != TextAnchor.UpperLeft || text.text != number.ToString()) // Text 정렬과 번호 문자열 확인
                {
                    return false; // 숫자 표시 규칙 불일치 시 실패 반환
                }
            }

            return true; // 모든 슬롯 숫자 위치 검증 성공
        }

        private static void Log(string label, bool passed) // 개별 검증 결과 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 6 {label}"); // PASS 또는 FAIL 로그 출력
        }
    }
}
