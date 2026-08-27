using System.IO; // 파일 경로 기능 참조
using ProjectI.Diagnostics; // 개발 설정 참조
using ProjectI.Scenes; // 씬 제어기 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 에디터 씬 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 자동 실행
    public static class Phase1ProjectSetup // Phase 1 자동 설정 도구
    {
        private const string RootFolder = "Assets/ProjectI"; // 프로젝트 루트 폴더
        private const string ScenesFolder = RootFolder + "/Scenes"; // 씬 폴더 경로
        private const string ResourcesFolder = RootFolder + "/Resources"; // 리소스 폴더 경로
        private const string BootScenePath = ScenesFolder + "/Boot.unity"; // 부트 씬 경로
        private const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity"; // 메인 메뉴 씬 경로
        private const string ExplorationOfficeScenePath = ScenesFolder + "/ExplorationOffice.unity"; // 사무소 씬 경로
        private const string DevelopmentSettingsPath = ResourcesFolder + "/ProjectDevelopmentSettings.asset"; // 개발 설정 경로

        static Phase1ProjectSetup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoSetup; // 컴파일 이후 자동 설정 예약
        }

        private static void TryAutoSetup() // 자동 설정 진입
        {
            if (Application.isBatchMode) // 배치 실행 여부 확인
            {
                return; // 배치 실행 자동 설정 제외
            }

            if (RequiredScenesExist()) // 필수 씬 존재 확인
            {
                return; // 기존 설정 유지
            }

            RunSetup(); // 초기 자동 설정 실행
        }

        [MenuItem("Tools/Project I/Phase 1 Setup")] // 수동 설정 메뉴 등록
        public static void RunSetup() // Phase 1 전체 설정
        {
            EnsureFolder(RootFolder); // 프로젝트 루트 폴더 확인
            EnsureFolder(ScenesFolder); // 씬 폴더 확인
            EnsureFolder(ResourcesFolder); // 리소스 폴더 확인
            CreateDevelopmentSettings(); // 개발 설정 생성
            CreateBootScene(); // 부트 씬 생성
            CreateMainMenuScene(); // 메인 메뉴 씬 생성
            CreateExplorationOfficeScene(); // 탐사 사무소 씬 생성
            ConfigureBuildSettings(); // 빌드 씬 순서 설정
            ConfigureProjectSettings(); // 프로젝트 기본 설정
            AssetDatabase.SaveAssets(); // 생성 에셋 저장
            AssetDatabase.Refresh(); // 에셋 데이터 갱신
            SceneAsset bootSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath); // 부트 씬 에셋 조회
            EditorSceneManager.playModeStartScene = bootSceneAsset; // 플레이 시작 씬 고정
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single); // 부트 씬 열기
            ProjectDevelopmentSettings settings = AssetDatabase.LoadAssetAtPath<ProjectDevelopmentSettings>(DevelopmentSettingsPath); // 개발 설정 조회

            if (settings == null || settings.RunValidationAfterSetup) // 자동 검증 설정 확인
            {
                Phase1Validator.Validate(false); // Phase 1 자동 검증
            }

            Debug.Log("[Project I] Phase 1 자동 설정 완료"); // 설정 완료 로그 출력
        }

        private static bool RequiredScenesExist() // 필수 씬 존재 검사
        {
            return File.Exists(BootScenePath) && File.Exists(MainMenuScenePath) && File.Exists(ExplorationOfficeScenePath); // 세 씬 존재 결과 반환
        }

        private static void EnsureFolder(string folderPath) // 폴더 생성 확인
        {
            if (AssetDatabase.IsValidFolder(folderPath)) // 기존 폴더 확인
            {
                return; // 기존 폴더 유지
            }

            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/'); // 부모 폴더 경로 계산
            string folderName = Path.GetFileName(folderPath); // 생성 폴더 이름 계산

            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath)) // 부모 폴더 누락 확인
            {
                EnsureFolder(parentPath); // 부모 폴더 선행 생성
            }

            AssetDatabase.CreateFolder(parentPath, folderName); // 대상 폴더 생성
        }

        private static void CreateDevelopmentSettings() // 개발 설정 에셋 생성
        {
            if (AssetDatabase.LoadAssetAtPath<ProjectDevelopmentSettings>(DevelopmentSettingsPath) != null) // 기존 설정 확인
            {
                return; // 기존 설정 유지
            }

            ProjectDevelopmentSettings settings = ScriptableObject.CreateInstance<ProjectDevelopmentSettings>(); // 개발 설정 인스턴스 생성
            AssetDatabase.CreateAsset(settings, DevelopmentSettingsPath); // 개발 설정 에셋 저장
        }

        private static void CreateBootScene() // 부트 씬 생성
        {
            if (File.Exists(BootScenePath)) // 기존 부트 씬 확인
            {
                return; // 기존 부트 씬 유지
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // 빈 부트 씬 생성
            GameObject controllerObject = new GameObject("[Scene] Boot"); // 부트 제어 객체 생성
            controllerObject.AddComponent<BootSceneController>(); // 부트 제어기 추가
            CreateCamera(); // 기본 카메라 생성
            EditorSceneManager.SaveScene(scene, BootScenePath); // 부트 씬 저장
        }

        private static void CreateMainMenuScene() // 메인 메뉴 씬 생성
        {
            if (File.Exists(MainMenuScenePath)) // 기존 메인 메뉴 씬 확인
            {
                return; // 기존 메인 메뉴 씬 유지
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // 빈 메인 메뉴 씬 생성
            GameObject controllerObject = new GameObject("[Scene] MainMenu"); // 메인 메뉴 제어 객체 생성
            controllerObject.AddComponent<MainMenuSceneController>(); // 메인 메뉴 제어기 추가
            CreateCamera(); // 기본 카메라 생성
            EditorSceneManager.SaveScene(scene, MainMenuScenePath); // 메인 메뉴 씬 저장
        }

        private static void CreateExplorationOfficeScene() // 탐사 사무소 씬 생성
        {
            if (File.Exists(ExplorationOfficeScenePath)) // 기존 사무소 씬 확인
            {
                return; // 기존 사무소 씬 유지
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // 빈 사무소 씬 생성
            GameObject controllerObject = new GameObject("[Scene] ExplorationOffice"); // 사무소 제어 객체 생성
            controllerObject.AddComponent<ExplorationOfficeSceneController>(); // 사무소 제어기 추가
            CreateCamera(); // 기본 카메라 생성
            CreateDirectionalLight(); // 기본 방향광 생성
            EditorSceneManager.SaveScene(scene, ExplorationOfficeScenePath); // 사무소 씬 저장
        }

        private static void CreateCamera() // 기본 카메라 생성
        {
            GameObject cameraObject = new GameObject("Main Camera"); // 카메라 객체 생성
            Camera camera = cameraObject.AddComponent<Camera>(); // 카메라 컴포넌트 추가
            camera.clearFlags = CameraClearFlags.SolidColor; // 단색 배경 설정
            camera.backgroundColor = new Color(0.035f, 0.035f, 0.045f, 1f); // 어두운 배경색 설정
            cameraObject.tag = "MainCamera"; // 메인 카메라 태그 설정
        }

        private static void CreateDirectionalLight() // 기본 방향광 생성
        {
            GameObject lightObject = new GameObject("Directional Light"); // 방향광 객체 생성
            Light directionalLight = lightObject.AddComponent<Light>(); // 조명 컴포넌트 추가
            directionalLight.type = LightType.Directional; // 방향광 타입 설정
            directionalLight.intensity = 1f; // 기본 밝기 설정
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // 기본 조명 각도 설정
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

        private static void ConfigureProjectSettings() // 프로젝트 기본 설정 구성
        {
            PlayerSettings.productName = "Project I"; // 제품 이름 설정
            PlayerSettings.colorSpace = ColorSpace.Linear; // 선형 색 공간 설정
            EditorUserBuildSettings.development = true; // 개발 빌드 기본 활성화
        }
    }
}
