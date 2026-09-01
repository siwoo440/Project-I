using System.IO; // 대상 씬과 프리팹 파일 확인 기능 참조
using System.Linq; // 씬 루트와 계층 이름 검색 기능 참조
using ProjectI.Economy; // Day23 가격·영구 보관·판매·공동 자금·채무 기능 참조
using ProjectI.Items; // 기존 WorldItem 가격 연결 기능 참조
using UnityEditor; // 에디터 메뉴·프리팹·재질 생성 기능 참조
using UnityEditor.SceneManagement; // ExplorationOffice 씬 열기·저장 기능 참조
using UnityEngine; // Primitive 모델링과 Transform 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day23 거리형 사무소 구역 자동 구성
    public static class Phase5Day23Setup // 건물 거리·사무소 내부 경제 기능·도로 마차 재배치 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day23 테스트 대상 씬 경로
        private const string PedestalPrefabFolder = "Assets/ProjectI/Prefabs/Office"; // 공통 사무소 프리팹 폴더
        private const string PedestalPrefabPath = "Assets/ProjectI/Prefabs/Office/StoragePedestal.prefab"; // 모든 보관 단상이 공유할 프리팹 경로
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // Day21 공통 마차 프리팹 경로
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day23"; // 거리와 사무소 생성 재질 폴더
        private const string LegacySystemRootName = "===Day23 Office Economy System==="; // 이전 단순 경제 테스트 구역 루트 이름
        private const string DistrictRootName = "===Day23 Office Street District==="; // 새 거리형 테스트 구역 루트 이름
        private const string ReadyMarkerName = "===Day23 Office Street Ready v2==="; // 새 거리형 Day23 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day23 Office Economy Ready v1==="; // 이전 Day23 완료 마커 이름
        private const string Day21RootName = "===Day21 Wagon System==="; // 기존 마차 테스트 시스템 루트 이름
        private const int DefaultStorageValueLimit = 1000; // 가치 1000 이상 보관 금지 기본 상한
        private static readonly Vector3 StreetDistrictCenter = new Vector3(44f, 0f, 5f); // 다른 테스트 구역과 분리한 새 거리 중심
        private static readonly Vector3 WagonStreetPosition = StreetDistrictCenter + new Vector3(0f, 0.05f, -7.2f); // 중앙 도로 위 마차 주차 위치
        private static readonly Vector3 OfficeCenter = StreetDistrictCenter + new Vector3(9.2f, 0f, 1.5f); // 도로 오른쪽 사무소 건물 중심

        static Phase5Day23Setup() // 자동 적용 생성자
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 다음 에디터 틱에 거리형 Day23 구성 예약
        }

        [MenuItem("Tools/Project I/Day 23/Apply Office Street District")] // 거리형 Day23 전체 재구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 거리·사무소·마차 전체 재구성
        {
            ApplyDay23(true, true); // 모든 Day23 도시형 구성을 강제로 다시 생성
        }

        [MenuItem("Tools/Project I/Day 23/Rebuild Storage Pedestal Prefab")] // 보관 단상 공통 프리팹 재생성 메뉴 등록
        public static void RebuildPedestalFromMenu() // StoragePedestal.prefab 단독 재생성
        {
            EnsureFolders(); // 프리팹·재질 폴더 확보
            BuildStoragePedestalPrefab(); // 공통 단상 프리팹 생성
            AssetDatabase.SaveAssets(); // 프리팹과 재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            EditorUtility.DisplayDialog("Project I", "StoragePedestal.prefab 재생성이 완료되었습니다.", "확인"); // 수동 실행 결과 안내
        }

        private static void TryAutoApply() // 에디터 로드 후 자동 거리형 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 안전하지 않은 상태에서는 자동 구성 중단
            }

            ApplyDay23(false, false); // 아직 새 거리형 완료 마커가 없을 때만 자동 구성
        }

        private static void ApplyDay23(bool showDialog, bool force) // 거리·건물·사무소 경제 기능과 마차 위치 전체 구성
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 사무소 씬 존재 여부 확인
            {
                Debug.LogError("[Project I] Day23 대상 ExplorationOffice 씬을 찾을 수 없습니다."); // 씬 누락 오류 출력
                return; // 구성 중단
            }

            EnsureFolders(); // Day23 프리팹과 생성 재질 폴더 확보
            GameObject pedestalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PedestalPrefabPath); // 기존 공통 보관 단상 프리팹 조회

            if (pedestalPrefab == null || force) // 최초 생성 또는 강제 재생성 여부 확인
            {
                pedestalPrefab = BuildStoragePedestalPrefab(); // 실제 아이템을 표시하는 공통 단상 프리팹 생성
            }

            if (pedestalPrefab == null) // 공통 단상 프리팹 생성 실패 확인
            {
                Debug.LogError("[Project I] StoragePedestal.prefab 생성에 실패했습니다."); // 생성 실패 오류 출력
                return; // 씬 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // ExplorationOffice 씬 단독 열기
            GameObject existingMarker = FindRoot(scene, ReadyMarkerName); // 새 거리형 완료 마커 조회

            if (!force && existingMarker != null) // 이미 최신 거리형 구성이 저장됐는지 확인
            {
                return; // 중복 모델링과 사용자 배치 변경 방지
            }

            RemoveRoot(scene, LegacySystemRootName); // 이전 Day23 단순 바닥 경제 구역 제거
            RemoveRoot(scene, LegacyReadyMarkerName); // 이전 Day23 완료 마커 제거
            RemoveRoot(scene, DistrictRootName); // 기존 거리형 구역 제거
            RemoveRoot(scene, ReadyMarkerName); // 기존 최신 완료 마커 제거

            Material ground = GetOrCreateMaterial("Street_Ground", new Color(0.11f, 0.105f, 0.09f), 0.01f, 0.22f); // 도시 기초 지면 재질
            Material road = GetOrCreateMaterial("Street_Road", new Color(0.055f, 0.058f, 0.06f), 0.01f, 0.28f); // 중앙 도로 재질
            Material sidewalk = GetOrCreateMaterial("Street_Sidewalk", new Color(0.28f, 0.28f, 0.26f), 0.01f, 0.34f); // 양쪽 보도 재질
            Material roadLine = GetOrCreateMaterial("Street_RoadLine", new Color(0.72f, 0.67f, 0.42f), 0.02f, 0.38f); // 도로 중앙 표식 재질
            Material brickA = GetOrCreateMaterial("Building_BrickA", new Color(0.29f, 0.17f, 0.12f), 0.02f, 0.24f); // 붉은 벽돌형 건물 재질
            Material brickB = GetOrCreateMaterial("Building_BrickB", new Color(0.23f, 0.21f, 0.18f), 0.02f, 0.24f); // 회갈색 건물 재질
            Material plaster = GetOrCreateMaterial("Building_Plaster", new Color(0.42f, 0.38f, 0.30f), 0.01f, 0.27f); // 밝은 회반죽 건물 재질
            Material roof = GetOrCreateMaterial("Building_Roof", new Color(0.11f, 0.075f, 0.055f), 0.02f, 0.25f); // 건물 지붕 재질
            Material window = GetOrCreateMaterial("Building_Window", new Color(0.10f, 0.18f, 0.20f), 0.05f, 0.62f); // 건물 창문 재질
            Material wood = GetOrCreateMaterial("Building_Wood", new Color(0.20f, 0.11f, 0.05f), 0.03f, 0.29f); // 문·간판용 목재 재질
            Material officeStone = GetOrCreateMaterial("Office_Stone", new Color(0.31f, 0.30f, 0.27f), 0.02f, 0.32f); // 사무소 벽체 재질
            Material officeFloor = GetOrCreateMaterial("Office_Floor", new Color(0.19f, 0.13f, 0.075f), 0.02f, 0.31f); // 사무소 내부 바닥 재질
            Material saleMaterial = GetOrCreateMaterial("OfficeEconomy_Sale", new Color(0.12f, 0.32f, 0.14f), 0.08f, 0.38f); // 사무소 판매대 재질
            Material ledgerMaterial = GetOrCreateMaterial("OfficeEconomy_Ledger", new Color(0.18f, 0.11f, 0.055f), 0.02f, 0.28f); // 채무 장부 재질
            Material lampMetal = GetOrCreateMaterial("StreetLamp_Metal", new Color(0.08f, 0.085f, 0.09f), 0.58f, 0.34f); // 가로등 금속 재질
            Material lampGlass = GetOrCreateMaterial("StreetLamp_Glass", new Color(0.68f, 0.52f, 0.22f), 0.08f, 0.58f); // 가로등 램프 재질

            GameObject districtRoot = new GameObject(DistrictRootName); // 새 거리형 테스트 구역 루트 생성
            districtRoot.transform.position = StreetDistrictCenter; // 모든 거리 모델을 새 테스트 영역 중심에 배치

            CreateLocalBox(districtRoot.transform, "StreetFoundation", new Vector3(0f, -0.16f, 0f), new Vector3(30f, 0.30f, 38f), Quaternion.identity, ground, true); // 도로와 건물을 지탱하는 독립 기초 지면 생성
            CreateLocalBox(districtRoot.transform, "MainRoad", new Vector3(0f, 0.025f, 0f), new Vector3(8.5f, 0.05f, 34f), Quaternion.identity, road, true); // 남북 방향 긴 중앙 도로 생성
            CreateLocalBox(districtRoot.transform, "Sidewalk_Left", new Vector3(-5.75f, 0.10f, 0f), new Vector3(3.0f, 0.20f, 34f), Quaternion.identity, sidewalk, true); // 도로 왼쪽 보도 생성
            CreateLocalBox(districtRoot.transform, "Sidewalk_Right", new Vector3(5.75f, 0.10f, 0f), new Vector3(3.0f, 0.20f, 34f), Quaternion.identity, sidewalk, true); // 도로 오른쪽 보도 생성

            for (int index = -7; index <= 7; index++) // 도로 중앙선 표식 구간 순회
            {
                CreateLocalBox(districtRoot.transform, $"RoadLine_{index + 8:00}", new Vector3(0f, 0.065f, index * 2.15f), new Vector3(0.16f, 0.02f, 1.0f), Quaternion.identity, roadLine, false); // 짧은 중앙선 반복 생성
            }

            CreateStreetBuilding(districtRoot.transform, "Building_Left_01", new Vector3(-10.0f, 0f, -11.0f), new Vector3(7.0f, 5.4f, 7.0f), brickA, roof, window, wood); // 거리 왼쪽 첫 일반 건물 생성
            CreateStreetBuilding(districtRoot.transform, "Building_Left_02", new Vector3(-10.2f, 0f, 0.0f), new Vector3(7.4f, 6.6f, 8.0f), plaster, roof, window, wood); // 거리 왼쪽 중앙 일반 건물 생성
            CreateStreetBuilding(districtRoot.transform, "Building_Left_03", new Vector3(-10.0f, 0f, 11.2f), new Vector3(7.0f, 5.9f, 7.0f), brickB, roof, window, wood); // 거리 왼쪽 세 번째 일반 건물 생성
            CreateStreetBuilding(districtRoot.transform, "Building_Right_01", new Vector3(10.2f, 0f, -12.0f), new Vector3(7.2f, 5.6f, 6.5f), plaster, roof, window, wood); // 거리 오른쪽 남쪽 일반 건물 생성
            CreateStreetBuilding(districtRoot.transform, "Building_Right_02", new Vector3(10.0f, 0f, 13.0f), new Vector3(7.0f, 6.2f, 6.2f), brickA, roof, window, wood); // 거리 오른쪽 북쪽 일반 건물 생성

            CreateStreetLamp(districtRoot.transform, "StreetLamp_L_01", new Vector3(-4.25f, 0f, -11f), lampMetal, lampGlass); // 왼쪽 남쪽 가로등 생성
            CreateStreetLamp(districtRoot.transform, "StreetLamp_L_02", new Vector3(-4.25f, 0f, 7f), lampMetal, lampGlass); // 왼쪽 북쪽 가로등 생성
            CreateStreetLamp(districtRoot.transform, "StreetLamp_R_01", new Vector3(4.25f, 0f, -2f), lampMetal, lampGlass); // 오른쪽 중앙 가로등 생성
            CreateStreetLamp(districtRoot.transform, "StreetLamp_R_02", new Vector3(4.25f, 0f, 14f), lampMetal, lampGlass); // 오른쪽 북쪽 가로등 생성

            Transform officeBuilding = CreateOfficeBuilding(districtRoot.transform, OfficeCenter - StreetDistrictCenter, officeStone, officeFloor, roof, window, wood); // 도로 오른쪽에 실제 출입 가능한 사무소 건물 생성
            Transform officeInterior = new GameObject("OfficeInterior").transform; // 사무소 경제 기능 전용 내부 루트 생성
            officeInterior.SetParent(officeBuilding, false); // 사무소 건물 내부에 경제 기능 계층 연결
            CampaignEconomy economy = officeInterior.gameObject.AddComponent<CampaignEconomy>(); // 공동 자금과 판매 배율 상태 추가
            economy.Configure(0, 1f); // 기본 공동 자금 0과 판매 배율 1.0 설정

            string[] pedestalNames = // 사무소 내부 공통 단상 6개 이름 정의
            {
                "Day23_StoragePedestal_01", // 첫 번째 보관 단상 이름
                "Day23_StoragePedestal_02", // 두 번째 보관 단상 이름
                "Day23_StoragePedestal_03", // 세 번째 보관 단상 이름
                "Day23_StoragePedestal_04", // 네 번째 보관 단상 이름
                "Day23_StoragePedestal_05", // 다섯 번째 보관 단상 이름
                "Day23_StoragePedestal_06" // 여섯 번째 보관 단상 이름
            }; // 단상 이름 배열 정의 완료

            Vector3[] pedestalPositions = // 사무소 동쪽 절반에 두 줄로 배치할 단상 위치 정의
            {
                new Vector3(1.4f, 0f, -3.2f), // 1번 단상 위치
                new Vector3(3.5f, 0f, -3.2f), // 2번 단상 위치
                new Vector3(1.4f, 0f, 0f), // 3번 단상 위치
                new Vector3(3.5f, 0f, 0f), // 4번 단상 위치
                new Vector3(1.4f, 0f, 3.2f), // 5번 단상 위치
                new Vector3(3.5f, 0f, 3.2f) // 6번 단상 위치
            }; // 단상 배치 배열 정의 완료

            for (int index = 0; index < pedestalNames.Length; index++) // 공통 단상 6개 순회
            {
                GameObject pedestalInstance = PrefabUtility.InstantiatePrefab(pedestalPrefab, scene) as GameObject; // StoragePedestal.prefab 씬 인스턴스 생성

                if (pedestalInstance == null) // 인스턴스 생성 실패 확인
                {
                    continue; // 해당 단상만 건너뜀
                }

                pedestalInstance.name = pedestalNames[index]; // 단상 고정 식별 이름 지정
                pedestalInstance.transform.SetParent(officeInterior, false); // 실제 사무소 내부에 단상 배치
                pedestalInstance.transform.localPosition = pedestalPositions[index]; // 실내 두 줄 규격 위치 적용
                pedestalInstance.transform.localRotation = Quaternion.identity; // 단상 정면 방향 통일
            }

            GameObject saleCounterObject = CreateLocalBox(officeInterior, "Day23_SaleCounter", new Vector3(-1.8f, 0.65f, 3.7f), new Vector3(2.6f, 1.3f, 1.25f), Quaternion.identity, saleMaterial, true); // 사무소 입구 왼쪽 안쪽에 판매대 생성
            OfficeSaleCounter saleCounter = saleCounterObject.AddComponent<OfficeSaleCounter>(); // 기존 빠른 슬롯 회수품 판매 기능 추가
            GameObject soldItems = new GameObject("SoldItems"); // 판매 완료 아이템 숨김 루트 생성
            soldItems.transform.SetParent(saleCounterObject.transform, false); // 판매대 내부로 숨김 루트 연결
            saleCounter.Configure(economy, soldItems.transform); // 판매대와 공동 자금 상태 연결

            GameObject debtLedgerObject = CreateLocalBox(officeInterior, "Day23_DebtLedger", new Vector3(-1.8f, 0.90f, -3.7f), new Vector3(2.3f, 1.8f, 0.42f), Quaternion.identity, ledgerMaterial, true); // 사무소 입구 오른쪽 안쪽에 채무 장부 생성
            DebtLedger debtLedger = debtLedgerObject.AddComponent<DebtLedger>(); // 6단계 채무 상환 기능 추가
            debtLedger.Configure(economy); // 공동 자금과 채무 장부 연결

            ConfigureTreasureValue(scene, "Day20_SilverCoin", 300); // 은 동전 테스트 가격 연결
            ConfigureTreasureValue(scene, "Day20_MetalOrnament", 750); // 장인의 금속 장식 테스트 가격 연결
            ConfigureTreasureValue(scene, "Day20_Crown", 2250); // 왕관 테스트 가격 연결
            ConfigureTreasureValue(scene, "Day20_GodsStatue", 1500); // 신들의 조각상 테스트 가격 연결
            MoveWagonToStreet(scene); // 기존 Day21 공통 마차를 새 중앙 도로로 이동하고 옛 테스트 바닥 제거

            GameObject marker = new GameObject(ReadyMarkerName); // 거리형 Day23 완료 마커 생성
            EditorUtility.SetDirty(economy); // 공동 경제 상태 저장 대상으로 표시
            EditorUtility.SetDirty(saleCounter); // 판매대 연결 상태 저장 대상으로 표시
            EditorUtility.SetDirty(debtLedger); // 채무 장부 연결 상태 저장 대상으로 표시
            EditorUtility.SetDirty(marker); // 완료 마커 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 거리와 사무소·마차 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 최신 도시형 테스트 구역 씬 저장
            AssetDatabase.SaveAssets(); // 단상 프리팹과 생성 재질 저장
            AssetDatabase.Refresh(); // 새 에셋 즉시 갱신
            bool success = Phase5Day23Validator.Validate(false); // 거리·사무소·경제·마차 전체 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day23 거리·사무소·도로 마차 배치가 완료되었습니다." : "Day23 검증 실패 - Console을 확인하세요.", "확인"); // 수동 결과 표시
            }
        }

        private static Transform CreateOfficeBuilding(Transform parent, Vector3 localPosition, Material wallMaterial, Material floorMaterial, Material roofMaterial, Material windowMaterial, Material woodMaterial) // 실제 출입 가능한 사무소 건물 모델 생성
        {
            GameObject office = new GameObject("OfficeBuilding"); // 사무소 건물 계층 루트 생성
            office.transform.SetParent(parent, false); // 거리형 테스트 구역 아래 배치
            office.transform.localPosition = localPosition; // 도로 오른쪽 건물 위치 적용

            CreateLocalBox(office.transform, "Office_Floor", new Vector3(0f, 0.10f, 0f), new Vector3(10f, 0.20f, 12f), Quaternion.identity, floorMaterial, true); // 사무소 내부 목재 바닥 생성
            CreateLocalBox(office.transform, "Office_EastWall", new Vector3(5f, 2.25f, 0f), new Vector3(0.25f, 4.5f, 12f), Quaternion.identity, wallMaterial, true); // 사무소 뒤쪽 긴 벽 생성
            CreateLocalBox(office.transform, "Office_NorthWall", new Vector3(0f, 2.25f, 6f), new Vector3(10f, 4.5f, 0.25f), Quaternion.identity, wallMaterial, true); // 사무소 북쪽 벽 생성
            CreateLocalBox(office.transform, "Office_SouthWall", new Vector3(0f, 2.25f, -6f), new Vector3(10f, 4.5f, 0.25f), Quaternion.identity, wallMaterial, true); // 사무소 남쪽 벽 생성
            CreateLocalBox(office.transform, "Office_WestWall_North", new Vector3(-5f, 2.25f, 3.75f), new Vector3(0.25f, 4.5f, 4.5f), Quaternion.identity, wallMaterial, true); // 도로 쪽 벽의 북쪽 문 옆 구간 생성
            CreateLocalBox(office.transform, "Office_WestWall_South", new Vector3(-5f, 2.25f, -3.75f), new Vector3(0.25f, 4.5f, 4.5f), Quaternion.identity, wallMaterial, true); // 도로 쪽 벽의 남쪽 문 옆 구간 생성
            CreateLocalBox(office.transform, "Office_EntranceHeader", new Vector3(-5f, 4.0f, 0f), new Vector3(0.25f, 1.0f, 3.0f), Quaternion.identity, wallMaterial, true); // 중앙 출입구 위 상단 벽 생성
            CreateLocalBox(office.transform, "Office_Roof", new Vector3(0f, 4.65f, 0f), new Vector3(10.5f, 0.28f, 12.5f), Quaternion.identity, roofMaterial, true); // 사무소 지붕 생성
            CreateLocalBox(office.transform, "Office_Sign", new Vector3(-5.18f, 3.72f, 0f), new Vector3(0.12f, 0.72f, 2.5f), Quaternion.identity, woodMaterial, false); // 도로에서 보이는 사무소 목재 간판 생성
            CreateLocalBox(office.transform, "Office_Window_North", new Vector3(-5.16f, 2.35f, 3.55f), new Vector3(0.06f, 1.25f, 1.35f), Quaternion.identity, windowMaterial, false); // 입구 왼쪽 창문 표현 생성
            CreateLocalBox(office.transform, "Office_Window_South", new Vector3(-5.16f, 2.35f, -3.55f), new Vector3(0.06f, 1.25f, 1.35f), Quaternion.identity, windowMaterial, false); // 입구 오른쪽 창문 표현 생성
            CreateLocalBox(office.transform, "Office_EntranceStep", new Vector3(-5.55f, 0.16f, 0f), new Vector3(1.1f, 0.30f, 3.0f), Quaternion.identity, floorMaterial, true); // 도로 보도에서 실내로 들어가는 낮은 출입 계단 생성
            return office.transform; // 완성 사무소 Transform 반환
        }

        private static void CreateStreetBuilding(Transform parent, string name, Vector3 localPosition, Vector3 size, Material wallMaterial, Material roofMaterial, Material windowMaterial, Material woodMaterial) // 거리용 일반 건물 한 채 생성
        {
            GameObject building = new GameObject(name); // 일반 건물 계층 루트 생성
            building.transform.SetParent(parent, false); // 거리형 테스트 구역 아래 배치
            building.transform.localPosition = localPosition; // 거리 좌우 지정 위치 적용
            float height = size.y; // 건물 높이 값 저장
            CreateLocalBox(building.transform, "Body", new Vector3(0f, height * 0.5f, 0f), size, Quaternion.identity, wallMaterial, true); // 단순 건물 본체 생성
            CreateLocalBox(building.transform, "Roof", new Vector3(0f, height + 0.18f, 0f), new Vector3(size.x + 0.5f, 0.34f, size.z + 0.5f), Quaternion.identity, roofMaterial, true); // 돌출형 평지붕 생성
            float roadFacingX = localPosition.x < 0f ? size.x * 0.5f + 0.04f : -(size.x * 0.5f + 0.04f); // 도로를 향한 건물 전면 X 방향 계산
            CreateLocalBox(building.transform, "Door", new Vector3(roadFacingX, 1.05f, 0f), new Vector3(0.10f, 2.1f, 1.15f), Quaternion.identity, woodMaterial, false); // 도로 쪽 출입문 표현 생성
            CreateLocalBox(building.transform, "Window_01", new Vector3(roadFacingX, 2.45f, -2.0f), new Vector3(0.08f, 1.0f, 1.20f), Quaternion.identity, windowMaterial, false); // 전면 첫 창문 생성
            CreateLocalBox(building.transform, "Window_02", new Vector3(roadFacingX, 2.45f, 2.0f), new Vector3(0.08f, 1.0f, 1.20f), Quaternion.identity, windowMaterial, false); // 전면 두 번째 창문 생성
        }

        private static void CreateStreetLamp(Transform parent, string name, Vector3 localPosition, Material metalMaterial, Material lampMaterial) // 도로변 가로등 한 개 생성
        {
            GameObject lamp = new GameObject(name); // 가로등 계층 루트 생성
            lamp.transform.SetParent(parent, false); // 거리형 테스트 구역 아래 배치
            lamp.transform.localPosition = localPosition; // 보도 가장자리 위치 적용
            CreateLocalCylinder(lamp.transform, "Pole", new Vector3(0f, 1.75f, 0f), new Vector3(0.10f, 1.75f, 0.10f), Quaternion.identity, metalMaterial, false); // 긴 금속 가로등 기둥 생성
            CreateLocalBox(lamp.transform, "Arm", new Vector3(0f, 3.45f, 0f), new Vector3(0.75f, 0.10f, 0.10f), Quaternion.identity, metalMaterial, false); // 상단 가로 지지대 생성
            CreateLocalBox(lamp.transform, "Lamp", new Vector3(0f, 3.22f, 0f), new Vector3(0.42f, 0.48f, 0.42f), Quaternion.identity, lampMaterial, false); // 램프 외형 생성
        }

        private static void MoveWagonToStreet(Scene scene) // 기존 Day21 공통 마차를 새 거리 중앙 도로로 재배치
        {
            GameObject day21Root = FindRoot(scene, Day21RootName); // 기존 Day21 마차 시스템 루트 조회

            if (day21Root == null) // 이전 Day21 루트가 없는지 확인
            {
                day21Root = new GameObject(Day21RootName); // 기존 Validator 호환을 위한 Day21 루트 복구
            }

            RemoveChildIfExists(day21Root.transform, "Wagon_TestFloor"); // 기존 독립 마차 테스트 바닥 제거
            Transform wagonTransform = day21Root.transform.Find("Wagon_Day21_Test"); // Day21 공통 마차 씬 인스턴스 조회

            if (wagonTransform == null) // 기존 마차 인스턴스가 없는지 확인
            {
                GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 공통 Wagon.prefab 조회

                if (wagonPrefab != null) // 공통 프리팹 존재 여부 확인
                {
                    GameObject wagonInstance = PrefabUtility.InstantiatePrefab(wagonPrefab, scene) as GameObject; // 공통 프리팹 인스턴스 생성

                    if (wagonInstance != null) // 인스턴스 생성 성공 여부 확인
                    {
                        wagonInstance.name = "Wagon_Day21_Test"; // 기존 Day21 Validator와 같은 마차 이름 유지
                        wagonInstance.transform.SetParent(day21Root.transform, true); // Day21 시스템 루트 직접 자식으로 유지
                        wagonTransform = wagonInstance.transform; // 새 마차 Transform 저장
                    }
                }
            }

            if (wagonTransform == null) // 마차를 찾거나 생성하지 못했는지 확인
            {
                Debug.LogError("[Project I] Day23 거리로 이동할 Wagon_Day21_Test를 찾을 수 없습니다."); // 마차 누락 오류 출력
                return; // 재배치 중단
            }

            wagonTransform.position = WagonStreetPosition; // 새 거리 중앙 도로 남쪽 구간으로 마차 이동
            wagonTransform.rotation = Quaternion.identity; // 말이 도로 북쪽 방향을 바라보도록 정렬
            EditorUtility.SetDirty(wagonTransform); // 마차 Transform 변경 저장 대상으로 표시
        }

        private static GameObject BuildStoragePedestalPrefab() // 실제 회수품을 상판 위에 표시하는 공통 보관 단상 프리팹 생성
        {
            EnsureFolders(); // 프리팹과 재질 폴더 존재 보장
            Material stone = GetOrCreateMaterial("StoragePedestal_Stone", new Color(0.28f, 0.27f, 0.24f), 0.02f, 0.30f); // 단상 석재 본체 재질 확보
            Material trim = GetOrCreateMaterial("StoragePedestal_Trim", new Color(0.38f, 0.28f, 0.11f), 0.48f, 0.42f); // 단상 금속 장식 재질 확보
            GameObject root = new GameObject("StoragePedestal"); // 공통 단상 프리팹 루트 생성
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 플레이어 F Raycast용 단상 Collider 추가
            collider.center = new Vector3(0f, 0.70f, 0f); // 단상 전체 중심에 상호작용 Collider 배치
            collider.size = new Vector3(1.55f, 1.55f, 1.55f); // 단상 전체를 보기 쉬운 상호작용 범위 설정
            CreateLocalBox(root.transform, "Base", new Vector3(0f, 0.18f, 0f), new Vector3(1.45f, 0.36f, 1.45f), Quaternion.identity, stone, false); // 넓은 사각 받침 생성
            CreateLocalCylinder(root.transform, "Pillar", new Vector3(0f, 0.67f, 0f), new Vector3(0.58f, 0.42f, 0.58f), Quaternion.identity, stone, false); // 중앙 원형 기둥 생성
            CreateLocalBox(root.transform, "Top", new Vector3(0f, 1.10f, 0f), new Vector3(1.25f, 0.18f, 1.25f), Quaternion.identity, stone, false); // 실제 회수품을 놓을 상판 생성
            CreateLocalBox(root.transform, "Trim", new Vector3(0f, 1.20f, 0f), new Vector3(1.34f, 0.07f, 1.34f), Quaternion.identity, trim, false); // 상판 가장자리 장식 생성
            GameObject displayObject = new GameObject("DisplayPoint"); // 실제 WorldItem 표시 위치 생성
            displayObject.transform.SetParent(root.transform, false); // 단상 루트 아래 배치
            displayObject.transform.localPosition = new Vector3(0f, 1.42f, 0f); // 상판 위 표시 높이 적용
            OfficeStoragePedestal pedestal = root.AddComponent<OfficeStoragePedestal>(); // 가격 제한 영구 보관 기능 추가
            pedestal.Configure(displayObject.transform, DefaultStorageValueLimit); // 가치 1000 이상 차단 규칙과 표시 위치 연결
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PedestalPrefabPath); // 완성 단상을 실제 .prefab으로 저장
            Object.DestroyImmediate(root); // 임시 모델링 오브젝트를 열린 씬에서 제거
            AssetDatabase.SaveAssets(); // 프리팹과 재질 저장
            AssetDatabase.Refresh(); // 생성 프리팹 즉시 임포트
            return savedPrefab == null ? AssetDatabase.LoadAssetAtPath<GameObject>(PedestalPrefabPath) : savedPrefab; // 저장된 단상 프리팹 반환
        }

        private static void ConfigureTreasureValue(Scene scene, string objectName, int value) // 기존 Day20 회수품에 감정 없는 확정 가격 상태 연결
        {
            Transform target = FindTransformByName(scene, objectName); // 대상 Day20 테스트 회수품 검색

            if (target == null) // 테스트 회수품 존재 여부 확인
            {
                Debug.LogWarning($"[Project I] Day23 가격 설정 대상 {objectName}을 찾지 못했습니다."); // 선행 시험품 누락 안내
                return; // 가격 설정 중단
            }

            WorldItem worldItem = target.GetComponent<WorldItem>(); // 기존 WorldItem 기능 조회

            if (worldItem == null) // 기존 빠른 슬롯 아이템 여부 확인
            {
                return; // 일반 오브젝트에는 가격 상태를 추가하지 않음
            }

            RecoverableValue recoverable = target.GetComponent<RecoverableValue>(); // 기존 가격 상태 조회

            if (recoverable == null) // 가격 상태 미부착 여부 확인
            {
                recoverable = target.gameObject.AddComponent<RecoverableValue>(); // 기존 WorldItem에 최소 가격 컴포넌트 추가
            }

            recoverable.Configure(value); // 확정된 테스트 가격 저장
            EditorUtility.SetDirty(recoverable); // 가격 상태 씬 저장 대상으로 표시
        }

        private static Transform FindTransformByName(Scene scene, string objectName) // 비활성 자식 포함 정확한 이름의 Transform 검색
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 씬 모든 루트 순회
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true); // 비활성 포함 전체 계층 조회
                Transform match = transforms.FirstOrDefault(item => item.name == objectName); // 정확한 이름 일치 검색

                if (match != null) // 일치 대상 발견 여부 확인
                {
                    return match; // 첫 일치 Transform 반환
                }
            }

            return null; // 대상 없음 반환
        }

        private static void RemoveChildIfExists(Transform parent, string childName) // 특정 직접 자식 오브젝트 제거
        {
            if (parent == null) // 부모 유효성 확인
            {
                return; // 제거 중단
            }

            Transform child = parent.Find(childName); // 지정 이름 직접 자식 검색

            if (child != null) // 제거 대상 존재 여부 확인
            {
                Object.DestroyImmediate(child.gameObject); // 이전 테스트 오브젝트 즉시 제거
            }
        }

        private static GameObject CreateLocalBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // Cube 기반 거리·건물 부품 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 Cube 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 로컬 기준 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 오브젝트 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Collider 조회

                if (collider != null) // Collider 존재 여부 확인
                {
                    Object.DestroyImmediate(collider); // 장식 오브젝트 물리 Collider 제거
                }
            }

            return part; // 생성된 Cube 반환
        }

        private static GameObject CreateLocalCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider) // Cylinder 기반 단상·가로등 부품 생성
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 기본 Cylinder 프리미티브 생성
            part.name = name; // 계층 식별 이름 지정
            part.transform.SetParent(parent, false); // 지정 부모 아래 로컬 기준 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localRotation = localRotation; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            ApplyMaterial(part, material); // 생성 오브젝트 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Collider collider = part.GetComponent<Collider>(); // 기본 Cylinder Collider 조회

                if (collider != null) // Collider 존재 여부 확인
                {
                    Object.DestroyImmediate(collider); // 장식 오브젝트 Collider 제거
                }
            }

            return part; // 생성된 Cylinder 반환
        }

        private static void ApplyMaterial(GameObject target, Material material) // 생성 Primitive에 공용 재질 적용
        {
            Renderer renderer = target.GetComponent<Renderer>(); // 생성 오브젝트 Renderer 조회

            if (renderer != null && material != null) // Renderer와 재질 유효성 확인
            {
                renderer.sharedMaterial = material; // 공용 생성 재질 연결
            }
        }

        private static Material GetOrCreateMaterial(string materialName, Color baseColor, float metallic, float smoothness) // URP 거리·건물 재질 생성 또는 재사용
        {
            EnsureFolder(MaterialFolder); // Day23 재질 폴더 존재 보장
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 생성 재질 조회

            if (material == null) // 최초 생성 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // Unity 6 URP Lit Shader 조회

                if (shader == null) // URP Shader 조회 실패 여부 확인
                {
                    shader = Shader.Find("Standard"); // 에디터 안전용 Standard Shader 대체
                }

                material = new Material(shader); // 새 재질 인스턴스 생성
                AssetDatabase.CreateAsset(material, path); // 프로젝트 .mat 에셋으로 저장
            }

            if (material.HasProperty("_BaseColor")) // URP 기본색 속성 존재 확인
            {
                material.SetColor("_BaseColor", baseColor); // URP 기본색 적용
            }

            if (material.HasProperty("_Color")) // Standard 호환 색상 속성 존재 확인
            {
                material.SetColor("_Color", baseColor); // 대체 기본색 적용
            }

            if (material.HasProperty("_Metallic")) // 금속도 속성 존재 확인
            {
                material.SetFloat("_Metallic", metallic); // 금속도 적용
            }

            if (material.HasProperty("_Smoothness")) // 매끄러움 속성 존재 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 매끄러움 적용
            }

            EditorUtility.SetDirty(material); // 재질 변경 저장 대상으로 표시
            return material; // 생성 또는 갱신 재질 반환
        }

        private static void EnsureFolders() // Day23 생성에 필요한 프리팹·재질 폴더 확보
        {
            EnsureFolder(PedestalPrefabFolder); // 공통 사무소 단상 프리팹 폴더 생성
            EnsureFolder(MaterialFolder); // Day23 생성 재질 폴더 생성
        }

        private static void EnsureFolder(string path) // AssetDatabase 기반 중첩 폴더 안전 생성
        {
            string[] parts = path.Split('/'); // 경로를 단계별 폴더 이름으로 분리
            string current = parts[0]; // Assets 루트부터 시작

            for (int index = 1; index < parts.Length; index++) // 모든 하위 폴더 단계 순회
            {
                string next = $"{current}/{parts[index]}"; // 다음 단계 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 폴더 미존재 여부 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 현재 상위 폴더 아래 새 폴더 생성
                }

                current = next; // 다음 단계 기준 경로 갱신
            }
        }

        private static GameObject FindRoot(Scene scene, string rootName) // 지정 이름의 씬 루트 검색
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 일치하는 첫 루트 반환
        }

        private static void RemoveRoot(Scene scene, string rootName) // 기존 Day23 구역 또는 완료 마커 제거
        {
            GameObject existing = FindRoot(scene, rootName); // 제거 대상 루트 검색

            if (existing != null) // 기존 오브젝트 존재 여부 확인
            {
                Object.DestroyImmediate(existing); // 중복 구성을 막기 위해 즉시 제거
            }
        }
    }
}
