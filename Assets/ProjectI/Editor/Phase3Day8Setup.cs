using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트와 자식 검색 기능 참조
using ProjectI.Brightness; // 이동형 BrightnessSource 기능 참조
using ProjectI.Items; // WorldItem과 CarryType 기능 참조
using ProjectI.Lighting; // 휴대 조명과 F1 디버그 페이지 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 8 휴대 조명 자동 구성
    public static class Phase3Day8Setup // 횃불·랜턴·이동형 밝기·연료 테스트 구역 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string BrightnessZoneName = "10_BrightnessTest"; // 7일차 밝기 시험 모듈 이름
        private const string PortableTestName = "PortableLightTest"; // 8일차 휴대 조명 시험 루트 이름
        private const string ReadyMarkerName = "===Day8 Torch Tip Light Ready==="; // 횃불 끝부분 광원 위치 수정 자동 적용 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 기존 테스트 재질 폴더 경로

        static Phase3Day8Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 후 휴대 조명 구성 예약
        }

        [MenuItem("Tools/Project I/Day 8/Apply Portable Lights")] // 수동 Day 8 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 구성 실행
        {
            ApplyDay8(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 구성 중단
            }

            ApplyDay8(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay8(bool showDialog, bool force) // Day 8 전체 휴대 조명 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day 8 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 루트 조회
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회

            if (mapRoot == null || player == null) // 필수 기존 구조 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 8 구성 전에 Day 3 Test Map과 Player가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // Day 8 구성 중단
            }

            Transform brightnessZone = mapRoot.transform.Find(BrightnessZoneName); // 7일차 밝기 테스트 모듈 조회

            if (brightnessZone == null) // 7일차 밝기 모듈 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 8 구성 전에 10_BrightnessTest가 필요합니다. Day 7 Setup을 먼저 적용하세요."); // 선행 Day 7 누락 오류 출력
                return; // Day 8 구성 중단
            }

            BuildPortableLightTest(brightnessZone); // OutdoorPlaza에 시험용 횃불과 랜턴 생성
            ConfigureDebugPage(player); // F1 통합 디버그 목록에 휴대 조명 페이지 추가
            EnsureMarker(scene); // Day 8 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 프로젝트 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트 상태 갱신
            bool validationPassed = Phase3Day8Validator.Validate(false); // Day 8 구조와 이동형 광원 규칙 검증

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day 8 횃불·랜턴·연료·이동형 밝기 구성이 완료되었습니다." : "Day 8 검증 실패 - Console을 확인하세요."; // 결과 안내 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 결과 대화상자 표시
            }
        }

        private static void BuildPortableLightTest(Transform brightnessZone) // 기존 밝기 시험 모듈에 휴대 조명 시험 구역 생성
        {
            Transform existingTest = brightnessZone.Find(PortableTestName); // 기존 8일차 시험 루트 조회

            if (existingTest != null) // 기존 시험 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existingTest.gameObject); // 강제 재적용 시 중복 휴대 조명 제거
            }

            Transform outdoorPlaza = brightnessZone.Find("OutdoorPlaza"); // 7일차 건물 입구 앞 외부 광장 조회

            if (outdoorPlaza == null) // 외부 광장 존재 여부 확인
            {
                Debug.LogError("[Project I] PortableLightTest를 배치할 OutdoorPlaza를 찾을 수 없습니다."); // 배치 기준 누락 오류 출력
                return; // 휴대 조명 생성 중단
            }

            Renderer plazaRenderer = outdoorPlaza.GetComponent<Renderer>(); // 광장 Bounds 계산용 Renderer 조회
            Vector3 center = plazaRenderer == null ? outdoorPlaza.position : plazaRenderer.bounds.center; // 광장 중심 월드 위치 계산
            float topY = plazaRenderer == null ? 0.3f : plazaRenderer.bounds.max.y + 0.35f; // 아이템이 바닥 위에 놓일 높이 계산
            GameObject testRoot = new GameObject(PortableTestName); // 8일차 휴대 조명 시험 루트 생성
            testRoot.transform.SetParent(brightnessZone); // 7일차 밝기 시험 모듈 아래에 연결
            Material metalMaterial = LoadMaterial("Test_Metal"); // 손잡이와 랜턴 몸체용 테스트 재질 조회
            Material flameMaterial = LoadMaterial("Test_Orange"); // 횃불 불꽃 표시용 테스트 재질 조회
            Material glowMaterial = LoadMaterial("Test_Yellow"); // 랜턴 발광부 표시용 테스트 재질 조회
            CreateTorch(testRoot.transform, new Vector3(center.x, topY, center.z + 2.2f), metalMaterial, flameMaterial); // 광장 북쪽에 시험용 횃불 생성
            CreateLantern(testRoot.transform, new Vector3(center.x, topY + 0.05f, center.z - 2.2f), metalMaterial, glowMaterial); // 광장 남쪽에 시험용 랜턴 생성
        }

        private static void CreateTorch(Transform parent, Vector3 position, Material handleMaterial, Material flameMaterial) // OneHand 시험용 횃불 생성
        {
            GameObject root = CreatePortableRoot(parent, "TestTorch", position, "시험용 횃불", new Vector3(0.28f, 0.85f, 0.28f), 0.14f); // 횃불 월드 아이템 루트 생성
            CreateVisualPrimitive(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.08f, 0.38f, 0.08f), handleMaterial); // 세로 손잡이 시각 요소 생성
            CreateVisualPrimitive(root.transform, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.50f, 0f), new Vector3(0.18f, 0.24f, 0.18f), flameMaterial); // 실제 불꽃 표시는 횃불 머리 위치에 유지
            GameObject lightOrigin = new GameObject("TorchLightOrigin"); // 횃불 불꽃 끝부분에 실제 주변광 중심을 둘 별도 광원 시작점 생성
            lightOrigin.transform.SetParent(root.transform, false); // 횃불 루트 아래에 광원 시작점 연결
            lightOrigin.transform.localPosition = new Vector3(0f, 0.52f, 0f); // 횃불 불꽃 끝부분과 거의 같은 위치에 광원 중심 배치
            Light visualLight = lightOrigin.AddComponent<Light>(); // 횃불 실제 화면용 Point Light 추가
            visualLight.type = LightType.Point; // 횃불은 전 방향 주변광 유지
            visualLight.range = 7f; // 너무 멀리 뻗지 않는 근거리 주변 조명 범위 적용
            visualLight.intensity = 4.5f; // 플레이어 앞과 주변을 분명하게 볼 수 있는 시각 밝기 적용
            visualLight.color = new Color(1f, 0.58f, 0.24f); // 횃불 느낌의 따뜻한 화면 광원 색상 적용
            BrightnessSource source = root.AddComponent<BrightnessSource>(); // 게임 판정용 밝기 광원 추가
            source.Configure(0.35f, 7f, false, visualLight, BrightnessSourceType.Portable, BrightnessEmissionShape.Omnidirectional, 52f); // 횃불 끝부분을 중심으로 모든 방향 주변 밝기 계산
            PortableLightItem portable = root.AddComponent<PortableLightItem>(); // 점화·소화·연료 기능 추가
            portable.Configure(60f, 1f, false); // 연료 60초 분량과 초당 1 소비, 시작 소화 상태 설정
        }

        private static void CreateLantern(Transform parent, Vector3 position, Material bodyMaterial, Material glowMaterial) // OneHand 시험용 랜턴 생성
        {
            GameObject root = CreatePortableRoot(parent, "TestLantern", position, "시험용 랜턴", new Vector3(0.46f, 0.62f, 0.38f), 0.20f); // 랜턴 월드 아이템 루트 생성
            CreateVisualPrimitive(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.42f, 0.30f), bodyMaterial); // 랜턴 금속 몸체 시각 요소 생성
            CreateVisualPrimitive(root.transform, "Glow", PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0f), new Vector3(0.24f, 0.28f, 0.24f), glowMaterial); // 랜턴 중심 발광부 시각 요소 생성

            GameObject beamOrigin = new GameObject("LanternBeamOrigin"); // 플레이어 화면 정중앙을 향할 장거리 빔 시작점 생성
            beamOrigin.transform.SetParent(root.transform, false); // 랜턴 루트 아래에 빔 시작점 연결
            beamOrigin.transform.localPosition = new Vector3(0f, 0.10f, 0.22f); // 랜턴 전면에서 빔이 시작되도록 약간 앞으로 배치
            Light beamLight = beamOrigin.AddComponent<Light>(); // 실제 화면용 장거리 Spot Light 추가
            beamLight.type = LightType.Spot; // 랜턴을 전방 집중형 빔으로 변경
            beamLight.range = 22f; // 복도와 방 정면을 길게 확인할 수 있는 장거리 범위 적용
            beamLight.intensity = 8f; // 먼 거리에서도 중심부가 식별되는 빔 밝기 적용
            beamLight.spotAngle = 52f; // 중앙 시야를 확보하면서 과도하게 넓지 않은 외부 빔 각도 적용
            beamLight.innerSpotAngle = 30f; // 화면 정중앙은 더 강하고 안정적으로 보이도록 내부 빔 각도 적용
            beamLight.color = new Color(1f, 0.84f, 0.56f); // 랜턴의 따뜻한 장거리 빔 색상 적용
            BrightnessSource beamSource = root.AddComponent<BrightnessSource>(); // 랜턴 주 빔 게임 판정용 광원 추가
            beamSource.Configure(0.55f, 22f, false, beamLight, BrightnessSourceType.Portable, BrightnessEmissionShape.Cone, 52f); // 시각 Spot Light와 동일하게 전방 원뿔형 게임 밝기 계산

            GameObject ambientOrigin = new GameObject("LanternAmbientOrigin"); // 랜턴 자체 주변과 플레이어 몸 가까이를 약하게 밝힐 보조광 시작점 생성
            ambientOrigin.transform.SetParent(root.transform, false); // 랜턴 루트 아래에 근거리 주변광 연결
            ambientOrigin.transform.localPosition = new Vector3(0f, 0.10f, 0.08f); // 랜턴 몸체 근처에 주변광 중심 배치
            Light ambientLight = ambientOrigin.AddComponent<Light>(); // 근거리 화면용 Point Light 추가
            ambientLight.type = LightType.Point; // 랜턴 주변은 모든 방향으로 약하게 빛나도록 설정
            ambientLight.range = 4.5f; // 플레이어 주변만 보조하는 짧은 범위 적용
            ambientLight.intensity = 1.8f; // 장거리 빔보다 약한 주변광 밝기 적용
            ambientLight.color = new Color(1f, 0.76f, 0.42f); // 장거리 빔과 자연스럽게 섞이는 따뜻한 주변광 색상 적용
            BrightnessSource ambientSource = ambientOrigin.AddComponent<BrightnessSource>(); // 근거리 게임 밝기 계산용 보조 광원 추가
            ambientSource.Configure(0.12f, 4.5f, false, ambientLight, BrightnessSourceType.Portable, BrightnessEmissionShape.Omnidirectional, 52f); // 플레이어 주변에는 약한 모든 방향 밝기만 추가

            PortableLightAim aim = root.AddComponent<PortableLightAim>(); // 손에 든 동안 빔 중심을 플레이어 카메라 정중앙으로 보정하는 기능 추가
            aim.Configure(beamOrigin.transform, 24f); // 화면 중앙 약 24m 전방을 조준하도록 설정
            PortableLightItem portable = root.AddComponent<PortableLightItem>(); // 점화·소화·연료 기능 추가
            portable.Configure(120f, 1f, false); // 연료 120초 분량과 초당 1 소비, 시작 소화 상태 설정
        }

        private static GameObject CreatePortableRoot(Transform parent, string objectName, Vector3 position, string displayName, Vector3 colliderSize, float carryRadius) // 휴대 조명 공통 WorldItem 루트 생성
        {
            GameObject root = new GameObject(objectName); // 휴대 조명 루트 오브젝트 생성
            root.transform.SetParent(parent); // 8일차 시험 루트 아래에 연결
            root.transform.position = position; // 외부 광장 테스트 위치 적용
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 월드 상호작용과 물리용 Collider 추가
            collider.size = colliderSize; // 횃불 또는 랜턴에 맞는 충돌 크기 적용
            Rigidbody body = root.AddComponent<Rigidbody>(); // 월드에 내려놓을 수 있는 Rigidbody 추가
            body.mass = 1f; // 작은 휴대 조명 기본 질량 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 월드 이동 시 물리 보간 적용
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 빠른 배치 변화에도 안정적인 충돌 방식 적용
            WorldItem item = root.AddComponent<WorldItem>(); // 기존 빠른 슬롯·줍기·버리기 시스템 연결
            item.Configure(displayName, carryRadius, CarryType.OneHand); // 두 휴대 조명을 모두 한손 슬롯 아이템으로 설정
            return root; // 생성한 휴대 조명 루트 반환
        }

        private static GameObject CreateVisualPrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material) // Collider 없는 휴대 조명 시각 Primitive 생성
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType); // 기본 Primitive 시각 요소 생성
            visual.name = objectName; // 시각 요소 이름 지정
            visual.transform.SetParent(parent, false); // 휴대 조명 루트 아래에 로컬 기준 연결
            visual.transform.localPosition = localPosition; // 요청된 로컬 위치 적용
            visual.transform.localRotation = Quaternion.identity; // 기본 로컬 회전 적용
            visual.transform.localScale = localScale; // 요청된 시각 크기 적용
            Renderer renderer = visual.GetComponent<Renderer>(); // Primitive Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 기존 테스트 재질 적용
            }

            Collider collider = visual.GetComponent<Collider>(); // 시각 Primitive 기본 Collider 조회

            if (collider != null) // 불필요한 자식 Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 월드 물리는 루트 BoxCollider 하나만 사용하도록 제거
            }

            return visual; // 생성한 시각 요소 반환
        }

        private static void ConfigureDebugPage(GameObject player) // F1 통합 디버그 목록에 휴대 조명 페이지 공급자 추가
        {
            PortableLightDebugPage page = player.GetComponent<PortableLightDebugPage>(); // 기존 휴대 조명 디버그 페이지 조회

            if (page == null) // 아직 페이지 공급자가 없는지 확인
            {
                page = player.AddComponent<PortableLightDebugPage>(); // 새 DebugPageProvider 추가로 Registry 자동 등록 준비
            }

            EditorUtility.SetDirty(page); // 새 디버그 페이지 컴포넌트 저장 대상으로 표시
        }

        private static Material LoadMaterial(string materialName) // 기존 테스트 재질 조회
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 전체 경로 생성
            return AssetDatabase.LoadAssetAtPath<Material>(path); // 재질 에셋 반환
        }

        private static void EnsureMarker(Scene scene) // Day 8 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 Day 8 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 완료 마커 숨김
            }
        }
    }
}
