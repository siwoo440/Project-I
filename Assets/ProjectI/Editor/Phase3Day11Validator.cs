using System.Linq; // 씬 루트 검색 기능 참조
using ProjectI.Power; // 발전기와 전기등 검증 기능 참조
using UnityEditor; // 에디터 검증 메뉴 참조
using UnityEditor.SceneManagement; // 검증 대상 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day11Validator // Day 11 발전기·전기등 구성 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 탐사 씬 경로
        private const string ReadyMarkerName = "===Day11 Generator Power Ready==="; // 자동 적용 완료 마커 이름

        [MenuItem("Tools/Project I/Day 11/Validate")] // 수동 Day 11 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴에서 Day 11 검증 실행
        {
            Validate(true); // 전체 검증과 결과 대화상자 표시
        }

        public static bool Validate(bool showDialog) // 발전기 연결과 초기 전력 상태 정적 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GeneratorController generator = Object.FindFirstObjectByType<GeneratorController>(); // 씬 발전기 조회
            ElectricLightController[] electricLights = Object.FindObjectsByType<ElectricLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 활성·비활성 전기등 전체 조회
            bool markerPass = scene.GetRootGameObjects().Any(root => root.name == ReadyMarkerName); // 완료 마커 존재 여부 검사
            bool generatorPass = generator != null && generator.MaxFuel >= 1f && generator.CurrentFuel >= 0f && generator.FuelConsumptionPerSecond > 0f; // 발전기 연료와 소비 설정 검사
            bool connectionPass = generator != null && generator.ConnectedLightCount >= 4; // 발전기에 4개 이상 전기등 연결 여부 검사
            bool lightsPass = electricLights.Length >= 4 && electricLights.All(light => light != null && light.BrightnessSources != null && light.BrightnessSources.Length > 0); // 각 전기등의 밝기 광원 연결 검사
            bool initialStatePass = generator != null && !generator.IsRunning && electricLights.All(light => !light.IsPowered); // 시작 시 발전기와 전기등 소등 상태 검사
            bool passed = markerPass && generatorPass && connectionPass && lightsPass && initialStatePass; // 전체 검증 결과 계산

            if (!markerPass) // 완료 마커 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day11] 완료 마커가 없습니다."); // 완료 마커 누락 오류 출력
            }

            if (!generatorPass) // 발전기 기본 설정 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day11] 발전기 또는 연료 설정이 올바르지 않습니다."); // 발전기 설정 오류 출력
            }

            if (!connectionPass) // 전기등 연결 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day11] 발전기에 전기등 4개가 연결되지 않았습니다."); // 발전기 연결 오류 출력
            }

            if (!lightsPass) // 전기등 광원 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day11] 전기등 또는 BrightnessSource 구성이 부족합니다."); // 전기등 구성 오류 출력
            }

            if (!initialStatePass) // 초기 소등 상태 검증 실패 여부 확인
            {
                Debug.LogError("[Project I][Day11] 시작 상태가 발전기 OFF / 전기등 OFF가 아닙니다."); // 초기 전력 상태 오류 출력
            }

            if (passed) // 전체 검증 성공 여부 확인
            {
                Debug.Log("[Project I][Day11] 발전기·전기등·연료 소비 구성이 정적으로 정상입니다."); // 검증 성공 로그 출력
            }

            if (showDialog) // 수동 검증 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", passed ? "Day 11 검증 통과" : "Day 11 검증 실패 - Console 확인", "확인"); // 검증 결과 대화상자 표시
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
