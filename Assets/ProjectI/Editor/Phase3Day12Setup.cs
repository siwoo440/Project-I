using System.IO; // 생성 재질 폴더 확인 기능 참조
using System.Linq; // 씬 루트와 자식 검색 기능 참조
using ProjectI.Brightness; // 기존 게임 밝기 광원 기능 참조
using ProjectI.Power; // 11~12일차 전력 시스템 기능 참조
using UnityEditor; // 에디터 메뉴와 에셋 저장 기능 참조
using UnityEditor.SceneManagement; // 씬 열기와 저장 기능 참조
using UnityEngine; // 게임 오브젝트와 재질 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 12 전력 시험 환경 자동 구성
    public static class Phase3Day12Setup // 방 단위 배전반·전등·전동 철제문 시험 환경 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string MapRootName = "===Day3 Test Map==="; // 공용 테스트 맵 루트 이름
        private const string BrightnessZoneName = "10_BrightnessTest"; // 기존 대형 실내 시험 구역 이름
        private const string Day11RootName = "Day11_PowerTest"; // 기존 발전기·직결 전등 시험 루트 이름
        private const string Day12RootName = "12_PowerControlTest"; // 12일차 방 전력 시험 루트 이름
        private const string ReadyMarkerName = "===Day12 Room Power Ready v3==="; // 글자 방향 수정본 자동 적용 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day12 Room Power Ready v2==="; // 이전 글자 방향 버전 자동 적용 마커 이름
        private const string GeneratedRootFolder = "Assets/ProjectI/Art/Generated"; // 자동 생성 아트 루트 경로
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day12"; // 12일차 생성 재질 경로
        private const float LabelScale = 0.5f; // 배전반·방·문 라벨 전체 표시 크기 축소 배율

        static Phase3Day12Setup() // 자동 적용 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 12일차 구성 예약
        }

        [MenuItem("Tools/Project I/Day 12/Apply Room Power + Iron Doors")] // 수동 12일차 적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 12일차 구성 실행
        {
            ApplyDay12(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 구성 중단
            }

            ApplyDay12(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay12(bool showDialog, bool force) // Day 12 전체 방 전력 시험 환경 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬이 없으면 자동 적용 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day 12 완료 마커 조회

            if (!force && existingMarker != null) // 이미 12일차 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MapRootName); // 공용 테스트 맵 루트 조회

            if (mapRoot == null) // 선행 테스트 맵 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 12 구성 전에 Day 3 Test Map이 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            Transform brightnessZone = mapRoot.transform.Find(BrightnessZoneName); // 기존 실내 밝기 시험 구역 조회

            if (brightnessZone == null) // 대형 실내 시험 구역 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 12 구성 전에 10_BrightnessTest가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // 구성 중단
            }

            IndoorBrightnessArea indoorArea = brightnessZone.GetComponentInChildren<IndoorBrightnessArea>(true); // 방을 배치할 기존 실내 영역 조회
            GeneratorController generator = Object.FindFirstObjectByType<GeneratorController>(); // 11일차 발전기 조회

            if (indoorArea == null || indoorArea.Volume == null || generator == null) // 실내 영역과 발전기 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 12 구성에 필요한 IndoorBrightnessArea 또는 Day 11 발전기를 찾을 수 없습니다."); // 선행 전력 구조 누락 오류 출력
                return; // 구성 중단
            }

            EnsureMaterialFolders(); // 12일차 생성 재질 폴더 확보
            Day12Materials materials = BuildMaterials(); // 방·전등·문·배전반 전용 URP 재질 생성
            DisableDay11DirectLights(indoorArea.transform); // 11일차 직결 전등을 시각적으로 비활성화
            RepositionGenerator(generator, indoorArea.Volume.bounds); // 기존 발전기를 새 제어 시험 구역 서비스 위치로 이동
            RepositionFixedFireLights(indoorArea); // 기존 횃불과 화로를 삭제하지 않고 벽면·개방 구역으로 이동
            Transform existingRoot = indoorArea.transform.Find(Day12RootName); // 기존 12일차 시험 루트 검색

            if (existingRoot != null) // 기존 12일차 구조 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot.gameObject); // 강제 적용을 위해 기존 구조 제거
            }

            GameObject day12Root = new GameObject(Day12RootName); // 12일차 전력 시험 루트 생성
            day12Root.transform.SetParent(indoorArea.transform, false); // 기존 대형 실내 영역 아래에 연결
            Bounds bounds = indoorArea.Volume.bounds; // 실내 영역 월드 Bounds 조회
            BuildCorridorDetail(day12Root.transform, bounds, materials); // 방 앞 서비스 통로 세부 요소 생성
            RoomBuildResult[] rooms = BuildRooms(day12Root.transform, bounds, materials); // 3개 방과 전등·철제문·로컬 패널 생성
            BuildDistributionBoard(day12Root.transform, bounds, materials, generator, rooms); // 상세 중앙 배전반과 모든 버튼 연결
            EnsureMarker(scene); // 12일차 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // 방·전력 시험 구조를 탐사 사무소 씬에 저장
            AssetDatabase.SaveAssets(); // 생성 재질과 씬 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase3Day12Validator.Validate(false); // Day 12 자동 구성 정적 검증 실행

            if (showDialog) // 수동 적용 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", validationPassed ? "Day 12 방 전력·배전반·철제문 구성이 완료되었습니다." : "Day 12 검증 실패 - Console을 확인하세요.", "확인"); // 수동 적용 결과 안내
            }
        }

        private static void DisableDay11DirectLights(Transform indoorAreaRoot) // 기존 발전기 직결 전등 시각적 비활성화
        {
            Transform day11Root = indoorAreaRoot.Find(Day11RootName); // 기존 Day 11 시험 루트 조회

            if (day11Root == null) // 기존 Day 11 루트 존재 여부 확인
            {
                return; // 직결 전등 비활성화 생략
            }

            ElectricLightController[] oldLights = day11Root.GetComponentsInChildren<ElectricLightController>(true); // 기존 발전기 직결 전등 전체 조회

            foreach (ElectricLightController oldLight in oldLights) // 기존 전등 전체 순회
            {
                if (oldLight != null) // 유효 기존 전등 여부 확인
                {
                    oldLight.gameObject.SetActive(false); // 새 방 전력 시험을 방해하지 않도록 전등 루트 비활성화
                }
            }
        }

        private static void RepositionGenerator(GeneratorController generator, Bounds bounds) // 기존 발전기를 배전반 근처 서비스 구역으로 이동
        {
            Vector3 generatorPosition = new Vector3(bounds.min.x + 3.0f, bounds.min.y + 0.05f, bounds.min.z + 3.2f); // 남서쪽 서비스 구역 발전기 위치 계산
            generator.transform.position = generatorPosition; // 기존 발전기 전체 모델 위치 변경
            generator.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // 배전반 시험 공간 기준 발전기 방향 정렬
            EditorUtility.SetDirty(generator.transform); // 변경된 발전기 Transform 저장 대상으로 표시
        }

        private static void RepositionFixedFireLights(IndoorBrightnessArea indoorArea) // Day 9 횃불과 화로를 새 방 배치와 겹치지 않게 이동
        {
            Bounds bounds = indoorArea.Volume.bounds; // 기존 대형 홀 내부 월드 Bounds 조회
            float torchY = bounds.min.y + 1.9f; // 기존 횃불 눈높이 유지
            float wallOffset = 0.18f; // 기존 Indoor 영역 경계보다 실제 외벽 쪽으로 붙이는 보정 거리
            Transform northTorch = FindNamedDescendant(indoorArea.transform, "WallTorch_North"); // 기존 북쪽 횃불 조회
            Transform southTorch = FindNamedDescendant(indoorArea.transform, "WallTorch_South"); // 기존 남쪽 횃불 조회
            Transform eastTorch = FindNamedDescendant(indoorArea.transform, "WallTorch_East"); // 기존 동쪽 횃불 조회
            Transform brazier = FindNamedDescendant(indoorArea.transform, "Brazier_Center"); // 기존 중앙 화로 조회

            if (northTorch != null) // 북쪽 횃불 존재 여부 확인
            {
                northTorch.position = new Vector3(bounds.min.x - wallOffset, torchY, bounds.center.z + 2.6f); // 북쪽 방들과 겹치지 않도록 실제 서쪽 외벽 가까이 이동
                EditorUtility.SetDirty(northTorch); // 이동된 횃불 Transform 저장 대상으로 표시
            }

            if (southTorch != null) // 남쪽 횃불 존재 여부 확인
            {
                southTorch.position = new Vector3(bounds.center.x + 4.0f, torchY, bounds.min.z - wallOffset); // 배전반 반대편 실제 남쪽 외벽 가까이 이동
                EditorUtility.SetDirty(southTorch); // 이동된 횃불 Transform 저장 대상으로 표시
            }

            if (eastTorch != null) // 동쪽 횃불 존재 여부 확인
            {
                eastTorch.position = new Vector3(bounds.max.x + wallOffset, torchY, bounds.center.z - 1.8f); // 실제 동쪽 외벽 가까운 중앙 아래쪽으로 이동
                EditorUtility.SetDirty(eastTorch); // 이동된 횃불 Transform 저장 대상으로 표시
            }

            if (brazier != null) // 화로 존재 여부 확인
            {
                brazier.position = new Vector3(bounds.max.x - 3.0f, bounds.min.y + 0.55f, bounds.min.z + 3.0f); // 방 출입구와 배전반을 피한 남동쪽 개방 공간으로 이동
                EditorUtility.SetDirty(brazier); // 이동된 화로 Transform 저장 대상으로 표시
            }
        }

        private static RoomBuildResult[] BuildRooms(Transform parent, Bounds bounds, Day12Materials materials) // 대형 실내 홀 안에 3개 독립 방 생성
        {
            RoomBuildResult[] results = new RoomBuildResult[3]; // 방 3개 결과 배열 생성
            float floorY = bounds.min.y + 0.12f; // 기존 바닥 위 방 구조 기준 높이 계산
            float roomWidth = Mathf.Min(6.4f, (bounds.size.x - 1.4f) / 3.0f); // 세 방이 외벽을 따라 연속 배치될 폭 계산
            float roomDepth = Mathf.Min(7.2f, bounds.size.z * 0.42f); // 홀 중앙 통로를 남기는 방 깊이 계산
            float roomZ = bounds.max.z + 0.22f - (roomDepth * 0.5f); // Indoor 영역보다 바깥쪽 실제 북쪽 외벽 가까이 방 후면을 밀착 배치
            float xSpacing = roomWidth; // 세 방의 측면 벽이 자연스럽게 이어지도록 연속 간격 적용
            float roomHeight = Mathf.Min(5.2f, bounds.size.y - 1.0f); // 기존 지붕 안쪽 방 높이 계산
            Vector3[] centers = new Vector3[3]; // 방 중심 위치 배열 생성
            centers[0] = new Vector3(bounds.center.x - xSpacing, floorY, roomZ); // ROOM 01 중심 위치 저장
            centers[1] = new Vector3(bounds.center.x, floorY, roomZ); // ROOM 02 중심 위치 저장
            centers[2] = new Vector3(bounds.center.x + xSpacing, floorY, roomZ); // ROOM 03 중심 위치 저장
            string[] names = { "ROOM 01 - STORAGE", "ROOM 02 - WORKSHOP", "ROOM 03 - ARCHIVE" }; // 배전반 표시용 방 이름 정의

            for (int index = 0; index < results.Length; index++) // 방 3개 전체 순회
            {
                results[index] = BuildSingleRoom(parent, centers[index], roomWidth, roomDepth, roomHeight, index, names[index], materials); // 각 위치에 상세 방 생성
            }

            return results; // 생성된 방 결과 배열 반환
        }

        private static RoomBuildResult BuildSingleRoom(Transform parent, Vector3 worldCenter, float width, float depth, float height, int index, string displayName, Day12Materials materials) // 방 하나와 전력 장치 생성
        {
            GameObject roomRoot = new GameObject($"RoomPower_{index + 1:00}"); // 방 전력 루트 생성
            roomRoot.transform.SetParent(parent); // Day 12 시험 루트 아래에 방 연결
            roomRoot.transform.position = worldCenter; // 계산된 방 중심 위치 적용
            float wallThickness = 0.20f; // 내부 방 금속 벽 두께 정의
            float doorWidth = 1.9f; // 철제문 개구부 폭 정의
            float doorHeight = 3.35f; // 철제문 개구부 높이 정의
            float frontZ = -(depth * 0.5f); // 복도 방향 방 전면 Z 위치 계산
            float wallY = height * 0.5f; // 벽 중심 높이 계산
            float frontSegmentWidth = (width - doorWidth) * 0.5f; // 출입구 좌우 전면 벽 폭 계산
            float frontSegmentOffset = (doorWidth * 0.5f) + (frontSegmentWidth * 0.5f); // 출입구 중심에서 벽 중심까지 거리 계산
            CreateVisualPrimitive(roomRoot.transform, "FloorPlate", PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(width, 0.14f, depth), Vector3.zero, materials.Floor, true); // 방 금속 바닥판 생성
            CreateVisualPrimitive(roomRoot.transform, "Ceiling", PrimitiveType.Cube, new Vector3(0f, height, 0f), new Vector3(width, 0.18f, depth), Vector3.zero, materials.Ceiling, true); // 방 천장판 생성
            CreateVisualPrimitive(roomRoot.transform, "BackWallTopTrim", PrimitiveType.Cube, new Vector3(0f, height - 0.16f, (depth * 0.5f) - 0.08f), new Vector3(width - 0.25f, 0.18f, 0.14f), Vector3.zero, materials.Trim, false); // 기존 홀 북쪽 외벽과 맞닿는 후면 상단 보강재 생성
            CreateVisualPrimitive(roomRoot.transform, "LeftWall", PrimitiveType.Cube, new Vector3(-(width * 0.5f), wallY, 0f), new Vector3(wallThickness, height, depth), Vector3.zero, materials.Wall, true); // 방 왼쪽 벽 생성
            CreateVisualPrimitive(roomRoot.transform, "RightWall", PrimitiveType.Cube, new Vector3(width * 0.5f, wallY, 0f), new Vector3(wallThickness, height, depth), Vector3.zero, materials.Wall, true); // 방 오른쪽 벽 생성
            CreateVisualPrimitive(roomRoot.transform, "FrontWall_Left", PrimitiveType.Cube, new Vector3(-frontSegmentOffset, wallY, frontZ), new Vector3(frontSegmentWidth, height, wallThickness), Vector3.zero, materials.Wall, true); // 출입구 왼쪽 전면 벽 생성
            CreateVisualPrimitive(roomRoot.transform, "FrontWall_Right", PrimitiveType.Cube, new Vector3(frontSegmentOffset, wallY, frontZ), new Vector3(frontSegmentWidth, height, wallThickness), Vector3.zero, materials.Wall, true); // 출입구 오른쪽 전면 벽 생성
            CreateVisualPrimitive(roomRoot.transform, "FrontWall_Lintel", PrimitiveType.Cube, new Vector3(0f, doorHeight + ((height - doorHeight) * 0.5f), frontZ), new Vector3(doorWidth, height - doorHeight, wallThickness), Vector3.zero, materials.Wall, true); // 출입구 상단 벽 생성
            BuildRoomStructuralDetails(roomRoot.transform, width, depth, height, materials); // 방 모서리 보강재와 케이블 덕트 생성
            PowerConsumer lightConsumer = BuildElectricRoomLight(roomRoot.transform, height, displayName, materials); // 방 중앙 전기등과 공통 전력 소비자 생성
            PoweredIronDoor door = BuildPoweredIronDoor(roomRoot.transform, new Vector3(0f, 0f, frontZ - 0.12f), doorWidth, doorHeight, displayName, materials); // 방 전면 상세 철제문 생성
            PowerConsumer doorConsumer = door.PowerConsumer; // 철제문 공통 전력 소비자 조회
            RoomPowerZone roomZone = roomRoot.AddComponent<RoomPowerZone>(); // 방 단위 전력 구역 기능 추가
            roomZone.Configure(displayName, true, new[] { lightConsumer, doorConsumer }); // 방 전등과 문을 하나의 전력 구역으로 연결
            BuildLocalDoorPanel(roomRoot.transform, new Vector3(-1.55f, 1.35f, frontZ - 0.27f), door, displayName, materials); // 문 옆 OPEN/CLOSE 로컬 제어반 생성
            CreateRoomNumberPlate(roomRoot.transform, new Vector3(0f, doorHeight + 0.55f, frontZ - 0.18f), displayName, materials); // 철제문 위 방 번호판 생성
            return new RoomBuildResult(roomZone, lightConsumer.GetComponent<ElectricLightController>(), door); // 배전반 연결용 방 결과 반환
        }

        private static void BuildRoomStructuralDetails(Transform parent, float width, float depth, float height, Day12Materials materials) // 방 내부 구조 세부 요소 생성
        {
            float beamY = height - 0.18f; // 천장 보강 빔 높이 계산
            CreateVisualPrimitive(parent, "CeilingBeam_Front", PrimitiveType.Cube, new Vector3(0f, beamY, -(depth * 0.30f)), new Vector3(width - 0.35f, 0.18f, 0.18f), Vector3.zero, materials.Trim, false); // 전면 천장 보강 빔 생성
            CreateVisualPrimitive(parent, "CeilingBeam_Back", PrimitiveType.Cube, new Vector3(0f, beamY, depth * 0.30f), new Vector3(width - 0.35f, 0.18f, 0.18f), Vector3.zero, materials.Trim, false); // 후면 천장 보강 빔 생성
            CreateVisualPrimitive(parent, "CableTray", PrimitiveType.Cube, new Vector3(-(width * 0.33f), height - 0.38f, 0f), new Vector3(0.22f, 0.12f, depth - 0.5f), Vector3.zero, materials.DarkMetal, false); // 천장 전선 덕트 생성
            CreateVisualPrimitive(parent, "Cable_Line_A", PrimitiveType.Cube, new Vector3(-(width * 0.33f) - 0.05f, height - 0.47f, 0f), new Vector3(0.035f, 0.035f, depth - 0.65f), Vector3.zero, materials.Cable, false); // 첫 번째 노출 케이블 생성
            CreateVisualPrimitive(parent, "Cable_Line_B", PrimitiveType.Cube, new Vector3(-(width * 0.33f) + 0.05f, height - 0.47f, 0f), new Vector3(0.035f, 0.035f, depth - 0.65f), Vector3.zero, materials.Cable, false); // 두 번째 노출 케이블 생성

            for (int corner = 0; corner < 4; corner++) // 네 모서리 보강 기둥 생성 반복
            {
                float x = corner % 2 == 0 ? -(width * 0.5f) + 0.10f : (width * 0.5f) - 0.10f; // 모서리 X 위치 계산
                float z = corner < 2 ? -(depth * 0.5f) + 0.10f : (depth * 0.5f) - 0.10f; // 모서리 Z 위치 계산
                CreateVisualPrimitive(parent, $"CornerBrace_{corner + 1:00}", PrimitiveType.Cube, new Vector3(x, height * 0.5f, z), new Vector3(0.16f, height - 0.25f, 0.16f), Vector3.zero, materials.Trim, false); // 모서리 금속 보강재 생성
            }
        }

        private static PowerConsumer BuildElectricRoomLight(Transform parent, float roomHeight, string roomName, Day12Materials materials) // 방 천장 상세 전기등 생성
        {
            GameObject root = new GameObject("PoweredCeilingLight"); // 방 전기등 루트 생성
            root.transform.SetParent(parent, false); // 방 루트 아래 전기등 연결
            root.transform.localPosition = new Vector3(0f, roomHeight - 0.35f, 0.55f); // 방 중앙 천장 아래 전등 위치 지정
            CreateVisualPrimitive(root.transform, "MountPlate", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.62f, 0.08f, 0.62f), Vector3.zero, materials.DarkMetal, false); // 천장 고정 금속판 생성
            CreateVisualPrimitive(root.transform, "Housing", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.56f, 0.14f, 0.56f), Vector3.zero, materials.LampHousing, false); // 전등 금속 하우징 생성
            GameObject offGlass = CreateVisualPrimitive(root.transform, "Glass_Off", PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(0.46f, 0.07f, 0.46f), Vector3.zero, materials.LampOff, false); // 소등 유리 생성
            GameObject glowGlass = CreateVisualPrimitive(root.transform, "Glass_Glow", PrimitiveType.Cylinder, new Vector3(0f, -0.13f, 0f), new Vector3(0.44f, 0.075f, 0.44f), Vector3.zero, materials.LampOn, false); // 점등 발광 유리 생성
            BuildLampGuard(root.transform, materials); // 전등 보호 철망과 볼트 생성
            GameObject lightOrigin = new GameObject("LightOrigin"); // 실제 Unity Light 시작점 생성
            lightOrigin.transform.SetParent(root.transform, false); // 전등 루트 아래 광원 연결
            lightOrigin.transform.localPosition = new Vector3(0f, -0.30f, 0f); // 발광 유리 아래 광원 위치 지정
            Light visualLight = lightOrigin.AddComponent<Light>(); // 실제 화면용 Point Light 추가
            visualLight.type = LightType.Point; // 방 전 방향 조명 설정
            visualLight.range = 7.2f; // 한 방 중심 조명 영향 거리 설정
            visualLight.intensity = 4.2f; // 방 내부 화면 밝기 설정
            visualLight.color = new Color(1f, 0.84f, 0.60f); // 오래된 산업용 백열등 색 적용
            visualLight.shadows = LightShadows.Soft; // 부드러운 실시간 그림자 적용
            BrightnessSource brightnessSource = root.AddComponent<BrightnessSource>(); // 게임 판정용 고정 밝기 광원 추가
            brightnessSource.Configure(0.30f, 7.2f, false, visualLight, BrightnessSourceType.Fixed, BrightnessEmissionShape.Omnidirectional, 52f); // 게임용 밝기와 시작 소등 상태 설정
            ElectricLightController lightController = root.AddComponent<ElectricLightController>(); // 전기등 전력 반응 기능 추가
            lightController.Configure($"{roomName} 천장등", false, new[] { brightnessSource }, new[] { glowGlass }, new[] { offGlass }); // 점등·소등 시각 요소 연결
            PowerConsumer consumer = root.AddComponent<PowerConsumer>(); // 방 공통 전력 소비자 추가
            consumer.Configure($"{roomName} LIGHT", false); // 방 전력에서 시작 정전 상태로 연결
            return consumer; // 방 전력 구역 연결용 소비자 반환
        }

        private static void BuildLampGuard(Transform parent, Day12Materials materials) // 산업용 전등 보호 가드 세부 모델 생성
        {
            float guardY = -0.24f; // 보호 가드 중심 높이 정의
            CreateVisualPrimitive(parent, "GuardRing", PrimitiveType.Cylinder, new Vector3(0f, guardY, 0f), new Vector3(0.58f, 0.025f, 0.58f), Vector3.zero, materials.Trim, false); // 전등 아래 보호 링 생성
            CreateVisualPrimitive(parent, "GuardBar_X1", PrimitiveType.Cube, new Vector3(-0.32f, guardY + 0.10f, 0f), new Vector3(0.035f, 0.30f, 0.035f), Vector3.zero, materials.Trim, false); // 첫 번째 보호 세로봉 생성
            CreateVisualPrimitive(parent, "GuardBar_X2", PrimitiveType.Cube, new Vector3(0.32f, guardY + 0.10f, 0f), new Vector3(0.035f, 0.30f, 0.035f), Vector3.zero, materials.Trim, false); // 두 번째 보호 세로봉 생성
            CreateVisualPrimitive(parent, "GuardBar_Z1", PrimitiveType.Cube, new Vector3(0f, guardY + 0.10f, -0.32f), new Vector3(0.035f, 0.30f, 0.035f), Vector3.zero, materials.Trim, false); // 세 번째 보호 세로봉 생성
            CreateVisualPrimitive(parent, "GuardBar_Z2", PrimitiveType.Cube, new Vector3(0f, guardY + 0.10f, 0.32f), new Vector3(0.035f, 0.30f, 0.035f), Vector3.zero, materials.Trim, false); // 네 번째 보호 세로봉 생성

            for (int bolt = 0; bolt < 4; bolt++) // 하우징 고정 볼트 4개 생성 반복
            {
                float angle = bolt * 90f * Mathf.Deg2Rad; // 볼트 원형 배치 각도 계산
                Vector3 position = new Vector3(Mathf.Cos(angle) * 0.42f, 0.14f, Mathf.Sin(angle) * 0.42f); // 하우징 가장자리 볼트 위치 계산
                CreateVisualPrimitive(parent, $"HousingBolt_{bolt + 1:00}", PrimitiveType.Sphere, position, new Vector3(0.055f, 0.035f, 0.055f), Vector3.zero, materials.Bolt, false); // 금속 볼트 머리 생성
            }
        }

        private static PoweredIronDoor BuildPoweredIronDoor(Transform parent, Vector3 localPosition, float doorWidth, float doorHeight, string roomName, Day12Materials materials) // 상세 전동 철제문 생성
        {
            GameObject root = new GameObject("PoweredIronDoor"); // 전동 철제문 루트 생성
            root.transform.SetParent(parent, false); // 방 루트 아래 철제문 연결
            root.transform.localPosition = localPosition; // 방 전면 출입구 위치 적용
            float frameThickness = 0.18f; // 철제문 프레임 두께 정의
            CreateVisualPrimitive(root.transform, "Frame_Left", PrimitiveType.Cube, new Vector3(-(doorWidth * 0.5f) - 0.14f, doorHeight * 0.5f, 0f), new Vector3(frameThickness, doorHeight + 0.35f, 0.32f), Vector3.zero, materials.DoorFrame, true); // 왼쪽 문틀 생성
            CreateVisualPrimitive(root.transform, "Frame_Right", PrimitiveType.Cube, new Vector3((doorWidth * 0.5f) + 0.14f, doorHeight * 0.5f, 0f), new Vector3(frameThickness, doorHeight + 0.35f, 0.32f), Vector3.zero, materials.DoorFrame, true); // 오른쪽 문틀 생성
            CreateVisualPrimitive(root.transform, "Frame_Top", PrimitiveType.Cube, new Vector3(0f, doorHeight + 0.13f, 0f), new Vector3(doorWidth + 0.48f, 0.22f, 0.34f), Vector3.zero, materials.DoorFrame, true); // 상단 문틀 생성
            CreateVisualPrimitive(root.transform, "SlideRail", PrimitiveType.Cube, new Vector3((doorWidth * 0.65f), doorHeight + 0.38f, 0.02f), new Vector3(doorWidth * 2.2f, 0.14f, 0.20f), Vector3.zero, materials.DarkMetal, false); // 문 슬라이딩 상부 레일 생성
            CreateVisualPrimitive(root.transform, "MotorHousing", PrimitiveType.Cube, new Vector3((doorWidth * 0.80f), doorHeight + 0.61f, 0.04f), new Vector3(0.85f, 0.42f, 0.46f), Vector3.zero, materials.DarkMetal, false); // 전동 모터 하우징 생성
            CreateVisualPrimitive(root.transform, "MotorCap", PrimitiveType.Cylinder, new Vector3((doorWidth * 0.80f), doorHeight + 0.61f, -0.26f), new Vector3(0.25f, 0.30f, 0.25f), new Vector3(90f, 0f, 0f), materials.Trim, false); // 모터 원통형 캡 생성
            GameObject movingPanel = new GameObject("MovingPanel"); // 실제 이동할 철제문 패널 루트 생성
            movingPanel.transform.SetParent(root.transform, false); // 철제문 루트 아래 이동 패널 연결
            Vector3 closedPosition = new Vector3(0f, doorHeight * 0.5f, 0f); // 완전 닫힘 패널 로컬 위치 계산
            Vector3 openPosition = new Vector3(doorWidth + 0.34f, doorHeight * 0.5f, 0f); // 오른쪽 벽 뒤로 이동한 완전 열림 위치 계산
            movingPanel.transform.localPosition = closedPosition; // 시작 닫힘 위치 적용
            CreateVisualPrimitive(movingPanel.transform, "SteelSlab", PrimitiveType.Cube, Vector3.zero, new Vector3(doorWidth - 0.10f, doorHeight - 0.08f, 0.22f), Vector3.zero, materials.Door, true); // 두꺼운 철제문 본판 생성
            CreateDoorPanelDetails(movingPanel.transform, doorWidth, doorHeight, materials); // 보강대·리벳·손잡이·경고 띠 생성
            PowerConsumer consumer = root.AddComponent<PowerConsumer>(); // 철제문 공통 전력 소비자 추가
            PoweredIronDoor door = root.AddComponent<PoweredIronDoor>(); // 전동 철제문 이동 제어 기능 추가
            consumer.Configure($"{roomName} DOOR", false); // 시작 정전 상태 전력 소비자 설정
            door.Configure($"{roomName} 철제문", consumer, movingPanel.transform, closedPosition, openPosition, 2.25f, PoweredIronDoorState.Closed, null, null, null, null); // 닫힘 시작 상태와 슬라이딩 이동 범위 설정
            return door; // 방 전력과 배전반 연결용 철제문 반환
        }

        private static void CreateDoorPanelDetails(Transform movingPanel, float doorWidth, float doorHeight, Day12Materials materials) // 철제문 패널의 산업용 세부 모델 생성
        {
            CreateVisualPrimitive(movingPanel, "Brace_Top", PrimitiveType.Cube, new Vector3(0f, doorHeight * 0.28f, -0.14f), new Vector3(doorWidth - 0.28f, 0.16f, 0.08f), Vector3.zero, materials.Trim, false); // 상단 수평 보강대 생성
            CreateVisualPrimitive(movingPanel, "Brace_Middle", PrimitiveType.Cube, new Vector3(0f, 0f, -0.14f), new Vector3(doorWidth - 0.28f, 0.16f, 0.08f), Vector3.zero, materials.Trim, false); // 중앙 수평 보강대 생성
            CreateVisualPrimitive(movingPanel, "Brace_Bottom", PrimitiveType.Cube, new Vector3(0f, -(doorHeight * 0.28f), -0.14f), new Vector3(doorWidth - 0.28f, 0.16f, 0.08f), Vector3.zero, materials.Trim, false); // 하단 수평 보강대 생성
            CreateVisualPrimitive(movingPanel, "Brace_Left", PrimitiveType.Cube, new Vector3(-(doorWidth * 0.32f), 0f, -0.14f), new Vector3(0.14f, doorHeight - 0.35f, 0.08f), Vector3.zero, materials.Trim, false); // 왼쪽 세로 보강대 생성
            CreateVisualPrimitive(movingPanel, "Brace_Right", PrimitiveType.Cube, new Vector3(doorWidth * 0.32f, 0f, -0.14f), new Vector3(0.14f, doorHeight - 0.35f, 0.08f), Vector3.zero, materials.Trim, false); // 오른쪽 세로 보강대 생성
            CreateVisualPrimitive(movingPanel, "WarningStripe_A", PrimitiveType.Cube, new Vector3(-0.34f, -(doorHeight * 0.36f), -0.19f), new Vector3(0.20f, 0.52f, 0.04f), new Vector3(0f, 0f, -28f), materials.Warning, false); // 첫 번째 경고 띠 생성
            CreateVisualPrimitive(movingPanel, "WarningStripe_B", PrimitiveType.Cube, new Vector3(0f, -(doorHeight * 0.36f), -0.19f), new Vector3(0.20f, 0.52f, 0.04f), new Vector3(0f, 0f, -28f), materials.Warning, false); // 두 번째 경고 띠 생성
            CreateVisualPrimitive(movingPanel, "WarningStripe_C", PrimitiveType.Cube, new Vector3(0.34f, -(doorHeight * 0.36f), -0.19f), new Vector3(0.20f, 0.52f, 0.04f), new Vector3(0f, 0f, -28f), materials.Warning, false); // 세 번째 경고 띠 생성
            CreateVisualPrimitive(movingPanel, "Handle", PrimitiveType.Cube, new Vector3(-(doorWidth * 0.28f), 0f, -0.25f), new Vector3(0.12f, 0.62f, 0.12f), Vector3.zero, materials.DarkMetal, false); // 비상 수동 손잡이 생성

            for (int row = 0; row < 4; row++) // 철제문 리벳 행 반복
            {
                for (int column = 0; column < 2; column++) // 철제문 리벳 열 반복
                {
                    float x = column == 0 ? -(doorWidth * 0.40f) : doorWidth * 0.40f; // 좌우 리벳 X 위치 계산
                    float y = Mathf.Lerp(-(doorHeight * 0.40f), doorHeight * 0.40f, row / 3f); // 세로 리벳 Y 위치 계산
                    CreateVisualPrimitive(movingPanel, $"Rivet_{row}_{column}", PrimitiveType.Sphere, new Vector3(x, y, -0.20f), new Vector3(0.055f, 0.055f, 0.035f), Vector3.zero, materials.Bolt, false); // 철제문 고정 리벳 생성
                }
            }
        }

        private static void BuildLocalDoorPanel(Transform parent, Vector3 localPosition, PoweredIronDoor door, string roomName, Day12Materials materials) // 철제문 옆 단일 토글 조작 패널 생성
        {
            GameObject panel = new GameObject("LocalDoorControl"); // 문 옆 제어반 루트 생성
            panel.transform.SetParent(parent, false); // 방 루트 아래 로컬 제어반 연결
            panel.transform.localPosition = localPosition; // 철제문 왼쪽 복도 측 위치 적용
            CreateVisualPrimitive(panel.transform, "Cabinet", PrimitiveType.Cube, Vector3.zero, new Vector3(0.58f, 0.82f, 0.18f), Vector3.zero, materials.Panel, true); // 벽면형 축소 로컬 제어반 금속 케이스 생성
            CreateText(panel.transform, "DOOR", new Vector3(0f, 0.27f, -0.105f), new Vector3(0f, 180f, 0f), 0.085f, Color.white); // 로컬 제어반 DOOR 라벨 생성
            GameObject openLamp = CreateIndicator(panel.transform, "OpenLamp", new Vector3(-0.16f, 0.06f, -0.11f), materials.GreenGlow, 0.72f); // 문 열림 녹색 상태등 생성
            GameObject closedLamp = CreateIndicator(panel.transform, "ClosedLamp", new Vector3(0.02f, 0.06f, -0.11f), materials.RedGlow, 0.72f); // 문 닫힘 빨간 상태등 생성
            GameObject movingLamp = CreateIndicator(panel.transform, "MovingLamp", new Vector3(0.20f, 0.06f, -0.11f), materials.AmberGlow, 0.72f); // 문 이동 노란 상태등 생성
            GameObject noPowerLamp = CreateIndicator(panel.transform, "NoPowerLamp", new Vector3(0f, -0.10f, -0.11f), materials.DimRed, 0.72f); // 문 정전 어두운 빨간 상태등 생성
            DistributionBoardButton toggleSwitch = CreateToggleSwitch(panel.transform, "DOOR_TOGGLE", new Vector3(0f, -0.28f, -0.12f), materials); // OPEN/CLOSE 공용 단일 토글 스위치 생성
            toggleSwitch.Configure($"{roomName} 철제문", DistributionBoardButtonAction.DoorToggle, null, null, door, toggleSwitch.transform.Find("SwitchLever")); // 단일 로컬 스위치를 철제문에 연결
            Transform movingPanel = door.transform.Find("MovingPanel"); // 기존 철제문 이동 패널 조회
            Vector3 closedPosition = new Vector3(0f, movingPanel.localPosition.y, 0f); // 철제문 닫힘 위치 재구성
            Vector3 openPosition = new Vector3(2.24f, movingPanel.localPosition.y, 0f); // 철제문 열림 위치 재구성
            door.Configure(door.DisplayName, door.PowerConsumer, movingPanel, closedPosition, openPosition, 2.25f, PoweredIronDoorState.Closed, new[] { openLamp }, new[] { closedLamp }, new[] { movingLamp }, new[] { noPowerLamp }); // 로컬 상태등을 기존 철제문 설정에 추가 연결
        }

        private static void CreateRoomNumberPlate(Transform parent, Vector3 localPosition, string roomName, Day12Materials materials) // 방 출입구 상단 번호판 생성
        {
            CreateVisualPrimitive(parent, "RoomNamePlate", PrimitiveType.Cube, localPosition, new Vector3(2.25f, 0.32f, 0.07f), Vector3.zero, materials.Plate, false); // 축소된 검은 금속 방 이름판 생성
            CreateText(parent, roomName, localPosition + new Vector3(0f, 0f, -0.055f), new Vector3(0f, 180f, 0f), 0.075f, Color.white); // 이름판 앞 방 명칭 텍스트 생성
        }

        private static void BuildDistributionBoard(Transform parent, Bounds bounds, Day12Materials materials, GeneratorController generator, RoomBuildResult[] rooms) // 벽면형 소형 중앙 배전반과 단일 토글 스위치 생성
        {
            GameObject root = new GameObject("MainDistributionBoard"); // 중앙 배전반 루트 생성
            root.transform.SetParent(parent); // Day 12 시험 루트 아래 배전반 연결
            root.transform.position = new Vector3(bounds.center.x - 3.1f, bounds.min.y + 0.18f, bounds.min.z - 0.14f); // Indoor 경계보다 실제 남쪽 외벽 쪽으로 붙인 벽걸이 위치 지정
            MainDistributionBoardController board = root.AddComponent<MainDistributionBoardController>(); // 중앙 배전반 전력 관리 기능 추가
            BuildBoardCabinet(root.transform, materials); // 축소 배전반 캐비닛·프레임·볼트 생성
            GameObject mainOnLamp = CreateIndicator(root.transform, "Main_ON_Lamp", new Vector3(0.05f, 2.78f, 0.22f), materials.GreenGlow, 0.82f); // 메인 통전 녹색 상태등 생성
            GameObject mainOffLamp = CreateIndicator(root.transform, "Main_OFF_Lamp", new Vector3(0.31f, 2.78f, 0.22f), materials.RedGlow, 0.82f); // 메인 정전 빨간 상태등 생성
            CreateText(root.transform, "MAIN POWER", new Vector3(-1.00f, 2.78f, 0.24f), Vector3.zero, 0.095f, Color.white); // 메인 전원 라벨 생성
            DistributionBoardButton mainSwitch = CreateToggleSwitch(root.transform, "MAIN_TOGGLE", new Vector3(1.22f, 2.78f, 0.23f), materials); // 메인 전원 단일 토글 스위치 생성
            mainSwitch.Configure("시설 메인 전원", DistributionBoardButtonAction.MainPowerToggle, board, null, null, mainSwitch.transform.Find("SwitchLever")); // 메인 전원 토글 기능 연결
            GameObject[] roomOnIndicators = new GameObject[rooms.Length]; // 방별 녹색 통전 표시 배열 생성
            GameObject[] roomOffIndicators = new GameObject[rooms.Length]; // 방별 빨간 정전 표시 배열 생성
            GameObject[] doorOpenIndicators = new GameObject[rooms.Length]; // 철제문별 열림 표시 배열 생성
            GameObject[] doorClosedIndicators = new GameObject[rooms.Length]; // 철제문별 닫힘 표시 배열 생성
            GameObject[] doorMovingIndicators = new GameObject[rooms.Length]; // 철제문별 이동 중 상태 표시 배열 생성
            GameObject[] doorNoPowerIndicators = new GameObject[rooms.Length]; // 철제문별 정전 상태 표시 배열 생성
            PoweredIronDoor[] doors = new PoweredIronDoor[rooms.Length]; // 배전반 원격 제어 철제문 배열 생성
            RoomPowerZone[] zones = new RoomPowerZone[rooms.Length]; // 배전반 방 전력 구역 배열 생성

            for (int index = 0; index < rooms.Length; index++) // 방별 단일 전원 스위치 행 생성 반복
            {
                float rowY = 2.12f - (index * 0.31f); // 축소 배전반의 방 전원 행 세로 위치 계산
                zones[index] = rooms[index].Zone; // 현재 방 전력 구역 저장
                doors[index] = rooms[index].Door; // 현재 방 철제문 저장
                CreateText(root.transform, $"ROOM {index + 1:00}", new Vector3(-1.05f, rowY, 0.24f), Vector3.zero, 0.082f, Color.white); // 방 번호 라벨 생성
                roomOnIndicators[index] = CreateIndicator(root.transform, $"Room{index + 1:00}_ON_Lamp", new Vector3(0.05f, rowY, 0.22f), materials.GreenGlow, 0.74f); // 방 통전 녹색 상태등 생성
                roomOffIndicators[index] = CreateIndicator(root.transform, $"Room{index + 1:00}_OFF_Lamp", new Vector3(0.31f, rowY, 0.22f), materials.RedGlow, 0.74f); // 방 정전 빨간 상태등 생성
                DistributionBoardButton roomSwitch = CreateToggleSwitch(root.transform, $"ROOM{index + 1:00}_TOGGLE", new Vector3(1.22f, rowY, 0.23f), materials); // 방 ON/OFF 공용 단일 토글 스위치 생성
                roomSwitch.Configure($"ROOM {index + 1:00} 전원", DistributionBoardButtonAction.RoomPowerToggle, board, zones[index], null, roomSwitch.transform.Find("SwitchLever")); // 방 전원 토글 기능 연결
            }

            for (int index = 0; index < rooms.Length; index++) // 철제문 단일 원격 스위치 행 생성 반복
            {
                float rowY = 0.92f - (index * 0.31f); // 축소 배전반의 문 제어 행 세로 위치 계산
                CreateText(root.transform, $"DOOR {index + 1:00}", new Vector3(-1.05f, rowY, 0.24f), Vector3.zero, 0.082f, Color.white); // 문 번호 라벨 생성
                doorOpenIndicators[index] = CreateIndicator(root.transform, $"Door{index + 1:00}_OpenLamp", new Vector3(-0.08f, rowY, 0.22f), materials.GreenGlow, 0.68f); // 문 열림 녹색 표시등 생성
                doorClosedIndicators[index] = CreateIndicator(root.transform, $"Door{index + 1:00}_ClosedLamp", new Vector3(0.14f, rowY, 0.22f), materials.RedGlow, 0.68f); // 문 닫힘 빨간 표시등 생성
                doorMovingIndicators[index] = CreateIndicator(root.transform, $"Door{index + 1:00}_MovingLamp", new Vector3(0.36f, rowY, 0.22f), materials.AmberGlow, 0.68f); // 문 이동 노란 표시등 생성
                doorNoPowerIndicators[index] = CreateIndicator(root.transform, $"Door{index + 1:00}_NoPowerLamp", new Vector3(0.58f, rowY, 0.22f), materials.DimRed, 0.68f); // 문 정전 상태등 생성
                DistributionBoardButton doorSwitch = CreateToggleSwitch(root.transform, $"DOOR{index + 1:00}_TOGGLE", new Vector3(1.22f, rowY, 0.23f), materials); // 문 OPEN/CLOSE 공용 단일 토글 스위치 생성
                doorSwitch.Configure($"DOOR {index + 1:00}", DistributionBoardButtonAction.DoorToggle, board, zones[index], doors[index], doorSwitch.transform.Find("SwitchLever")); // 원격 문 토글 기능 연결
            }

            board.Configure(generator, true, zones, doors, new[] { mainOnLamp }, new[] { mainOffLamp }, roomOnIndicators, roomOffIndicators, doorOpenIndicators, doorClosedIndicators, doorMovingIndicators, doorNoPowerIndicators); // 발전기·방·문·상태등 전체를 중앙 배전반에 연결
            EditorUtility.SetDirty(board); // 배전반 직렬화 상태 저장 대상으로 표시
        }

        private static void BuildBoardCabinet(Transform parent, Day12Materials materials) // 벽걸이형 소형 산업용 중앙 배전반 캐비닛 모델 생성
        {
            CreateVisualPrimitive(parent, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 1.65f, 0f), new Vector3(3.65f, 3.30f, 0.34f), Vector3.zero, materials.Panel, true); // 벽면에 붙는 축소 메인 철제 캐비닛 생성
            CreateVisualPrimitive(parent, "FrontPlate", PrimitiveType.Cube, new Vector3(0f, 1.65f, 0.19f), new Vector3(3.40f, 3.05f, 0.055f), Vector3.zero, materials.PanelFront, false); // 축소 앞면 제어판 플레이트 생성
            CreateVisualPrimitive(parent, "HeaderPlate", PrimitiveType.Cube, new Vector3(0f, 3.08f, 0.23f), new Vector3(3.28f, 0.32f, 0.055f), Vector3.zero, materials.Plate, false); // 상단 시설 제어 명판 생성
            CreateText(parent, "FACILITY POWER CONTROL", new Vector3(0f, 3.08f, 0.27f), Vector3.zero, 0.095f, Color.white); // 상단 시설 제어 명칭 생성
            CreateVisualPrimitive(parent, "Divider_Main", PrimitiveType.Cube, new Vector3(0f, 2.48f, 0.23f), new Vector3(3.20f, 0.040f, 0.045f), Vector3.zero, materials.Trim, false); // 메인과 방 전원 영역 구분선 생성
            CreateText(parent, "ROOM POWER", new Vector3(-0.92f, 2.34f, 0.25f), Vector3.zero, 0.068f, new Color(0.75f, 0.78f, 0.72f)); // 방 전원 소제목 생성
            CreateText(parent, "SWITCH", new Vector3(1.22f, 2.34f, 0.25f), Vector3.zero, 0.060f, new Color(0.82f, 0.72f, 0.42f)); // 방 단일 스위치 열 제목 생성
            CreateVisualPrimitive(parent, "Divider_Door", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0.23f), new Vector3(3.20f, 0.040f, 0.045f), Vector3.zero, materials.Trim, false); // 방 전원과 문 제어 영역 구분선 생성
            CreateText(parent, "SECURITY DOORS", new Vector3(-0.82f, 1.12f, 0.25f), Vector3.zero, 0.068f, new Color(0.75f, 0.78f, 0.72f)); // 철제문 제어 소제목 생성
            CreateText(parent, "TOGGLE", new Vector3(1.22f, 1.12f, 0.25f), Vector3.zero, 0.060f, new Color(0.82f, 0.72f, 0.42f)); // 문 단일 토글 열 제목 생성
            BuildBoardVents(parent, materials); // 배전반 측면형 통풍구 생성

            for (int bolt = 0; bolt < 8; bolt++) // 배전반 전면 고정 볼트 생성 반복
            {
                float x = bolt % 2 == 0 ? -1.68f : 1.68f; // 축소 캐비닛 좌우 볼트 X 위치 계산
                float y = 0.18f + ((bolt / 2) * 0.96f); // 축소 캐비닛 볼트 세로 위치 계산
                CreateVisualPrimitive(parent, $"CabinetBolt_{bolt + 1:00}", PrimitiveType.Sphere, new Vector3(x, y, 0.235f), new Vector3(0.050f, 0.050f, 0.030f), Vector3.zero, materials.Bolt, false); // 캐비닛 전면 볼트 생성
            }
        }

        private static void BuildBoardVents(Transform parent, Day12Materials materials) // 축소 배전반 하단 환기 슬롯 생성
        {
            for (int index = 0; index < 5; index++) // 환기 슬롯 5개 생성 반복
            {
                float x = -0.56f + (index * 0.28f); // 환기 슬롯 가로 위치 계산
                CreateVisualPrimitive(parent, $"Vent_{index + 1:00}", PrimitiveType.Cube, new Vector3(x, 0.08f, 0.225f), new Vector3(0.18f, 0.032f, 0.036f), Vector3.zero, materials.DarkMetal, false); // 하단 검은 환기 슬롯 생성
            }
        }

        private static void BuildCorridorDetail(Transform parent, Bounds bounds, Day12Materials materials) // 벽면 방 앞 복도와 발전기 배선 세부 요소 생성
        {
            float floorY = bounds.min.y + 0.15f; // 기존 바닥 위 복도 표시 높이 계산
            float roomDepth = Mathf.Min(7.2f, bounds.size.z * 0.42f); // 방 생성과 같은 깊이 계산
            float roomFrontZ = bounds.max.z + 0.22f - roomDepth; // 북쪽 외벽에 붙은 방들의 전면 출입구 Z 위치 계산
            CreateVisualPrimitive(parent, "ServiceLane", PrimitiveType.Cube, new Vector3(bounds.center.x, floorY, roomFrontZ - 0.65f), new Vector3(bounds.size.x - 3.0f, 0.025f, 0.10f), Vector3.zero, materials.Warning, false, true); // 세 방 출입구 앞을 따라 이어지는 경고 안내선 생성
            float generatorX = bounds.min.x + 3.0f; // 발전기 X 위치 재계산
            float boardX = bounds.center.x - 3.1f; // 벽면 배전반 X 위치 재계산
            float cableCenterX = (generatorX + boardX) * 0.5f; // 발전기와 배전반 사이 가로 배선 중심 계산
            float cableWidth = Mathf.Abs(boardX - generatorX); // 발전기와 배전반 사이 가로 배선 길이 계산
            CreateVisualPrimitive(parent, "GeneratorCableTray_Vertical", PrimitiveType.Cube, new Vector3(generatorX, floorY + 0.04f, bounds.min.z + 1.45f), new Vector3(0.24f, 0.07f, 2.85f), Vector3.zero, materials.DarkMetal, false, true); // 발전기에서 남쪽 벽으로 내려오는 바닥 케이블 덕트 생성
            CreateVisualPrimitive(parent, "GeneratorCableTray_Wall", PrimitiveType.Cube, new Vector3(cableCenterX, floorY + 0.04f, bounds.min.z + 0.03f), new Vector3(cableWidth, 0.07f, 0.24f), Vector3.zero, materials.DarkMetal, false, true); // 남쪽 벽을 따라 배전반까지 이어지는 바닥 케이블 덕트 생성
        }

        private static DistributionBoardButton CreateToggleSwitch(Transform parent, string name, Vector3 localPosition, Day12Materials materials) // 직접 F 상호작용 가능한 단일 토글 스위치 생성
        {
            GameObject switchRoot = new GameObject(name); // 토글 스위치 루트 생성
            switchRoot.transform.SetParent(parent, false); // 대상 제어반 아래 스위치 연결
            switchRoot.transform.localPosition = localPosition; // 제어반 기준 스위치 위치 적용
            BoxCollider collider = switchRoot.AddComponent<BoxCollider>(); // F Raycast용 스위치 상호작용 Collider 추가
            collider.center = new Vector3(0f, 0f, 0.055f); // 스위치 전면에 상호작용 중심 배치
            collider.size = new Vector3(0.46f, 0.36f, 0.24f); // 한 번에 조준하기 쉬운 단일 스위치 영역 설정
            CreateVisualPrimitive(switchRoot.transform, "SwitchBase", PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.30f, 0.105f), Vector3.zero, materials.DarkMetal, false); // 검은 스위치 베이스 생성
            CreateVisualPrimitive(switchRoot.transform, "SwitchPlate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.07f), new Vector3(0.30f, 0.24f, 0.045f), Vector3.zero, materials.Plate, false); // 안쪽 금속 스위치 플레이트 생성
            GameObject lever = CreateVisualPrimitive(switchRoot.transform, "SwitchLever", PrimitiveType.Cube, new Vector3(0f, 0f, 0.15f), new Vector3(0.085f, 0.30f, 0.075f), Vector3.zero, materials.Warning, false); // 현재 상태에 따라 기울어지는 황색 레버 생성
            CreateVisualPrimitive(lever.transform, "LeverCap", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0f), new Vector3(0.095f, 0.095f, 0.075f), Vector3.zero, materials.Bolt, false); // 레버 끝 금속 손잡이 생성
            DistributionBoardButton button = switchRoot.AddComponent<DistributionBoardButton>(); // 공통 토글 상호작용 기능 추가
            return button; // 기능 연결을 위한 토글 스위치 반환
        }

        private static GameObject CreateIndicator(Transform parent, string name, Vector3 localPosition, Material material, float scale = 1f) // 배전반 상태 표시 전구 생성
        {
            GameObject indicatorRoot = new GameObject(name); // 상태등 전체 활성화용 루트 생성
            indicatorRoot.transform.SetParent(parent, false); // 대상 제어반 아래 상태등 루트 연결
            indicatorRoot.transform.localPosition = localPosition; // 상태등 기준 위치 적용
            CreateVisualPrimitive(indicatorRoot.transform, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.015f), new Vector3(0.15f * scale, 0.045f * scale, 0.15f * scale), new Vector3(90f, 0f, 0f), material, false); // 상태등 크기에 맞춘 테두리 생성
            CreateVisualPrimitive(indicatorRoot.transform, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.055f * scale), new Vector3(0.10f * scale, 0.10f * scale, 0.06f * scale), Vector3.zero, material, false); // 상태등 크기에 맞춘 발광 렌즈 생성
            return indicatorRoot; // 상태 전환 대상 전체 상태등 루트 반환
        }

        private static TextMesh CreateText(Transform parent, string text, Vector3 localPosition, Vector3 localEulerAngles, float characterSize, Color color) // 3D 장비 라벨 텍스트 생성
        {
            GameObject textObject = new GameObject($"Label_{text.Replace(' ', '_')}"); // 장비 라벨 오브젝트 생성
            textObject.transform.SetParent(parent, false); // 대상 장비 아래 라벨 연결
            textObject.transform.localPosition = localPosition; // 라벨 로컬 위치 적용
            textObject.transform.localEulerAngles = localEulerAngles + new Vector3(0f, 180f, 0f); // 모든 라벨을 정면 기준 반대 방향으로 뒤집어 거꾸로 보이는 문제 수정
            TextMesh textMesh = textObject.AddComponent<TextMesh>(); // 기본 3D TextMesh 추가
            textMesh.text = text; // 표시 문자열 저장
            textMesh.fontSize = 64; // 고해상도 기본 폰트 크기 설정
            textMesh.characterSize = characterSize * LabelScale; // 요청에 맞춰 모든 Day 12 라벨을 약 절반 크기로 축소
            textMesh.anchor = TextAnchor.MiddleCenter; // 라벨 중심 정렬 설정
            textMesh.alignment = TextAlignment.Center; // 라벨 문자 중앙 정렬 설정
            textMesh.color = color; // 장비 라벨 색상 설정
            return textMesh; // 생성된 라벨 반환
        }

        private static Transform FindNamedDescendant(Transform root, string objectName) // 이름으로 기존 테스트 오브젝트 자식 검색
        {
            if (root == null) // 검색 루트 존재 여부 확인
            {
                return null; // 루트가 없으면 검색 실패 반환
            }

            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(target => target != null && target.name == objectName); // 비활성 자식까지 포함해 첫 일치 Transform 반환
        }

        private static GameObject CreateVisualPrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Material material, bool keepCollider, bool useWorldPosition = false) // 공통 상세 모델 Primitive 생성
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType); // 지정 종류 기본 Primitive 생성
            primitive.name = name; // 계층 구조 확인용 이름 지정
            primitive.transform.SetParent(parent, false); // 대상 부모 아래에 모델 연결

            if (useWorldPosition) // 입력 위치를 월드 좌표로 사용할지 확인
            {
                primitive.transform.position = localPosition; // 월드 위치 직접 적용
            }
            else // 일반 로컬 좌표 방식 처리
            {
                primitive.transform.localPosition = localPosition; // 부모 기준 로컬 위치 적용
            }

            primitive.transform.localEulerAngles = localEulerAngles; // 모델 회전 적용
            primitive.transform.localScale = localScale; // 모델 크기 적용
            Renderer renderer = primitive.GetComponent<Renderer>(); // Primitive 렌더러 조회

            if (renderer != null && material != null) // 렌더러와 재질 존재 여부 확인
            {
                renderer.sharedMaterial = material; // 생성 모델에 공용 재질 적용
            }

            Collider collider = primitive.GetComponent<Collider>(); // 자동 생성 Collider 조회

            if (!keepCollider && collider != null) // 장식용 모델의 Collider 제거 여부 확인
            {
                Object.DestroyImmediate(collider); // 장식 Primitive 물리 충돌 제거
            }

            return primitive; // 생성된 모델 오브젝트 반환
        }

        private static void EnsureMaterialFolders() // 자동 생성 재질 폴더 구조 확보
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRootFolder)) // Generated 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder("Assets/ProjectI/Art", "Generated"); // 자동 생성 아트 루트 폴더 생성
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) // Day12 재질 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder(GeneratedRootFolder, "Day12"); // 12일차 전용 재질 폴더 생성
            }
        }

        private static Day12Materials BuildMaterials() // 방·전등·문·배전반용 재질 묶음 생성
        {
            Day12Materials materials = new Day12Materials(); // 재질 묶음 객체 생성
            materials.Floor = GetOrCreateMaterial("Room_Floor", new Color(0.10f, 0.11f, 0.11f), 0.55f, 0.40f, Color.black); // 어두운 산업용 금속 바닥 재질 생성
            materials.Ceiling = GetOrCreateMaterial("Room_Ceiling", new Color(0.14f, 0.14f, 0.13f), 0.45f, 0.32f, Color.black); // 낡은 금속 천장 재질 생성
            materials.Wall = GetOrCreateMaterial("Room_Wall", new Color(0.18f, 0.19f, 0.18f), 0.62f, 0.34f, Color.black); // 회색 철제 방 벽 재질 생성
            materials.Trim = GetOrCreateMaterial("Industrial_Trim", new Color(0.08f, 0.09f, 0.09f), 0.85f, 0.42f, Color.black); // 검은 금속 보강재 재질 생성
            materials.DarkMetal = GetOrCreateMaterial("Dark_Metal", new Color(0.055f, 0.06f, 0.06f), 0.90f, 0.35f, Color.black); // 진한 기계 금속 재질 생성
            materials.Cable = GetOrCreateMaterial("Power_Cable", new Color(0.035f, 0.025f, 0.02f), 0.10f, 0.25f, Color.black); // 검은 고무 전선 재질 생성
            materials.Bolt = GetOrCreateMaterial("Steel_Bolt", new Color(0.30f, 0.31f, 0.29f), 0.95f, 0.52f, Color.black); // 밝은 금속 볼트 재질 생성
            materials.LampHousing = GetOrCreateMaterial("Lamp_Housing", new Color(0.16f, 0.15f, 0.12f), 0.78f, 0.40f, Color.black); // 전등 금속 하우징 재질 생성
            materials.LampOff = GetOrCreateMaterial("Lamp_Off", new Color(0.27f, 0.24f, 0.18f), 0.04f, 0.44f, Color.black); // 소등 유리 재질 생성
            materials.LampOn = GetOrCreateMaterial("Lamp_On", new Color(1.0f, 0.78f, 0.43f), 0.02f, 0.48f, new Color(2.6f, 1.55f, 0.60f)); // 따뜻한 전등 발광 재질 생성
            materials.Door = GetOrCreateMaterial("IronDoor_Steel", new Color(0.20f, 0.21f, 0.20f), 0.88f, 0.36f, Color.black); // 무거운 철제문 본판 재질 생성
            materials.DoorFrame = GetOrCreateMaterial("IronDoor_Frame", new Color(0.07f, 0.075f, 0.075f), 0.92f, 0.38f, Color.black); // 철제문 프레임 재질 생성
            materials.Warning = GetOrCreateMaterial("Warning_Yellow", new Color(0.80f, 0.52f, 0.05f), 0.40f, 0.34f, Color.black); // 산업용 황색 경고 재질 생성
            materials.Panel = GetOrCreateMaterial("Panel_Cabinet", new Color(0.14f, 0.17f, 0.15f), 0.72f, 0.38f, Color.black); // 배전반 철제 캐비닛 재질 생성
            materials.PanelFront = GetOrCreateMaterial("Panel_Front", new Color(0.20f, 0.23f, 0.20f), 0.68f, 0.40f, Color.black); // 배전반 전면판 재질 생성
            materials.Plate = GetOrCreateMaterial("Equipment_Plate", new Color(0.035f, 0.04f, 0.04f), 0.65f, 0.32f, Color.black); // 장비 검은 명판 재질 생성
            materials.GreenButton = GetOrCreateMaterial("Button_Green", new Color(0.08f, 0.38f, 0.12f), 0.38f, 0.45f, new Color(0.02f, 0.12f, 0.03f)); // 녹색 ON·OPEN 버튼 재질 생성
            materials.RedButton = GetOrCreateMaterial("Button_Red", new Color(0.48f, 0.07f, 0.05f), 0.38f, 0.45f, new Color(0.16f, 0.015f, 0.01f)); // 빨간 OFF·CLOSE 버튼 재질 생성
            materials.GreenGlow = GetOrCreateMaterial("Indicator_Green", new Color(0.08f, 0.70f, 0.12f), 0.10f, 0.52f, new Color(0.10f, 2.2f, 0.20f)); // 녹색 상태등 발광 재질 생성
            materials.RedGlow = GetOrCreateMaterial("Indicator_Red", new Color(0.78f, 0.08f, 0.045f), 0.10f, 0.52f, new Color(2.4f, 0.08f, 0.03f)); // 빨간 상태등 발광 재질 생성
            materials.AmberGlow = GetOrCreateMaterial("Indicator_Amber", new Color(0.95f, 0.54f, 0.06f), 0.10f, 0.52f, new Color(2.5f, 0.85f, 0.04f)); // 노란 이동 상태등 발광 재질 생성
            materials.DimRed = GetOrCreateMaterial("Indicator_NoPower", new Color(0.20f, 0.02f, 0.02f), 0.10f, 0.30f, new Color(0.12f, 0.0f, 0.0f)); // 정전 표시용 어두운 빨간 재질 생성
            return materials; // 완성된 재질 묶음 반환
        }

        private static Material GetOrCreateMaterial(string materialName, Color baseColor, float metallic, float smoothness, Color emissionColor) // URP Lit 재질 생성 또는 갱신
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 저장 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 생성 재질 조회
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // Unity 6 URP 기본 Lit Shader 조회

            if (shader == null) // URP Lit Shader 존재 여부 확인
            {
                shader = Shader.Find("Standard"); // 비상용 기본 Standard Shader 조회
            }

            if (material == null) // 기존 재질 존재 여부 확인
            {
                material = new Material(shader); // 새 재질 객체 생성
                material.name = materialName; // 재질 이름 지정
                AssetDatabase.CreateAsset(material, path); // 프로젝트 에셋으로 재질 저장
            }
            else if (shader != null) // 기존 재질과 Shader 존재 여부 확인
            {
                material.shader = shader; // 현재 프로젝트용 Shader로 동기화
            }

            if (material.HasProperty("_BaseColor")) // URP BaseColor 속성 존재 여부 확인
            {
                material.SetColor("_BaseColor", baseColor); // URP 기본 색상 적용
            }

            if (material.HasProperty("_Color")) // Standard 호환 Color 속성 존재 여부 확인
            {
                material.SetColor("_Color", baseColor); // 기본 색상 호환 적용
            }

            if (material.HasProperty("_Metallic")) // 금속성 속성 존재 여부 확인
            {
                material.SetFloat("_Metallic", metallic); // 금속성 값 적용
            }

            if (material.HasProperty("_Smoothness")) // 매끄러움 속성 존재 여부 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 표면 매끄러움 값 적용
            }

            if (emissionColor.maxColorComponent > 0.001f && material.HasProperty("_EmissionColor")) // 발광 재질 여부 확인
            {
                material.EnableKeyword("_EMISSION"); // 발광 Shader 키워드 활성화
                material.SetColor("_EmissionColor", emissionColor); // 발광 색과 강도 적용
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // 실시간 발광 재질 플래그 적용
            }
            else if (material.HasProperty("_EmissionColor")) // 비발광 재질의 Emission 속성 존재 여부 확인
            {
                material.DisableKeyword("_EMISSION"); // 불필요한 발광 Shader 키워드 비활성화
                material.SetColor("_EmissionColor", Color.black); // 발광 색상 제거
            }

            EditorUtility.SetDirty(material); // 변경 재질 저장 대상으로 표시
            return material; // 생성 또는 갱신된 재질 반환
        }

        private static void EnsureMarker(Scene scene) // 수정된 12일차 자동 적용 완료 마커 확보
        {
            GameObject legacyMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == LegacyReadyMarkerName); // 이전 버전 완료 마커 조회

            if (legacyMarker != null) // 이전 버전 마커 존재 여부 확인
            {
                Object.DestroyImmediate(legacyMarker); // 새 배치가 다시 자동 적용되도록 이전 마커 정리
            }

            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 수정 버전 완료 마커 조회

            if (marker == null) // 수정 버전 완료 마커 존재 여부 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
            }

            marker.hideFlags = HideFlags.None; // 계층 창에서 확인 가능한 일반 오브젝트 유지
        }

        private sealed class RoomBuildResult // 배전반 연결용 방 생성 결과 묶음
        {
            public readonly RoomPowerZone Zone; // 방 전력 구역 참조
            public readonly ElectricLightController Light; // 방 전기등 참조
            public readonly PoweredIronDoor Door; // 방 전동 철제문 참조

            public RoomBuildResult(RoomPowerZone zone, ElectricLightController light, PoweredIronDoor door) // 방 생성 결과 초기화
            {
                Zone = zone; // 방 전력 구역 저장
                Light = light; // 방 전기등 저장
                Door = door; // 방 철제문 저장
            }
        }

        private sealed class Day12Materials // 12일차 자동 생성 재질 묶음
        {
            public Material Floor; // 방 바닥 재질
            public Material Ceiling; // 방 천장 재질
            public Material Wall; // 방 벽 재질
            public Material Trim; // 구조 보강재 재질
            public Material DarkMetal; // 어두운 기계 금속 재질
            public Material Cable; // 전선 재질
            public Material Bolt; // 볼트·리벳 재질
            public Material LampHousing; // 전등 하우징 재질
            public Material LampOff; // 전등 소등 유리 재질
            public Material LampOn; // 전등 점등 발광 재질
            public Material Door; // 철제문 본판 재질
            public Material DoorFrame; // 철제문 프레임 재질
            public Material Warning; // 산업 경고 재질
            public Material Panel; // 배전반 캐비닛 재질
            public Material PanelFront; // 배전반 전면 재질
            public Material Plate; // 장비 명판 재질
            public Material GreenButton; // 녹색 버튼 재질
            public Material RedButton; // 빨간 버튼 재질
            public Material GreenGlow; // 녹색 상태등 재질
            public Material RedGlow; // 빨간 상태등 재질
            public Material AmberGlow; // 노란 상태등 재질
            public Material DimRed; // 정전 표시등 재질
        }
    }
}
