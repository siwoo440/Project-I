using System.Linq; // 광원 개수와 루트 검색 기능 참조
using ProjectI.Brightness; // 밝기 시스템 기능 참조
using UnityEditor; // 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day7Validator // Day 7 밝기 코어와 건축물 모듈 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 씬 경로

        [MenuItem("Tools/Project I/Day 7/Validate")] // 수동 Day 7 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            bool success = Validate(true); // 전체 Day 7 검증 실행

            if (!success) // 실패 여부 확인
            {
                Debug.LogError("[Project I] Day 7 검증 실패 - 위 FAIL 항목을 확인하세요."); // 실패 안내 출력
            }
        }

        public static bool Validate(bool showDialog) // Day 7 구조와 핵심 계산 규칙 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject systemRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "===Brightness System==="); // 밝기 시스템 루트 조회
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "===Day3 Test Map==="); // 공용 테스트 맵 루트 조회
            Transform zone = mapRoot == null ? null : mapRoot.transform.Find("10_BrightnessTest"); // Day 7 시험 모듈 조회
            Transform building = zone == null ? null : zone.Find("MassiveIndoorBuilding"); // 대형 건축물 조회
            IndoorBrightnessArea indoorArea = building == null ? null : building.GetComponentInChildren<IndoorBrightnessArea>(true); // 건축물 내부 방 영역 조회
            BrightnessSource[] zoneSources = zone == null ? new BrightnessSource[0] : zone.GetComponentsInChildren<BrightnessSource>(true); // Day 7 광원 전체 조회
            int outdoorCount = zoneSources.Count(source => source.OwnerArea == null); // 외부 광원 수 계산
            int indoorCount = zoneSources.Count(source => source.OwnerArea != null); // 내부 광원 수 계산
            bool systemPass = systemRoot != null && systemRoot.GetComponent<BrightnessManager>() != null && systemRoot.GetComponent<NaturalLightController>() != null; // 관리자와 자연광 컨트롤러 검증
            bool sensorPass = player != null && player.GetComponent<PlayerBrightnessSensor>() != null; // 플레이어 밝기 센서 검증
            bool modulePass = zone != null && zone.Find("ConnectorWalkway") != null && zone.Find("OutdoorPlaza") != null; // 기존 맵과 연결된 Day 7 모듈 구조 검증
            bool buildingPass = building != null && building.Find("Floor") != null && building.Find("Roof") != null && building.Find("WestWall_DoorLintel") != null; // 대형 건축물 기본 구조 검증
            bool areaPass = indoorArea != null && indoorArea.Volume != null && indoorArea.Volume.isTrigger && indoorArea.Volume.size.x >= 20f && indoorArea.Volume.size.z >= 15f; // 거대한 내부 방 영역 크기와 Trigger 검증
            bool sourcePass = outdoorCount >= 2 && indoorCount >= 3; // 외부 광원 2개와 내부 광원 3개 이상 검증
            bool debugPagePass = player != null && player.GetComponent<BrightnessDebugHud>() != null; // 공통 F1 창에 등록될 밝기 디버그 페이지 공급자 검증
            bool mathPass = ValidateMathRules(); // 거리 감쇠와 최종 합산 순수 수학 규칙 검증
            bool success = systemPass && sensorPass && modulePass && buildingPass && areaPass && sourcePass && debugPagePass && mathPass; // 전체 검증 결과 계산

            LogResult("Brightness System", systemPass); // 밝기 관리자 구성 결과 출력
            LogResult("Player Brightness Sensor", sensorPass); // 플레이어 센서 결과 출력
            LogResult("Connected Test Module", modulePass); // 기존 테스트 맵 연결 구조 결과 출력
            LogResult("Massive Indoor Building", buildingPass); // 대형 건축물 결과 출력
            LogResult("Indoor Room Volume", areaPass); // 내부 방 영역 결과 출력
            LogResult("Outdoor 2 + Indoor 3 Sources", sourcePass); // 외부·내부 광원 분리 결과 출력
            LogResult("Brightness Debug Page Provider", debugPagePass); // 공통 디버그 페이지 공급자 결과 출력
            LogResult("Brightness Math Rules", mathPass); // 밝기 수학 규칙 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 7 밝기 코어·외부/내부·건축물 모듈 검증 완료" : "[Project I] FAIL - Day 7 밝기 코어 검증 항목을 확인하세요."); // 전체 결과 출력

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 7", success ? "Day 7 검증 PASS" : "Day 7 검증 FAIL - Console을 확인하세요.", "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateMathRules() // 실제 계산 코드에 사용되는 순수 밝기 수학 규칙 검증
        {
            bool centerFull = Mathf.Approximately(BrightnessMath.CalculateContribution(0.6f, 0f, 10f), 0.6f); // 광원 중심에서 기본 밝기 100% 적용 검증
            bool halfDistance = Mathf.Approximately(BrightnessMath.CalculateContribution(0.6f, 5f, 10f), 0.3f); // Range 절반 거리에서 50% 감쇠 검증
            bool rangeZero = Mathf.Approximately(BrightnessMath.CalculateContribution(0.6f, 10f, 10f), 0f); // Range 끝에서 광원 영향 0 검증
            bool combinedClamp = Mathf.Approximately(BrightnessMath.Combine(0.8f, 0.5f), 1f); // 자연광+지역광 합계가 1을 넘지 않도록 제한 검증
            return centerFull && halfDistance && rangeZero && combinedClamp; // 모든 밝기 수학 규칙 결과 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 Console 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 7 {label}"); // PASS 또는 FAIL 문구 출력
        }
    }
}
