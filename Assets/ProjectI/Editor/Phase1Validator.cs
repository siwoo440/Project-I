using System.Linq; // 목록 검색 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.Rendering; // 렌더 파이프라인 설정 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase1Validator // Phase 1 검증 도구
    {
        private static readonly string[] RequiredScenePaths = // 필수 씬 경로 목록
        {
            "Assets/ProjectI/Scenes/Boot.unity", // 부트 씬 경로
            "Assets/ProjectI/Scenes/MainMenu.unity", // 메인 메뉴 씬 경로
            "Assets/ProjectI/Scenes/ExplorationOffice.unity" // 사무소 씬 경로
        };

        [MenuItem("Tools/Project I/Validate Phase 1")] // 수동 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 검증 실행
        {
            Validate(true); // 결과 대화상자 포함 검증
        }

        public static bool Validate(bool showDialog) // Phase 1 전체 검증
        {
            bool scenesExist = RequiredScenePaths.All(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null); // 필수 씬 존재 확인
            string[] enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(); // 활성 빌드 씬 수집
            bool buildScenesMatch = enabledScenes.SequenceEqual(RequiredScenePaths); // 빌드 씬 순서 확인
            bool linearColorSpace = PlayerSettings.colorSpace == ColorSpace.Linear; // 선형 색 공간 확인
            bool urpConfigured = GraphicsSettings.defaultRenderPipeline != null; // 렌더 파이프라인 설정 확인
            bool success = scenesExist && buildScenesMatch && linearColorSpace && urpConfigured; // 전체 검증 결과 계산

            LogResult("필수 Scene", scenesExist); // 씬 검증 결과 출력
            LogResult("Build Settings 순서", buildScenesMatch); // 빌드 설정 결과 출력
            LogResult("Linear Color Space", linearColorSpace); // 색 공간 결과 출력
            LogResult("URP Render Pipeline Asset", urpConfigured); // URP 결과 출력

            if (showDialog) // 대화상자 표시 여부 확인
            {
                string message = success ? "Phase 1 검증 성공" : "Phase 1 검증 실패 - Console 확인"; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 표시
            }

            return success; // 전체 검증 결과 반환
        }

        private static void LogResult(string label, bool success) // 개별 검증 결과 출력
        {
            if (success) // 성공 여부 확인
            {
                Debug.Log($"[Project I] PASS - {label}"); // 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError($"[Project I] FAIL - {label}"); // 실패 로그 출력
        }
    }
}
