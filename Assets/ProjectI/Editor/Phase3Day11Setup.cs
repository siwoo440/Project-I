using System.IO; // 생성 재질 폴더 확인 기능 참조
using System.Linq; // 씬 루트와 자식 검색 기능 참조
using ProjectI.Brightness; // 기존 게임 밝기 광원 기능 참조
using ProjectI.Power; // 11일차 발전기와 전기등 기능 참조
using UnityEditor; // 에디터 메뉴와 에셋 저장 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // 게임 오브젝트와 재질 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 11 전력 시험 환경 자동 구성
    public static class Phase3Day11Setup // 발전기·전기등·연료 소비 시험 환경 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string BrightnessZoneName = "10_BrightnessTest"; // 기존 실내 밝기 시험 구역 이름
        private const string Day11RootName = "Day11_PowerTest"; // 11일차 전력 시험 루트 이름
        private const string ReadyMarkerName = "===Day11 Generator Power Ready==="; // 11일차 자동 적용 완료 마커 이름
        private const string GeneratedRootFolder = "Assets/ProjectI/Art/Generated"; // 자동 생성 아트 루트 경로
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day11"; // 발전기와 전기등 생성 재질 경로

        static Phase3Day11Setup() // 자동 적용 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 11일차 구성 예약
        }

        [MenuItem("Tools/Project I/Day 11/Apply Generator + Electric Lights")] // 수동 11일차 적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 11일차 구성 실행
        {
            ApplyDay11(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 구성 중단
            }

            ApplyDay11(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay11(bool showDialog, bool force) // Day 11 전체 발전기·전기등 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬이 없으면 자동 적용 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day 11 완료 마커 조회

            if (!force && existingMarker != null) // 이미 11일차 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 기존 테스트 맵 루트 조회

            if (mapRoot == null) // 선행 테스트 맵 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 11 구성 전에 Day 3 Test Map이 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            Transform brightnessZone = mapRoot.transform.Find(BrightnessZoneName); // 기존 실내 밝기 시험 구역 조회

            if (brightnessZone == null) // 7일차 실내 시험 구역 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 11 구성 전에 10_BrightnessTest가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            IndoorBrightnessArea indoorArea = brightnessZone.GetComponentInChildren<IndoorBrightnessArea>(true); // 전기등을 배치할 실내 영역 조회

            if (indoorArea == null || indoorArea.Volume == null) // 실내 영역과 Collider 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 11 구성에 필요한 IndoorBrightnessArea를 찾을 수 없습니다."); // 실내 구조 누락 오류 출력
                return; // 구성 중단
            }

            EnsureMaterialFolders(); // 발전기 전용 생성 재질 폴더 확보
            Day11Materials materials = BuildMaterials(); // 발전기와 전기등용 URP 재질 생성
            Transform existingRoot = indoorArea.transform.Find(Day11RootName); // 기존 Day 11 전력 시험 루트 검색

            if (existingRoot != null) // 기존 전력 시험 구조 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot.gameObject); // 강제 적용과 부분 적용 복구를 위해 기존 구조 제거
            }

            GameObject day11Root = new GameObject(Day11RootName); // Day 11 전력 시험 루트 생성
            day11Root.transform.SetParent(indoorArea.transform, false); // 기존 실내 영역 아래에 전력 시험 구조 연결
            Bounds bounds = indoorArea.Volume.bounds; // 실내 영역 월드 Bounds 조회
            ElectricLightController[] electricLights = BuildElectricLights(day11Root.transform, bounds, materials); // 천장 전기등 4개 생성
            GeneratorController generator = BuildGenerator(day11Root.transform, bounds, materials, electricLights); // 디자인 발전기와 전력 연결 생성
            EditorUtility.SetDirty(generator); // 발전기 직렬화 상태 저장 대상으로 표시
            EnsureMarker(scene); // 11일차 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // 전력 시험 구조를 탐사 사무소 씬에 저장
            AssetDatabase.SaveAssets(); // 생성 재질과 씬 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase3Day11Validator.Validate(false); // Day 11 자동 구성 정적 검증 실행

            if (showDialog) // 수동 적용 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", validationPassed ? "Day 11 발전기·전기등 구성이 완료되었습니다." : "Day 11 검증 실패 - Console을 확인하세요.", "확인"); // 수동 적용 결과 안내
            }
        }

        private static ElectricLightController[] BuildElectricLights(Transform parent, Bounds bounds, Day11Materials materials) // 실내 천장 전기등 4개 생성
        {
            ElectricLightController[] lights = new ElectricLightController[4]; // 발전기와 연결할 전기등 배열 생성
            float xOffset = Mathf.Min(3.2f, bounds.extents.x * 0.48f); // 방 크기에 맞춘 좌우 배치 간격 계산
            float zOffset = Mathf.Min(3.2f, bounds.extents.z * 0.48f); // 방 크기에 맞춘 앞뒤 배치 간격 계산
            float ceilingY = bounds.max.y - 0.32f; // 천장 아래 전기등 설치 높이 계산
            Vector3[] positions = new Vector3[4]; // 전기등 월드 위치 배열 생성
            positions[0] = new Vector3(bounds.center.x - xOffset, ceilingY, bounds.center.z - zOffset); // 북서쪽 전기등 위치 저장
            positions[1] = new Vector3(bounds.center.x + xOffset, ceilingY, bounds.center.z - zOffset); // 북동쪽 전기등 위치 저장
            positions[2] = new Vector3(bounds.center.x - xOffset, ceilingY, bounds.center.z + zOffset); // 남서쪽 전기등 위치 저장
            positions[3] = new Vector3(bounds.center.x + xOffset, ceilingY, bounds.center.z + zOffset); // 남동쪽 전기등 위치 저장

            for (int index = 0; index < lights.Length; index++) // 전기등 배치 위치 전체 순회
            {
                lights[index] = CreateElectricLight(parent, $"ElectricLight_{index + 1:00}", $"전기등 {index + 1}", positions[index], materials); // 각 위치에 전기등 생성
            }

            return lights; // 생성된 전기등 배열 반환
        }

        private static ElectricLightController CreateElectricLight(Transform parent, string objectName, string displayName, Vector3 worldPosition, Day11Materials materials) // 전기등 하나 생성
        {
            GameObject root = new GameObject(objectName); // 전기등 루트 생성
            root.transform.SetParent(parent); // Day 11 전력 시험 루트 아래에 연결
            root.transform.position = worldPosition; // 계산된 천장 위치 적용
            CreateVisualPrimitive(root.transform, "CeilingPlate", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.62f, 0.08f, 0.62f), Vector3.zero, materials.Metal); // 천장 고정 금속판 생성
            CreateVisualPrimitive(root.transform, "Housing", PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(0.52f, 0.16f, 0.52f), Vector3.zero, materials.DarkMetal); // 전기등 금속 하우징 생성
            GameObject offGlass = CreateVisualPrimitive(root.transform, "Glass_Off", PrimitiveType.Cylinder, new Vector3(0f, -0.19f, 0f), new Vector3(0.43f, 0.07f, 0.43f), Vector3.zero, materials.LampOff); // 소등 상태 유리 표현 생성
            GameObject glow = CreateVisualPrimitive(root.transform, "Glass_Glow", PrimitiveType.Cylinder, new Vector3(0f, -0.20f, 0f), new Vector3(0.41f, 0.075f, 0.41f), Vector3.zero, materials.LampOn); // 점등 상태 발광판 생성
            GameObject lightOrigin = new GameObject("LightOrigin"); // 실제 전기등 광원 시작점 생성
            lightOrigin.transform.SetParent(root.transform, false); // 전기등 루트 아래 광원 시작점 연결
            lightOrigin.transform.localPosition = new Vector3(0f, -0.34f, 0f); // 발광판 아래 실제 광원 위치 지정
            Light visualLight = lightOrigin.AddComponent<Light>(); // 실제 화면용 Point Light 추가
            visualLight.type = LightType.Point; // 전기등 주변 전 방향 조명 설정
            visualLight.range = 8.5f; // 실내 전기등 영향 거리 설정
            visualLight.intensity = 4.6f; // 실내 전기등 화면 밝기 설정
            visualLight.color = new Color(1f, 0.88f, 0.68f); // 낡은 백열등 느낌의 따뜻한 색 적용
            visualLight.shadows = LightShadows.Soft; // 전기등 부드러운 실시간 그림자 적용
            BrightnessSource brightnessSource = root.AddComponent<BrightnessSource>(); // 게임 판정용 고정 밝기 광원 추가
            brightnessSource.Configure(0.26f, 8.5f, false, visualLight, BrightnessSourceType.Fixed, BrightnessEmissionShape.Omnidirectional, 52f); // 전기등 게임 밝기 값과 시작 소등 상태 적용
            ElectricLightController controller = root.AddComponent<ElectricLightController>(); // 발전기 전력 수신 기능 추가
            controller.Configure(displayName, false, new[] { brightnessSource }, new[] { glow }, new[] { offGlass }); // 시작 정전 상태와 시각 요소 연결
            EditorUtility.SetDirty(controller); // 전기등 설정 저장 대상으로 표시
            return controller; // 생성된 전기등 반환
        }

        private static GeneratorController BuildGenerator(Transform parent, Bounds bounds, Day11Materials materials, ElectricLightController[] electricLights) // 발전기 본체와 전력 공급 기능 생성
        {
            GameObject root = new GameObject("Generator_Main"); // 발전기 루트 생성
            root.transform.SetParent(parent); // Day 11 전력 시험 루트 아래에 연결
            float generatorX = Mathf.Lerp(bounds.min.x, bounds.center.x, 0.38f); // 방 한쪽 벽 근처 발전기 X 위치 계산
            float generatorZ = Mathf.Lerp(bounds.center.z, bounds.max.z, 0.18f); // 중앙 통로를 피한 발전기 Z 위치 계산
            root.transform.position = new Vector3(generatorX, bounds.min.y + 0.05f, generatorZ); // 실내 바닥 위 발전기 위치 적용
            BoxCollider interactionCollider = root.AddComponent<BoxCollider>(); // F 상호작용용 발전기 Collider 추가
            interactionCollider.center = new Vector3(0f, 0.88f, 0f); // 발전기 본체 중심에 상호작용 영역 배치
            interactionCollider.size = new Vector3(2.65f, 1.85f, 1.55f); // 전체 발전기 외형을 감싸는 상호작용 영역 설정
            CreateVisualPrimitive(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0f), new Vector3(2.55f, 0.22f, 1.35f), Vector3.zero, materials.DarkMetal); // 무거운 발전기 하부 베이스 생성
            CreateVisualPrimitive(root.transform, "LeftFoot", PrimitiveType.Cube, new Vector3(-0.88f, 0.02f, 0f), new Vector3(0.42f, 0.14f, 1.15f), Vector3.zero, materials.Rubber); // 왼쪽 진동 방지 받침 생성
            CreateVisualPrimitive(root.transform, "RightFoot", PrimitiveType.Cube, new Vector3(0.88f, 0.02f, 0f), new Vector3(0.42f, 0.14f, 1.15f), Vector3.zero, materials.Rubber); // 오른쪽 진동 방지 받침 생성
            CreateFrame(root.transform, materials.Metal); // 발전기 외부 보호 프레임 생성
            CreateVisualPrimitive(root.transform, "EngineBlock", PrimitiveType.Cube, new Vector3(-0.28f, 0.78f, 0f), new Vector3(1.30f, 0.90f, 0.92f), Vector3.zero, materials.Body); // 중앙 엔진 블록 생성
            CreateVisualPrimitive(root.transform, "EngineTop", PrimitiveType.Cube, new Vector3(-0.28f, 1.28f, 0f), new Vector3(1.08f, 0.16f, 0.78f), Vector3.zero, materials.DarkMetal); // 엔진 상부 덮개 생성
            CreateVisualPrimitive(root.transform, "FuelTank", PrimitiveType.Cylinder, new Vector3(-0.35f, 1.55f, 0f), new Vector3(0.43f, 0.74f, 0.43f), new Vector3(0f, 0f, 90f), materials.Body); // 상부 원통형 연료 탱크 생성
            CreateVisualPrimitive(root.transform, "FuelCap", PrimitiveType.Cylinder, new Vector3(-0.35f, 1.86f, 0f), new Vector3(0.13f, 0.08f, 0.13f), Vector3.zero, materials.Metal); // 연료 주입구 뚜껑 생성
            CreateVisualPrimitive(root.transform, "Alternator", PrimitiveType.Cylinder, new Vector3(0.63f, 0.76f, 0f), new Vector3(0.46f, 0.62f, 0.46f), new Vector3(0f, 0f, 90f), materials.Metal); // 우측 발전기 코일 하우징 생성
            CreateVisualPrimitive(root.transform, "AlternatorBand", PrimitiveType.Cylinder, new Vector3(0.63f, 0.76f, 0f), new Vector3(0.49f, 0.18f, 0.49f), new Vector3(0f, 0f, 90f), materials.Warning); // 발전기 코일 경고색 밴드 생성
            GameObject flywheel = CreateVisualPrimitive(root.transform, "Flywheel", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.69f, -0.55f), new Vector3(0.40f, 0.12f, 0.40f), new Vector3(90f, 0f, 0f), materials.Metal); // 외부 회전 플라이휠 생성
            CreateVisualPrimitive(root.transform, "FlywheelHub", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.69f, -0.565f), new Vector3(0.18f, 0.14f, 0.18f), new Vector3(90f, 0f, 0f), materials.Warning); // 플라이휠 중심 허브 생성
            CreateVisualPrimitive(root.transform, "ControlPanel", PrimitiveType.Cube, new Vector3(0.82f, 1.36f, -0.44f), new Vector3(0.68f, 0.54f, 0.16f), new Vector3(-8f, 0f, 0f), materials.DarkMetal); // 앞쪽 기울어진 제어 패널 생성
            CreateVisualPrimitive(root.transform, "PanelStripe", PrimitiveType.Cube, new Vector3(0.82f, 1.49f, -0.535f), new Vector3(0.50f, 0.08f, 0.03f), new Vector3(-8f, 0f, 0f), materials.Warning); // 제어 패널 경고색 표시 생성
            GameObject greenIndicator = CreateVisualPrimitive(root.transform, "Indicator_Green", PrimitiveType.Sphere, new Vector3(0.66f, 1.34f, -0.545f), new Vector3(0.10f, 0.10f, 0.06f), Vector3.zero, materials.GreenGlow); // 가동 상태 초록 표시등 생성
            GameObject redIndicator = CreateVisualPrimitive(root.transform, "Indicator_Red", PrimitiveType.Sphere, new Vector3(0.96f, 1.34f, -0.545f), new Vector3(0.10f, 0.10f, 0.06f), Vector3.zero, materials.RedGlow); // 정지 상태 빨간 표시등 생성
            CreateVisualPrimitive(root.transform, "ExhaustBase", PrimitiveType.Cylinder, new Vector3(-0.92f, 1.31f, 0.28f), new Vector3(0.10f, 0.48f, 0.10f), Vector3.zero, materials.DarkMetal); // 엔진 배기 파이프 하단 생성
            CreateVisualPrimitive(root.transform, "ExhaustPipe", PrimitiveType.Cylinder, new Vector3(-0.92f, 1.72f, 0.28f), new Vector3(0.13f, 0.38f, 0.13f), Vector3.zero, materials.Metal); // 엔진 배기 파이프 상단 생성
            CreateVisualPrimitive(root.transform, "ExhaustCap", PrimitiveType.Cylinder, new Vector3(-0.92f, 2.08f, 0.28f), new Vector3(0.20f, 0.06f, 0.20f), Vector3.zero, materials.DarkMetal); // 배기구 빗물 차단 캡 생성
            GameObject[] fuelGaugeSegments = CreateFuelGauge(root.transform, materials.GreenGlow); // 제어 패널 연료 게이지 5칸 생성
            GeneratorController controller = root.AddComponent<GeneratorController>(); // 발전기 작동·연료·전력 제어 기능 추가
            controller.Configure("주 발전기", 100f, 100f, 0.25f, false, electricLights, new[] { greenIndicator }, new[] { redIndicator }, fuelGaugeSegments, new[] { flywheel.transform }, 520f); // 초기 연료와 연결 전기등·시각 요소 설정
            return controller; // 생성된 발전기 반환
        }

        private static void CreateFrame(Transform parent, Material material) // 발전기 외부 금속 프레임 생성
        {
            CreateVisualPrimitive(parent, "Frame_LeftFront", PrimitiveType.Cube, new Vector3(-1.12f, 1.02f, -0.56f), new Vector3(0.10f, 1.85f, 0.10f), Vector3.zero, material); // 좌측 전면 세로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_RightFront", PrimitiveType.Cube, new Vector3(1.12f, 1.02f, -0.56f), new Vector3(0.10f, 1.85f, 0.10f), Vector3.zero, material); // 우측 전면 세로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_LeftBack", PrimitiveType.Cube, new Vector3(-1.12f, 1.02f, 0.56f), new Vector3(0.10f, 1.85f, 0.10f), Vector3.zero, material); // 좌측 후면 세로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_RightBack", PrimitiveType.Cube, new Vector3(1.12f, 1.02f, 0.56f), new Vector3(0.10f, 1.85f, 0.10f), Vector3.zero, material); // 우측 후면 세로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_TopFront", PrimitiveType.Cube, new Vector3(0f, 1.91f, -0.56f), new Vector3(2.34f, 0.10f, 0.10f), Vector3.zero, material); // 상단 전면 가로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_TopBack", PrimitiveType.Cube, new Vector3(0f, 1.91f, 0.56f), new Vector3(2.34f, 0.10f, 0.10f), Vector3.zero, material); // 상단 후면 가로 프레임 생성
            CreateVisualPrimitive(parent, "Frame_TopLeft", PrimitiveType.Cube, new Vector3(-1.12f, 1.91f, 0f), new Vector3(0.10f, 0.10f, 1.22f), Vector3.zero, material); // 상단 좌측 연결 프레임 생성
            CreateVisualPrimitive(parent, "Frame_TopRight", PrimitiveType.Cube, new Vector3(1.12f, 1.91f, 0f), new Vector3(0.10f, 0.10f, 1.22f), Vector3.zero, material); // 상단 우측 연결 프레임 생성
        }

        private static GameObject[] CreateFuelGauge(Transform parent, Material gaugeMaterial) // 발전기 제어 패널 연료 게이지 생성
        {
            GameObject[] segments = new GameObject[5]; // 5단계 연료 표시 배열 생성

            for (int index = 0; index < segments.Length; index++) // 게이지 5칸 전체 순회
            {
                float x = 0.62f + index * 0.10f; // 각 게이지 조각 가로 위치 계산
                segments[index] = CreateVisualPrimitive(parent, $"FuelGauge_{index + 1}", PrimitiveType.Cube, new Vector3(x, 1.18f, -0.548f), new Vector3(0.07f, 0.06f, 0.025f), new Vector3(-8f, 0f, 0f), gaugeMaterial); // 작은 발광 연료 게이지 조각 생성
            }

            return segments; // 생성된 연료 게이지 배열 반환
        }

        private static GameObject CreateVisualPrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Material material) // 장식용 기본 도형 생성
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType); // 지정한 Unity 기본 도형 생성
            visual.name = objectName; // Hierarchy 식별 이름 지정
            visual.transform.SetParent(parent, false); // 지정 부모 아래에 로컬 기준으로 연결
            visual.transform.localPosition = localPosition; // 로컬 위치 적용
            visual.transform.localRotation = Quaternion.Euler(localEulerAngles); // 로컬 회전 적용
            visual.transform.localScale = localScale; // 로컬 크기 적용
            Collider collider = visual.GetComponent<Collider>(); // 자동 생성된 장식 Collider 조회

            if (collider != null) // 장식 Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 발전기 루트 상호작용 Collider와 충돌하지 않도록 제거
            }

            Renderer renderer = visual.GetComponent<Renderer>(); // 도형 Renderer 조회

            if (renderer != null && material != null) // 렌더러와 재질 존재 여부 확인
            {
                renderer.sharedMaterial = material; // 생성한 URP 재질 적용
            }

            return visual; // 생성된 시각 요소 반환
        }

        private static Day11Materials BuildMaterials() // Day 11 발전기와 전기등 전용 재질 묶음 생성
        {
            Day11Materials materials = new Day11Materials(); // 재질 묶음 생성
            materials.Body = CreateOrUpdateMaterial("Generator_Body", new Color(0.18f, 0.24f, 0.19f), 0.45f, 0.30f, Color.black); // 낡은 군용 녹색 본체 재질 생성
            materials.Metal = CreateOrUpdateMaterial("Generator_Metal", new Color(0.28f, 0.30f, 0.29f), 0.82f, 0.42f, Color.black); // 노출 금속 프레임 재질 생성
            materials.DarkMetal = CreateOrUpdateMaterial("Generator_DarkMetal", new Color(0.08f, 0.09f, 0.085f), 0.72f, 0.26f, Color.black); // 엔진과 패널용 어두운 금속 재질 생성
            materials.Rubber = CreateOrUpdateMaterial("Generator_Rubber", new Color(0.025f, 0.025f, 0.025f), 0.05f, 0.12f, Color.black); // 진동 방지 고무 재질 생성
            materials.Warning = CreateOrUpdateMaterial("Generator_Warning", new Color(0.78f, 0.53f, 0.08f), 0.38f, 0.28f, Color.black); // 공업용 경고 노란색 재질 생성
            materials.GreenGlow = CreateOrUpdateMaterial("Generator_GreenGlow", new Color(0.06f, 0.26f, 0.08f), 0.05f, 0.30f, new Color(0.15f, 2.4f, 0.22f)); // 가동 표시와 연료 게이지 발광 재질 생성
            materials.RedGlow = CreateOrUpdateMaterial("Generator_RedGlow", new Color(0.30f, 0.04f, 0.035f), 0.05f, 0.30f, new Color(2.5f, 0.08f, 0.05f)); // 정지 표시 빨간 발광 재질 생성
            materials.LampOff = CreateOrUpdateMaterial("ElectricLight_Off", new Color(0.24f, 0.23f, 0.20f), 0.05f, 0.38f, Color.black); // 꺼진 전기등 유리 재질 생성
            materials.LampOn = CreateOrUpdateMaterial("ElectricLight_On", new Color(0.95f, 0.80f, 0.55f), 0.02f, 0.46f, new Color(3.2f, 2.2f, 1.1f)); // 켜진 전기등 따뜻한 발광 재질 생성
            return materials; // 완성된 재질 묶음 반환
        }

        private static Material CreateOrUpdateMaterial(string materialName, Color baseColor, float metallic, float smoothness, Color emissionColor) // URP Lit 재질 생성 또는 갱신
        {
            string assetPath = $"{MaterialFolder}/{materialName}.mat"; // 재질 에셋 저장 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 생성 재질 조회
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 현재 URP 기본 Lit Shader 조회

            if (shader == null) // URP Lit Shader 탐색 실패 여부 확인
            {
                shader = Shader.Find("Standard"); // 에디터 구성 중 최소 대체 Shader 조회
            }

            if (shader == null) // 사용 가능한 기본 Shader 존재 여부 확인
            {
                Debug.LogError($"[Project I][Day11] {materialName} 재질을 생성할 Shader를 찾을 수 없습니다."); // Shader 누락 오류 출력
                return material; // 기존 재질이 있으면 그대로 반환하고 새 생성은 중단
            }

            if (material == null) // 기존 재질이 없는지 확인
            {
                material = new Material(shader); // 새 재질 생성
                material.name = materialName; // 에셋 내부 이름 지정
                AssetDatabase.CreateAsset(material, assetPath); // 프로젝트 에셋으로 재질 저장
            }
            else if (shader != null) // 기존 재질과 유효 Shader 존재 여부 확인
            {
                material.shader = shader; // 현재 프로젝트 Shader로 재질 갱신
            }

            if (material.HasProperty("_BaseColor")) // URP 기본색 속성 존재 여부 확인
            {
                material.SetColor("_BaseColor", baseColor); // URP 기본색 적용
            }

            material.color = baseColor; // 호환 가능한 기본색 값도 함께 적용

            if (material.HasProperty("_Metallic")) // 금속성 속성 존재 여부 확인
            {
                material.SetFloat("_Metallic", metallic); // 금속성 값 적용
            }

            if (material.HasProperty("_Smoothness")) // 매끄러움 속성 존재 여부 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 표면 매끄러움 값 적용
            }

            if (material.HasProperty("_EmissionColor")) // 발광 속성 존재 여부 확인
            {
                material.SetColor("_EmissionColor", emissionColor); // 발광 색상과 강도 적용
            }

            if (emissionColor.maxColorComponent > 0.01f) // 실제 발광 재질 여부 확인
            {
                material.EnableKeyword("_EMISSION"); // URP 발광 키워드 활성화
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // 실시간 발광 재질 플래그 지정
            }
            else // 일반 비발광 재질 처리
            {
                material.DisableKeyword("_EMISSION"); // 불필요한 발광 키워드 비활성화
            }

            EditorUtility.SetDirty(material); // 수정된 재질 저장 대상으로 표시
            return material; // 생성 또는 갱신한 재질 반환
        }

        private static void EnsureMaterialFolders() // 자동 생성 재질 폴더 구조 확보
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRootFolder)) // Generated 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder("Assets/ProjectI/Art", "Generated"); // 공용 Generated 폴더 생성
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) // Day11 재질 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder(GeneratedRootFolder, "Day11"); // Day11 전용 재질 폴더 생성
            }
        }

        private static void EnsureMarker(Scene scene) // Day 11 자동 적용 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 검색

            if (marker == null) // 완료 마커 미생성 여부 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 개발용 마커 숨김
            }
        }

        private sealed class Day11Materials // Day 11 생성 재질 묶음
        {
            public Material Body; // 발전기 본체 재질
            public Material Metal; // 노출 금속 재질
            public Material DarkMetal; // 어두운 엔진 금속 재질
            public Material Rubber; // 고무 받침 재질
            public Material Warning; // 경고 노란색 재질
            public Material GreenGlow; // 초록 상태 발광 재질
            public Material RedGlow; // 빨간 상태 발광 재질
            public Material LampOff; // 소등 전기등 재질
            public Material LampOn; // 점등 전기등 발광 재질
        }
    }
}
