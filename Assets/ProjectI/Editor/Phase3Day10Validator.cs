using System.Linq; // 씬 루트와 Light 목록 검증 기능 참조
using ProjectI.Brightness; // 기존 자연광·밝기 관리자·실내 영역 참조
using ProjectI.Diagnostics; // F1 시간·자연광 페이지 참조
using ProjectI.TimeOfDay; // 게임 시간 프로필과 컨트롤러 참조
using UnityEditor; // 에디터 검증 메뉴 참조
using UnityEditor.SceneManagement; // 검증 대상 씬 열기 기능 참조
using UnityEngine; // Light·Mathf 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase3Day10Validator // Day 10 시간대·자연광 시스템 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 검증 대상 탐사 씬 경로

        [MenuItem("Tools/Project I/Day 10/Validate")] // 수동 Day 10 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴에서 Day 10 검증 실행
        {
            Validate(true); // 전체 Day 10 검증 후 결과 대화상자 표시
        }

        public static bool Validate(bool showDialog) // 시간 프로필·Directional Light·기존 밝기 시스템 연결 검증
        {
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameTimeController controller = Object.FindFirstObjectByType<GameTimeController>(); // 게임 시간 컨트롤러 조회
            NaturalLightController naturalLight = Object.FindFirstObjectByType<NaturalLightController>(); // 기존 자연광 컨트롤러 조회
            BrightnessManager brightnessManager = Object.FindFirstObjectByType<BrightnessManager>(); // 기존 최종 밝기 관리자 조회
            IndoorBrightnessArea indoorArea = Object.FindFirstObjectByType<IndoorBrightnessArea>(); // 실내 자연광 제외 검증용 방 영역 조회
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // F1 페이지가 연결된 플레이어 조회
            NaturalLightDebugPage debugPage = player == null ? null : player.GetComponent<NaturalLightDebugPage>(); // 시간·자연광 F1 페이지 조회
            bool profilePass = ValidateProfile(); // 시간대별 태양·달 밝기 프로필 검증
            bool componentsPass = controller != null && naturalLight != null && brightnessManager != null; // 핵심 시간·자연광 구성 요소 존재 검증
            bool configurationPass = controller != null && Mathf.Approximately(controller.StartHour, 12f) && Mathf.Approximately(controller.RealSecondsPerGameMinute, 1f); // 12시 시작·1초당 게임 1분 설정 검증
            bool directionalPass = ValidateDirectionalLights(controller); // 태양·달 실제 Directional Light 구성 검증
            bool visualSyncPass = ValidateVisualSync(controller, naturalLight); // 낮·밤 게임 밝기와 화면 Light 동기화 검증
            bool indoorRulePass = ValidateIndoorNaturalRule(controller, naturalLight, brightnessManager, indoorArea); // 실내에서 자연광 값이 직접 더해지지 않는 기존 규칙 유지 검증
            bool debugPagePass = debugPage != null && debugPage.SortOrder == 60; // F1 Time / Natural Light 페이지 추가와 순서 검증
            bool success = profilePass && componentsPass && configurationPass && directionalPass && visualSyncPass && indoorRulePass && debugPagePass; // 전체 Day 10 검증 결과 계산

            LogResult("Time Profile", profilePass); // 시간대 프로필 결과 출력
            LogResult("Core Components", componentsPass); // 핵심 구성 요소 결과 출력
            LogResult("12:00 Start / 1s = 1 Game Minute", configurationPass); // 기본 시간 진행 설정 결과 출력
            LogResult("Sun + Moon Directional Lights", directionalPass); // 태양·달 화면 Light 결과 출력
            LogResult("Day / Night Visual Sync", visualSyncPass); // 게임 밝기와 화면 Light 동기화 결과 출력
            LogResult("Indoor Natural Light Exclusion", indoorRulePass); // 실내 자연광 제외 규칙 결과 출력
            LogResult("F1 Time / Natural Light Page", debugPagePass); // 새 F1 페이지 결과 출력
            Debug.Log(success ? "[Project I] PASS - Day 10 시간대 및 자연광 검증 완료" : "[Project I] FAIL - Day 10 검증 항목을 확인하세요."); // 전체 검증 결과 출력

            if (showDialog) // 수동 실행 결과 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I Day 10", success ? "Day 10 검증 PASS" : "Day 10 검증 FAIL - Console의 개별 항목을 확인하세요.", "확인"); // 최종 검증 결과 안내
            }

            return success; // 전체 검증 결과 반환
        }

        private static bool ValidateProfile() // 핵심 시간대와 자연광 수치 프로필 검증
        {
            bool phasePass = GameTimeProfile.EvaluatePhase(6f) == GameTimePhase.Dawn // 06시 새벽 판정 확인
                && GameTimeProfile.EvaluatePhase(12f) == GameTimePhase.Day // 12시 낮 판정 확인
                && GameTimeProfile.EvaluatePhase(19f) == GameTimePhase.Dusk // 19시 저녁 판정 확인
                && GameTimeProfile.EvaluatePhase(23f) == GameTimePhase.Night; // 23시 밤 판정 확인
            bool sunPass = GameTimeProfile.EvaluateSunBrightness(12f) >= 0.60f // 정오 태양이 강한지 확인
                && Mathf.Approximately(GameTimeProfile.EvaluateSunBrightness(0f), 0f) // 자정 태양이 0인지 확인
                && GameTimeProfile.EvaluateSunBrightness(6f) > 0f // 새벽 태양이 증가 중인지 확인
                && GameTimeProfile.EvaluateSunBrightness(19f) > 0f; // 저녁 태양이 감소 중인지 확인
            bool moonPass = GameTimeProfile.EvaluateMoonBrightness(0f) > 0f // 자정 달빛 존재 여부 확인
                && Mathf.Approximately(GameTimeProfile.EvaluateMoonBrightness(12f), 0f) // 정오 달빛이 0인지 확인
                && GameTimeProfile.EvaluateMoonBrightness(19f) > 0f; // 저녁 달빛 증가 여부 확인
            return phasePass && sunPass && moonPass; // 시간대·태양·달 프로필 전체 결과 반환
        }

        private static bool ValidateDirectionalLights(GameTimeController controller) // 실제 태양·달 Directional Light 구성 검증
        {
            if (controller == null || controller.SunLight == null || controller.MoonLight == null) // 시간 컨트롤러와 두 화면 Light 존재 여부 확인
            {
                return false; // 필수 Directional Light 누락 검증 실패
            }

            bool typePass = controller.SunLight.type == LightType.Directional && controller.MoonLight.type == LightType.Directional; // 두 화면 광원이 Directional인지 확인
            bool distinctPass = controller.SunLight != controller.MoonLight; // 태양과 달이 서로 다른 Light인지 확인
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 모든 Light 조회
            bool legacyPass = allLights.Where(light => light != null && light.type == LightType.Directional && light != controller.SunLight && light != controller.MoonLight).All(light => !light.enabled); // Day 10 이외 Directional Light가 중복 발광하지 않는지 확인
            return typePass && distinctPass && legacyPass; // Directional Light 전체 검증 결과 반환
        }

        private static bool ValidateVisualSync(GameTimeController controller, NaturalLightController naturalLight) // 낮과 밤 게임 밝기·실제 화면 조명 상태 동기화 검증
        {
            if (controller == null || naturalLight == null || controller.SunLight == null || controller.MoonLight == null) // 검증 필수 참조 존재 여부 확인
            {
                return false; // 참조 누락 검증 실패
            }

            float restoreHour = controller.StartHour; // 검증 후 복원할 기본 시간 저장
            controller.SetTime(12f); // 정오 상태 적용
            bool dayPass = naturalLight.SunBrightness >= 0.60f // 정오 게임 태양 밝기 확인
                && Mathf.Approximately(naturalLight.MoonBrightness, 0f) // 정오 게임 달빛 제거 확인
                && controller.SunLight.enabled // 정오 실제 태양 Light 활성 확인
                && controller.SunLight.intensity > 0f // 정오 실제 태양 강도 확인
                && !controller.MoonLight.enabled; // 정오 실제 달 Light 비활성 확인
            controller.SetTime(0f); // 자정 상태 적용
            bool nightPass = Mathf.Approximately(naturalLight.SunBrightness, 0f) // 자정 게임 태양 밝기 제거 확인
                && naturalLight.MoonBrightness > 0f // 자정 게임 달빛 존재 확인
                && !controller.SunLight.enabled // 자정 실제 태양 Light 비활성 확인
                && controller.MoonLight.enabled // 자정 실제 달 Light 활성 확인
                && controller.MoonLight.intensity > 0f; // 자정 실제 달 Light 강도 확인
            controller.SetTime(restoreHour); // 검증 후 시작 시간 상태로 복원
            return dayPass && nightPass; // 낮·밤 두 상태가 모두 동기화될 때 성공 반환
        }

        private static bool ValidateIndoorNaturalRule(GameTimeController controller, NaturalLightController naturalLight, BrightnessManager brightnessManager, IndoorBrightnessArea indoorArea) // 실내 자연광 직접 제외 규칙 검증
        {
            if (controller == null || naturalLight == null || brightnessManager == null || indoorArea == null || indoorArea.Volume == null) // 필요한 기존 밝기 구성 요소 존재 여부 확인
            {
                return false; // 검증 참조 누락 실패 반환
            }

            brightnessManager.Configure(naturalLight); // Edit Mode에서도 기존 BrightnessManager가 현재 자연광 컨트롤러를 사용하도록 참조 보장
            float restoreHour = controller.StartHour; // 검증 후 복원할 시작 시간 저장
            controller.SetTime(12f); // 자연광이 가장 강한 낮 상태 적용
            Vector3 indoorPosition = indoorArea.Volume.transform.TransformPoint(indoorArea.Volume.center); // 실내 영역 Collider 중심 월드 위치 계산
            BrightnessSample sample = brightnessManager.SampleBrightness(indoorPosition); // 방 중심의 기존 최종 밝기 규칙 계산
            controller.SetTime(restoreHour); // 검증 후 시작 시간으로 복원
            return sample.AreaType == BrightnessAreaType.Indoor && Mathf.Approximately(sample.NaturalBrightness, 0f); // 실내 판정과 자연광 0 유지 여부 반환
        }

        private static void LogResult(string label, bool passed) // 개별 검증 결과 Console 출력
        {
            Debug.Log($"[Project I] {(passed ? "PASS" : "FAIL")} - Day 10 {label}"); // PASS 또는 FAIL 한 줄 결과 출력
        }
    }
}
