using System.Linq; // 목록 검색 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 자동 검사
    public static class Phase1Day2Finalize // 2일차 Phase 1 마무리 도구
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

        static Phase1Day2Finalize() // 자동 마무리 등록
        {
            EditorApplication.delayCall += TryAutoFinalize; // 컴파일 이후 자동 마무리 예약
        }

        private static void TryAutoFinalize() // 자동 마무리 진입
        {
            if (Application.isBatchMode) // 배치 실행 여부 확인
            {
                return; // 배치 실행 자동 작업 제외
            }

            if (!NeedsFinalization()) // 마무리 작업 필요 여부 확인
            {
                return; // 이미 완료된 상태 유지
            }

            FinalizePhase1(false); // 자동 마무리 실행
        }

        [MenuItem("Tools/Project I/Finalize Phase 1 Day 2")] // 수동 마무리 메뉴 등록
        public static void FinalizeFromMenu() // 메뉴 마무리 실행
        {
            FinalizePhase1(true); // 대화상자 포함 마무리 실행
        }

        private static bool NeedsFinalization() // 마무리 필요 여부 검사
        {
            bool templateAssetsRemain = ObsoleteTemplatePaths.Any(path => AssetDatabase.LoadAssetAtPath<Object>(path) != null || AssetDatabase.IsValidFolder(path)); // 기본 템플릿 잔여물 확인
            string[] enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(); // 활성 빌드 씬 수집
            bool buildScenesMismatch = !enabledScenes.SequenceEqual(RequiredScenePaths); // 빌드 씬 불일치 확인
            SceneAsset playModeStartScene = EditorSceneManager.playModeStartScene; // 플레이 시작 씬 조회
            string playModeStartScenePath = playModeStartScene == null ? string.Empty : AssetDatabase.GetAssetPath(playModeStartScene); // 플레이 시작 씬 경로 계산
            bool playModeStartMismatch = playModeStartScenePath != BootScenePath; // 부트 씬 시작 불일치 확인
            bool productNameMismatch = PlayerSettings.productName != "Project I"; // 제품 이름 불일치 확인
            bool colorSpaceMismatch = PlayerSettings.colorSpace != ColorSpace.Linear; // 색 공간 불일치 확인
            bool developmentBuildDisabled = !EditorUserBuildSettings.development; // 개발 빌드 비활성 확인

            return templateAssetsRemain || buildScenesMismatch || playModeStartMismatch || productNameMismatch || colorSpaceMismatch || developmentBuildDisabled; // 마무리 필요 결과 반환
        }

        private static void FinalizePhase1(bool showDialog) // Phase 1 마무리 실행
        {
            DeleteTemplateAssets(); // 기본 템플릿 잔여물 삭제
            ConfigureBuildSettings(); // 빌드 씬 순서 재설정
            ConfigurePlayModeStartScene(); // 플레이 시작 씬 재설정
            ConfigureProjectSettings(); // 프로젝트 기본 설정 재확정
            AssetDatabase.SaveAssets(); // 변경 에셋 저장
            AssetDatabase.Refresh(); // 에셋 데이터 갱신
            bool success = Phase1Validator.Validate(false); // Phase 1 최종 검증 실행

            if (showDialog) // 대화상자 표시 여부 확인
            {
                string message = success ? "2일차 Phase 1 마무리 완료" : "마무리 후 검증 실패 - Console 확인"; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 마무리 결과 대화상자 표시
            }

            if (success) // 검증 성공 여부 확인
            {
                Debug.Log("[Project I] 2일차 Phase 1 기준선 정리 완료"); // 마무리 성공 로그 출력
                return; // 성공 처리 종료
            }

            Debug.LogError("[Project I] 2일차 Phase 1 마무리 후 검증 항목을 확인하세요."); // 마무리 실패 로그 출력
        }

        private static void DeleteTemplateAssets() // 기본 템플릿 잔여물 삭제
        {
            foreach (string path in ObsoleteTemplatePaths) // 삭제 대상 순회
            {
                bool assetExists = AssetDatabase.LoadAssetAtPath<Object>(path) != null; // 에셋 존재 여부 확인
                bool folderExists = AssetDatabase.IsValidFolder(path); // 폴더 존재 여부 확인

                if (!assetExists && !folderExists) // 삭제 대상 없음 확인
                {
                    continue; // 다음 대상으로 이동
                }

                bool deleted = AssetDatabase.DeleteAsset(path); // 에셋 또는 폴더 삭제

                if (deleted) // 삭제 성공 여부 확인
                {
                    Debug.Log($"[Project I] 기본 템플릿 삭제: {path}"); // 삭제 성공 로그 출력
                    continue; // 다음 대상으로 이동
                }

                Debug.LogWarning($"[Project I] 기본 템플릿 삭제 실패: {path}"); // 삭제 실패 경고 출력
            }
        }

        private static void ConfigureBuildSettings() // 빌드 설정 구성
        {
            EditorBuildSettings.scenes = new[] // 빌드 씬 목록 지정
            {
                new EditorBuildSettingsScene(BootScenePath, true), // 부트 씬 등록
                new EditorBuildSettingsScene(MainMenuScenePath, true), // 메인 메뉴 씬 등록
                new EditorBuildSettingsScene(ExplorationOfficeScenePath, true) // 탐사 사무소 씬 등록
            };
        }

        private static void ConfigurePlayModeStartScene() // 플레이 시작 씬 설정
        {
            SceneAsset bootSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath); // 부트 씬 에셋 조회

            if (bootSceneAsset == null) // 부트 씬 누락 확인
            {
                Debug.LogError($"[Project I] 부트 씬을 찾을 수 없습니다: {BootScenePath}"); // 부트 씬 누락 오류 출력
                return; // 시작 씬 설정 중단
            }

            EditorSceneManager.playModeStartScene = bootSceneAsset; // 플레이 모드 시작 씬 지정
        }

        private static void ConfigureProjectSettings() // 프로젝트 기본 설정 구성
        {
            PlayerSettings.productName = "Project I"; // 제품 이름 설정
            PlayerSettings.colorSpace = ColorSpace.Linear; // 선형 색 공간 설정
            EditorUserBuildSettings.development = true; // 개발 빌드 기본 활성화
        }
    }
}
