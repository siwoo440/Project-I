using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트 검색 기능 참조
using ProjectI.Brightness; // 밝기 시스템 기능 참조
using UnityEditor; // 유니티 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 7 자동 구성
    public static class Phase3Day7Setup // 외부·내부 밝기 코어와 대형 건축물 테스트 모듈 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string PlayerRootName = "Player"; // 플레이어 루트 이름
        private const string BrightnessSystemName = "===Brightness System==="; // 밝기 시스템 루트 이름
        private const string BrightnessZoneName = "10_BrightnessTest"; // Day 7 밝기 시험 모듈 이름
        private const string ReadyMarkerName = "===Day7 Brightness Ready==="; // Day 7 자동 적용 완료 마커
        private const string MaterialFolder = "Assets/ProjectI/Art/Test/Materials"; // 기존 테스트 재질 폴더 경로

        static Phase3Day7Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 후 구성 예약
        }

        [MenuItem("Tools/Project I/Day 7/Apply Brightness Core + Building")] // 수동 Day 7 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 구성 실행
        {
            ApplyDay7(true, true); // 강제 재적용과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // 자동 구성 중단
            }

            ApplyDay7(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay7(bool showDialog, bool force) // Day 7 전체 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬이 없으면 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (!force && existingMarker != null) // 이미 자동 적용된 씬인지 확인
            {
                return; // 중복 적용 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 루트 조회
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerRootName); // 플레이어 루트 조회

            if (mapRoot == null || player == null) // 선행 테스트 맵과 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 7 구성 전에 Day 3 Test Map과 Player가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // Day 7 구성 중단
            }

            BrightnessManager manager = BuildBrightnessSystem(scene); // 외부·내부 밝기 계산 관리자와 자연광 구성
            PlayerBrightnessSensor sensor = ConfigurePlayerSensor(player, manager); // 플레이어 현재 밝기 센서 연결
            BuildBrightnessTestModule(mapRoot); // 기존 테스트 맵 옆에 대형 건축물 모듈 자연스럽게 추가
            ConfigureBrightnessDebugPage(player, sensor); // 밝기 정보를 공통 F1 디버그 페이지 공급자로 구성
            EnsureMarker(scene); // Day 7 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 변경된 탐사 사무소 씬 저장
            AssetDatabase.SaveAssets(); // 에셋 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool validationPassed = Phase3Day7Validator.Validate(false); // Day 7 구조 자동 검증

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day 7 밝기 코어와 대형 실내 건축물 모듈 구성이 완료되었습니다." : "Day 7 검증 실패 - Console을 확인하세요."; // 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 안내 표시
            }
        }

        private static BrightnessManager BuildBrightnessSystem(Scene scene) // 밝기 관리자와 자연광 시스템 구성
        {
            GameObject existingRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == BrightnessSystemName); // 기존 밝기 시스템 루트 조회

            if (existingRoot != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot); // 강제 재구성을 위해 기존 밝기 시스템 제거
            }

            GameObject systemRoot = new GameObject(BrightnessSystemName); // 새 밝기 시스템 루트 생성
            NaturalLightController naturalLight = systemRoot.AddComponent<NaturalLightController>(); // 태양·달 자연광 컨트롤러 추가
            naturalLight.Configure(0.55f, 0.05f); // Day 7 외부 테스트용 태양 0.55 + 달 0.05 설정
            BrightnessManager manager = systemRoot.AddComponent<BrightnessManager>(); // 외부·내부 밝기 계산 관리자 추가
            manager.Configure(naturalLight); // 관리자와 자연광 컨트롤러 연결
            return manager; // 생성된 밝기 관리자 반환
        }

        private static PlayerBrightnessSensor ConfigurePlayerSensor(GameObject player, BrightnessManager manager) // 플레이어 밝기 센서 구성
        {
            PlayerBrightnessSensor sensor = GetOrAddComponent<PlayerBrightnessSensor>(player); // 플레이어 현재 밝기 센서 확보
            sensor.Configure(manager); // 센서와 밝기 관리자 연결
            EditorUtility.SetDirty(sensor); // 센서 변경 저장 대상으로 표시
            return sensor; // 구성된 센서 반환
        }

        private static void BuildBrightnessTestModule(GameObject mapRoot) // 09 테스트 구역 옆에 연결형 대형 밝기 시험 건축물 생성
        {
            Transform existingZone = mapRoot.transform.Find(BrightnessZoneName); // 기존 Day 7 시험 모듈 조회

            if (existingZone != null) // 기존 시험 모듈 존재 여부 확인
            {
                Object.DestroyImmediate(existingZone.gameObject); // 중복 방지를 위해 기존 모듈 제거
            }

            Vector3 buildingCenter = ResolveBuildingCenter(mapRoot); // 기존 테스트 맵 오른쪽에 자연스럽게 이어질 건축물 중심 계산
            const float buildingWidth = 24f; // 대형 건축물 가로 길이
            const float buildingDepth = 18f; // 대형 건축물 세로 길이
            const float buildingHeight = 8f; // 대형 건축물 벽 높이
            const float wallThickness = 0.5f; // 벽 두께
            const float doorWidth = 4f; // 출입구 폭
            const float doorHeight = 4f; // 출입구 높이
            float westFaceX = buildingCenter.x - (buildingWidth * 0.5f); // 건축물 서쪽 출입구 벽 X 위치 계산
            Material floorMaterial = LoadMaterial("Test_Blue"); // 기존 테스트 바닥 재질 조회
            Material wallMaterial = LoadMaterial("Test_Metal"); // 건축물 벽·지붕 재질 조회
            Material lampMaterial = LoadMaterial("Test_Yellow"); // 광원 표시 재질 조회
            Material outdoorMaterial = LoadMaterial("Test_Orange"); // 외부 구역 표시 재질 조회
            GameObject zone = new GameObject(BrightnessZoneName); // Day 7 시험 모듈 루트 생성
            zone.transform.SetParent(mapRoot.transform); // 기존 공용 테스트 맵의 다음 모듈로 연결

            BuildConnectorAndPlaza(zone.transform, mapRoot, buildingCenter, westFaceX, floorMaterial, outdoorMaterial); // 09 영역에서 건축물까지 이어지는 통로와 외부 광장 생성
            GameObject building = new GameObject("MassiveIndoorBuilding"); // 대형 실내 건축물 루트 생성
            building.transform.SetParent(zone.transform); // Day 7 시험 모듈 아래에 건축물 연결
            CreatePrimitive(building.transform, "Floor", PrimitiveType.Cube, new Vector3(buildingCenter.x, 0f, buildingCenter.z), new Vector3(buildingWidth, 0.20f, buildingDepth), floorMaterial, true); // 건축물 내부 바닥 생성
            CreatePrimitive(building.transform, "Roof", PrimitiveType.Cube, new Vector3(buildingCenter.x, buildingHeight, buildingCenter.z), new Vector3(buildingWidth, 0.35f, buildingDepth), wallMaterial, true); // 외부 자연광이 내부 판정과 시각적으로 분리되도록 지붕 생성
            CreatePrimitive(building.transform, "NorthWall", PrimitiveType.Cube, new Vector3(buildingCenter.x, buildingHeight * 0.5f, buildingCenter.z + (buildingDepth * 0.5f)), new Vector3(buildingWidth, buildingHeight, wallThickness), wallMaterial, true); // 북쪽 외벽 생성
            CreatePrimitive(building.transform, "SouthWall", PrimitiveType.Cube, new Vector3(buildingCenter.x, buildingHeight * 0.5f, buildingCenter.z - (buildingDepth * 0.5f)), new Vector3(buildingWidth, buildingHeight, wallThickness), wallMaterial, true); // 남쪽 외벽 생성
            CreatePrimitive(building.transform, "EastWall", PrimitiveType.Cube, new Vector3(buildingCenter.x + (buildingWidth * 0.5f), buildingHeight * 0.5f, buildingCenter.z), new Vector3(wallThickness, buildingHeight, buildingDepth), wallMaterial, true); // 동쪽 외벽 생성

            float westSegmentDepth = (buildingDepth - doorWidth) * 0.5f; // 출입구를 제외한 서쪽 벽 한쪽 길이 계산
            float westSegmentOffset = (doorWidth * 0.5f) + (westSegmentDepth * 0.5f); // 출입구 중심에서 벽 조각 중심까지 거리 계산
            CreatePrimitive(building.transform, "WestWall_North", PrimitiveType.Cube, new Vector3(westFaceX, buildingHeight * 0.5f, buildingCenter.z + westSegmentOffset), new Vector3(wallThickness, buildingHeight, westSegmentDepth), wallMaterial, true); // 출입구 북쪽 서쪽 벽 생성
            CreatePrimitive(building.transform, "WestWall_South", PrimitiveType.Cube, new Vector3(westFaceX, buildingHeight * 0.5f, buildingCenter.z - westSegmentOffset), new Vector3(wallThickness, buildingHeight, westSegmentDepth), wallMaterial, true); // 출입구 남쪽 서쪽 벽 생성
            CreatePrimitive(building.transform, "WestWall_DoorLintel", PrimitiveType.Cube, new Vector3(westFaceX, doorHeight + ((buildingHeight - doorHeight) * 0.5f), buildingCenter.z), new Vector3(wallThickness, buildingHeight - doorHeight, doorWidth), wallMaterial, true); // 출입구 상단 벽 생성

            GameObject areaObject = new GameObject("IndoorRoomArea"); // 건물 내부 한 방 전체를 담당할 밝기 영역 생성
            areaObject.transform.SetParent(building.transform); // 건축물 아래에 방 영역 연결
            areaObject.transform.position = new Vector3(buildingCenter.x, 0f, buildingCenter.z); // 건축물 중심과 영역 기준점 일치
            BoxCollider areaCollider = areaObject.AddComponent<BoxCollider>(); // 방 내부 위치 판정용 BoxCollider 추가
            IndoorBrightnessArea indoorArea = areaObject.AddComponent<IndoorBrightnessArea>(); // 내부 전용 밝기 영역 기능 추가
            indoorArea.Configure("Brightness Test Hall", new Vector3(buildingWidth - 1.2f, buildingHeight - 0.6f, buildingDepth - 1.2f), new Vector3(0f, (buildingHeight - 0.6f) * 0.5f, 0f)); // 벽 안쪽 전체를 하나의 방 영역으로 지정
            areaCollider.isTrigger = true; // 플레이어 물리 이동을 막지 않는 Trigger 영역으로 유지

            CreateBrightnessLamp(zone.transform, "OutdoorLamp_A", new Vector3(westFaceX - 2.2f, 4.2f, buildingCenter.z + 4.2f), 0.35f, 10f, lampMaterial); // 출입구 북쪽 외부 광원 생성
            CreateBrightnessLamp(zone.transform, "OutdoorLamp_B", new Vector3(westFaceX - 2.2f, 4.2f, buildingCenter.z - 4.2f), 0.35f, 10f, lampMaterial); // 출입구 남쪽 외부 광원 생성
            CreateBrightnessLamp(areaObject.transform, "IndoorLamp_A", new Vector3(buildingCenter.x - 6f, 5.8f, buildingCenter.z), 0.38f, 10f, lampMaterial); // 방 서쪽 내부 광원 생성
            CreateBrightnessLamp(areaObject.transform, "IndoorLamp_B", new Vector3(buildingCenter.x, 5.8f, buildingCenter.z), 0.46f, 10f, lampMaterial); // 방 중앙 내부 광원 생성
            CreateBrightnessLamp(areaObject.transform, "IndoorLamp_C", new Vector3(buildingCenter.x + 6f, 5.8f, buildingCenter.z), 0.38f, 10f, lampMaterial); // 방 동쪽 내부 광원 생성
        }

        private static Vector3 ResolveBuildingCenter(GameObject mapRoot) // 기존 09 테스트 모듈 오른쪽에 Day 7 건축물 위치 자동 계산
        {
            Transform inventoryFloor = mapRoot.transform.Find("09_InventoryTest/InventoryFloor"); // 09 인벤토리 테스트 바닥 조회

            if (inventoryFloor != null) // 기존 09 모듈 존재 여부 확인
            {
                Renderer floorRenderer = inventoryFloor.GetComponent<Renderer>(); // 09 바닥 Renderer 조회

                if (floorRenderer != null) // 바닥 Bounds 계산 가능 여부 확인
                {
                    Bounds bounds = floorRenderer.bounds; // 09 테스트 바닥 월드 Bounds 조회
                    float centerX = bounds.max.x + 17f; // 기존 바닥 오른쪽 끝에서 연결 통로 5m와 건물 반폭 12m를 더해 중심 계산
                    return new Vector3(centerX, 0f, bounds.center.z); // 기존 모듈과 같은 Z축 선상에 건축물 중심 배치
                }
            }

            return new Vector3(16f, 0f, -36f); // 09 구역을 찾지 못했을 때 사용하는 안전한 기본 위치
        }

        private static void BuildConnectorAndPlaza(Transform zone, GameObject mapRoot, Vector3 buildingCenter, float westFaceX, Material floorMaterial, Material outdoorMaterial) // 기존 맵과 건축물을 자연스럽게 이어주는 외부 공간 구성
        {
            float sourceEdgeX = westFaceX - 5f; // 건축물 출입구 앞 연결 시작 위치 추정
            Transform inventoryFloor = mapRoot.transform.Find("09_InventoryTest/InventoryFloor"); // 기존 09 바닥 조회

            if (inventoryFloor != null) // 09 바닥 존재 여부 확인
            {
                Renderer renderer = inventoryFloor.GetComponent<Renderer>(); // 기존 바닥 Renderer 조회

                if (renderer != null) // Bounds 조회 가능 여부 확인
                {
                    sourceEdgeX = renderer.bounds.max.x; // 실제 09 바닥 오른쪽 끝을 연결 시작점으로 사용
                }
            }

            float connectorLength = Mathf.Max(1f, westFaceX - sourceEdgeX); // 기존 모듈과 건축물 사이 실제 연결 길이 계산
            float connectorCenterX = sourceEdgeX + (connectorLength * 0.5f); // 연결 통로 중심 X 계산
            CreatePrimitive(zone, "ConnectorWalkway", PrimitiveType.Cube, new Vector3(connectorCenterX, 0f, buildingCenter.z), new Vector3(connectorLength, 0.16f, 4.5f), floorMaterial, true); // 기존 09 모듈과 건축물을 직접 이어주는 통로 생성
            CreatePrimitive(zone, "OutdoorPlaza", PrimitiveType.Cube, new Vector3(westFaceX - 2.4f, -0.01f, buildingCenter.z), new Vector3(4.8f, 0.14f, 12f), outdoorMaterial, true); // 출입구 앞 외부 밝기 비교용 광장 생성
        }

        private static void CreateBrightnessLamp(Transform parent, string objectName, Vector3 position, float brightness, float range, Material markerMaterial) // 화면용 Point Light와 게임용 BrightnessSource를 함께 생성
        {
            GameObject lampRoot = new GameObject(objectName); // 광원 루트 생성
            lampRoot.transform.SetParent(parent); // 외부 또는 내부 영역 부모 아래 배치
            lampRoot.transform.position = position; // 광원 월드 위치 지정
            GameObject marker = CreatePrimitive(lampRoot.transform, "LampMarker", PrimitiveType.Sphere, position, new Vector3(0.35f, 0.35f, 0.35f), markerMaterial, false); // 광원 위치 확인용 작은 구체 생성
            marker.transform.localPosition = Vector3.zero; // 광원 루트 중심과 표시 구체 중심 일치
            Light pointLight = lampRoot.AddComponent<Light>(); // 실제 화면 밝기 표현용 Unity Light 추가
            pointLight.type = LightType.Point; // 점광원 방식 지정
            pointLight.range = range; // 게임용 영향 거리와 비슷한 시각적 범위 적용
            pointLight.intensity = 5f; // 테스트 맵에서 구분 가능한 기본 시각 밝기 적용
            pointLight.color = new Color(1f, 0.78f, 0.46f); // 따뜻한 테스트 광원 색상 적용
            BrightnessSource source = lampRoot.AddComponent<BrightnessSource>(); // 게임 로직용 밝기 광원 추가
            source.Configure(brightness, range, true, pointLight); // 게임용 밝기와 거리, 화면 Light 연결
        }

        private static void ConfigureBrightnessDebugPage(GameObject player, PlayerBrightnessSensor sensor) // 밝기 정보를 공통 F1 디버그 페이지 공급자로 구성
        {
            BrightnessDebugHud hud = GetOrAddComponent<BrightnessDebugHud>(player); // 플레이어 루트에 밝기 디버그 페이지 공급자 확보
            hud.Configure(sensor); // 현재 플레이어 밝기 센서 연결
            EditorUtility.SetDirty(hud); // 공급자 참조 변경 저장 대상으로 표시
        }

        private static GameObject CreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 테스트 구조물 Primitive 생성
        {
            GameObject target = GameObject.CreatePrimitive(primitiveType); // 기본 Primitive 생성
            target.name = objectName; // 오브젝트 이름 지정
            target.transform.SetParent(parent); // 지정 부모 아래에 연결
            target.transform.position = position; // 월드 위치 지정
            target.transform.localScale = scale; // 테스트 구조물 크기 지정
            Renderer renderer = target.GetComponent<Renderer>(); // Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 기존 테스트 재질 적용
            }

            if (!keepCollider) // Collider가 필요 없는 시각 표시물인지 확인
            {
                Collider collider = target.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 여부 확인
                {
                    Object.DestroyImmediate(collider); // 불필요 Collider 제거
                }
            }

            return target; // 생성한 Primitive 반환
        }

        private static Material LoadMaterial(string materialName) // 기존 테스트 재질 조회
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 전체 경로 생성
            return AssetDatabase.LoadAssetAtPath<Material>(path); // 재질 에셋 반환
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 기존 또는 새 컴포넌트 확보
        {
            T component = target.GetComponent<T>(); // 기존 컴포넌트 조회
            return component != null ? component : target.AddComponent<T>(); // 기존 컴포넌트가 없으면 새로 추가하여 반환
        }

        private static void EnsureMarker(Scene scene) // Day 7 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 조회

            if (marker == null) // 마커 미생성 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서 마커 숨김
            }
        }
    }
}
