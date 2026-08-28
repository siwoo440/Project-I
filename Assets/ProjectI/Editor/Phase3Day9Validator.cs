using System.Linq; // 씬 루트·자식 검색과 조건 검증 기능 참조
using ProjectI.Brightness; // 고정 BrightnessSource와 방 영역 기능 참조
using ProjectI.Diagnostics; // F1 광원 숫자·전체 계산 페이지 기능 참조
using ProjectI.Lighting; // 고정 광원 컨트롤러와 페이지 기능 참조
using UnityEditor; // 에디터 메뉴 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day9Validator // Day 9 고정 광원과 F1 밝기 진단 구성 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 탐사 씬 경로

        [MenuItem("Tools/Project I/Day 9/Validate")] // 수동 Day 9 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 검증 실행
        {
            Validate(true); // 전체 Day 9 검증 후 대화상자 표시
        }

        public static bool Validate(bool showDialog) // 씬에 적용된 Day 9 핵심 구조와 상태 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회
            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "===Day3 Test Map==="); // 테스트 맵 루트 조회
            Transform brightnessZone = mapRoot == null ? null : mapRoot.transform.Find("10_BrightnessTest"); // 밝기 시험 모듈 조회
            IndoorBrightnessArea indoorArea = brightnessZone == null ? null : brightnessZone.GetComponentInChildren<IndoorBrightnessArea>(true); // 고정 광원을 담는 현재 방 영역 조회
            FixedLightController[] fixedLights = indoorArea == null ? new FixedLightController[0] : indoorArea.GetComponentsInChildren<FixedLightController>(true); // 현재 방의 모든 고정 광원 컨트롤러 조회
            bool countPass = fixedLights.Length == 4; // 벽 횃불 3개와 화로 1개 존재 여부 검증
            bool namesPass = HasLight(fixedLights, "WallTorch_North") && HasLight(fixedLights, "WallTorch_South") && HasLight(fixedLights, "WallTorch_East") && HasLight(fixedLights, "Brazier_Center"); // 예정된 4개 고정 광원 이름 검증
            bool fixedTypePass = fixedLights.All(ValidateFixedSourceType); // 모든 연결 광원이 Fixed 타입인지 검증
            bool startOffPass = fixedLights.All(light => light != null && !light.IsLit && light.BrightnessSources.All(source => source != null && !source.SourceEnabled)); // 모든 고정 광원이 처음 꺼진 상태인지 검증
            bool togglePass = ValidateToggle(fixedLights.FirstOrDefault()); // 실제 컨트롤러 TurnOn/TurnOff가 BrightnessSource와 동기화되는지 검증
            bool oldLampRemovalPass = brightnessZone != null && FindDescendant(brightnessZone, "IndoorLamp_A") == null && FindDescendant(brightnessZone, "IndoorLamp_B") == null && FindDescendant(brightnessZone, "IndoorLamp_C") == null; // 이전 실내 시험 램프 제거 여부 검증
            bool fixedPagePass = player != null && player.GetComponent<FixedLightDebugPage>() != null; // F1 고정 광원 페이지 추가 여부 검증
            bool calculationPagePass = player != null && player.GetComponent<LightCalculationDebugPage>() != null; // 현재 위치 전체 광원 계산 페이지 추가 여부 검증
            bool labelManagerPass = player != null && player.GetComponent<LightDebugLabelManager>() != null; // F1 월드 광원 기여 밝기 숫자 표시 기능 검증
            bool success = countPass && namesPass && fixedTypePass && startOffPass && togglePass && oldLampRemovalPass && fixedPagePass && calculationPagePass && labelManagerPass; // 전체 Day 9 검증 결과 계산

            LogResult("4 Fixed Lights", countPass); // 고정 광원 개수 결과 출력
            LogResult("Fixed Light Names", namesPass); // 고정 광원 이름 결과 출력
            LogResult("BrightnessSourceType.Fixed", fixedTypePass); // Fixed 광원 타입 결과 출력
            LogResult("Start Unlit", startOffPass); // 시작 소화 상태 결과 출력
            LogResult("F Toggle State Sync", togglePass); // 컨트롤러·BrightnessSource 동기화 결과 출력
            LogResult("Old IndoorLamp Removal", oldLampRemovalPass); // 이전 시험 램프 삭제 결과 출력
            LogResult("F1 Fixed Light Debug Page", fixedPagePass); // 고정 광원 디버그 페이지 결과 출력
            LogResult("F1 Light Calculation Page", calculationPagePass); // 전체 광원 계산 페이지 결과 출력
            LogResult("F1 World Contribution Labels", labelManagerPass); // 월드 숫자 라벨 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 9 고정 광원 및 F1 광원 진단 검증 완료" : "[Project I] FAIL - Day 9 검증 항목을 확인하세요."); // 전체 검증 결과 출력

            if (showDialog) // 수동 실행 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 9", success ? "Day 9 검증 PASS" : "Day 9 검증 FAIL - Console의 개별 항목을 확인하세요.", "확인"); // 최종 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool HasLight(FixedLightController[] lights, string objectName) // 지정 GameObject 이름의 고정 광원 존재 여부 확인
        {
            return lights.Any(light => light != null && light.gameObject.name == objectName); // 이름 일치 고정 광원 검색 결과 반환
        }

        private static bool ValidateFixedSourceType(FixedLightController light) // 고정 조명에 속한 모든 BrightnessSource 타입 검증
        {
            if (light == null || light.BrightnessSources == null || light.BrightnessSources.Length == 0) // 컨트롤러와 광원 배열 유효성 확인
            {
                return false; // 광원 누락 검증 실패 반환
            }

            return light.BrightnessSources.All(source => source != null && source.SourceType == BrightnessSourceType.Fixed); // 모든 연결 광원이 Fixed일 때 성공 반환
        }

        private static bool ValidateToggle(FixedLightController light) // 컨트롤러 켜기·끄기와 실제 광원 상태 동기화 검증
        {
            if (light == null || light.BrightnessSources == null || light.BrightnessSources.Length == 0) // 검증 대상 고정 광원 존재 여부 확인
            {
                return false; // 검증 실패 반환
            }

            light.TurnOn(); // 테스트 광원을 임시로 켜기
            bool onPass = light.IsLit && light.BrightnessSources.All(source => source != null && source.SourceEnabled); // 컨트롤러와 모든 BrightnessSource가 함께 켜졌는지 확인
            light.TurnOff(); // 씬 기본 상태를 유지하도록 다시 끄기
            bool offPass = !light.IsLit && light.BrightnessSources.All(source => source != null && !source.SourceEnabled); // 컨트롤러와 모든 BrightnessSource가 함께 꺼졌는지 확인
            EditorUtility.SetDirty(light); // 검증 후 복구된 꺼짐 상태를 저장 대상으로 표시
            return onPass && offPass; // 켜기와 끄기 모두 정상일 때 성공 반환
        }

        private static Transform FindDescendant(Transform root, string targetName) // 자식 깊이와 상관없이 이름으로 오브젝트 검색
        {
            if (root == null) // 검색 루트 누락 확인
            {
                return null; // 검색 실패 반환
            }

            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == targetName); // 비활성 자식까지 포함해 이름 일치 대상 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 Console 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 9 {label}"); // PASS 또는 FAIL 한 줄 결과 출력
        }
    }
}
