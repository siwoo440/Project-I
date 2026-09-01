using System.IO; // 대상 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트 검색 기능 참조
using ProjectI.Wagon; // Day21 마차 런타임 기능 참조
using UnityEditor; // 프리팹·재질·메뉴 생성 기능 참조
using UnityEditor.SceneManagement; // 테스트 씬 열기·저장 기능 참조
using UnityEngine; // 프리미티브 모델링과 Transform 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 완료 뒤 Day21 자동 구성 등록
    public static class Phase5Day21Setup // 세로형 대형 창고 마차 프리팹 생성과 테스트 씬 배치
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day21 테스트 대상 씬 경로
        private const string PrefabFolder = "Assets/ProjectI/Prefabs/Wagon"; // 모든 맵이 공유할 마차 프리팹 폴더
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공통 규격 마차 프리팹 경로
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day21"; // Day21 마차 생성 재질 폴더
        private const string SceneRootName = "===Day21 Wagon System==="; // Day21 테스트 루트 이름
        private const string ReadyMarkerName = "===Day21 Wagon Ready v1==="; // Day21 적용 완료 마커 이름
        private static readonly Vector3 TestPosition = new Vector3(0f, 0.05f, 18f); // 기존 시험장과 분리한 마차 테스트 위치

        static Phase5Day21Setup() // 자동 구성 생성자
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 후 자동 적용 예약
        }

        [MenuItem("Tools/Project I/Day 21/Apply Wagon System")] // 수동 전체 재구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 전체 Day21 구성
        {
            ApplyDay21(true, true); // 프리팹과 씬을 모두 강제 재구성
        }

        [MenuItem("Tools/Project I/Day 21/Rebuild Wagon Prefab Only")] // 공통 마차 프리팹만 재생성하는 메뉴 등록
        public static void RebuildPrefabFromMenu() // 프리팹 단독 재생성
        {
            EnsureFolders(); // 생성 폴더 확보
            BuildWagonPrefab(); // 세로형 마차 프리팹 생성
            AssetDatabase.SaveAssets(); // 생성된 프리팹과 재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            EditorUtility.DisplayDialog("Project I", "Wagon.prefab 재생성이 완료되었습니다.", "확인"); // 수동 실행 결과 안내
        }

        private static void TryAutoApply() // 에디터 로드 후 자동 Day21 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay21(false, false); // 필요한 경우에만 프리팹과 테스트 씬 자동 구성
        }

        private static void ApplyDay21(bool showDialog, bool force) // Day21 프리팹 생성과 테스트 씬 배치
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                Debug.LogError("[Project I] Day21 적용 대상 ExplorationOffice 씬을 찾을 수 없습니다."); // 씬 누락 오류 출력
                return; // 구성 중단
            }

            EnsureFolders(); // 프리팹과 재질 폴더 확보
            GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 기존 공통 마차 프리팹 조회

            if (wagonPrefab == null || force) // 최초 생성 또는 강제 재구성 여부 확인
            {
                wagonPrefab = BuildWagonPrefab(); // 동일 규격 세로형 마차 프리팹 생성
            }

            if (wagonPrefab == null) // 프리팹 생성 실패 여부 확인
            {
                Debug.LogError("[Project I] Wagon.prefab 생성에 실패했습니다."); // 생성 실패 로그 출력
                return; // 씬 배치 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // Day21 테스트 씬 단독 열기
            GameObject existingMarker = FindRoot(scene, ReadyMarkerName); // 기존 완료 마커 검색

            if (!force && existingMarker != null) // 이미 구성된 씬인지 확인
            {
                return; // 사용자 프리팹 배치와 씬 수정을 보존하고 중복 생성 방지
            }

            RemoveRoot(scene, SceneRootName); // 기존 Day21 테스트 루트 제거
            RemoveRoot(scene, ReadyMarkerName); // 기존 완료 마커 제거
            Material testFloorMaterial = GetOrCreateMaterial("Wagon_TestGround", new Color(0.08f, 0.095f, 0.08f), 0.05f, 0.36f); // 마차 시험장 바닥 재질 확보
            GameObject sceneRoot = new GameObject(SceneRootName); // Day21 시험장 루트 생성
            CreateBox(sceneRoot.transform, "Wagon_TestFloor", TestPosition + new Vector3(0f, -0.02f, 0f), new Vector3(9f, 0.06f, 21f), Quaternion.identity, testFloorMaterial, true); // 긴 마차 전체가 올라갈 시험 바닥 생성
            GameObject wagonInstance = PrefabUtility.InstantiatePrefab(wagonPrefab, scene) as GameObject; // 공통 Wagon.prefab 인스턴스 생성

            if (wagonInstance == null) // 프리팹 인스턴스 생성 실패 확인
            {
                Object.DestroyImmediate(sceneRoot); // 불완전 시험장 루트 제거
                Debug.LogError("[Project I] Day21 Wagon.prefab 씬 인스턴스 생성에 실패했습니다."); // 인스턴스 실패 로그 출력
                return; // 구성 중단
            }

            wagonInstance.name = "Wagon_Day21_Test"; // 테스트 씬 인스턴스 이름 지정
            wagonInstance.transform.SetParent(sceneRoot.transform, true); // Day21 시험장 루트 아래 배치
            wagonInstance.transform.position = TestPosition; // 테스트맵 기준 위치 지정
            wagonInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 말이 맵 중앙 방향을 향하도록 회전
            GameObject marker = new GameObject(ReadyMarkerName); // Day21 완료 마커 생성
            EditorUtility.SetDirty(marker); // 완료 마커 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 프리팹 인스턴스와 시험장 저장
            AssetDatabase.SaveAssets(); // 프리팹과 재질 저장
            bool success = Phase5Day21Validator.Validate(false); // Day21 전체 구조 정적 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day21 마차 프리팹·적재·공동 보관 구성이 완료되었습니다." : "Day21 검증 실패 - Console을 확인하세요.", "확인"); // 결과 대화상자 표시
            }
        }

        private static GameObject BuildWagonPrefab() // 길고 큰 후방 창고와 말 외형을 포함한 공통 프리팹 생성
        {
            EnsureFolders(); // 생성 폴더 재확인
            Material wood = GetOrCreateMaterial("Wagon_Wood", new Color(0.23f, 0.12f, 0.055f), 0.04f, 0.30f); // 진한 목재 재질
            Material woodLight = GetOrCreateMaterial("Wagon_WoodLight", new Color(0.38f, 0.20f, 0.085f), 0.03f, 0.34f); // 밝은 목재 재질
            Material metal = GetOrCreateMaterial("Wagon_Metal", new Color(0.12f, 0.13f, 0.14f), 0.62f, 0.42f); // 바퀴·보강재 금속 재질
            Material roof = GetOrCreateMaterial("Wagon_Roof", new Color(0.16f, 0.12f, 0.09f), 0.02f, 0.22f); // 창고 지붕 재질
            Material cargo = GetOrCreateMaterial("Wagon_CargoMarker", new Color(0.18f, 0.32f, 0.16f), 0.02f, 0.30f); // 창고 내부 적재 구역 표시 재질
            Material horse = GetOrCreateMaterial("Wagon_Horse", new Color(0.30f, 0.16f, 0.07f), 0.01f, 0.38f); // 말 몸통 재질
            Material horseDark = GetOrCreateMaterial("Wagon_HorseDark", new Color(0.095f, 0.055f, 0.03f), 0.01f, 0.28f); // 갈기·꼬리·발굽 재질
            Material harness = GetOrCreateMaterial("Wagon_Harness", new Color(0.055f, 0.035f, 0.022f), 0.12f, 0.30f); // 마구 재질

            GameObject root = new GameObject("Wagon"); // 모든 맵이 공유할 프리팹 최상위 루트
            GameObject visual = new GameObject("Visual"); // 시각 모델링 계층 루트
            visual.transform.SetParent(root.transform, false); // 프리팹 루트 아래 시각 계층 배치

            CreateBox(visual.transform, "Main_Chassis", new Vector3(0f, 0.78f, -0.65f), new Vector3(4.2f, 0.38f, 11.9f), Quaternion.identity, wood, true); // 긴 차체 하부 프레임 생성
            CreateBox(visual.transform, "Front_CrossBeam", new Vector3(0f, 0.98f, 5.15f), new Vector3(4.45f, 0.32f, 0.38f), Quaternion.identity, metal, true); // 전면 차축 보강대 생성
            CreateBox(visual.transform, "Rear_CrossBeam", new Vector3(0f, 0.98f, -6.45f), new Vector3(4.45f, 0.32f, 0.38f), Quaternion.identity, metal, true); // 후면 차체 보강대 생성

            GameObject warehouse = new GameObject("LargeCargoWarehouse"); // 세로형 대형 후방 창고 루트 생성
            warehouse.transform.SetParent(visual.transform, false); // 차체 시각 계층 아래 배치
            CreateBox(warehouse.transform, "Warehouse_Floor", new Vector3(0f, 1.28f, -1.25f), new Vector3(3.8f, 0.24f, 9.8f), Quaternion.identity, woodLight, true); // 약 10m 길이 창고 바닥 생성
            CreateBox(warehouse.transform, "Warehouse_LeftWall", new Vector3(-1.88f, 2.62f, -1.25f), new Vector3(0.22f, 2.7f, 9.8f), Quaternion.identity, wood, true); // 왼쪽 긴 창고 벽 생성
            CreateBox(warehouse.transform, "Warehouse_RightWall", new Vector3(1.88f, 2.62f, -1.25f), new Vector3(0.22f, 2.7f, 9.8f), Quaternion.identity, wood, true); // 오른쪽 긴 창고 벽 생성
            CreateBox(warehouse.transform, "Warehouse_Roof", new Vector3(0f, 4.03f, -1.25f), new Vector3(4.05f, 0.24f, 9.8f), Quaternion.identity, roof, true); // 대형 창고 지붕 생성
            CreateBox(warehouse.transform, "Warehouse_FrontWall", new Vector3(0f, 2.62f, 3.63f), new Vector3(3.8f, 2.7f, 0.24f), Quaternion.identity, wood, true); // 운전석 쪽 창고 전면 벽 생성
            CreateBox(warehouse.transform, "RearDoor_LeftPost", new Vector3(-1.63f, 2.55f, -6.12f), new Vector3(0.50f, 2.55f, 0.28f), Quaternion.identity, wood, true); // 후방 개방구 왼쪽 기둥 생성
            CreateBox(warehouse.transform, "RearDoor_RightPost", new Vector3(1.63f, 2.55f, -6.12f), new Vector3(0.50f, 2.55f, 0.28f), Quaternion.identity, wood, true); // 후방 개방구 오른쪽 기둥 생성
            CreateBox(warehouse.transform, "RearDoor_Header", new Vector3(0f, 3.63f, -6.12f), new Vector3(3.8f, 0.48f, 0.28f), Quaternion.identity, wood, true); // 후방 개방구 상단 보강대 생성
            CreateBox(warehouse.transform, "Rear_LoadingRamp", new Vector3(0f, 0.72f, -6.78f), new Vector3(3.25f, 0.18f, 1.55f), Quaternion.Euler(-10f, 0f, 0f), woodLight, true); // 대형 물건을 넣기 쉬운 후방 경사판 생성
            CreateBox(warehouse.transform, "Cargo_ZoneFloor", new Vector3(0f, 1.43f, -1.70f), new Vector3(3.25f, 0.035f, 7.85f), Quaternion.identity, cargo, false); // 적재 판정 위치를 보여주는 내부 바닥 표시 생성

            float[] ribPositions = { -4.55f, -2.15f, 0.25f, 2.65f }; // 긴 창고를 지지하는 세로 구간 위치 정의

            foreach (float z in ribPositions) // 창고 길이에 맞춰 보강 프레임 반복 생성
            {
                CreateBox(warehouse.transform, $"WallBrace_L_{z:0.00}", new Vector3(-1.72f, 2.60f, z), new Vector3(0.14f, 2.58f, 0.18f), Quaternion.identity, metal, false); // 왼쪽 수직 보강대 생성
                CreateBox(warehouse.transform, $"WallBrace_R_{z:0.00}", new Vector3(1.72f, 2.60f, z), new Vector3(0.14f, 2.58f, 0.18f), Quaternion.identity, metal, false); // 오른쪽 수직 보강대 생성
                CreateBox(warehouse.transform, $"RoofRib_{z:0.00}", new Vector3(0f, 3.85f, z), new Vector3(3.55f, 0.12f, 0.18f), Quaternion.identity, metal, false); // 천장 가로 보강대 생성
            }

            GameObject cargoAreaObject = new GameObject("CargoArea"); // 실제 회수품 확보 Trigger 오브젝트 생성
            cargoAreaObject.transform.SetParent(root.transform, false); // 마차 프리팹 루트 아래 기능 계층 배치
            cargoAreaObject.transform.localPosition = new Vector3(0f, 2.30f, -1.70f); // 큰 후방 창고 내부 중심에 배치
            BoxCollider cargoTrigger = cargoAreaObject.AddComponent<BoxCollider>(); // 창고 내부 적재 판정 Collider 추가
            cargoTrigger.size = new Vector3(3.30f, 2.05f, 7.80f); // 벽과 후방 입구 여유를 둔 실제 적재 범위 설정
            cargoTrigger.isTrigger = true; // 물리 막힘 없이 진입·이탈 판정만 활성화
            WagonCargoArea cargoArea = cargoAreaObject.AddComponent<WagonCargoArea>(); // 회수품 확보 상태 관리 기능 추가
            cargoArea.Configure(cargoTrigger); // Trigger 참조 연결

            GameObject wheels = new GameObject("Wheels"); // 긴 차체용 바퀴 계층 생성
            wheels.transform.SetParent(visual.transform, false); // 시각 계층 아래 배치
            float[] wheelZ = { -4.85f, -1.85f, 1.15f, 4.15f }; // 길어진 차체에 맞춘 4축 위치 정의

            for (int index = 0; index < wheelZ.Length; index++) // 4개 축 순회
            {
                int number = index + 1; // 바퀴 표시 번호 계산
                CreateWheel(wheels.transform, $"Wheel_Left_{number:00}", new Vector3(-2.28f, 1.12f, wheelZ[index]), metal, wood); // 왼쪽 바퀴 생성
                CreateWheel(wheels.transform, $"Wheel_Right_{number:00}", new Vector3(2.28f, 1.12f, wheelZ[index]), metal, wood); // 오른쪽 바퀴 생성
                CreateBox(wheels.transform, $"Axle_{number:00}", new Vector3(0f, 1.12f, wheelZ[index]), new Vector3(4.60f, 0.18f, 0.18f), Quaternion.identity, metal, false); // 좌우 바퀴 연결 축 생성
            }

            GameObject driver = new GameObject("DriverSection"); // 운전석 계층 생성
            driver.transform.SetParent(visual.transform, false); // 차체 시각 계층 아래 배치
            CreateBox(driver.transform, "DriverDeck", new Vector3(0f, 1.30f, 4.55f), new Vector3(3.70f, 0.24f, 1.60f), Quaternion.identity, woodLight, true); // 창고 앞 운전용 발판 생성
            CreateBox(driver.transform, "DriverBench", new Vector3(0f, 1.92f, 4.20f), new Vector3(2.45f, 0.40f, 0.58f), Quaternion.identity, wood, true); // 운전석 벤치 생성
            CreateBox(driver.transform, "DriverBackrest", new Vector3(0f, 2.42f, 3.93f), new Vector3(2.45f, 0.90f, 0.18f), Quaternion.identity, wood, true); // 운전석 등받이 생성
            CreateBox(driver.transform, "FrontRail_Left", new Vector3(-1.72f, 1.95f, 4.92f), new Vector3(0.12f, 1.35f, 0.12f), Quaternion.identity, metal, false); // 왼쪽 안전 난간 기둥 생성
            CreateBox(driver.transform, "FrontRail_Right", new Vector3(1.72f, 1.95f, 4.92f), new Vector3(0.12f, 1.35f, 0.12f), Quaternion.identity, metal, false); // 오른쪽 안전 난간 기둥 생성
            CreateBox(driver.transform, "FrontRail_Top", new Vector3(0f, 2.58f, 4.92f), new Vector3(3.55f, 0.12f, 0.12f), Quaternion.identity, metal, false); // 운전석 앞 난간 생성

            GameObject sharedChest = new GameObject("SharedStorageChest"); // 일반 장비 공동 보관함 기능 루트 생성
            sharedChest.transform.SetParent(root.transform, false); // 프리팹 루트 아래 기능·외형 함께 배치
            sharedChest.transform.localPosition = new Vector3(-2.25f, 1.58f, 3.65f); // 운전석 왼쪽에서 쉽게 접근할 위치 지정
            BoxCollider chestCollider = sharedChest.AddComponent<BoxCollider>(); // F 상호작용 Raycast용 Collider 추가
            chestCollider.center = new Vector3(0f, 0.12f, 0f); // 상자 중심 높이 조정
            chestCollider.size = new Vector3(1.25f, 1.05f, 1.55f); // 상자 상호작용 범위 설정
            WagonSharedStorage sharedStorage = sharedChest.AddComponent<WagonSharedStorage>(); // 기존 빠른 슬롯과 연결되는 공동 보관 기능 추가
            GameObject storedItems = new GameObject("StoredItems"); // 실제 보관 아이템 숨김 루트 생성
            storedItems.transform.SetParent(sharedChest.transform, false); // 보관함 자식으로 연결
            sharedStorage.Configure(storedItems.transform, 12); // Day21 공동 보관 용량과 루트 연결
            CreateBox(sharedChest.transform, "Chest_Body", new Vector3(0f, 0f, 0f), new Vector3(1.20f, 0.85f, 1.48f), Quaternion.identity, wood, false); // 공동 보관함 본체 외형 생성
            CreateBox(sharedChest.transform, "Chest_Lid", new Vector3(0f, 0.55f, 0f), new Vector3(1.28f, 0.22f, 1.55f), Quaternion.identity, woodLight, false); // 공동 보관함 뚜껑 외형 생성
            CreateBox(sharedChest.transform, "Chest_Band_A", new Vector3(-0.38f, 0.16f, 0f), new Vector3(0.10f, 0.98f, 1.58f), Quaternion.identity, metal, false); // 상자 왼쪽 금속띠 생성
            CreateBox(sharedChest.transform, "Chest_Band_B", new Vector3(0.38f, 0.16f, 0f), new Vector3(0.10f, 0.98f, 1.58f), Quaternion.identity, metal, false); // 상자 오른쪽 금속띠 생성

            GameObject horseRoot = new GameObject("Horse"); // 마차 앞 말 외형 계층 생성
            horseRoot.transform.SetParent(visual.transform, false); // 마차 시각 계층 아래 배치
            CreateCapsule(horseRoot.transform, "Horse_Body", new Vector3(0f, 1.92f, 8.55f), new Vector3(1.40f, 1.80f, 1.40f), Quaternion.Euler(90f, 0f, 0f), horse, true); // 진행 방향으로 긴 말 몸통 생성
            CreateCapsule(horseRoot.transform, "Horse_Chest", new Vector3(0f, 2.12f, 9.48f), new Vector3(1.18f, 1.02f, 1.18f), Quaternion.identity, horse, true); // 앞가슴 볼륨 생성
            CreateCapsule(horseRoot.transform, "Horse_Neck", new Vector3(0f, 2.82f, 9.86f), new Vector3(0.72f, 0.98f, 0.72f), Quaternion.Euler(-24f, 0f, 0f), horse, true); // 위로 뻗은 목 생성
            CreateSphere(horseRoot.transform, "Horse_Head", new Vector3(0f, 3.42f, 10.30f), new Vector3(0.88f, 0.78f, 1.02f), Quaternion.identity, horse, true); // 말 머리 생성
            CreateSphere(horseRoot.transform, "Horse_Muzzle", new Vector3(0f, 3.24f, 10.92f), new Vector3(0.72f, 0.48f, 0.78f), Quaternion.identity, horse, true); // 앞으로 튀어나온 주둥이 생성
            CreateBox(horseRoot.transform, "Horse_Ear_Left", new Vector3(-0.28f, 4.04f, 10.26f), new Vector3(0.18f, 0.58f, 0.18f), Quaternion.Euler(0f, 0f, -12f), horseDark, false); // 왼쪽 귀 생성
            CreateBox(horseRoot.transform, "Horse_Ear_Right", new Vector3(0.28f, 4.04f, 10.26f), new Vector3(0.18f, 0.58f, 0.18f), Quaternion.Euler(0f, 0f, 12f), horseDark, false); // 오른쪽 귀 생성
            CreateBox(horseRoot.transform, "Horse_Mane", new Vector3(0f, 3.05f, 9.63f), new Vector3(0.18f, 1.20f, 0.80f), Quaternion.Euler(-20f, 0f, 0f), horseDark, false); // 목 뒤 갈기 생성

            float[] legX = { -0.52f, 0.52f }; // 좌우 다리 위치 정의
            float[] legZ = { 8.00f, 9.18f }; // 앞뒤 다리 위치 정의
            int legNumber = 1; // 다리 이름 번호 초기화

            foreach (float z in legZ) // 앞뒤 다리 열 순회
            {
                foreach (float x in legX) // 좌우 다리 순회
                {
                    CreateCapsule(horseRoot.transform, $"Horse_Leg_{legNumber:00}", new Vector3(x, 0.92f, z), new Vector3(0.32f, 0.88f, 0.32f), Quaternion.identity, horse, true); // 말 다리 생성
                    CreateBox(horseRoot.transform, $"Horse_Hoof_{legNumber:00}", new Vector3(x, 0.18f, z + 0.05f), new Vector3(0.50f, 0.28f, 0.62f), Quaternion.identity, horseDark, true); // 말 발굽 생성
                    legNumber++; // 다음 다리 번호 증가
                }
            }

            CreateCapsule(horseRoot.transform, "Horse_Tail", new Vector3(0f, 1.78f, 7.00f), new Vector3(0.28f, 0.95f, 0.28f), Quaternion.Euler(42f, 0f, 0f), horseDark, false); // 뒤로 늘어진 꼬리 생성
            CreateBox(horseRoot.transform, "Harness_Shaft_Left", new Vector3(-0.72f, 1.36f, 6.70f), new Vector3(0.13f, 0.13f, 3.25f), Quaternion.identity, harness, true); // 마차와 말을 잇는 왼쪽 연결봉 생성
            CreateBox(horseRoot.transform, "Harness_Shaft_Right", new Vector3(0.72f, 1.36f, 6.70f), new Vector3(0.13f, 0.13f, 3.25f), Quaternion.identity, harness, true); // 마차와 말을 잇는 오른쪽 연결봉 생성
            CreateBox(horseRoot.transform, "Harness_Yoke", new Vector3(0f, 1.62f, 8.08f), new Vector3(1.75f, 0.18f, 0.18f), Quaternion.identity, harness, false); // 말 앞가슴을 잡아주는 가로 마구 생성
            CreateBox(horseRoot.transform, "Harness_BreastStrap", new Vector3(0f, 2.03f, 9.38f), new Vector3(1.32f, 0.16f, 0.18f), Quaternion.identity, harness, false); // 가슴 마구 표현 생성

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, WagonPrefabPath); // 완성된 공통 규격 마차를 실제 .prefab으로 저장
            Object.DestroyImmediate(root); // 임시 모델링 GameObject를 현재 열린 씬에서 제거
            AssetDatabase.SaveAssets(); // 프리팹과 재질 에셋 저장
            AssetDatabase.Refresh(); // 새 프리팹 즉시 임포트
            return savedPrefab == null ? AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath) : savedPrefab; // 저장된 Wagon.prefab 반환
        }

        private static void CreateWheel(Transform parent, string name, Vector3 localPosition, Material tireMaterial, Material hubMaterial) // 긴 마차용 대형 바퀴 한 개 생성
        {
            GameObject wheel = CreateCylinder(parent, name, localPosition, new Vector3(2.15f, 0.22f, 2.15f), Quaternion.Euler(0f, 0f, 90f), tireMaterial, true); // 금속 테두리 대형 바퀴 생성
            CreateCylinder(wheel.transform, "Hub", Vector3.zero, new Vector3(0.72f, 0.32f, 0.72f), Quaternion.identity, hubMaterial, false); // 바퀴 중앙 목재 허브 생성
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // 큐브 기반 마차 부품 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 Cube 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 부품 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 확인
                {
                    Object.DestroyImmediate(collider); // 장식 부품 물리 Collider 제거
                }
            }

            return part; // 생성된 부품 반환
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // 실린더 기반 바퀴·허브 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 기본 Cylinder 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 부품 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 확인
                {
                    Object.DestroyImmediate(collider); // 장식 부품 Collider 제거
                }
            }

            return part; // 생성된 실린더 반환
        }

        private static GameObject CreateCapsule(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // 말 몸통·다리용 Capsule 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 기본 Capsule 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 부품 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 확인
                {
                    Object.DestroyImmediate(collider); // 장식 부품 Collider 제거
                }
            }

            return part; // 생성된 Capsule 반환
        }

        private static GameObject CreateSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // 말 머리·주둥이용 Sphere 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 기본 Sphere 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 부품 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 확인
                {
                    Object.DestroyImmediate(collider); // 장식 부품 Collider 제거
                }
            }

            return part; // 생성된 Sphere 반환
        }

        private static void ApplyMaterial(GameObject target, Material material) // 생성 프리미티브 재질 지정
        {
            Renderer renderer = target.GetComponent<Renderer>(); // 생성 프리미티브 Renderer 조회

            if (renderer != null && material != null) // Renderer와 재질 유효성 확인
            {
                renderer.sharedMaterial = material; // 공용 생성 재질 연결
            }
        }

        private static Material GetOrCreateMaterial(string materialName, Color baseColor, float metallic, float smoothness) // URP 테스트 재질 생성 또는 재사용
        {
            EnsureFolder(MaterialFolder); // 재질 폴더 존재 보장
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 재질 미생성 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // Unity 6 URP 기본 Lit Shader 조회

                if (shader == null) // URP Shader 조회 실패 여부 확인
                {
                    shader = Shader.Find("Standard"); // 에디터 안전용 기본 Shader 대체
                }

                material = new Material(shader); // 새 재질 인스턴스 생성
                AssetDatabase.CreateAsset(material, path); // 프로젝트 에셋으로 저장
            }

            if (material.HasProperty("_BaseColor")) // URP BaseColor 속성 존재 확인
            {
                material.SetColor("_BaseColor", baseColor); // URP 기본색 적용
            }

            if (material.HasProperty("_Color")) // Standard 호환 색상 속성 존재 확인
            {
                material.SetColor("_Color", baseColor); // 대체 기본색 적용
            }

            if (material.HasProperty("_Metallic")) // 금속도 속성 존재 확인
            {
                material.SetFloat("_Metallic", metallic); // 재질 금속도 적용
            }

            if (material.HasProperty("_Smoothness")) // 매끄러움 속성 존재 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 재질 매끄러움 적용
            }

            EditorUtility.SetDirty(material); // 재질 변경 저장 대상으로 표시
            return material; // 생성 또는 갱신된 재질 반환
        }

        private static void EnsureFolders() // Day21 생성에 필요한 모든 폴더 확보
        {
            EnsureFolder(PrefabFolder); // 공통 마차 프리팹 폴더 생성
            EnsureFolder(MaterialFolder); // 생성 재질 폴더 생성
        }

        private static void EnsureFolder(string path) // AssetDatabase 기반 중첩 폴더 안전 생성
        {
            string[] parts = path.Split('/'); // 경로를 단계별 폴더 이름으로 분리
            string current = parts[0]; // Assets 루트부터 시작

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 순회
            {
                string next = $"{current}/{parts[index]}"; // 다음 단계 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 해당 폴더 미존재 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 상위 폴더 아래 새 폴더 생성
                }

                current = next; // 다음 단계 기준 경로 갱신
            }
        }

        private static GameObject FindRoot(Scene scene, string rootName) // 지정 이름의 씬 루트 검색
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 일치하는 첫 루트 반환
        }

        private static void RemoveRoot(Scene scene, string rootName) // 기존 Day21 시험장 또는 마커 제거
        {
            GameObject existing = FindRoot(scene, rootName); // 제거 대상 루트 검색

            if (existing != null) // 기존 오브젝트 존재 확인
            {
                Object.DestroyImmediate(existing); // 씬 중복 방지를 위해 즉시 제거
            }
        }
    }
}
