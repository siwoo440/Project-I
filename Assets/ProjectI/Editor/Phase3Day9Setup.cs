using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트와 자식 검색 기능 참조
using ProjectI.Brightness; // 고정 BrightnessSource와 플레이어 밝기 센서 참조
using ProjectI.Diagnostics; // F1 라벨·상세 계산 페이지 기능 참조
using ProjectI.Lighting; // 고정 환경 광원 컨트롤러와 디버그 페이지 참조
using UnityEditor; // 유니티 에디터 메뉴와 저장 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 9 고정 광원·F1 진단 환경 자동 구성
    public static class Phase3Day9Setup // 벽 횃불·화로와 월드 밝기 숫자·전체 계산 페이지 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string BrightnessZoneName = "10_BrightnessTest"; // 7~9일차 밝기 시험 모듈 이름
        private const string ReadyMarkerName = "===Day9 Fixed Light Debug Ready==="; // Day 9 자동 적용 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 기존 테스트 재질 폴더 경로

        static Phase3Day9Setup() // 자동 적용 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day 9 구성 예약
        }

        [MenuItem("Tools/Project I/Day 9/Apply Fixed Lights + Debug")] // 수동 Day 9 적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day 9 구성 실행
        {
            ApplyDay9(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 적용 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 구성 중단
            }

            ApplyDay9(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay9(bool showDialog, bool force) // Day 9 전체 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬이 없으면 자동 적용 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day 9 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 테스트 맵 루트 조회
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // 플레이어 루트 조회

            if (mapRoot == null || player == null) // 선행 테스트 맵과 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 9 구성 전에 Day 3 Test Map과 Player가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            Transform brightnessZone = mapRoot.transform.Find(BrightnessZoneName); // 기존 밝기 시험 모듈 조회

            if (brightnessZone == null) // Day 7 밝기 테스트 모듈 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 9 구성 전에 10_BrightnessTest가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            IndoorBrightnessArea indoorArea = brightnessZone.GetComponentInChildren<IndoorBrightnessArea>(true); // 대형 건물 내부 밝기 영역 조회

            if (indoorArea == null || indoorArea.Volume == null) // 방 영역과 Collider 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 9 구성에 필요한 IndoorBrightnessArea를 찾을 수 없습니다."); // 방 영역 누락 오류 출력
                return; // 구성 중단
            }

            RemoveOldIndoorTestLamps(brightnessZone); // Day 7 단순 실내 시험용 램프 3개 제거
            BuildFixedLights(indoorArea); // 벽 횃불 3개와 중앙 화로 생성
            ConfigureDebugTools(player); // F1 고정 광원 페이지·전체 계산 페이지·월드 숫자 라벨 추가
            EnsureMarker(scene); // Day 9 완료 마커 생성
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 씬 저장
            AssetDatabase.SaveAssets(); // 프로젝트 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트 상태 갱신
            bool validationPassed = Phase3Day9Validator.Validate(false); // Day 9 구성 자동 검증

            if (showDialog) // 수동 적용 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", validationPassed ? "Day 9 고정 광원 및 F1 광원 진단 구성이 완료되었습니다." : "Day 9 검증 실패 - Console을 확인하세요.", "확인"); // 결과 대화상자 표시
            }
        }

        private static void RemoveOldIndoorTestLamps(Transform brightnessZone) // 역할이 겹치는 Day 7 실내 시험용 램프 제거
        {
            string[] oldNames = { "IndoorLamp_A", "IndoorLamp_B", "IndoorLamp_C" }; // 제거할 이전 테스트 램프 이름 목록

            foreach (string oldName in oldNames) // 이전 램프 이름 전체 순회
            {
                Transform oldLamp = FindDescendant(brightnessZone, oldName); // 밝기 시험 구역에서 해당 램프 검색

                if (oldLamp != null) // 기존 램프가 실제로 존재하는지 확인
                {
                    Object.DestroyImmediate(oldLamp.gameObject); // 9일차 고정 광원과 중복되지 않도록 기존 시험 램프 삭제
                }
            }
        }

        private static void BuildFixedLights(IndoorBrightnessArea indoorArea) // 현재 방에 벽 횃불 3개와 화로 1개 구성
        {
            Transform areaTransform = indoorArea.transform; // Fixed BrightnessSource의 부모 방 기준점 조회
            string[] newNames = { "WallTorch_North", "WallTorch_South", "WallTorch_East", "Brazier_Center" }; // 중복 제거할 Day 9 고정 조명 이름 목록

            foreach (string lightName in newNames) // Day 9 조명 이름 전체 순회
            {
                Transform existing = FindDescendant(areaTransform, lightName); // 기존 동일 조명 검색

                if (existing != null) // 강제 재적용 시 기존 조명이 있는지 확인
                {
                    Object.DestroyImmediate(existing.gameObject); // 중복 조명 제거
                }
            }

            Bounds bounds = indoorArea.Volume.bounds; // 내부 방 월드 Bounds 조회
            float torchY = bounds.min.y + 1.9f; // 플레이어 눈높이 근처 벽 횃불 높이 계산
            float wallInset = 0.7f; // 외벽 Collider 안쪽으로 조명을 들여놓을 거리
            Material metalMaterial = LoadMaterial("Test_Metal"); // 횃불 몸체와 화로 받침 테스트 재질 조회
            Material flameMaterial = LoadMaterial("Test_Orange"); // 불꽃 표시 테스트 재질 조회
            CreateWallTorch(areaTransform, "WallTorch_North", "북쪽 벽 횃불", new Vector3(bounds.center.x, torchY, bounds.max.z - wallInset), metalMaterial, flameMaterial); // 북쪽 벽 고정 횃불 생성
            CreateWallTorch(areaTransform, "WallTorch_South", "남쪽 벽 횃불", new Vector3(bounds.center.x, torchY, bounds.min.z + wallInset), metalMaterial, flameMaterial); // 남쪽 벽 고정 횃불 생성
            CreateWallTorch(areaTransform, "WallTorch_East", "동쪽 벽 횃불", new Vector3(bounds.max.x - wallInset, torchY, bounds.center.z), metalMaterial, flameMaterial); // 동쪽 벽 고정 횃불 생성
            CreateBrazier(areaTransform, new Vector3(bounds.center.x, bounds.min.y + 0.55f, bounds.center.z), metalMaterial, flameMaterial); // 방 중앙 고정 화로 생성
        }

        private static void CreateWallTorch(Transform parent, string objectName, string displayName, Vector3 position, Material metalMaterial, Material flameMaterial) // 벽 횃불 하나 생성
        {
            GameObject root = new GameObject(objectName); // 벽 횃불 루트 생성
            root.transform.SetParent(parent); // IndoorBrightnessArea 아래에 연결해 Fixed 광원 소속 방 설정
            root.transform.position = position; // 계산된 벽 위치 적용
            BoxCollider collider = root.AddComponent<BoxCollider>(); // F 상호작용 Raycast와 간단 충돌용 Collider 추가
            collider.size = new Vector3(0.45f, 1.15f, 0.45f); // 벽 횃불 상호작용 영역 크기 적용
            CreateVisualPrimitive(root.transform, "Bracket", PrimitiveType.Cube, new Vector3(0f, -0.18f, 0f), new Vector3(0.16f, 0.55f, 0.16f), metalMaterial); // 간단한 테스트 횃불 몸체 생성
            GameObject flame = CreateVisualPrimitive(root.transform, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(0.24f, 0.30f, 0.24f), flameMaterial); // 점화 상태 시각 불꽃 생성
            GameObject lightOrigin = new GameObject("LightOrigin"); // 실제 Point Light 시작점 생성
            lightOrigin.transform.SetParent(root.transform, false); // 횃불 루트 아래에 광원 시작점 연결
            lightOrigin.transform.localPosition = new Vector3(0f, 0.44f, 0f); // 불꽃 중심에 광원 위치 배치
            Light visualLight = lightOrigin.AddComponent<Light>(); // 실제 화면용 Point Light 추가
            visualLight.type = LightType.Point; // 벽 횃불 주변 모든 방향 조명 설정
            visualLight.range = 7f; // 근거리 벽 횃불 영향 범위 설정
            visualLight.intensity = 4.2f; // 테스트용 화면 밝기 적용
            visualLight.color = new Color(1f, 0.58f, 0.24f); // 따뜻한 횃불 색상 적용
            BrightnessSource source = root.AddComponent<BrightnessSource>(); // 게임 판정용 고정 밝기 광원 추가
            source.Configure(0.30f, 7f, false, visualLight, BrightnessSourceType.Fixed, BrightnessEmissionShape.Omnidirectional, 52f); // 방 소속 Fixed 횃불 기본값 적용
            FixedLightController controller = root.AddComponent<FixedLightController>(); // F 토글과 점화 상태 관리 기능 추가
            controller.Configure(displayName, false, new[] { source }, new[] { flame }); // 모든 벽 횃불은 처음 꺼진 상태로 시작
        }

        private static void CreateBrazier(Transform parent, Vector3 position, Material metalMaterial, Material flameMaterial) // 방 중앙 화로 생성
        {
            GameObject root = new GameObject("Brazier_Center"); // 중앙 화로 루트 생성
            root.transform.SetParent(parent); // IndoorBrightnessArea 아래에 연결해 현재 방 Fixed 광원으로 설정
            root.transform.position = position; // 방 중앙 바닥 위치 적용
            BoxCollider collider = root.AddComponent<BoxCollider>(); // F 상호작용과 물리 장애물용 Collider 추가
            collider.size = new Vector3(1.2f, 1.1f, 1.2f); // 화로 크기에 맞는 충돌 영역 적용
            CreateVisualPrimitive(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(0.55f, 0.25f, 0.55f), metalMaterial); // 화로 받침 시각 요소 생성
            GameObject flame = CreateVisualPrimitive(root.transform, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.48f, 0f), new Vector3(0.52f, 0.62f, 0.52f), flameMaterial); // 큰 화로 불꽃 시각 요소 생성
            GameObject lightOrigin = new GameObject("LightOrigin"); // 화로 실제 광원 시작점 생성
            lightOrigin.transform.SetParent(root.transform, false); // 화로 루트 아래에 광원 시작점 연결
            lightOrigin.transform.localPosition = new Vector3(0f, 0.55f, 0f); // 화로 불꽃 중심에 광원 위치 배치
            Light visualLight = lightOrigin.AddComponent<Light>(); // 실제 화면용 Point Light 추가
            visualLight.type = LightType.Point; // 화로 주변 전 방향 조명 설정
            visualLight.range = 11f; // 벽 횃불보다 넓은 방 중앙 영향 범위 적용
            visualLight.intensity = 6.5f; // 중앙 화로에 더 강한 화면 밝기 적용
            visualLight.color = new Color(1f, 0.50f, 0.18f); // 강한 따뜻한 화염 색상 적용
            BrightnessSource source = root.AddComponent<BrightnessSource>(); // 게임 판정용 고정 밝기 광원 추가
            source.Configure(0.50f, 11f, false, visualLight, BrightnessSourceType.Fixed, BrightnessEmissionShape.Omnidirectional, 52f); // 방 중앙 화로 Fixed 밝기 기본값 적용
            FixedLightController controller = root.AddComponent<FixedLightController>(); // F 토글과 점화 상태 관리 기능 추가
            controller.Configure("중앙 화로", false, new[] { source }, new[] { flame }); // 화로도 처음 꺼진 상태로 시작
        }

        private static void ConfigureDebugTools(GameObject player) // F1 고정 광원·전체 계산·월드 숫자 진단 도구 추가
        {
            PlayerBrightnessSensor sensor = player.GetComponent<PlayerBrightnessSensor>(); // 플레이어 밝기 센서 조회
            FixedLightDebugPage fixedPage = player.GetComponent<FixedLightDebugPage>(); // 기존 고정 광원 F1 페이지 조회

            if (fixedPage == null) // 고정 광원 페이지 미생성 여부 확인
            {
                fixedPage = player.AddComponent<FixedLightDebugPage>(); // F1 네 번째 페이지 공급자 추가
            }

            LightCalculationDebugPage calculationPage = player.GetComponent<LightCalculationDebugPage>(); // 기존 전체 광원 계산 페이지 조회

            if (calculationPage == null) // 전체 계산 페이지 미생성 여부 확인
            {
                calculationPage = player.AddComponent<LightCalculationDebugPage>(); // F1 다섯 번째 상세 계산 페이지 공급자 추가
            }

            calculationPage.Configure(sensor); // 플레이어 밝기 센서를 상세 계산 페이지에 연결
            LightDebugLabelManager labelManager = player.GetComponent<LightDebugLabelManager>(); // 기존 월드 광원 숫자 라벨 관리자 조회

            if (labelManager == null) // 라벨 관리자 미생성 여부 확인
            {
                labelManager = player.AddComponent<LightDebugLabelManager>(); // F1 상태에서 모든 광원 옆 숫자 표시 기능 추가
            }

            DebugPageManager debugManager = Object.FindFirstObjectByType<DebugPageManager>(); // 기존 F1 공통 페이지 관리자 조회
            Camera playerCamera = Camera.main; // MainCamera 태그의 1인칭 카메라 조회

            if (playerCamera == null) // MainCamera를 찾지 못했는지 확인
            {
                playerCamera = Object.FindFirstObjectByType<Camera>(); // 첫 활성 카메라를 안전 대체로 조회
            }

            labelManager.Configure(debugManager, sensor, playerCamera); // 월드 광원 숫자 라벨에 F1 상태·플레이어 위치·카메라 연결
            EditorUtility.SetDirty(fixedPage); // 새 고정 광원 F1 페이지 저장 대상으로 표시
            EditorUtility.SetDirty(calculationPage); // 새 전체 계산 페이지 저장 대상으로 표시
            EditorUtility.SetDirty(labelManager); // 새 월드 숫자 라벨 관리자 저장 대상으로 표시
        }

        private static GameObject CreateVisualPrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material) // Collider 없는 테스트 시각 Primitive 생성
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType); // 기본 Primitive 생성
            visual.name = objectName; // 시각 요소 이름 지정
            visual.transform.SetParent(parent, false); // 요청된 부모 아래에 로컬 기준 연결
            visual.transform.localPosition = localPosition; // 로컬 위치 적용
            visual.transform.localRotation = Quaternion.identity; // 기본 로컬 회전 적용
            visual.transform.localScale = localScale; // 시각 크기 적용
            Renderer renderer = visual.GetComponent<Renderer>(); // Primitive Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 기존 테스트 재질 적용
            }

            Collider primitiveCollider = visual.GetComponent<Collider>(); // Primitive 자동 Collider 조회

            if (primitiveCollider != null) // 루트 상호작용 Collider와 중복되는지 확인
            {
                Object.DestroyImmediate(primitiveCollider); // 자식 시각 요소 Collider 제거
            }

            return visual; // 생성한 시각 요소 반환
        }

        private static Transform FindDescendant(Transform root, string targetName) // 자식 깊이에 상관없이 이름으로 오브젝트 검색
        {
            if (root == null) // 검색 루트 누락 확인
            {
                return null; // 검색 실패 반환
            }

            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == targetName); // 비활성 자식까지 포함해 첫 이름 일치 Transform 반환
        }

        private static Material LoadMaterial(string materialName) // 기존 테스트 재질 조회
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{materialName}.mat"); // 지정 이름의 테스트 Material 에셋 반환
        }

        private static void EnsureMarker(Scene scene) // Day 9 자동 적용 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (marker == null) // 아직 Day 9 마커가 없는지 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서는 개발용 마커 숨김
            }
        }
    }
}
