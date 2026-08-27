using System.Linq; // 목록 검색 기능 참조
using ProjectI.Player; // 플레이어 기능 참조
using ProjectI.World; // 월드 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 입력 액션 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase2Day4Validator // 4일차 플레이어와 테스트 맵 검증 도구
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions"; // 입력 액션 에셋 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름

        [MenuItem("Tools/Project I/Day 4/Validate")] // 수동 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 검증 실행
        {
            Validate(true); // 대화상자 포함 검증 실행
        }

        public static bool Validate(bool showDialog) // 4일차 전체 검증
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath); // 입력 액션 에셋 조회
            InputActionMap playerMap = inputActions == null ? null : inputActions.FindActionMap("Player", false); // Player 액션 맵 조회
            bool movementActionsExist = playerMap != null && playerMap.FindAction("Move", false) != null && playerMap.FindAction("Look", false) != null && playerMap.FindAction("Sprint", false) != null && playerMap.FindAction("Jump", false) != null && playerMap.FindAction("Crouch", false) != null; // 4일차 이동 필수 액션 검증
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 대상 씬 열기
            GameObject[] roots = scene.GetRootGameObjects(); // 씬 루트 오브젝트 조회
            GameObject player = roots.FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = roots.FirstOrDefault(root => root.name == MapRootName); // 테스트 맵 루트 조회
            bool playerCoreExists = player != null && player.GetComponent<CharacterController>() != null && player.GetComponent<PlayerMovement>() != null && player.GetComponent<PlayerStamina>() != null && player.GetComponent<PlayerInputReader>() != null; // 기존 플레이어 핵심 기능 검증
            bool day4ComponentsExist = player != null && player.GetComponent<PlayerHealth>() != null && player.GetComponent<PlayerCrouch>() != null && player.GetComponent<PlayerFallDamage>() != null; // 4일차 신규 플레이어 기능 검증
            bool day4ZonesExist = mapRoot != null && mapRoot.transform.Find("05_CrouchTest") != null && mapRoot.transform.Find("06_FallTest") != null && mapRoot.transform.Find("07_MovingPlatformTest") != null; // 4일차 테스트 구역 검증
            bool movingPlatformExists = Object.FindFirstObjectByType<MovingPlatform>() != null; // 이동 플랫폼 컴포넌트 검증
            bool healthLogicPasses = ValidateHealthLogic(); // 체력 순수 로직 자체 검사
            bool fallLogicPasses = ValidateFallDamageLogic(); // 추락 피해 계산 로직 자체 검사
            bool success = movementActionsExist && playerCoreExists && day4ComponentsExist && day4ZonesExist && movingPlatformExists && healthLogicPasses && fallLogicPasses; // 전체 검증 결과 계산

            LogResult("Move / Look / Sprint / Jump / Crouch Actions", movementActionsExist); // 이동 입력 검증 결과 출력
            LogResult("Player Core Components", playerCoreExists); // 기존 플레이어 기능 검증 결과 출력
            LogResult("Health / Crouch / Fall Damage Components", day4ComponentsExist); // 4일차 플레이어 기능 검증 결과 출력
            LogResult("Crouch / Fall / Moving Platform Zones", day4ZonesExist); // 4일차 테스트 구역 검증 결과 출력
            LogResult("Moving Platform Component", movingPlatformExists); // 이동 플랫폼 검증 결과 출력
            LogResult("Health Logic", healthLogicPasses); // 체력 로직 검증 결과 출력
            LogResult("Fall Damage Logic", fallLogicPasses); // 추락 피해 로직 검증 결과 출력

            if (showDialog) // 대화상자 표시 여부 확인
            {
                string message = success ? "Day 4 검증 성공" : "Day 4 검증 실패 - Console 확인"; // 검증 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateHealthLogic() // 체력 순수 상태 로직 검사
        {
            HealthState state = new HealthState(100f); // 테스트용 최대 체력 100 상태 생성
            float firstDamage = state.ApplyDamage(25f); // 25 피해 적용
            float healAmount = state.Heal(10f); // 10 회복 적용
            float lethalDamage = state.ApplyDamage(200f); // 치명 피해 적용
            return Approximately(firstDamage, 25f) && Approximately(healAmount, 10f) && lethalDamage > 0f && Approximately(state.CurrentHealth, 0f) && state.IsDead; // 피해·회복·사망 결과 검증
        }

        private static bool ValidateFallDamageLogic() // 추락 피해 계산 로직 검사
        {
            float safeDamage = FallDamageCalculator.Calculate(2f, 3f, 20f, 100f); // 안전 거리 추락 피해 계산
            float normalDamage = FallDamageCalculator.Calculate(5f, 3f, 20f, 100f); // 일반 추락 피해 계산
            float cappedDamage = FallDamageCalculator.Calculate(20f, 3f, 20f, 100f); // 최대 피해 제한 추락 계산
            return Approximately(safeDamage, 0f) && Approximately(normalDamage, 40f) && Approximately(cappedDamage, 100f); // 안전·일반·최대 피해 결과 검증
        }

        private static bool Approximately(float left, float right) // 실수 근사 비교
        {
            return Mathf.Abs(left - right) <= 0.001f; // 작은 오차 범위 안인지 반환
        }

        private static void LogResult(string label, bool success) // 개별 검증 결과 출력
        {
            if (success) // 성공 여부 확인
            {
                Debug.Log($"[Project I] PASS - Day 4 {label}"); // 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError($"[Project I] FAIL - Day 4 {label}"); // 실패 로그 출력
        }
    }
}
