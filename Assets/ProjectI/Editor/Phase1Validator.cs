using System.Linq; // 목록 검색 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.Rendering; // 렌더 파이프라인 설정 참조
using UnityEngine.Rendering.Universal; // URP 에셋 타입 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase1Validator // Phase 1 검증 도구
    {
        private const string BootScenePath = "Assets/ProjectI/Scenes/Boot.unity"; // 부트 씬 경로
        private const string MainMenuScenePath = "Assets/ProjectI/Scenes/MainMenu.unity"; // 메인 메뉴 씬 경로
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 사무소 씬 경로

        private static readonly string[] RequiredScenePaths = // 필수 씬 경로 목록
        {
            BootScenePath, // 부트 씬 경로 등록
            MainMenuScenePath, // 메인 메뉴 씬 경로 등록
            ExplorationOfficeScenePath // 사무소 씬 경로 등록
        };

        private static readonly string[] ObsoleteTemplatePaths = // 삭제 대상 기본 템플릿 경로 목록
        {
            "Assets/Readme.asset", // 기본 리드미 에셋 경로
            "Assets/Scenes", // 기본 샘플 씬 폴더 경로
            "Assets/TutorialInfo" // 기본 튜토리얼 폴더 경로
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
            SceneAsset playModeStartScene = EditorSceneManager.playModeStartScene; // 플레이 시작 씬 조회
            string playModeStartScenePath = playModeStartScene == null ? string.Empty : AssetDatabase.GetAssetPath(playModeStartScene); // 플레이 시작 씬 경로 계산
            bool playModeStartsFromBoot = playModeStartScenePath == BootScenePath; // 부트 씬 시작 여부 확인
            bool linearColorSpace = PlayerSettings.colorSpace == ColorSpace.Linear; // 선형 색 공간 확인
            RenderPipelineAsset activeRenderPipeline = GraphicsSettings.currentRenderPipeline; // 현재 품질에 적용된 활성 렌더 파이프라인 조회
            bool urpConfigured = activeRenderPipeline is UniversalRenderPipelineAsset; // 실제 활성 파이프라인이 URP인지 확인
            bool productNameConfigured = PlayerSettings.productName == "Project I"; // 제품 이름 확인
            bool developmentBuildEnabled = EditorUserBuildSettings.development; // 개발 빌드 기본 설정 확인
            bool templateAssetsRemoved = ObsoleteTemplatePaths.All(path => AssetDatabase.LoadAssetAtPath<Object>(path) == null && !AssetDatabase.IsValidFolder(path)); // 기본 템플릿 잔여물 제거 확인
            bool success = scenesExist && buildScenesMatch && playModeStartsFromBoot && linearColorSpace && urpConfigured && productNameConfigured && developmentBuildEnabled && templateAssetsRemoved; // 전체 검증 결과 계산

            LogResult("필수 Scene", scenesExist); // 씬 검증 결과 출력
            LogResult("Build Settings 순서", buildScenesMatch); // 빌드 설정 결과 출력
            LogResult("Play Mode Boot 시작", playModeStartsFromBoot); // 플레이 시작 씬 결과 출력
            LogResult("Linear Color Space", linearColorSpace); // 색 공간 결과 출력
            LogResult("URP Render Pipeline Asset", urpConfigured); // URP 결과 출력
            LogResult("Product Name", productNameConfigured); // 제품 이름 결과 출력
            LogResult("Development Build 기본 설정", developmentBuildEnabled); // 개발 빌드 결과 출력
            LogResult("Unity 기본 템플릿 정리", templateAssetsRemoved); // 템플릿 정리 결과 출력

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
