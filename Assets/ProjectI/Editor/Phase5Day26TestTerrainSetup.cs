using System.Collections.Generic; // 배치 목록 기능 참조
using System.IO; // 폴더·파일 경로 기능 참조
using ProjectI.Economy; // 회수품 가격 참조
using ProjectI.Items; // ItemDefinition·WorldItem 참조
using ProjectI.Loop; // 이동 지점·일차 마감 장부 참조
using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // 씬 편집·저장 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 도구 네임스페이스
{
    public static class Phase5Day26TestTerrainSetup // 26일차 Terrain 테스트 던전·일차 마감 장부 구성 도구 (메뉴 실행 전용)
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // Persistent 씬 경로
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity"; // 사무소 씬 경로
        private const string TestDungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 씬 경로
        private const string GeneratedFolder = "Assets/ProjectI/Art/Generated/Day26"; // 26일차 생성 에셋 폴더
        private const string DefinitionFolder = "Assets/ProjectI/Resources/Day24Recovery/Definitions"; // ItemDefinition 폴더
        private const string LedgerName = "Day26_DayEndLedger"; // 일차 마감 장부 오브젝트 이름
        private const float TerrainSize = 64f; // Terrain 가로·세로 크기
        private const float TerrainHeight = 16f; // Terrain 최대 높이
        private const float BaseHeight = 3f; // 평지 기준 높이 (Terrain 바닥에서)
        private const int HeightResolution = 129; // 높이맵 해상도 (0.5m 간격)
        private const int AlphaResolution = 128; // 지면 재질 해상도
        private static readonly Vector3 TerrainOrigin = new Vector3(-32f, -BaseHeight, -44f); // 평지 표면이 y=0이 되도록 Terrain 배치
        private static readonly Vector3 StopPosition = Vector3.zero; // 마차 정차 지점 (Day24와 동일)
        private static readonly Vector3 EntryPosition = new Vector3(0f, 0f, -18f); // 마차 진입 지점 (Day24와 동일)

        private static readonly ItemSpawn[] ItemSpawns = // 테스트 회수품 배치 목록 (ItemId, 가치, 위치)
        {
            new ItemSpawn("recoverable.silver_coin", 250, new Vector2(5.5f, 3f)), // 마차 옆 가까운 은화
            new ItemSpawn("recoverable.silver_coin", 400, new Vector2(-6f, 7f)), // 마차 앞 왼쪽 은화
            new ItemSpawn("recoverable.artisan_metal_ornament", 650, new Vector2(11f, 12f)), // 앞쪽 오른편 금속 장식
            new ItemSpawn("recoverable.silver_coin", 300, new Vector2(-17f, -6f)), // 왼쪽 바위 뒤 은화
            new ItemSpawn("recoverable.artisan_metal_ornament", 900, new Vector2(-15f, 13f)), // 왼쪽 언덕 위 금속 장식
            new ItemSpawn("recoverable.gods_statue", 1500, new Vector2(18f, -8f)), // 오른쪽 움푹한 곳 조각상
            new ItemSpawn("recoverable.crown", 2250, new Vector2(2f, 15f)) // 가장 안쪽 왕관
        };

        [MenuItem("Tools/Project I/Day 26/Build Terrain Test Dungeon + Day-End Ledger")] // 메뉴 등록
        public static void Apply() // 26일차 테스트 환경 전체 구성
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) // Play Mode 여부 확인
            {
                Debug.LogWarning("[Project I] Play Mode에서는 26일차 구성을 실행할 수 없습니다."); // 안내
                return; // 실행 중단
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 변경 보호
            {
                return; // 사용자가 취소하면 중단
            }

            EnsureFolder(GeneratedFolder); // 생성 에셋 폴더 확보
            BuildTerrainTestDungeon(); // Terrain 테스트 던전 생성
            AddDayEndLedgerToOffice(); // 사무소 일차 마감 장부 배치
            MovePersistentUi(); // 빠른 슬롯 HUD·F1 디버그 창을 Persistent로 이동
            SetPlayModeStartScene(); // Play 시작 씬을 Persistent로 지정
            AssetDatabase.SaveAssets(); // 에셋 저장
            EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // 테스트 시작 씬 열기
            Debug.Log("[Project I] 26일차 Terrain 테스트 던전·일차 마감 장부 구성 완료"); // 완료 로그
        }

        public static void ApplyFromCommandLine() // 배치 모드 실행 진입점
        {
            ValidateItemDefinitions(); // 고정 ItemId 검증 결과 기록
            EnsureFolder(GeneratedFolder); // 생성 에셋 폴더 확보
            BuildTerrainTestDungeon(); // Terrain 테스트 던전 생성
            AddDayEndLedgerToOffice(); // 사무소 일차 마감 장부 배치
            MovePersistentUi(); // 빠른 슬롯 HUD·F1 디버그 창을 Persistent로 이동
            SetPlayModeStartScene(); // Play 시작 씬 지정
            AssetDatabase.SaveAssets(); // 에셋 저장
            Debug.Log("[Project I] 26일차 구성 배치 실행 완료"); // 완료 로그
        }

        [MenuItem("Tools/Project I/Day 26/Move Player HUD + F1 Debug UI to Persistent")] // 전역 UI를 Persistent 씬으로 이동하는 메뉴
        public static void MovePersistentUi() // 빠른 슬롯 HUD·F1 디버그 창·EventSystem을 00_WagonPersistent로 이동하고 연결
        {
            if (Application.isBatchMode == false && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 씬 변경 보호
            {
                return; // 사용자가 취소하면 중단
            }

            Scene persistent = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // Persistent 씬 열기
            Scene office = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Additive); // 사무소 씬 함께 열기
            MoveRootToPersistent(office, persistent, "PlayerHUDCanvas"); // 빠른 슬롯 HUD Canvas 이동
            MoveRootToPersistent(office, persistent, "DebugPageCanvas"); // F1 디버그 창 이동
            GameObject officeEventSystem = FindRoot(office, root => root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null); // 사무소 EventSystem 조회
            GameObject persistentEventSystem = FindRoot(persistent, root => root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null); // Persistent EventSystem 조회

            if (officeEventSystem != null && persistentEventSystem == null) // Persistent에 EventSystem이 없는지 확인
            {
                SceneManager.MoveGameObjectToScene(officeEventSystem, persistent); // UI 입력 처리기를 Persistent로 이동
            }
            else if (officeEventSystem != null) // 양쪽에 모두 있는 경우
            {
                Object.DestroyImmediate(officeEventSystem); // 중복 EventSystem 제거
            }

            WireQuickSlotHud(persistent); // 플레이어 QuickSlotHud와 Canvas 슬롯 연결
            EditorSceneManager.MarkSceneDirty(persistent); // 변경 표시
            EditorSceneManager.MarkSceneDirty(office); // 변경 표시
            EditorSceneManager.SaveScene(persistent, PersistentScenePath); // Persistent 씬 저장
            EditorSceneManager.SaveScene(office, OfficeScenePath); // 사무소 씬 저장
            EditorSceneManager.CloseScene(office, true); // 사무소 씬 닫기
            Debug.Log("[Project I] 빠른 슬롯 HUD·F1 디버그 창을 00_WagonPersistent로 이동 완료"); // 결과 로그
        }

        public static void MovePersistentUiFromCommandLine() // 배치 모드 실행 진입점
        {
            MovePersistentUi(); // 전역 UI 이동
            AssetDatabase.SaveAssets(); // 에셋 저장
        }

        private static void MoveRootToPersistent(Scene office, Scene persistent, string rootName) // 사무소 루트 오브젝트를 Persistent로 이동 (이미 있으면 사무소 쪽 제거)
        {
            GameObject source = FindRoot(office, root => root.name == rootName); // 사무소 루트 조회

            if (source == null) // 사무소에 없는지 확인
            {
                return; // 이미 이동된 상태
            }

            if (FindRoot(persistent, root => root.name == rootName) != null) // Persistent에 이미 있는지 확인
            {
                Object.DestroyImmediate(source); // 중복 사무소 UI 제거
                return; // 종료
            }

            SceneManager.MoveGameObjectToScene(source, persistent); // 동일 오브젝트를 Persistent 씬으로 이동
        }

        private static GameObject FindRoot(Scene scene, System.Func<GameObject, bool> predicate) // 씬 루트 오브젝트 조회
        {
            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (predicate(root)) // 조건 확인
                {
                    return root; // 첫 일치 반환
                }
            }

            return null; // 없음
        }

        private static void WireQuickSlotHud(Scene persistent) // Persistent 플레이어 QuickSlotHud에 슬롯 UI 참조 연결
        {
            GameObject canvas = FindRoot(persistent, root => root.name == "PlayerHUDCanvas"); // HUD Canvas 조회
            QuickSlotHud hud = null; // 플레이어 HUD 컴포넌트

            foreach (GameObject root in persistent.GetRootGameObjects()) // 루트 순회
            {
                hud = hud != null ? hud : root.GetComponentInChildren<QuickSlotHud>(true); // 첫 QuickSlotHud 조회
            }

            Transform panel = canvas == null ? null : canvas.transform.Find("QuickSlotPanel"); // 슬롯 패널 조회

            if (hud == null || panel == null) // 연결 대상 확인
            {
                Debug.LogError("[Project I] QuickSlotHud 또는 PlayerHUDCanvas/QuickSlotPanel을 찾지 못했습니다."); // 오류 안내
                return; // 중단
            }

            UnityEngine.UI.Image[] backgrounds = new UnityEngine.UI.Image[PlayerInventory.Capacity]; // 슬롯 배경
            UnityEngine.UI.Text[] numbers = new UnityEngine.UI.Text[PlayerInventory.Capacity]; // 슬롯 번호
            UnityEngine.UI.Text[] names = new UnityEngine.UI.Text[PlayerInventory.Capacity]; // 아이템 이름
            UnityEngine.UI.Text[] locks = new UnityEngine.UI.Text[PlayerInventory.Capacity]; // 잠금 표시

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 6칸 순회
            {
                Transform slot = panel.Find($"Slot_{index + 1}"); // 슬롯 조회
                backgrounds[index] = slot == null ? null : slot.GetComponent<UnityEngine.UI.Image>(); // 배경 연결
                numbers[index] = slot == null ? null : slot.Find("Number")?.GetComponent<UnityEngine.UI.Text>(); // 번호 연결
                names[index] = slot == null ? null : slot.Find("ItemName")?.GetComponent<UnityEngine.UI.Text>(); // 이름 연결
                locks[index] = slot == null ? null : slot.Find("Lock")?.GetComponent<UnityEngine.UI.Text>(); // 잠금 연결
            }

            hud.Configure(hud.GetComponent<PlayerInventory>(), backgrounds, numbers, names, locks); // HUD 참조 연결
            EditorUtility.SetDirty(hud); // 변경 기록
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud); // 플레이어 프리팹 인스턴스 오버라이드로 저장
        }

        [MenuItem("Tools/Project I/Day 26/Validate Item Definitions")] // 고정 ItemId 검증 메뉴
        public static bool ValidateItemDefinitions() // ItemDefinition 고정 ID·중복·복구 Prefab 검증
        {
            Dictionary<string, string> seen = new Dictionary<string, string>(); // ID별 에셋 경로
            int errors = 0; // 오류 개수

            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { DefinitionFolder })) // 정의 에셋 순회
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); // 에셋 경로
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path); // 정의 로드

                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId) || definition.ItemId.StartsWith("LEGACY_")) // 고정 ID 여부 확인
                {
                    Debug.LogError($"[Project I] 고정 ItemId 누락 또는 LEGACY 사용 / {path}"); // 오류 안내
                    errors++; // 오류 집계
                    continue; // 다음 정의
                }

                if (seen.TryGetValue(definition.ItemId, out string other)) // 중복 ID 확인
                {
                    Debug.LogError($"[Project I] ItemId 중복 / {definition.ItemId} / {other} · {path}"); // 오류 안내
                    errors++; // 오류 집계
                }

                seen[definition.ItemId] = path; // ID 등록

                if (definition.RecoveryPrefab == null || definition.RecoveryPrefab.GetComponent<WorldItem>() == null) // 복구 Prefab 확인
                {
                    Debug.LogError($"[Project I] RecoveryPrefab 누락 또는 WorldItem 없음 / {definition.ItemId}"); // 오류 안내
                    errors++; // 오류 집계
                }
            }

            Debug.Log($"[Project I] ItemDefinition 검증 완료 / 정의 {seen.Count}개 / 오류 {errors}개"); // 결과 로그
            return errors == 0; // 검증 결과 반환
        }

        [MenuItem("Tools/Project I/Day 26/Set Play Mode Start Scene = 00_WagonPersistent")] // 시작 씬만 다시 지정하는 메뉴
        public static void SetPlayModeStartScene() // Play 버튼이 Persistent + Office에서 바로 시작하도록 지정
        {
            SceneAsset startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PersistentScenePath); // Persistent 씬 에셋 조회
            EditorSceneManager.playModeStartScene = startScene; // Play 시작 씬 지정
            Debug.Log($"[Project I] Play 시작 씬 = {PersistentScenePath}"); // 결과 로그
        }

        private static void BuildTerrainTestDungeon() // 작은 Terrain 야외 테스트 던전 생성
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // 빈 씬 생성
            Transform root = new GameObject("Day26_TerrainTestDungeon").transform; // 환경 루트 생성
            Terrain terrain = CreateTerrain(root); // Terrain 생성
            CreateBoundary(root); // 맵 밖 이탈 방지 벽 생성
            CreateProps(root, terrain); // 바위·나무 배치
            CreateLighting(root); // 조명·환경광 구성
            CreateTravelAnchor(scene); // 마차 진입·정차 지점 생성
            PlaceItems(scene, terrain); // 테스트 회수품 배치
            EditorSceneManager.SaveScene(scene, TestDungeonScenePath); // 기존 경로에 저장 (GUID 유지)
        }

        private static Terrain CreateTerrain(Transform root) // 높이·재질이 적용된 Terrain 생성
        {
            string dataPath = $"{GeneratedFolder}/TestDungeonTerrain.asset"; // TerrainData 저장 경로
            AssetDatabase.DeleteAsset(dataPath); // 이전 생성본 제거
            TerrainLayer[] layers = // 지면 재질 3종 (TerrainData 저장 전에 에셋으로 생성)
            {
                CreateLayer("Grass", new Color(0.27f, 0.37f, 0.18f), 6f), // 풀
                CreateLayer("Dirt", new Color(0.42f, 0.33f, 0.22f), 4f), // 흙길
                CreateLayer("Rock", new Color(0.40f, 0.40f, 0.41f), 5f) // 바위 경사
            };
            TerrainData data = new TerrainData(); // 새 TerrainData
            data.heightmapResolution = HeightResolution; // 높이맵 해상도 지정
            data.alphamapResolution = AlphaResolution; // 재질 해상도 지정
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize); // 크기 지정
            AssetDatabase.CreateAsset(data, dataPath); // 먼저 에셋으로 저장해야 재질 맵 텍스처가 하위 에셋으로 함께 저장됨
            data.SetHeights(0, 0, BuildHeights()); // 높이 적용
            data.terrainLayers = layers; // 지면 재질 연결
            data.SetAlphamaps(0, 0, BuildAlphamaps(data)); // 지면 재질 칠하기
            EditorUtility.SetDirty(data); // 변경 기록

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data); // Terrain 오브젝트 생성
            terrainObject.name = "Day26_Terrain"; // 이름 지정
            terrainObject.transform.SetParent(root, false); // 환경 루트 아래 배치
            terrainObject.transform.position = TerrainOrigin; // 평지 표면이 y=0이 되도록 이동
            Terrain terrain = terrainObject.GetComponent<Terrain>(); // Terrain 컴포넌트
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit"); // URP Terrain 셰이더

            if (terrainShader != null) // 셰이더 존재 확인
            {
                string materialPath = $"{GeneratedFolder}/TestDungeonTerrain.mat"; // Terrain 재질 경로
                AssetDatabase.DeleteAsset(materialPath); // 이전 생성본 제거
                Material material = new Material(terrainShader); // URP Terrain 재질
                AssetDatabase.CreateAsset(material, materialPath); // 재질 저장
                terrain.materialTemplate = material; // Terrain 재질 적용
            }

            return terrain; // 생성 Terrain 반환
        }

        private static float[,] BuildHeights() // 평지 통로와 가장자리 언덕을 가진 높이맵 계산
        {
            float[,] heights = new float[HeightResolution, HeightResolution]; // [z, x] 높이 배열

            for (int zi = 0; zi < HeightResolution; zi++) // 세로 순회
            {
                for (int xi = 0; xi < HeightResolution; xi++) // 가로 순회
                {
                    Vector2 world = GridToWorld(xi, zi); // 격자 → 월드 XZ
                    float edge = Mathf.Min(Mathf.Min(world.x + 32f, 32f - world.x), Mathf.Min(world.y + 44f, 20f - world.y)); // 가장자리까지 거리
                    float berm = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((7f - edge) / 7f)) * 6.5f; // 가장자리 언덕
                    float noise = (Mathf.PerlinNoise(world.x * 0.07f + 13f, world.y * 0.07f + 7f) - 0.5f) * 2.4f; // 완만한 기복
                    float hill = Bump(world, new Vector2(-15f, 13f), 7f, 3.2f); // 왼쪽 언덕
                    float dip = Bump(world, new Vector2(18f, -8f), 6f, -1.2f); // 오른쪽 움푹한 곳
                    float flatten = FlattenWeight(world); // 마차 통로·정차 구역 평탄화 가중치
                    float height = BaseHeight + Mathf.Lerp(noise + hill + dip, 0f, flatten) + berm * (1f - flatten * 0.8f); // 최종 높이
                    heights[zi, xi] = Mathf.Clamp01(height / TerrainHeight); // 정규화 높이 저장
                }
            }

            return heights; // 높이맵 반환
        }

        private static float FlattenWeight(Vector2 world) // 마차가 지나가는 길과 정차 구역을 평평하게 만드는 가중치
        {
            float road = world.y < 7f ? Mathf.Clamp01((6.5f - Mathf.Abs(world.x)) / 2.5f) : 0f; // 폭 8m 흙길 + 2.5m 완충
            float stop = Mathf.Clamp01((12f - Vector2.Distance(world, new Vector2(0f, 0f))) / 3f); // 정차 구역 반경 9m + 3m 완충
            return Mathf.Max(road, stop); // 둘 중 큰 값 사용
        }

        private static float Bump(Vector2 world, Vector2 center, float radius, float height) // 원형 언덕·구덩이
        {
            float t = Mathf.Clamp01(1f - Vector2.Distance(world, center) / radius); // 중심 거리 비율
            return height * t * t * (3f - 2f * t); // 부드러운 높이
        }

        private static float[,,] BuildAlphamaps(TerrainData data) // 길·경사·평지에 따라 지면 재질 칠하기
        {
            float[,,] maps = new float[AlphaResolution, AlphaResolution, 3]; // [z, x, layer]

            for (int zi = 0; zi < AlphaResolution; zi++) // 세로 순회
            {
                for (int xi = 0; xi < AlphaResolution; xi++) // 가로 순회
                {
                    float nx = xi / (float)(AlphaResolution - 1); // 정규화 X
                    float nz = zi / (float)(AlphaResolution - 1); // 정규화 Z
                    Vector2 world = new Vector2(TerrainOrigin.x + nx * TerrainSize, TerrainOrigin.z + nz * TerrainSize); // 월드 XZ
                    float steep = data.GetSteepness(nx, nz); // 경사 각도
                    float road = world.y < 7f ? Mathf.Clamp01((4.5f - Mathf.Abs(world.x)) / 1.2f) : 0f; // 흙길 비율
                    road = Mathf.Max(road, Mathf.Clamp01((7f - Vector2.Distance(world, Vector2.zero)) / 2f)); // 정차 구역 흙 비율
                    float rock = Mathf.Clamp01((steep - 22f) / 12f); // 경사면 바위 비율
                    float grass = Mathf.Max(0f, 1f - road - rock); // 나머지 풀
                    float sum = Mathf.Max(0.0001f, grass + road + rock); // 합계
                    maps[zi, xi, 0] = grass / sum; // 풀
                    maps[zi, xi, 1] = road / sum; // 흙길
                    maps[zi, xi, 2] = rock / sum; // 바위
                }
            }

            return maps; // 재질 맵 반환
        }

        private static TerrainLayer CreateLayer(string name, Color color, float tileSize) // 단색 노이즈 텍스처 기반 TerrainLayer 생성
        {
            string texturePath = $"{GeneratedFolder}/Terrain_{name}.png"; // 텍스처 경로
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); // 작은 텍스처
            System.Random random = new System.Random(name.GetHashCode()); // 재질별 고정 난수

            for (int y = 0; y < 32; y++) // 세로 픽셀 순회
            {
                for (int x = 0; x < 32; x++) // 가로 픽셀 순회
                {
                    float shade = 0.85f + (float)random.NextDouble() * 0.3f; // 밝기 흔들림
                    texture.SetPixel(x, y, new Color(color.r * shade, color.g * shade, color.b * shade, 1f)); // 픽셀 기록
                }
            }

            File.WriteAllBytes(texturePath, texture.EncodeToPNG()); // PNG 저장
            Object.DestroyImmediate(texture); // 임시 텍스처 해제
            AssetDatabase.ImportAsset(texturePath); // 텍스처 가져오기
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath); // 가져온 텍스처
            string layerPath = $"{GeneratedFolder}/TerrainLayer_{name}.terrainlayer"; // 레이어 경로
            AssetDatabase.DeleteAsset(layerPath); // 이전 생성본 제거
            TerrainLayer layer = new TerrainLayer { diffuseTexture = imported, tileSize = new Vector2(tileSize, tileSize) }; // 레이어 생성
            AssetDatabase.CreateAsset(layer, layerPath); // 레이어 저장
            return layer; // 레이어 반환
        }

        private static void CreateBoundary(Transform root) // 맵 가장자리 투명 벽
        {
            Transform boundary = new GameObject("Boundary").transform; // 벽 루트
            boundary.SetParent(root, false); // 환경 루트 아래
            CreateWall(boundary, "North", new Vector3(0f, 5f, 20.5f), new Vector3(66f, 20f, 1f)); // 북쪽 벽
            CreateWall(boundary, "South", new Vector3(0f, 5f, -44.5f), new Vector3(66f, 20f, 1f)); // 남쪽 벽
            CreateWall(boundary, "East", new Vector3(32.5f, 5f, -12f), new Vector3(1f, 20f, 66f)); // 동쪽 벽
            CreateWall(boundary, "West", new Vector3(-32.5f, 5f, -12f), new Vector3(1f, 20f, 66f)); // 서쪽 벽
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 size) // 투명 BoxCollider 벽 생성
        {
            GameObject wall = new GameObject(name); // 벽 오브젝트
            wall.transform.SetParent(parent, false); // 부모 지정
            wall.transform.localPosition = position; // 위치 지정
            BoxCollider collider = wall.AddComponent<BoxCollider>(); // 충돌체 추가
            collider.size = size; // 크기 지정
        }

        private static void CreateProps(Transform root, Terrain terrain) // 바위와 나무 배치
        {
            Transform props = new GameObject("Props").transform; // 소품 루트
            props.SetParent(root, false); // 환경 루트 아래
            Material rockMaterial = CreateLitMaterial("Prop_Rock", new Color(0.42f, 0.41f, 0.40f)); // 바위 재질
            Material barkMaterial = CreateLitMaterial("Prop_Bark", new Color(0.30f, 0.21f, 0.14f)); // 나무 줄기 재질
            Material leafMaterial = CreateLitMaterial("Prop_Leaf", new Color(0.18f, 0.32f, 0.15f)); // 나뭇잎 재질
            System.Random random = new System.Random(2626); // 고정 배치 난수
            int rocks = 0; // 배치 바위 수
            int trees = 0; // 배치 나무 수
            int guard = 0; // 무한 반복 방지

            while ((rocks < 14 || trees < 10) && guard++ < 600) // 목표 개수까지 배치
            {
                Vector2 point = new Vector2(-28f + (float)random.NextDouble() * 56f, -40f + (float)random.NextDouble() * 56f); // 후보 위치
                bool blocked = FlattenWeight(point) > 0.01f || Mathf.Abs(point.x) < 7.5f && point.y < 9f; // 통로·정차 구역 제외

                foreach (ItemSpawn spawn in ItemSpawns) // 아이템 주변 제외
                {
                    blocked |= Vector2.Distance(point, spawn.Position) < 3f; // 아이템 3m 이내 제외
                }

                if (blocked) // 제외 구역 확인
                {
                    continue; // 다음 후보
                }

                float groundY = SampleGround(terrain, point); // 지면 높이

                if (rocks < 14 && (trees >= 10 || random.NextDouble() < 0.55)) // 바위 배치 차례 확인
                {
                    GameObject rock = GameObject.CreatePrimitive(random.NextDouble() < 0.5 ? PrimitiveType.Cube : PrimitiveType.Sphere); // 바위 기본 도형
                    rock.name = $"Rock_{rocks:00}"; // 이름
                    rock.transform.SetParent(props, false); // 부모 지정
                    Vector3 scale = new Vector3(0.8f + (float)random.NextDouble() * 1.8f, 0.6f + (float)random.NextDouble() * 1.2f, 0.8f + (float)random.NextDouble() * 1.8f); // 불규칙 크기
                    rock.transform.localScale = scale; // 크기 적용
                    rock.transform.position = new Vector3(point.x, groundY + scale.y * 0.3f, point.y); // 절반쯤 묻힌 위치
                    rock.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 20f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 20f); // 불규칙 회전
                    rock.GetComponent<Renderer>().sharedMaterial = rockMaterial; // 재질 적용
                    rock.isStatic = true; // 정적 오브젝트
                    rocks++; // 개수 증가
                    continue; // 다음 후보
                }

                GameObject tree = new GameObject($"Tree_{trees:00}"); // 나무 루트
                tree.transform.SetParent(props, false); // 부모 지정
                tree.transform.position = new Vector3(point.x, groundY, point.y); // 지면 위치
                float trunkHeight = 2.4f + (float)random.NextDouble() * 1.6f; // 줄기 높이
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 줄기
                trunk.name = "Trunk"; // 이름
                trunk.transform.SetParent(tree.transform, false); // 나무 아래
                trunk.transform.localScale = new Vector3(0.35f, trunkHeight * 0.5f, 0.35f); // 줄기 크기
                trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f); // 줄기 위치
                trunk.GetComponent<Renderer>().sharedMaterial = barkMaterial; // 재질
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 나뭇잎
                crown.name = "Leaves"; // 이름
                crown.transform.SetParent(tree.transform, false); // 나무 아래
                float crownSize = 2.2f + (float)random.NextDouble() * 1.4f; // 나뭇잎 크기
                crown.transform.localScale = new Vector3(crownSize, crownSize * 0.9f, crownSize); // 나뭇잎 크기 적용
                crown.transform.localPosition = new Vector3(0f, trunkHeight + crownSize * 0.3f, 0f); // 나뭇잎 위치
                crown.GetComponent<Renderer>().sharedMaterial = leafMaterial; // 재질
                Object.DestroyImmediate(crown.GetComponent<Collider>()); // 나뭇잎은 통과 가능
                tree.isStatic = true; // 정적 오브젝트
                trees++; // 개수 증가
            }
        }

        private static void CreateLighting(Transform root) // 야외 조명과 환경광 구성
        {
            GameObject sun = new GameObject("Day26_Sun"); // 태양광 오브젝트
            sun.transform.SetParent(root, false); // 환경 루트 아래
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f); // 오후 각도
            Light light = sun.AddComponent<Light>(); // 조명 추가
            light.type = LightType.Directional; // 방향광
            light.intensity = 1.15f; // 밝기
            light.color = new Color(1f, 0.93f, 0.82f); // 따뜻한 오후 색
            light.shadows = LightShadows.Soft; // 부드러운 그림자
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; // 3색 환경광
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f); // 하늘 환경광
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.40f); // 수평 환경광
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.16f); // 지면 환경광
            RenderSettings.sun = light; // 태양 지정
        }

        private static void CreateTravelAnchor(Scene scene) // 마차 진입·정차 지점 생성 (Day24 규칙과 동일 좌표)
        {
            GameObject anchorRoot = new GameObject("Day24_TestDungeon_TravelAnchor"); // 이동 지점 루트
            SceneManager.MoveGameObjectToScene(anchorRoot, scene); // 테스트 던전 씬 소속
            GameObject entry = new GameObject("WagonEntryPoint"); // 진입 지점
            entry.transform.SetParent(anchorRoot.transform, false); // 루트 아래
            entry.transform.SetPositionAndRotation(EntryPosition, Quaternion.identity); // 진입 위치
            GameObject stop = new GameObject("WagonStopPoint"); // 정차 지점
            stop.transform.SetParent(anchorRoot.transform, false); // 루트 아래
            stop.transform.SetPositionAndRotation(StopPosition, Quaternion.identity); // 정차 위치
            MapTravelAnchor anchor = anchorRoot.AddComponent<MapTravelAnchor>(); // 이동 지점 컴포넌트
            anchor.Configure(TravelDestination.TestDungeon, entry.transform, stop.transform); // 목적지와 지점 연결
        }

        private static void PlaceItems(Scene scene, Terrain terrain) // 테스트 회수품 배치
        {
            Dictionary<string, ItemDefinition> definitions = LoadDefinitions(); // ItemId별 정의 조회
            Transform itemRoot = new GameObject("Day26_TestLoot").transform; // 아이템 정리용 루트
            SceneManager.MoveGameObjectToScene(itemRoot.gameObject, scene); // 던전 씬 소속
            Transform markerRoot = new GameObject("Day26_LootMarkers").transform; // 테스트용 위치 표시 기둥 루트
            SceneManager.MoveGameObjectToScene(markerRoot.gameObject, scene); // 던전 씬 소속
            Material markerMaterial = CreateLitMaterial("Loot_Marker", new Color(1f, 0.78f, 0.25f)); // 표시 기둥 재질
            markerMaterial.EnableKeyword("_EMISSION"); // 멀리서도 보이도록 발광 사용
            markerMaterial.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.2f) * 1.6f); // 발광 색
            EditorUtility.SetDirty(markerMaterial); // 발광 설정 저장
            int index = 0; // 배치 번호

            foreach (ItemSpawn spawn in ItemSpawns) // 배치 목록 순회
            {
                if (!definitions.TryGetValue(spawn.ItemId, out ItemDefinition definition) || definition.RecoveryPrefab == null) // 정의·프리팹 확인
                {
                    Debug.LogError($"[Project I] 26일차 테스트 회수품 배치 실패 / ItemId={spawn.ItemId} 정의 또는 RecoveryPrefab 없음"); // 누락 안내
                    continue; // 다음 아이템
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.RecoveryPrefab, scene); // 프리팹 인스턴스 생성
                instance.name = $"Loot_{index:00}_{definition.DisplayName}"; // 이름
                instance.transform.SetParent(itemRoot, false); // 루트 아래
                float groundY = SampleGround(terrain, spawn.Position); // 지면 높이
                instance.transform.SetPositionAndRotation(new Vector3(spawn.Position.x, groundY + 0.6f, spawn.Position.y), Quaternion.Euler(0f, index * 47f, 0f)); // 지면 위 배치
                RecoverableValue recoverable = instance.GetComponent<RecoverableValue>(); // 가격 컴포넌트

                if (recoverable != null) // 가격 컴포넌트 확인
                {
                    recoverable.Configure(spawn.Value); // 테스트 가치 지정
                    PrefabUtility.RecordPrefabInstancePropertyModifications(recoverable); // 인스턴스 값 저장
                }

                CreateVisual(markerRoot, $"Marker_{index:00}", PrimitiveType.Cylinder, new Vector3(spawn.Position.x + 0.7f, groundY + 1.1f, spawn.Position.y), new Vector3(0.08f, 1.1f, 0.08f), markerMaterial); // 아이템 옆 발광 표시 기둥 (충돌 없음)
                index++; // 번호 증가
            }
        }

        private static Dictionary<string, ItemDefinition> LoadDefinitions() // Definitions 폴더의 ItemDefinition 사전 생성
        {
            Dictionary<string, ItemDefinition> result = new Dictionary<string, ItemDefinition>(); // 결과 사전

            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { DefinitionFolder })) // 정의 에셋 순회
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)); // 정의 로드

                if (definition != null && !string.IsNullOrWhiteSpace(definition.ItemId)) // 유효 정의 확인
                {
                    result[definition.ItemId] = definition; // 사전 등록
                }
            }

            return result; // 결과 반환
        }

        private static void AddDayEndLedgerToOffice() // 사무소 채무 장부 옆에 일차 마감 장부 배치
        {
            Scene office = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single); // 사무소 씬 열기
            Transform debtLedger = null; // 채무 장부 참조

            foreach (GameObject root in office.GetRootGameObjects()) // 루트 순회
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 하위 순회
                {
                    if (child.name == LedgerName) // 이전 생성본 확인
                    {
                        Object.DestroyImmediate(child.gameObject); // 재생성을 위해 제거
                        break; // 다음 루트 검사
                    }
                }
            }

            foreach (GameObject root in office.GetRootGameObjects()) // 루트 재순회
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 하위 순회
                {
                    if (child.name == "Day23_DebtLedger") // 채무 장부 찾기
                    {
                        debtLedger = child; // 참조 저장
                    }
                }
            }

            if (debtLedger == null) // 채무 장부 누락 확인
            {
                Debug.LogError("[Project I] Day23_DebtLedger를 찾지 못해 일차 마감 장부를 배치하지 못했습니다."); // 오류 안내
                return; // 중단
            }

            Vector3 halfExtents = new Vector3(0.6f, 0.55f, 0.45f); // 장부 책상 반크기 + 여유
            Physics.SyncTransforms(); // 편집 중 충돌 판정 동기화
            Vector3 chosen = Vector3.zero; // 선택 위치
            bool found = false; // 빈 자리 발견 여부
            float floorY = debtLedger.position.y - debtLedger.lossyScale.y * 0.5f; // 바닥 측정 실패 시 기본 바닥 높이

            if (Physics.Raycast(debtLedger.position + debtLedger.forward * 1.2f + Vector3.up * 1.5f, Vector3.down, out RaycastHit floorHit, 4f, ~0, QueryTriggerInteraction.Ignore)) // 채무 장부 앞 실제 바닥 측정
            {
                floorY = floorHit.point.y; // 실제 바닥 윗면 높이 사용
            }
            Vector3[] candidates = // 채무 장부 주변 후보 위치
            {
                debtLedger.position + debtLedger.right * 2.1f, // 오른쪽
                debtLedger.position - debtLedger.right * 2.1f, // 왼쪽
                debtLedger.position + debtLedger.right * 3.2f, // 더 오른쪽
                debtLedger.position - debtLedger.right * 3.2f, // 더 왼쪽
                debtLedger.position + debtLedger.forward * 1.4f + debtLedger.right * 1.6f, // 앞 오른쪽
                debtLedger.position - debtLedger.forward * 1.4f + debtLedger.right * 1.6f, // 뒤 오른쪽
                debtLedger.position + debtLedger.forward * 1.4f - debtLedger.right * 1.6f, // 앞 왼쪽
                debtLedger.position - debtLedger.forward * 1.4f - debtLedger.right * 1.6f // 뒤 왼쪽
            };

            foreach (Vector3 candidate in candidates) // 후보 순회
            {
                Vector3 center = new Vector3(candidate.x, floorY + 0.05f + halfExtents.y, candidate.z); // 바닥 바로 위 박스 중심
                Collider[] hits = Physics.OverlapBox(center, halfExtents, debtLedger.rotation, ~0, QueryTriggerInteraction.Ignore); // 겹침 검사

                if (hits.Length == 0 && Physics.Raycast(center, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)) // 비어 있고 바닥이 있는지 확인
                {
                    chosen = new Vector3(candidate.x, floorY, candidate.z); // 바닥 위치 저장
                    found = true; // 발견
                    break; // 첫 빈 자리 사용
                }
            }

            if (!found) // 빈 자리 없음 확인
            {
                chosen = new Vector3((debtLedger.position + debtLedger.right * 2.1f).x, floorY, (debtLedger.position + debtLedger.right * 2.1f).z); // 기본 오른쪽 배치
                Debug.LogWarning("[Project I] 일차 마감 장부 빈 자리를 찾지 못해 채무 장부 오른쪽에 배치했습니다. 위치를 확인하세요."); // 확인 안내
            }

            GameObject ledger = new GameObject(LedgerName); // 장부 루트
            ledger.transform.SetParent(debtLedger.parent, true); // 채무 장부와 같은 부모
            ledger.transform.SetPositionAndRotation(chosen, debtLedger.rotation); // 위치·회전
            BoxCollider collider = ledger.AddComponent<BoxCollider>(); // 상호작용·충돌 박스
            collider.center = new Vector3(0f, 0.55f, 0f); // 박스 중심
            collider.size = new Vector3(1.1f, 1.1f, 0.8f); // 박스 크기
            ledger.AddComponent<DayEndLedgerInteractable>(); // 일차 마감 기능
            CreateVisual(ledger.transform, "Desk", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.9f, 0.7f), CreateLitMaterial("Ledger_Wood", new Color(0.33f, 0.22f, 0.13f))); // 책상
            CreateVisual(ledger.transform, "Book", PrimitiveType.Cube, new Vector3(0f, 0.94f, 0f), new Vector3(0.55f, 0.07f, 0.4f), CreateLitMaterial("Ledger_Book", new Color(0.55f, 0.12f, 0.10f))); // 장부 책
            CreateVisual(ledger.transform, "Candle", PrimitiveType.Cylinder, new Vector3(0.38f, 1.02f, 0.2f), new Vector3(0.06f, 0.09f, 0.06f), CreateLitMaterial("Ledger_Candle", new Color(0.93f, 0.88f, 0.72f))); // 촛대
            EditorSceneManager.MarkSceneDirty(office); // 변경 표시
            EditorSceneManager.SaveScene(office, OfficeScenePath); // 사무소 씬 저장
            Debug.Log($"[Project I] 일차 마감 장부 배치 / {chosen} / 빈 자리 탐색={(found ? "성공" : "기본 위치")}"); // 결과 로그
        }

        private static void CreateVisual(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material) // 충돌 없는 외형 도형 생성
        {
            GameObject visual = GameObject.CreatePrimitive(type); // 기본 도형
            visual.name = name; // 이름
            visual.transform.SetParent(parent, false); // 부모 지정
            visual.transform.localPosition = localPosition; // 위치
            visual.transform.localScale = localScale; // 크기
            Object.DestroyImmediate(visual.GetComponent<Collider>()); // 외형 충돌체 제거 (루트 박스 사용)
            visual.GetComponent<Renderer>().sharedMaterial = material; // 재질
        }

        private static Material CreateLitMaterial(string name, Color color) // URP Lit 단색 재질 생성·재사용
        {
            string path = $"{GeneratedFolder}/{name}.mat"; // 재질 경로
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 새로 만들어야 하는지 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); // URP Lit 셰이더
                material = new Material(shader); // 재질 생성
                AssetDatabase.CreateAsset(material, path); // 재질 저장
            }

            material.color = color; // 기본 색
            material.SetColor("_BaseColor", color); // URP 기본 색
            EditorUtility.SetDirty(material); // 변경 기록
            return material; // 재질 반환
        }

        private static float SampleGround(Terrain terrain, Vector2 point) // Terrain 표면 높이 조회
        {
            Vector3 world = new Vector3(point.x, 0f, point.y); // 월드 위치
            return terrain.SampleHeight(world) + terrain.transform.position.y; // 월드 표면 높이
        }

        private static Vector2 GridToWorld(int xi, int zi) // 높이맵 격자 → 월드 XZ
        {
            float step = TerrainSize / (HeightResolution - 1); // 격자 간격
            return new Vector2(TerrainOrigin.x + xi * step, TerrainOrigin.z + zi * step); // 월드 좌표
        }

        private static void EnsureFolder(string folderPath) // 중첩 폴더 생성
        {
            string[] segments = folderPath.Split('/'); // 경로 분리
            string current = segments[0]; // Assets

            for (int index = 1; index < segments.Length; index++) // 하위 순회
            {
                string next = current + "/" + segments[index]; // 다음 경로

                if (!AssetDatabase.IsValidFolder(next)) // 폴더 없음 확인
                {
                    AssetDatabase.CreateFolder(current, segments[index]); // 폴더 생성
                }

                current = next; // 경로 갱신
            }
        }

        private readonly struct ItemSpawn // 회수품 배치 정보
        {
            public readonly string ItemId; // 고정 ItemId
            public readonly int Value; // 테스트 가치
            public readonly Vector2 Position; // 월드 XZ 위치

            public ItemSpawn(string itemId, int value, Vector2 position) // 생성자
            {
                ItemId = itemId; // ID 저장
                Value = value; // 가치 저장
                Position = position; // 위치 저장
            }
        }
    }
}
