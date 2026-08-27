using System.IO; // 입력 파일 문자열 확인 기능 참조
using System.Linq; // 목록 검색 기능 참조
using System.Text.RegularExpressions; // Input Action JSON 정밀 검사 기능 참조
using ProjectI.Interaction; // 상호작용 기능 참조
using ProjectI.Items; // 아이템 기능 참조
using ProjectI.World; // 이동 플랫폼 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase2Day5Validator // 5일차 상호작용과 월드 아이템 검증 도구
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // Input Action 파일 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름

        [MenuItem("Tools/Project I/Day 5/Validate")] // 수동 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 검증 실행
        {
            Validate(true); // 대화상자 포함 검증 실행
        }

        public static bool Validate(bool showDialog) // 5일차 전체 검증
        {
            bool inputBindingPasses = ValidateInteractionInput(); // F 바인딩과 Hold 제거 검증
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 대상 씬 열기
            GameObject[] roots = scene.GetRootGameObjects(); // 씬 루트 오브젝트 조회
            GameObject player = roots.FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = roots.FirstOrDefault(root => root.name == MapRootName); // 테스트 맵 루트 조회
            bool playerComponentsPass = player != null && player.GetComponent<PlayerInteractor>() != null && player.GetComponent<PlayerCarryController>() != null && player.GetComponent<InteractionPromptHud>() != null; // 플레이어 5일차 핵심 컴포넌트 검증
            bool carryPointPass = player != null && player.GetComponentInChildren<Camera>(true) != null && player.GetComponentInChildren<Camera>(true).transform.Find("OneHandCarryPoint") != null && player.GetComponentInChildren<Camera>(true).transform.Find("TwoHandCarryPoint") != null; // 한손·양손 카메라 운반 지점 검증
            bool zonePass = mapRoot != null && mapRoot.transform.Find("08_InteractionTest") != null; // 상호작용 시험 구역 존재 검증
            int interactableCount = Object.FindObjectsByType<TestInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length; // 시험 상호작용 물체 수 조회
            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 시험 월드 아이템 목록 조회
            bool carryTypesPass = worldItems.Any(item => item.CarryType == CarryType.OneHand) && worldItems.Any(item => item.CarryType == CarryType.TwoHand); // 한손·양손 시험 아이템 모두 존재하는지 검증
            bool testObjectsPass = interactableCount >= 3 && worldItems.Length >= 2 && carryTypesPass; // Press/Hold/Toggle과 한손·양손 아이템 존재 여부 검증
            MovingPlatformPassengerTrigger passengerTrigger = Object.FindFirstObjectByType<MovingPlatformPassengerTrigger>(); // 이동 플랫폼 탑승 감지 기능 조회
            bool movingPlatformPass = passengerTrigger != null && passengerTrigger.Platform != null; // 부모 연결 대신 이동량 전달용 플랫폼 연결 검증
            bool progressLogicPass = ValidateProgressLogic(); // Hold 진행도 순수 로직 검증
            bool success = inputBindingPasses && playerComponentsPass && carryPointPass && zonePass && testObjectsPass && movingPlatformPass && progressLogicPass; // 전체 검증 결과 계산

            LogResult("Interact F Binding / No Fixed Hold", inputBindingPasses); // 입력 설정 검증 결과 출력
            LogResult("Player Interaction / Carry Components", playerComponentsPass); // 플레이어 컴포넌트 검증 결과 출력
            LogResult("OneHand / TwoHand Carry Points", carryPointPass); // 한손·양손 운반 지점 검증 결과 출력
            LogResult("Interaction Test Zone", zonePass); // 시험 구역 검증 결과 출력
            LogResult("Press / Hold / Toggle / OneHand / TwoHand Items", testObjectsPass); // 시험 오브젝트 검증 결과 출력
            LogResult("Moving Platform Passenger Link", movingPlatformPass); // 이동 플랫폼 탑승자 연결 검증 결과 출력
            LogResult("Hold Progress Logic", progressLogicPass); // Hold 진행도 로직 검증 결과 출력

            if (showDialog) // 대화상자 표시 여부 확인
            {
                string message = success ? "Day 5 검증 성공" : "Day 5 검증 실패 - Console 확인"; // 전체 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateInteractionInput() // Input Action JSON에서 5일차 규칙 검증
        {
            if (!File.Exists(InputActionsPath)) // 입력 파일 존재 여부 확인
            {
                return false; // 입력 파일이 없으면 실패 반환
            }

            string json = File.ReadAllText(InputActionsPath); // Input Action JSON 읽기
            string actionObjectPattern = "\\{(?=[^{}]*\\\"name\\\"\\s*:\\s*\\\"Interact\\\")(?=[^{}]*\\\"type\\\"\\s*:\\s*\\\"Button\\\")[^{}]*\\}"; // Interact Action 객체 검색 패턴
            Match actionMatch = Regex.Match(json, actionObjectPattern, RegexOptions.Singleline); // Interact Action 객체 조회
            bool actionExists = actionMatch.Success; // Interact Action 존재 여부 확인
            bool fixedHoldOnInteract = actionExists && Regex.IsMatch(actionMatch.Value, "\\\"interactions\\\"\\s*:\\s*\\\"Hold\\\"", RegexOptions.Singleline); // Interact 자체의 고정 Hold 여부 확인
            string fBindingPattern = "\\{(?=[^{}]*\\\"path\\\"\\s*:\\s*\\\"<Keyboard>/f\\\")(?=[^{}]*\\\"action\\\"\\s*:\\s*\\\"Interact\\\")[^{}]*\\}"; // F 키 Interact 바인딩 패턴
            string eBindingPattern = "\\{(?=[^{}]*\\\"path\\\"\\s*:\\s*\\\"<Keyboard>/e\\\")(?=[^{}]*\\\"action\\\"\\s*:\\s*\\\"Interact\\\")[^{}]*\\}"; // E 키 Interact 바인딩 패턴
            bool hasF = Regex.IsMatch(json, fBindingPattern, RegexOptions.Singleline); // F Interact 바인딩 존재 여부 확인
            bool hasLegacyE = Regex.IsMatch(json, eBindingPattern, RegexOptions.Singleline); // 기존 E Interact 바인딩 잔존 여부 확인
            return actionExists && hasF && !hasLegacyE && !fixedHoldOnInteract; // Interact에 한정한 입력 규칙 검증 결과 반환
        }

        private static bool ValidateProgressLogic() // Hold 진행도 계산 로직 검사
        {
            float start = InteractionProgress.Normalize(0f, 2f); // 시작 진행도 계산
            float middle = InteractionProgress.Normalize(1f, 2f); // 절반 진행도 계산
            float complete = InteractionProgress.Normalize(3f, 2f); // 완료 초과 진행도 계산
            float instant = InteractionProgress.Normalize(0f, 0f); // 즉시 완료 규칙 계산
            return Approximately(start, 0f) && Approximately(middle, 0.5f) && Approximately(complete, 1f) && Approximately(instant, 1f); // 기본 진행도 규칙 검증
        }

        private static bool Approximately(float left, float right) // 실수 근사 비교
        {
            return Mathf.Abs(left - right) <= 0.001f; // 작은 오차 범위 안인지 반환
        }

        private static void LogResult(string label, bool success) // 개별 검증 결과 출력
        {
            if (success) // 성공 여부 확인
            {
                Debug.Log($"[Project I] PASS - Day 5 {label}"); // 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError($"[Project I] FAIL - Day 5 {label}"); // 실패 로그 출력
        }
    }
}
