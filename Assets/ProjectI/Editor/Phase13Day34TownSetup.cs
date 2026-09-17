using System.Collections.Generic; // 목록
using System.Linq; // 조회
using ProjectI.Economy; // 판매대·상점·단상·장부
using ProjectI.EditorTools.Town; // 마을 제작 도구
using ProjectI.Loop; // 마차 정차 지점·하루 마감 장부
using UnityEditor; // 에디터 기능
using UnityEditor.SceneManagement; // 씬 열기·저장
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase13Day34TownSetup // 사무소 씬을 마을(도로 + 기능 건물) · 뒷골목 · 훈련장으로 재구성 (34일차)
    {
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity"; // 사무소 씬
        private const string TownRootName = "===Day34 Town==="; // 마을 루트
        private const string OldDistrictName = "===Day23 Office Street District==="; // 옛 거리
        private const string TestMapRootName = "===Day3 Test Map==="; // 훈련장 (기존 시험 맵)
        private const string HoldingName = "__Day34_Holding"; // 이동 중 임시 보관
        public const string ShopBuildingName = "Town_Shop"; // 상점 건물 이름 (33일차 메뉴에서 조회)
        public const float ShopShelfX = -1.6f; // 상점 진열대 x (실내 기준)
        public const float ShopTrayX = 1.75f; // 상점 수령대 x (실내 기준)
        public const float BuildingDepth = 10f; // 건물 깊이
        public const float BuildingWidth = 12f; // 건물 폭

        private static readonly Vector3 TownOrigin = new Vector3(-72f, 0f, 0f); // 마을 기준점 (훈련장 서쪽, 도로 중심선)
        private const float SidewalkTop = 0.15f; // 보도 높이
        private const float RoadTop = 0.02f; // 도로 높이
        private const float TownSouthZ = -32f; // 마을 남쪽 경계
        private const float TownNorthZ = 34f; // 마을 북쪽 경계
        private const float TownWestX = -19f; // 마을 서쪽 경계
        private const float TownEastX = 19.5f; // 마을 동쪽 경계
        private const float AlleySouthZ = 14f; // 뒷골목 남쪽 (보관 창고 북벽)
        private const float AlleyNorthZ = 18f; // 뒷골목 북쪽 (빈 건물 남벽)
        private const float TrainingEdgeX = 32f; // 훈련장 서쪽 경계 (월드 x = -40)
        private const float WagonStopZ = -22f; // 마차 정차 z
        private const float WagonEntryDistance = 12f; // 진입 거리

        [MenuItem("Project I/Day 34/Build Town And Training Ground")] // 메뉴 (빅토리아 시대 산업 도시 양식)
        public static void Setup() // 재구성
        {
            TownKit.ResetCache(); // 재질 캐시
            TownPalette.ResetCache(); // 음영 캐시
            Scene scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single); // 사무소 씬

            CampaignEconomy economy = Object.FindFirstObjectByType<CampaignEconomy>(FindObjectsInactive.Include); // 공동 자금 (옛 OfficeInterior)
            OfficeSaleCounter counter = Object.FindFirstObjectByType<OfficeSaleCounter>(FindObjectsInactive.Include); // 판매대
            OfficeSaleBell bell = Object.FindFirstObjectByType<OfficeSaleBell>(FindObjectsInactive.Include); // 판매 벨
            OfficeShopShelf shelf = Object.FindFirstObjectByType<OfficeShopShelf>(FindObjectsInactive.Include); // 진열대
            DebtLedger debtLedger = Object.FindFirstObjectByType<DebtLedger>(FindObjectsInactive.Include); // 채무 장부
            DayEndLedgerInteractable dayEndLedger = Object.FindFirstObjectByType<DayEndLedgerInteractable>(FindObjectsInactive.Include); // 하루 마감 장부
            List<OfficeStoragePedestal> pedestals = Object.FindObjectsByType<OfficeStoragePedestal>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(p => p.name).ToList(); // 보관 단상
            MapTravelAnchor anchor = Object.FindObjectsByType<MapTravelAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => a.Destination == TravelDestination.Office); // 마차 정차 지점

            List<string> missing = new List<string>(); // 누락
            if (economy == null) missing.Add("CampaignEconomy");
            if (counter == null) missing.Add("판매대");
            if (bell == null) missing.Add("판매 벨 (Day 32 메뉴)");
            if (shelf == null) missing.Add("상점 진열대 (Day 33 메뉴)");
            if (debtLedger == null) missing.Add("채무 장부");
            if (dayEndLedger == null) missing.Add("하루 마감 장부");
            if (pedestals.Count == 0) missing.Add("보관 단상");
            if (anchor == null) missing.Add("사무소 마차 정차 지점");

            if (missing.Count > 0) // 필수 요소 확인
            {
                Debug.LogError($"[Project I] 34일차 마을 구성 중단 / 누락: {string.Join(", ", missing)}"); // 오류
                return; // 종료
            }

            Transform shopRoot = shelf.transform.parent; // Day33_Shop
            GameObject holding = new GameObject(HoldingName); // 임시 보관
            SceneManager.MoveGameObjectToScene(holding, scene); // 씬 소속
            List<Transform> movable = new List<Transform> { counter.transform, bell.transform, shopRoot, debtLedger.transform, dayEndLedger.transform }; // 옮길 기능 요소
            movable.AddRange(pedestals.Select(p => p.transform)); // 단상
            movable.Add(economy.transform); // 공동 자금 (마지막: 자식이 모두 빠진 뒤)

            foreach (Transform item in movable) // 임시 보관으로 이동
            {
                item.SetParent(holding.transform, true); // 월드 위치 유지
            }

            DestroyRoot(scene, TownRootName); // 이전 마을
            DestroyRoot(scene, OldDistrictName); // 옛 거리 (기능 요소는 이미 빠짐)

            Transform town = new GameObject(TownRootName).transform; // 마을 루트
            SceneManager.MoveGameObjectToScene(town.gameObject, scene); // 씬 소속
            town.position = TownOrigin; // 위치

            BuildGround(town); // 지면·도로·보도
            BuildPerimeter(town); // 담장·출입 아치
            Dictionary<string, TownBuildingResult> buildings = BuildBuildings(town); // 건물
            BuildStreetProps(town); // 거리 소품
            BuildAlley(town); // 뒷골목
            BuildOutskirts(town); // 마을 밖 나무·울타리

            TownBuildingResult office = buildings["Town_Office"]; // 사무소
            TownBuildingResult sale = buildings["Town_Sale"]; // 판매소
            TownBuildingResult shop = buildings[ShopBuildingName]; // 상점
            TownBuildingResult storage = buildings["Town_Storage"]; // 보관 창고

            Place(economy.transform, office.Interior, Vector3.zero, 0f); // 공동 자금
            economy.name = "OfficeInterior"; // 기존 이름 유지
            Place(debtLedger.transform, office.Interior, new Vector3(-0.3f, 0.9f, office.InnerBackZ + 0.24f), 0f); // 채무 장부 (뒷벽 창 사이)
            Place(dayEndLedger.transform, office.Interior, new Vector3(1.6f, 0f, office.InnerBackZ + 1.05f), 0f); // 하루 마감 장부 (뒤에 의자)
            FurnishOffice(office); // 사무소 가구

            Place(counter.transform, sale.Interior, new Vector3(-0.9f, counter.transform.localScale.y * 0.5f, -1.4f), 0f); // 판매대
            Place(bell.transform, sale.Interior, new Vector3(-0.9f + (counter.transform.localScale.x * 0.5f) + 0.33f, 0f, -1.4f), 0f); // 벨 (판매대 오른쪽)
            FurnishSale(sale); // 판매소 가구

            Place(shopRoot, shop.Interior, Vector3.zero, 0f); // 상점 루트
            Transform shelfTransform = shelf.transform; // 진열대
            Transform trayTransform = shelf.Tray != null ? shelf.Tray.transform : shopRoot.Find("PickupTray"); // 수령대
            shelfTransform.localPosition = new Vector3(ShopShelfX, 0f, shop.InnerBackZ + 0.28f); // 뒷벽 앞
            shelfTransform.localRotation = Quaternion.identity; // 앞면 = 문 쪽

            if (trayTransform != null) // 수령대
            {
                trayTransform.localPosition = new Vector3(ShopTrayX, 0f, shop.InnerBackZ + 0.33f); // 진열대 오른쪽
                trayTransform.localRotation = Quaternion.identity; // 같은 방향
            }

            FurnishShop(shop); // 상점 가구

            float[] pedestalX = { -3.3f, 0f, 3.3f }; // 단상 x
            float[] pedestalZ = { -2.4f, 1.0f }; // 단상 z

            for (int index = 0; index < pedestals.Count; index++) // 단상 2줄 3칸
            {
                Vector3 local = new Vector3(pedestalX[index % 3], 0f, pedestalZ[Mathf.Min(1, index / 3)]); // 위치
                Place(pedestals[index].transform, storage.Interior, local, 0f); // 배치
            }

            FurnishStorage(storage); // 창고 가구
            FurnishVacant(buildings["Town_Vacant_NW"]); // 빈 점포
            FurnishVacant(buildings["Town_Vacant_NE"]); // 빈 점포
            ConfigureWagonStop(anchor, town); // 마차 정차·시작 위치
            SplitTrainingBoundary(scene); // 훈련장 출입구

            if (holding.transform.childCount == 0) // 임시 보관 정리
            {
                Object.DestroyImmediate(holding); // 제거
            }
            else // 남은 것 경고
            {
                Debug.LogWarning($"[Project I] 34일차 임시 보관에 남은 오브젝트 {holding.transform.childCount}개 — Hierarchy 의 {HoldingName} 을 확인하세요."); // 경고
            }

            MarkStatic(town, new List<Transform> { counter.transform, bell.transform, shopRoot, debtLedger.transform, dayEndLedger.transform, economy.transform }.Concat(pedestals.Select(p => p.transform)).ToList()); // 정적 배칭 (기능 요소 제외)
            Physics.SyncTransforms(); // 검사 준비
            string report = Validate(town, economy, counter, bell, shelf, debtLedger, dayEndLedger, pedestals, anchor); // 검사
            EditorSceneManager.MarkSceneDirty(scene); // 변경
            EditorSceneManager.SaveScene(scene); // 저장
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log($"[Project I] 34일차 마을·뒷골목·훈련장 구성 완료\n{report}"); // 완료
        }

        public static void SetupFromCommandLine() // 배치모드 실행용
        {
            Setup(); // 재구성
        }

        private static void Place(Transform item, Transform parent, Vector3 localPosition, float yaw) // 기능 요소 배치
        {
            item.SetParent(parent, false); // 부모 (로컬 값 기준)
            item.localPosition = localPosition; // 위치
            item.localRotation = Quaternion.Euler(0f, yaw, 0f); // 방향
        }

        private static void MarkStatic(Transform town, List<Transform> excluded) // 움직이지 않는 마을 외형을 정적 배칭 대상으로 지정
        {
            foreach (Renderer renderer in town.GetComponentsInChildren<Renderer>(true)) // 렌더러 순회
            {
                if (renderer is MeshRenderer && renderer.GetComponent<TextMesh>() == null && !excluded.Any(item => renderer.transform.IsChildOf(item))) // 기능 요소·글자 제외
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic); // 정적 배칭
                }
            }
        }

        private static void DestroyRoot(Scene scene, string name) // 루트 오브젝트 제거
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.name == name) // 일치
                {
                    Object.DestroyImmediate(root); // 제거
                }
            }
        }

        private static void BuildGround(Transform town) // 지면·돌길 도로·판석 보도·북쪽 광장
        {
            Transform group = TownKit.Group(town, "Ground", Vector3.zero); // 묶음
            TownKit.Box(group, "TownGround", new Vector3(3f, -0.25f, -5f), new Vector3(58f, 0.5f, 82f), TownPalette.Ground); // 흙 지면 (x -26~32, z -46~36)
            TownKit.Box(group, "Road", new Vector3(0f, RoadTop * 0.5f, -8.5f), new Vector3(8f, RoadTop, 75f), TownPalette.Setts); // 돌길 (z -46~29)

            for (float z = -45.6f; z < 29f; z += 0.6f) // 돌 줄눈 (가로)
            {
                TownKit.Box(group, "SettSeam", new Vector3(0f, RoadTop + 0.002f, z), new Vector3(7.8f, 0.004f, 0.035f), TownPalette.RoadSeam, false); // 줄눈
            }

            for (float x = -3.5f; x <= 3.5f; x += 1.0f) // 돌 줄눈 (세로)
            {
                TownKit.Box(group, "SettSeamLong", new Vector3(x, RoadTop + 0.002f, -8.5f), new Vector3(0.03f, 0.004f, 75f), TownPalette.RoadSeam, false); // 줄눈
            }

            for (int lane = -1; lane <= 1; lane += 2) // 배수로
            {
                TownKit.Box(group, "Gutter", new Vector3(lane * 3.7f, RoadTop + 0.003f, -8.5f), new Vector3(0.4f, 0.006f, 75f), TownPalette.Puddle, false); // 젖은 배수로
            }

            float walkLength = 29f - TownSouthZ; // 보도 길이
            float walkCenter = (29f + TownSouthZ) * 0.5f; // 보도 중심

            for (int side = -1; side <= 1; side += 2) // 좌우 보도
            {
                TownKit.Box(group, side < 0 ? "Sidewalk_W" : "Sidewalk_E", new Vector3(side * 5.6f, SidewalkTop * 0.5f, walkCenter), new Vector3(2.8f, SidewalkTop, walkLength), TownPalette.Flagstone); // 판석 보도
                TownKit.Box(group, side < 0 ? "Curb_W" : "Curb_E", new Vector3(side * 4.1f, (SidewalkTop + 0.02f) * 0.5f, walkCenter), new Vector3(0.22f, SidewalkTop + 0.02f, walkLength), TownPalette.DressingSoot); // 화강암 경계석

                for (float z = TownSouthZ + 0.9f; z < 29f; z += 0.9f) // 판석 가로 줄눈
                {
                    TownKit.Box(group, "FlagSeam", new Vector3(side * 5.6f, SidewalkTop + 0.002f, z), new Vector3(2.7f, 0.004f, 0.03f), TownPalette.RoadSeam, false); // 줄눈
                }

                TownKit.Box(group, "FlagSeamLong", new Vector3(side * 5.6f, SidewalkTop + 0.002f, walkCenter), new Vector3(0.03f, 0.004f, walkLength), TownPalette.RoadSeam, false); // 세로 줄눈
            }

            TownKit.Box(group, "NorthPlaza", new Vector3(0f, SidewalkTop * 0.5f, 31.5f), new Vector3(14f, SidewalkTop, 5f), TownPalette.Flagstone); // 북쪽 광장
            TownKit.Box(group, "NorthCurb", new Vector3(0f, (SidewalkTop + 0.02f) * 0.5f, 29.1f), new Vector3(8.2f, SidewalkTop + 0.02f, 0.22f), TownPalette.DressingSoot); // 도로 끝 경계석
            TownProps.Puddle(group, new Vector3(-1.8f, RoadTop, -6f), 1.4f); // 도로 웅덩이
            TownProps.Puddle(group, new Vector3(2.4f, RoadTop, 9f), 1.0f); // 도로 웅덩이
            TownProps.Puddle(group, new Vector3(-2.8f, RoadTop, 20f), 0.9f); // 도로 웅덩이
        }

        private static void BuildPerimeter(Transform town) // 벽돌 담장·남쪽 아치·건물 사이 주철 난간
        {
            Transform group = TownKit.Group(town, "Perimeter", Vector3.zero); // 묶음
            const float height = 2.6f; // 담 높이
            Material brick = TownPalette.SootBrick; // 담 벽돌
            TownProps.BrickWallRun(group, "Wall_West", new Vector3(TownWestX, 0f, TownSouthZ), new Vector3(TownWestX, 0f, TownNorthZ), height, brick); // 서쪽
            TownProps.BrickWallRun(group, "Wall_North", new Vector3(TownWestX, 0f, TownNorthZ), new Vector3(TownEastX, 0f, TownNorthZ), height, brick); // 북쪽
            TownProps.BrickWallRun(group, "Wall_East_S", new Vector3(TownEastX, 0f, TownSouthZ), new Vector3(TownEastX, 0f, AlleySouthZ - 0.25f), height, brick); // 동쪽 (골목 남쪽)
            TownProps.BrickWallRun(group, "Wall_East_N", new Vector3(TownEastX, 0f, AlleyNorthZ + 0.25f), new Vector3(TownEastX, 0f, TownNorthZ), height, brick); // 동쪽 (골목 북쪽)
            TownProps.BrickWallRun(group, "Wall_South_W", new Vector3(TownWestX, 0f, TownSouthZ), new Vector3(-7.6f, 0f, TownSouthZ), height, brick); // 남쪽 서편
            TownProps.BrickWallRun(group, "Wall_South_E", new Vector3(7.6f, 0f, TownSouthZ), new Vector3(TownEastX, 0f, TownSouthZ), height, brick); // 남쪽 동편
            TownProps.Arch(group, "Arch_TownGate", new Vector3(0f, 0f, TownSouthZ), 0f, 14.4f, 5.0f, "거점 마을", TownPalette.VictorianBrick); // 남쪽 출입 아치 (마차 통과 높이 5m)

            TownProps.IronRailing(group, "Railing_W1", new Vector3(-7.4f, 0f, 11.2f), new Vector3(-7.4f, 0f, 14.8f), 1.2f); // 서쪽 건물 사이
            TownProps.IronRailing(group, "Railing_W2", new Vector3(-7.4f, 0f, -4.8f), new Vector3(-7.4f, 0f, -1.2f), 1.2f); // 서쪽 건물 사이
            TownProps.IronRailing(group, "Railing_E", new Vector3(7.4f, 0f, -3.8f), new Vector3(7.4f, 0f, 1.8f), 1.2f); // 판매소·창고 사이
            TownProps.IronRailing(group, "Railing_E_South", new Vector3(7.4f, 0f, TownSouthZ + 0.4f), new Vector3(7.4f, 0f, -16.2f), 1.2f); // 판매소 남쪽
            TownProps.IronRailing(group, "Railing_W_South", new Vector3(-7.4f, 0f, TownSouthZ + 0.4f), new Vector3(-7.4f, 0f, -17.2f), 1.2f); // 사무소 남쪽
            TownProps.IronRailing(group, "Railing_W_North", new Vector3(-7.4f, 0f, 27.2f), new Vector3(-7.4f, 0f, TownNorthZ - 0.4f), 1.2f); // 북서
            TownProps.IronRailing(group, "Railing_E_North", new Vector3(7.4f, 0f, 27.2f), new Vector3(7.4f, 0f, TownNorthZ - 0.4f), 1.2f); // 북동
        }

        private static Dictionary<string, TownBuildingResult> BuildBuildings(Transform town) // 빅토리아 시대 연립 건물 6채
        {
            Transform group = TownKit.Group(town, "Buildings", Vector3.zero); // 묶음
            Dictionary<string, TownBuildingResult> result = new Dictionary<string, TownBuildingResult>(); // 결과
            float westX = -7f - (BuildingDepth * 0.5f); // 서쪽 건물 중심 x (앞면 x = -7)
            float eastX = 7f + (BuildingDepth * 0.5f); // 동쪽 건물 중심 x (앞면 x = 7)

            TownBuildingStyle office = new TownBuildingStyle { Brick = TownPalette.VictorianBrick, Dressing = TownPalette.Dressing, Paint = TownPalette.PaintBlack, DoorPaint = TownPalette.PaintBlack, Wallpaper = TownPalette.WallpaperGreen, Facade = TownFacade.Office, Storeys = 3, Dormers = 2, LitSeed = 3 }; // 사무소
            TownBuildingStyle sale = new TownBuildingStyle { Brick = TownPalette.StockBrick, Dressing = TownPalette.Dressing, Paint = TownPalette.PaintOxblood, DoorPaint = TownPalette.PaintOxblood, AwningA = TownPalette.AwningRed, AwningB = TownPalette.AwningCream, Wallpaper = TownPalette.Wallpaper, Facade = TownFacade.Shopfront, Storeys = 2, Dormers = 1, LitSeed = 5 }; // 판매소
            TownBuildingStyle shop = new TownBuildingStyle { Brick = TownPalette.VictorianBrick, Dressing = TownPalette.Dressing, Paint = TownPalette.PaintGreen, DoorPaint = TownPalette.PaintGreen, AwningA = TownPalette.AwningGreen, AwningB = TownPalette.AwningCream, Wallpaper = TownPalette.WallpaperGreen, Facade = TownFacade.Shopfront, Storeys = 3, Dormers = 2, LitSeed = 7 }; // 상점
            TownBuildingStyle storage = new TownBuildingStyle { Brick = TownPalette.SootBrick, Dressing = TownPalette.DressingSoot, Paint = TownPalette.PaintBlack, DoorPaint = TownPalette.PaintGreen, Wallpaper = TownPalette.Wallpaper, Facade = TownFacade.Warehouse, Storeys = 3, Dormers = 0, RoofPitch = 34f, LitSeed = 2 }; // 창고
            TownBuildingStyle vacantWest = new TownBuildingStyle { Brick = TownPalette.StockBrick, Dressing = TownPalette.DressingSoot, Paint = TownPalette.PaintGray, DoorPaint = TownPalette.PaintGray, Wallpaper = TownPalette.Wallpaper, Facade = TownFacade.Shopfront, Storeys = 2, Dormers = 1, Vacant = true, SignColor = new Color(0.8f, 0.78f, 0.7f), LitSeed = 11 }; // 빈 점포 (북서)
            TownBuildingStyle vacantEast = new TownBuildingStyle { Brick = TownPalette.VictorianBrick, Dressing = TownPalette.DressingSoot, Paint = TownPalette.PaintNavy, DoorPaint = TownPalette.PaintNavy, AwningA = TownPalette.AwningBlue, AwningB = TownPalette.AwningCream, Wallpaper = TownPalette.WallpaperGreen, Facade = TownFacade.Shopfront, Storeys = 3, Dormers = 1, Vacant = true, SignColor = new Color(0.8f, 0.78f, 0.7f), RoofPitch = 44f, LitSeed = 13 }; // 빈 점포 (북동)

            result["Town_Vacant_NW"] = TownBuilding.Build(group, "Town_Vacant_NW", new Vector3(westX, SidewalkTop, 21f), 90f, BuildingWidth, BuildingDepth, vacantWest, "빈 점포"); // 왼쪽 위
            result[ShopBuildingName] = TownBuilding.Build(group, ShopBuildingName, new Vector3(westX, SidewalkTop, 5f), 90f, BuildingWidth, BuildingDepth, shop, "상점"); // 왼쪽 가운데
            result["Town_Office"] = TownBuilding.Build(group, "Town_Office", new Vector3(westX, SidewalkTop, -11f), 90f, BuildingWidth, BuildingDepth, office, "사무소"); // 왼쪽 아래
            result["Town_Vacant_NE"] = TownBuilding.Build(group, "Town_Vacant_NE", new Vector3(eastX, SidewalkTop, 22.5f), -90f, 9f, BuildingDepth, vacantEast, "빈 점포"); // 오른쪽 위
            result["Town_Storage"] = TownBuilding.Build(group, "Town_Storage", new Vector3(eastX, SidewalkTop, 8f), -90f, BuildingWidth, BuildingDepth, storage, "보관 창고"); // 오른쪽 가운데
            result["Town_Sale"] = TownBuilding.Build(group, "Town_Sale", new Vector3(eastX, SidewalkTop, -10f), -90f, BuildingWidth, BuildingDepth, sale, "판매소"); // 오른쪽 아래

            TownBuilding.GhostSign(result["Town_Storage"], true, "화물 보관 · 운송 취급", 3.95f, 0.34f); // 골목 쪽 옆벽 광고
            TownBuilding.GhostSign(result["Town_Vacant_NE"], false, "증기 기관 수리", 3.95f, 0.34f); // 골목 쪽 옆벽 광고
            TownBuilding.GhostSign(result["Town_Office"], true, "원정대 모집", 3.95f, 0.3f); // 남쪽 옆벽 광고
            return result; // 반환
        }

        private static void BuildStreetProps(Transform town) // 가스 가로등·의자·우체통·말뚝·게시판·우물
        {
            Transform group = TownKit.Group(town, "StreetProps", Vector3.zero); // 묶음
            float walk = SidewalkTop; // 보도 높이

            foreach (float z in new[] { -26f, -13.4f, 1.8f, 16.4f }) // 서쪽 가스등 (연석 가까이)
            {
                TownProps.StreetLamp(group, "GasLamp_W", new Vector3(-4.6f, walk, z), 90f); // 가로등
            }

            foreach (float z in new[] { -19.6f, -3.2f, 10.6f, 24.4f }) // 동쪽 가스등
            {
                TownProps.StreetLamp(group, "GasLamp_E", new Vector3(4.6f, walk, z), 90f); // 가로등
            }

            foreach (float z in new[] { 13.2f, 18.8f }) // 뒷골목 입구 말뚝
            {
                TownProps.Bollard(group, new Vector3(4.6f, walk, z)); // 말뚝
            }

            foreach (float z in new[] { -28f, -30f }) // 남쪽 입구 말뚝
            {
                TownProps.Bollard(group, new Vector3(-4.6f, walk, z)); // 말뚝
                TownProps.Bollard(group, new Vector3(4.6f, walk, z)); // 말뚝
            }

            TownProps.Bench(group, new Vector3(-6.4f, walk, 13f), 90f); // 서쪽 의자
            TownProps.Bench(group, new Vector3(-6.4f, walk, -3f), 90f); // 서쪽 의자
            TownProps.Bench(group, new Vector3(6.4f, walk, -1f), -90f); // 동쪽 의자
            TownProps.PillarBox(group, new Vector3(-5.0f, walk, -18.4f), 90f); // 사무소 옆 우체통
            TownProps.NoticeBoard(group, new Vector3(-6.3f, walk, -20.6f), 90f, "공지"); // 사무소 옆 게시판
            TownProps.HitchingPost(group, new Vector3(5.9f, walk, -24f), 90f); // 마차 옆 말뚝
            TownProps.WaterTrough(group, new Vector3(6.1f, walk, -18.9f), 90f); // 말 물통
            TownProps.Barrel(group, new Vector3(6.3f, walk, -16.9f)); // 판매소 옆 통
            TownProps.Barrel(group, new Vector3(6.35f, walk, 1.4f), 0.8f); // 창고 옆 통
            TownProps.CrateStack(group, new Vector3(6.2f, walk, 0.3f), -90f); // 창고 옆 상자
            TownProps.Barrel(group, new Vector3(-6.4f, walk, 11.6f), 0.85f); // 상점 옆 통
            TownProps.Sack(group, new Vector3(-6.3f, walk, -1.6f)); // 자루
            TownProps.Pot(group, new Vector3(-6.4f, walk, -4.6f), 1f); // 화분
            TownProps.Well(group, new Vector3(0f, walk, 31.6f)); // 북쪽 우물 (펌프 광장)
            TownProps.Bench(group, new Vector3(-4.2f, walk, 32.6f), 180f); // 광장 의자
            TownProps.Bench(group, new Vector3(4.2f, walk, 32.6f), 180f); // 광장 의자
            TownProps.StreetLamp(group, "GasLamp_Plaza", new Vector3(-6.2f, walk, 30.4f), 0f); // 광장 가로등
            TownProps.Signpost(group, new Vector3(4.9f, walk, 14.3f), 0f, "훈련장"); // 뒷골목 안내 (화살표 +x = 골목)
            TownProps.Signpost(group, new Vector3(-5.8f, walk, -30.4f), -90f, "사무소"); // 입구 안내 (화살표 북쪽 +z)
        }

        private static void BuildAlley(Transform town) // 도로 → 훈련장 뒷골목 (그을린 벽돌·가스등·빨랫줄)
        {
            Transform group = TownKit.Group(town, "BackAlley", Vector3.zero); // 묶음
            float center = (AlleySouthZ + AlleyNorthZ) * 0.5f; // 골목 중심 z
            float length = TrainingEdgeX - 7f; // 길이
            TownKit.Box(group, "AlleyFloor", new Vector3(7f + (length * 0.5f), 0.015f, center), new Vector3(length, 0.03f, AlleyNorthZ - AlleySouthZ + 0.6f), TownPalette.Setts); // 돌바닥

            for (float x = 7.4f; x < TrainingEdgeX; x += 0.5f) // 바닥 줄눈
            {
                TownKit.Box(group, "AlleySeam", new Vector3(x, 0.032f, center), new Vector3(0.03f, 0.004f, AlleyNorthZ - AlleySouthZ), TownPalette.RoadSeam, false); // 줄눈
            }

            TownKit.Box(group, "AlleyDrain", new Vector3(7f + (length * 0.5f), 0.033f, center), new Vector3(length, 0.004f, 0.3f), TownPalette.Puddle, false); // 가운데 배수 홈
            TownKit.Box(group, "AlleyStep", new Vector3(7.15f, 0.08f, center), new Vector3(0.3f, 0.12f, AlleyNorthZ - AlleySouthZ - 0.4f), TownPalette.DressingSoot); // 보도에서 내려가는 턱

            TownProps.BrickWallRun(group, "AlleyWall_S", new Vector3(17.1f, 0f, AlleySouthZ - 0.25f), new Vector3(TrainingEdgeX, 0f, AlleySouthZ - 0.25f), 3.4f, TownPalette.SootBrick); // 남쪽 벽
            TownProps.BrickWallRun(group, "AlleyWall_N", new Vector3(17.1f, 0f, AlleyNorthZ + 0.25f), new Vector3(TrainingEdgeX, 0f, AlleyNorthZ + 0.25f), 3.4f, TownPalette.SootBrick); // 북쪽 벽
            TownProps.Arch(group, "Arch_Alley", new Vector3(8.2f, 0f, center), 90f, 3.3f, 3.1f, "뒷골목", TownPalette.SootBrick); // 골목 입구 아치
            TownProps.Arch(group, "Arch_TrainingGate", new Vector3(TrainingEdgeX - 0.4f, 0f, center), 90f, 3.2f, 3.4f, "훈련장", TownPalette.VictorianBrick); // 훈련장 문

            float[] lanternX = { 11f, 17f, 23f, 28f }; // 등 위치

            for (int index = 0; index < lanternX.Length; index++) // 벽에 매단 가스등
            {
                float z = index % 2 == 0 ? AlleySouthZ : AlleyNorthZ; // 번갈아
                float zOut = index % 2 == 0 ? 0.4f : -0.4f; // 벽에서 튀어나옴
                TownKit.Box(group, "LanternBracket", new Vector3(lanternX[index], 2.85f, z + (zOut * 0.5f)), new Vector3(0.04f, 0.04f, 0.42f), TownPalette.CastIron, false); // 받침
                TownKit.Box(group, "LanternScroll", new Vector3(lanternX[index], 2.7f, z + (zOut * 0.25f)), new Vector3(0.03f, 0.03f, 0.3f), TownPalette.CastIron, false, new Vector3(zOut > 0 ? -40f : 40f, 0f, 0f)); // 받침살
                TownProps.HangingLantern(group, new Vector3(lanternX[index], 2.85f, z + zOut), 0.2f); // 등
            }

            TownProps.LaundryLine(group, new Vector3(12.5f, 3.6f, AlleySouthZ + 0.15f), new Vector3(13.5f, 3.6f, AlleyNorthZ - 0.15f)); // 빨랫줄
            TownProps.LaundryLine(group, new Vector3(24.5f, 3.3f, AlleySouthZ + 0.15f), new Vector3(25.2f, 3.3f, AlleyNorthZ - 0.15f)); // 빨랫줄
            TownProps.Crate(group, new Vector3(9.2f, 0.03f, AlleySouthZ + 0.55f), 0.6f, 12f); // 상자
            TownProps.Barrel(group, new Vector3(10.2f, 0.03f, AlleySouthZ + 0.5f), 0.8f); // 통
            TownProps.CrateStack(group, new Vector3(14.2f, 0.03f, AlleyNorthZ - 0.6f), 180f); // 상자 더미
            TownProps.Sack(group, new Vector3(19.5f, 0.03f, AlleySouthZ + 0.45f)); // 석탄 자루
            TownProps.Sack(group, new Vector3(19.9f, 0.03f, AlleySouthZ + 0.5f)); // 석탄 자루
            TownKit.Box(group, "CoalHeap", new Vector3(20.8f, 0.12f, AlleySouthZ + 0.45f), new Vector3(0.9f, 0.24f, 0.6f), TownPalette.Soot, false, new Vector3(0f, 20f, 0f)); // 석탄 무더기
            TownProps.Barrel(group, new Vector3(21.8f, 0.03f, AlleyNorthZ - 0.5f), 0.9f); // 통
            TownProps.Barrel(group, new Vector3(22.5f, 0.03f, AlleyNorthZ - 0.45f), 0.75f); // 통
            TownProps.Crate(group, new Vector3(27.4f, 0.03f, AlleySouthZ + 0.55f), 0.55f, -20f); // 상자
            TownProps.WallPipe(group, new Vector3(18.4f, 0.03f, AlleySouthZ + 0.08f), 3.3f, 1f); // 배수관
            TownProps.WallPipe(group, new Vector3(26.2f, 0.03f, AlleyNorthZ - 0.08f), 3.3f, -1f); // 배수관
            TownProps.Puddle(group, new Vector3(15.5f, 0.03f, center + 0.4f), 1.1f); // 웅덩이
            TownProps.Puddle(group, new Vector3(22.8f, 0.03f, center - 0.5f), 0.8f); // 웅덩이
            TownKit.Box(group, "Debris_1", new Vector3(16.2f, 0.06f, AlleyNorthZ - 0.4f), new Vector3(0.4f, 0.06f, 0.25f), TownPalette.WoodDark, false, new Vector3(0f, 30f, 0f)); // 잔해
            TownKit.Box(group, "Debris_2", new Vector3(24f, 0.05f, AlleySouthZ + 0.35f), new Vector3(0.3f, 0.04f, 0.5f), TownPalette.Paper, false, new Vector3(0f, -20f, 0f)); // 신문지
            TownKit.Box(group, "Plank_Lean", new Vector3(29.6f, 0.9f, AlleyNorthZ - 0.25f), new Vector3(0.2f, 1.9f, 0.04f), TownPalette.WoodLight, false, new Vector3(-12f, 0f, 0f)); // 기댄 널빤지
            TownKit.Box(group, "Poster_1", new Vector3(20f, 1.8f, AlleyNorthZ - 0.005f), new Vector3(0.6f, 0.85f, 0.01f), TownPalette.Paper, false, new Vector3(0f, 0f, 3f)); // 벽보
            TownKit.Label(group, "PosterText_1", "현상 수배", new Vector3(20f, 2.05f, AlleyNorthZ - 0.012f), 0f, 0.1f, new Color(0.2f, 0.1f, 0.06f)); // 벽보 글씨 (골목 쪽에서 읽힘)
            TownKit.Box(group, "Poster_2", new Vector3(25.5f, 1.7f, AlleySouthZ + 0.005f), new Vector3(0.55f, 0.75f, 0.01f), TownPalette.ClothCream, false, new Vector3(0f, 0f, -4f)); // 벽보
            TownKit.Label(group, "PosterText_2", "훈련생 모집", new Vector3(25.5f, 1.95f, AlleySouthZ + 0.012f), 180f, 0.09f, new Color(0.2f, 0.1f, 0.06f)); // 벽보 글씨
        }

        private static void BuildOutskirts(Transform town) // 마을 밖 공장·굴뚝·나무·진입로 난간
        {
            Transform group = TownKit.Group(town, "Outskirts", Vector3.zero); // 묶음
            TownKit.Box(group, "Yard_East_S", new Vector3(25.8f, 0.005f, -9.3f), new Vector3(12f, 0.01f, 45f), TownPalette.Ground, false); // 동쪽 남 공터
            TownKit.Box(group, "Yard_East_N", new Vector3(25.8f, 0.005f, 27.5f), new Vector3(12f, 0.01f, 18f), TownPalette.Soot, false); // 공장 마당 (석탄재)
            TownKit.Box(group, "Grass_West", new Vector3(-22.5f, 0.005f, 0f), new Vector3(7f, 0.01f, 70f), TownPalette.Grass, false); // 서쪽 풀밭

            TownProps.FactoryHall(group, new Vector3(25.8f, 0f, 27.3f), 180f, 10f, 15f, 8.5f); // 북동 제철소 (골목 북쪽)
            TownProps.Smokestack(group, new Vector3(27f, 0f, 10f), 26f); // 제철소 굴뚝 (골목 남쪽 공터)
            TownProps.Smokestack(group, new Vector3(-23f, 0f, -6f), 22f); // 서쪽 굴뚝
            TownProps.Smokestack(group, new Vector3(25f, 0f, -24f), 18f); // 남동 굴뚝

            foreach (Vector3 position in new[] { new Vector3(-22.5f, 0f, -26f), new Vector3(-22f, 0f, 8f), new Vector3(-23f, 0f, 20f), new Vector3(-22.4f, 0f, 30f) }) // 서쪽 나무 (드문드문)
            {
                TownProps.Tree(group, position, 1.05f); // 나무
            }

            foreach (Vector3 position in new[] { new Vector3(28f, 0f, -14f), new Vector3(23.5f, 0f, -4f) }) // 동쪽 나무
            {
                TownProps.Tree(group, position, 0.9f); // 나무
            }

            foreach (Vector3 position in new[] { new Vector3(-10f, 0f, -36f), new Vector3(10f, 0f, -38f), new Vector3(-12f, 0f, -43f), new Vector3(12f, 0f, -44f) }) // 남쪽 나무
            {
                TownProps.Tree(group, position, 0.95f); // 나무
            }

            TownProps.IronRailing(group, "RoadRailing_W", new Vector3(-4.6f, 0f, -45.5f), new Vector3(-4.6f, 0f, TownSouthZ - 0.6f), 1.1f); // 진입로 난간
            TownProps.IronRailing(group, "RoadRailing_E", new Vector3(4.6f, 0f, -45.5f), new Vector3(4.6f, 0f, TownSouthZ - 0.6f), 1.1f); // 진입로 난간
            TownProps.StreetLamp(group, "GasLamp_Road", new Vector3(-5.4f, 0f, -38f), 0f); // 진입로 가로등
            TownProps.Bush(group, new Vector3(-6f, 0f, -34f), 1.2f); // 덤불
            TownProps.Bush(group, new Vector3(6.5f, 0f, -35f), 1.1f); // 덤불
            TownProps.Bush(group, new Vector3(-16f, 0f, -34.5f), 1.3f); // 덤불
            TownProps.Bush(group, new Vector3(15f, 0f, -34f), 1.2f); // 덤불
            TownKit.Box(group, "EdgeBerm_S", new Vector3(3f, 0.4f, -46.5f), new Vector3(58f, 1.3f, 1f), TownPalette.Grass); // 남쪽 끝 둔덕
            TownKit.Box(group, "EdgeBerm_W", new Vector3(-26.5f, 0.4f, -5f), new Vector3(1f, 1.3f, 82f), TownPalette.Grass); // 서쪽 끝 둔덕
            TownKit.Box(group, "EdgeBerm_N", new Vector3(3f, 0.4f, 36.5f), new Vector3(58f, 1.3f, 1f), TownPalette.Grass); // 북쪽 끝 둔덕
        }

        private static void FurnishOffice(TownBuildingResult office) // 사무소 가구 (벽난로·책장·지도)
        {
            Transform room = TownKit.Group(office.Interior, "Furniture", Vector3.zero); // 묶음
            TownProps.Chair(room, new Vector3(1.6f, 0f, office.InnerBackZ + 0.3f), 0f); // 마감 장부 의자
            TownProps.Bookshelf(room, new Vector3(office.InnerSideX - 0.22f, 0f, -2.4f), -90f, 2.2f); // 책장
            TownProps.Cabinet(room, new Vector3(office.InnerSideX - 0.28f, 0f, 1.7f), -90f); // 서류함
            TownProps.WallMap(room, new Vector3(-office.InnerSideX + 0.03f, 1.9f, -2.4f), 90f); // 벽 지도
            Fireplace(room, new Vector3(-office.InnerSideX, 0f, 2.4f), 90f); // 벽난로
            TownProps.Rug(room, new Vector3(0f, 0f, 0.4f), new Vector2(4f, 2.6f), TownPalette.ClothRed); // 깔개
            TownProps.Table(room, new Vector3(-3.2f, 0f, 1.2f), new Vector3(1.6f, 0.78f, 0.9f), 0f); // 회의 탁자
            TownProps.Chair(room, new Vector3(-3.2f, 0f, 0.4f), 0f); // 의자
            TownProps.Chair(room, new Vector3(-3.2f, 0f, 2.0f), 180f); // 의자
            TownProps.Candle(room, new Vector3(-3.4f, 0.78f, 1.2f)); // 촛대
            TownProps.Pot(room, new Vector3(office.InnerSideX - 0.4f, 0f, office.InnerFrontZ - 0.4f), 0.9f); // 화분
            TownKit.Label(room, "Plaque", "원정 사무소", new Vector3(-0.3f, 2.3f, office.InnerBackZ + 0.03f), 180f, 0.22f, new Color(0.9f, 0.76f, 0.4f)); // 뒷벽 명패
        }

        private static void FurnishSale(TownBuildingResult sale) // 판매소 가구
        {
            Transform room = TownKit.Group(sale.Interior, "Furniture", Vector3.zero); // 묶음
            TownProps.WallShelf(room, new Vector3(-1f, 1.3f, sale.InnerBackZ + 0.16f), 0f, 3f); // 벽 선반
            TownProps.CrateStack(room, new Vector3(3.9f, 0f, sale.InnerBackZ + 0.6f), 0f); // 상자 더미
            TownProps.Barrel(room, new Vector3(-4.8f, 0f, sale.InnerBackZ + 0.5f)); // 통
            TownProps.Barrel(room, new Vector3(-4.1f, 0f, sale.InnerBackZ + 0.45f), 0.8f); // 통
            TownProps.Table(room, new Vector3(4.2f, 0f, 1.6f), new Vector3(1.1f, 0.8f, 0.6f), -90f); // 옆 탁자
            TownProps.Scale(room, new Vector3(4.2f, 0.8f, 1.6f)); // 저울
            TownKit.Box(room, "PriceBoard", new Vector3(-sale.InnerSideX + 0.06f, 1.9f, 2.6f), new Vector3(0.05f, 1.0f, 1.6f), TownPalette.PaintBlack, false); // 매입 안내판
            TownKit.Label(room, "PriceBoardText", "회수품 매입", new Vector3(-sale.InnerSideX + 0.1f, 1.9f, 2.6f), 270f, 0.2f, new Color(0.95f, 0.9f, 0.75f)); // 안내판 글자 (+x 쪽에서 읽힘)
            TownProps.Rug(room, new Vector3(-0.9f, 0f, 2.6f), new Vector2(2.6f, 1.6f), TownPalette.ClothBlue); // 깔개
        }

        private static void FurnishShop(TownBuildingResult shop) // 상점 가구
        {
            Transform room = TownKit.Group(shop.Interior, "Furniture", Vector3.zero); // 묶음
            TownProps.Table(room, new Vector3(3.9f, 0f, 1.6f), new Vector3(1.8f, 1.0f, 0.7f), -90f); // 계산대
            TownProps.Scale(room, new Vector3(3.9f, 1.0f, 1.2f)); // 저울
            TownProps.Candle(room, new Vector3(3.9f, 1.0f, 2.1f)); // 촛대
            TownProps.Barrel(room, new Vector3(-4.8f, 0f, -0.8f)); // 통
            TownProps.Barrel(room, new Vector3(-4.8f, 0f, 0.1f), 0.8f); // 통
            TownProps.Sack(room, new Vector3(-4.2f, 0f, -0.4f)); // 자루
            TownProps.WallShelf(room, new Vector3(-shop.InnerSideX + 0.18f, 1.4f, 2.4f), 90f, 2.2f); // 벽 선반
            TownProps.Rug(room, new Vector3(-1f, 0f, 1.2f), new Vector2(3.2f, 2f), TownPalette.ClothGreen); // 깔개
            TownProps.Pot(room, new Vector3(shop.InnerSideX - 0.4f, 0f, shop.InnerFrontZ - 0.4f), 0.9f); // 화분
        }

        private static void FurnishStorage(TownBuildingResult storage) // 창고 가구
        {
            Transform room = TownKit.Group(storage.Interior, "Furniture", Vector3.zero); // 묶음
            TownProps.Rack(room, new Vector3(-3.3f, 0f, storage.InnerBackZ + 0.35f), 0f, 2.2f); // 랙
            TownProps.Rack(room, new Vector3(3.3f, 0f, storage.InnerBackZ + 0.35f), 0f, 2.2f); // 랙
            TownProps.CrateStack(room, new Vector3(-storage.InnerSideX + 0.5f, 0f, storage.InnerFrontZ - 0.95f), 90f); // 상자 더미
            TownProps.CrateStack(room, new Vector3(storage.InnerSideX - 0.5f, 0f, storage.InnerFrontZ - 0.95f), -90f); // 상자 더미
            TownProps.Barrel(room, new Vector3(-storage.InnerSideX + 0.4f, 0f, -0.6f)); // 통
            TownKit.Box(room, "NoticeBoard", new Vector3(0f, 2.9f, storage.InnerBackZ + 0.03f), new Vector3(2.6f, 0.45f, 0.03f), TownPalette.PaintBlack, false); // 안내판
            TownKit.Label(room, "Notice", "보관 가치 1000 미만", new Vector3(0f, 2.9f, storage.InnerBackZ + 0.05f), 180f, 0.2f, new Color(0.95f, 0.9f, 0.75f)); // 안내
        }

        private static void FurnishVacant(TownBuildingResult vacant) // 빈 점포 (비어 있지만 들어갈 수 있음)
        {
            Transform room = TownKit.Group(vacant.Interior, "Furniture", Vector3.zero); // 묶음
            TownKit.Box(room, "DustSheet", new Vector3(-vacant.InnerSideX + 1.1f, 0.45f, vacant.InnerBackZ + 0.9f), new Vector3(1.6f, 0.9f, 0.9f), TownPalette.ClothCream); // 천 덮인 가구
            TownKit.Box(room, "DustSheet_Drape", new Vector3(-vacant.InnerSideX + 1.1f, 0.25f, vacant.InnerBackZ + 1.37f), new Vector3(1.66f, 0.5f, 0.04f), TownPalette.ClothCream, false); // 늘어진 천
            TownProps.Crate(room, new Vector3(vacant.InnerSideX - 0.6f, 0f, vacant.InnerBackZ + 0.6f), 0.7f, 8f); // 상자
            TownProps.Crate(room, new Vector3(vacant.InnerSideX - 0.6f, 0.7f, vacant.InnerBackZ + 0.6f), 0.5f, -14f); // 상자
            TownKit.Box(room, "Ladder_L", new Vector3(1.2f, 1.2f, vacant.InnerBackZ + 0.35f), new Vector3(0.05f, 2.4f, 0.05f), TownPalette.WoodLight, false, new Vector3(-12f, 0f, 0f)); // 사다리
            TownKit.Box(room, "Ladder_R", new Vector3(1.6f, 1.2f, vacant.InnerBackZ + 0.35f), new Vector3(0.05f, 2.4f, 0.05f), TownPalette.WoodLight, false, new Vector3(-12f, 0f, 0f)); // 사다리

            for (int rung = 0; rung < 6; rung++) // 사다리 가로대
            {
                float y = 0.3f + (rung * 0.38f); // 높이
                TownKit.Box(room, "Rung", new Vector3(1.4f, y, vacant.InnerBackZ + 0.35f + (Mathf.Tan(12f * Mathf.Deg2Rad) * (y - 1.2f))), new Vector3(0.4f, 0.03f, 0.03f), TownPalette.WoodLight, false); // 가로대
            }

            TownKit.Cylinder(room, "PaintTin", new Vector3(0.6f, 0.12f, vacant.InnerBackZ + 0.5f), 0.22f, 0.24f, TownPalette.PaintGray); // 페인트 통
            TownKit.Box(room, "Newspapers", new Vector3(-0.8f, 0.01f, 0.6f), new Vector3(0.7f, 0.02f, 0.5f), TownPalette.Paper, false, new Vector3(0f, 17f, 0f)); // 바닥 신문
            TownKit.Box(room, "ToLetBoard", new Vector3(0f, 2.2f, vacant.InnerBackZ + 0.03f), new Vector3(1.6f, 0.5f, 0.03f), TownPalette.Paper, false); // 안내문
            TownKit.Label(room, "ToLetText", "임대 문의 · 사무소", new Vector3(0f, 2.2f, vacant.InnerBackZ + 0.05f), 180f, 0.14f, new Color(0.2f, 0.1f, 0.05f)); // 글씨
        }

        private static void Fireplace(Transform room, Vector3 wallPosition, float yaw) // 벽난로 (앞면 = 로컬 +z)
        {
            Transform fire = TownKit.Group(room, "Fireplace", wallPosition, yaw); // 묶음
            TownKit.Box(fire, "Surround", new Vector3(0f, 0.6f, 0.15f), new Vector3(1.6f, 1.2f, 0.3f), TownPalette.PaintBlack); // 틀
            TownKit.Box(fire, "Firebox", new Vector3(0f, 0.42f, 0.29f), new Vector3(0.8f, 0.7f, 0.04f), TownPalette.Soot, false); // 화구
            TownKit.Box(fire, "Grate", new Vector3(0f, 0.2f, 0.32f), new Vector3(0.6f, 0.12f, 0.08f), TownPalette.CastIron, false); // 쇠살대
            TownKit.Sphere(fire, "Embers", new Vector3(0f, 0.28f, 0.3f), new Vector3(0.4f, 0.12f, 0.12f), TownPalette.WindowLit); // 불씨
            TownKit.Box(fire, "Mantel", new Vector3(0f, 1.24f, 0.2f), new Vector3(1.9f, 0.08f, 0.42f), TownPalette.Dressing, false); // 선반
            TownKit.Box(fire, "Hearth", new Vector3(0f, 0.02f, 0.5f), new Vector3(1.7f, 0.04f, 0.5f), TownPalette.DressingSoot, false); // 앞 바닥
            TownKit.Box(fire, "Clock", new Vector3(0f, 1.45f, 0.2f), new Vector3(0.3f, 0.34f, 0.14f), TownPalette.WoodDark, false); // 시계
            TownKit.Cylinder(fire, "ClockFace", new Vector3(0f, 1.48f, 0.28f), 0.18f, 0.01f, TownPalette.PaintCream, false, new Vector3(90f, 0f, 0f)); // 문자판
            TownProps.Candle(fire, new Vector3(-0.7f, 1.28f, 0.2f)); // 촛대
            TownProps.Candle(fire, new Vector3(0.7f, 1.28f, 0.2f)); // 촛대
            TownKit.Box(fire, "Mirror", new Vector3(0f, 2.0f, 0.02f), new Vector3(1.1f, 0.8f, 0.03f), TownPalette.WindowDark, false); // 거울
            TownKit.Box(fire, "MirrorFrame", new Vector3(0f, 2.0f, 0.01f), new Vector3(1.24f, 0.94f, 0.02f), TownPalette.Gold, false); // 거울 틀
        }

        private static void ConfigureWagonStop(MapTravelAnchor anchor, Transform town) // 마차 정차·진입·시작 위치
        {
            Transform root = anchor.transform; // 정차 지점 루트
            root.position = town.TransformPoint(new Vector3(0f, RoadTop, WagonStopZ)); // 사무소 앞 도로
            root.rotation = Quaternion.identity; // 북쪽(+z) 방향
            Transform stop = anchor.StopPoint; // 정차 지점
            Transform entry = anchor.EntryPoint; // 진입 지점
            stop.localPosition = Vector3.zero; // 정차
            stop.localRotation = Quaternion.identity; // 방향
            entry.localPosition = new Vector3(0f, 0f, -WagonEntryDistance); // 남쪽에서 진입
            entry.localRotation = Quaternion.identity; // 방향
            Transform spawn = root.Find("PlayerSpawnPoint"); // 시작 위치

            if (spawn == null) // 생성
            {
                spawn = new GameObject("PlayerSpawnPoint").transform; // 오브젝트
                spawn.SetParent(root, false); // 부모
            }

            float officeFrontX = -7f; // 사무소 앞면 x
            spawn.position = town.TransformPoint(new Vector3(officeFrontX + 1.4f, SidewalkTop + 0.05f, -11f)); // 사무소 문 앞 보도
            spawn.rotation = Quaternion.Euler(0f, -90f, 0f); // 사무소 문을 바라봄 (-x)
            anchor.ConfigurePlayerSpawn(spawn); // 연결
            EditorUtility.SetDirty(anchor); // 저장 대상
        }

        private static void SplitTrainingBoundary(Scene scene) // 훈련장 서쪽 벽에 뒷골목 출입구
        {
            GameObject testMap = scene.GetRootGameObjects().FirstOrDefault(r => r.name == TestMapRootName); // 훈련장 루트

            if (testMap == null) // 없음
            {
                Debug.LogWarning("[Project I] 훈련장 루트를 찾지 못해 출입구를 만들지 못했습니다."); // 경고
                return; // 종료
            }

            Transform south = testMap.transform.Find("Boundary_West"); // 기존 서쪽 벽

            if (south == null) // 없음
            {
                Debug.LogWarning("[Project I] Boundary_West 를 찾지 못했습니다."); // 경고
                return; // 종료
            }

            Transform north = testMap.transform.Find("Boundary_West_North"); // 북쪽 조각

            if (north == null) // 생성
            {
                north = Object.Instantiate(south.gameObject, testMap.transform).transform; // 복제
                north.name = "Boundary_West_North"; // 이름
            }

            float x = TownOrigin.x + TrainingEdgeX; // 벽 x (-40)
            south.localPosition = new Vector3(x, 2f, (-30f + AlleySouthZ) * 0.5f); // 남쪽 조각 (z -30 ~ 14)
            south.localScale = new Vector3(0.5f, 4f, AlleySouthZ + 30f); // 크기
            north.localPosition = new Vector3(x, 2f, (AlleyNorthZ + 30f) * 0.5f); // 북쪽 조각 (z 18 ~ 30)
            north.localScale = new Vector3(0.5f, 4f, 30f - AlleyNorthZ); // 크기
            EditorUtility.SetDirty(south); // 저장 대상
            EditorUtility.SetDirty(north); // 저장 대상
        }

        private static string Validate(Transform town, CampaignEconomy economy, OfficeSaleCounter counter, OfficeSaleBell bell, OfficeShopShelf shelf, DebtLedger debtLedger, DayEndLedgerInteractable dayEndLedger, List<OfficeStoragePedestal> pedestals, MapTravelAnchor anchor) // 배치 검사 보고
        {
            List<string> lines = new List<string>(); // 보고
            List<string> warnings = new List<string>(); // 경고
            lines.Add($"사무소: 채무 장부 {debtLedger.transform.position:F1} · 하루 마감 {dayEndLedger.transform.position:F1} · 공동 자금 {economy.transform.parent?.parent?.name}"); // 사무소
            lines.Add($"판매소: 판매대 {counter.transform.position:F1} · 벨 {bell.transform.position:F1}"); // 판매소
            lines.Add($"상점: 진열대 {shelf.transform.position:F1} · 수령대 {(shelf.Tray == null ? "없음" : shelf.Tray.transform.position.ToString("F1"))}"); // 상점
            lines.Add($"보관 창고: 단상 {pedestals.Count}개"); // 창고
            lines.Add($"마차 정차 {anchor.StopPoint.position:F1} · 진입 {anchor.EntryPoint.position:F1} · 시작 위치 {(anchor.PlayerSpawnPoint == null ? "없음" : anchor.PlayerSpawnPoint.position.ToString("F1"))}"); // 마차

            List<Transform> functional = new List<Transform> { counter.transform, bell.transform, shelf.transform, debtLedger.transform, dayEndLedger.transform }; // 기능 요소
            functional.AddRange(pedestals.Select(p => p.transform)); // 단상

            if (shelf.Tray != null) // 수령대
            {
                functional.Add(shelf.Tray.transform); // 추가
            }

            foreach (Transform item in functional) // 벽·가구와 겹침 검사
            {
                Bounds bounds = WorldBounds(item); // 범위

                foreach (Collider hit in Physics.OverlapBox(bounds.center + (Vector3.up * 0.05f), (bounds.extents * 0.9f) - (Vector3.up * 0.05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) // 겹침
                {
                    if (!hit.transform.IsChildOf(item) && hit.name != "Floor" && !item.IsChildOf(hit.transform)) // 자기 자신·바닥 제외
                    {
                        warnings.Add($"{item.name} ↔ {hit.transform.parent?.name}/{hit.name}"); // 경고
                    }
                }
            }

            Vector3 stop = anchor.StopPoint.position; // 정차
            bool roadBelow = Physics.Raycast(stop + (Vector3.up * 2f), Vector3.down, out RaycastHit roadHit, 5f, ~0, QueryTriggerInteraction.Ignore) && roadHit.collider.name == "Road"; // 도로 확인
            bool spawnGround = anchor.PlayerSpawnPoint != null && Physics.Raycast(anchor.PlayerSpawnPoint.position + Vector3.up, Vector3.down, out RaycastHit spawnHit, 3f, ~0, QueryTriggerInteraction.Ignore) && spawnHit.point.y <= anchor.PlayerSpawnPoint.position.y + 0.01f; // 시작 위치 바닥
            lines.Add($"정차 지점 아래 도로 {roadBelow} · 시작 위치 바닥 {spawnGround}"); // 바닥
            lines.Add($"조명 {town.GetComponentsInChildren<Light>(true).Length}개 · 실내 영역 {town.GetComponentsInChildren<ProjectI.Brightness.IndoorBrightnessArea>(true).Length}개 · 렌더러 {town.GetComponentsInChildren<Renderer>(true).Length}개"); // 규모

            if (warnings.Count > 0) // 겹침 경고
            {
                Debug.LogWarning($"[Project I] 34일차 배치 겹침 {warnings.Count}건\n{string.Join("\n", warnings.Distinct().Take(30))}"); // 경고
            }

            if (!roadBelow || !spawnGround) // 바닥 경고
            {
                Debug.LogWarning($"[Project I] 34일차 마차 정차·시작 위치 바닥 확인 필요 / 도로 {roadBelow} · 시작 위치 {spawnGround}"); // 경고
            }

            return string.Join("\n", lines); // 보고
        }

        private static Bounds WorldBounds(Transform item) // 렌더러·충돌체 범위
        {
            bool has = false; // 초기화
            Bounds bounds = new Bounds(item.position, Vector3.zero); // 결과

            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true)) // 충돌체 기준
            {
                if (collider.isTrigger) // 트리거 제외
                {
                    continue; // 다음
                }

                if (!has) // 첫 범위
                {
                    bounds = collider.bounds; // 시작
                    has = true; // 표시
                }
                else // 확장
                {
                    bounds.Encapsulate(collider.bounds); // 포함
                }
            }

            return bounds; // 반환
        }
    }
}
