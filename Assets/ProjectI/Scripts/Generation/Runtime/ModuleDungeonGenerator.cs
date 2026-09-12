using System.Collections.Generic; // 목록 사용
using ProjectI.Economy; // 회수품 가치 참조
using ProjectI.Generation; // 배치 규칙 참조
using ProjectI.Items; // 아이템 정의 참조
using ProjectI.Loop; // 순간이동·일차 서비스 참조
using ProjectI.Persistence; // 캠페인 시드 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 조회 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class ModuleDungeonGenerator : MonoBehaviour // 모듈 프리팹의 출입구끼리 맞춰 붙여 던전을 만듭니다 (30일차 소켓 방식)
    {
        [Header("모듈")]
        [SerializeField] private DungeonModuleLibrary library; // 모듈 목록
        [SerializeField] private int seedOverride; // 0이 아니면 고정 시드

        [Header("규모")]
        [SerializeField] private int targetModules = 26; // 목표 모듈 수
        [SerializeField] private int minModules = 18; // 최소 모듈 수
        [SerializeField] private int exteriorDoorCount = 2; // 외부 씬 서브문 수 (외부 씬 배치 수를 읽어 넣음)
        [SerializeField] private bool enableBoss = true; // 보스방 사용
        [SerializeField] private int secretRoomCount = 1; // 비밀방 수
        [SerializeField] private int gridRadius = 90; // 배치 가능한 격자 반경 (칸)
        [SerializeField] private int floorsAbove = 2; // 시작 층 위로 만들 층 수
        [SerializeField] private int floorsBelow = 2; // 시작 층 아래로 만들 층 수

        [Header("내용물")]
        [SerializeField] private ItemDefinition keyDefinition; // 열쇠 정의
        [SerializeField] private InteriorLootEntry[] lootTable = System.Array.Empty<InteriorLootEntry>(); // 회수품 표
        [SerializeField] private float lootChancePerRoom = 0.72f; // 일반 방에 회수품이 놓일 확률

        [Header("배치")]
        [SerializeField] private Vector2 originXZ = new Vector2(0f, -38f); // 던전 중심 월드 XZ
        [SerializeField] private float surfaceClearance = 12f; // 지형 최저점부터 띄울 여유 깊이
        [SerializeField] private Transform exteriorReference; // 지형 높이를 잴 기준 (없으면 이 오브젝트)

        private const string GeneratedRootName = "GeneratedModuleDungeon"; // 생성물 루트 이름
        private const float FloorProbeTolerance = 0.2f; // 층 판정 여유 (바닥면에 놓인 물건 보정)
        private readonly List<DungeonModule> spawnedModules = new List<DungeonModule>(); // 생성된 모듈
        private readonly List<DungeonDoor> spawnedDoors = new List<DungeonDoor>(); // 생성된 문
        private readonly List<BreakableWall> spawnedBreakables = new List<BreakableWall>(); // 생성된 금 간 벽
        private readonly List<DungeonTeleportDoor> interiorDoors = new List<DungeonTeleportDoor>(); // 실내에 생성한 출입문
        private readonly List<DungeonTeleportDoor> exteriorDoors = new List<DungeonTeleportDoor>(); // 외부 씬에 놓인 출입문
        private readonly List<GameObject> spawnedPlugs = new List<GameObject>(); // 연결되지 않은 출입구를 막은 벽
        private int openPassageCount; // 문 없이 뚫린 연결 수
        private readonly List<WorldItem> spawnedLoot = new List<WorldItem>(); // 생성한 회수품·열쇠
        private Transform generatedRoot; // 생성물 루트

        public bool IsGenerated { get; private set; } // 생성 완료 여부
        public string FailureReason { get; private set; } = string.Empty; // 실패 사유
        public int Seed { get; private set; } // 사용한 시드
        public ModuleDungeonPlan Plan { get; private set; } // 배치 결과
        public Transform GeneratedRoot => generatedRoot; // 생성물 루트 공개
        public IReadOnlyList<DungeonModule> SpawnedModules => spawnedModules; // 모듈 공개
        public IReadOnlyList<DungeonDoor> SpawnedDoors => spawnedDoors; // 문 공개
        public IReadOnlyList<BreakableWall> BreakableWalls => spawnedBreakables; // 금 간 벽 공개
        public IReadOnlyList<DungeonTeleportDoor> InteriorDoors => interiorDoors; // 실내 문 공개
        public IReadOnlyList<DungeonTeleportDoor> ExteriorDoors => exteriorDoors; // 외부 문 공개
        public IReadOnlyList<GameObject> SpawnedPlugs => spawnedPlugs; // 막음 벽 공개
        public int OpenPassageCount => openPassageCount; // 뚫린 연결 수 공개 (문 있음 / 문 없음 / 벽으로 막힘 세 상태)
        public IReadOnlyList<WorldItem> SpawnedLoot => spawnedLoot; // 회수품 공개
        public DungeonModuleLibrary Library => library; // 모듈 목록 공개
        public float InteriorTopY { get; private set; } // 실내 최고 높이 한계
        public float GeneratedMaxY { get; private set; } // 실제 생성물 최고 높이

        public void ConfigureContent(ItemDefinition key, InteriorLootEntry[] loot) // 열쇠·회수품 표 지정 (에디터 구성)
        {
            keyDefinition = key; // 열쇠
            lootTable = loot ?? System.Array.Empty<InteriorLootEntry>(); // 회수품 표
        }

        public void ConfigureLibrary(DungeonModuleLibrary value, Transform reference) // 모듈 목록·지형 기준 지정 (에디터 구성)
        {
            library = value; // 모듈 목록
            exteriorReference = reference != null ? reference : transform; // 지형 기준
        }

        public void ConfigureScale(int target, int minimum, int exteriorDoors, bool boss, int secrets) // 규모 지정 (에디터 구성)
        {
            targetModules = Mathf.Max(4, target); // 목표
            minModules = Mathf.Clamp(minimum, 2, targetModules); // 최소
            exteriorDoorCount = Mathf.Max(0, exteriorDoors); // 서브문
            enableBoss = boss; // 보스방
            secretRoomCount = Mathf.Max(0, secrets); // 비밀방
        }

        public bool Generate(int seed) // 던전 생성 (실패하면 false)
        {
            Clear(); // 기존 생성물 제거
            Seed = seed; // 시드 기록

            if (library == null || library.Modules.Count == 0) // 모듈 목록 확인
            {
                FailureReason = "모듈 목록이 비어 있습니다"; // 사유
                Debug.LogError($"[Project I] 모듈 던전 생성 실패 / {FailureReason}", this); // 오류
                return false; // 실패
            }

            CollectExteriorDoors(); // 외부 씬 문 수집

            if (!ValidateExteriorDoors(out int subDoorCount)) // 외부 문 구성 확인
            {
                Debug.LogError($"[Project I] 모듈 던전 생성 실패 / {FailureReason}", this); // 오류
                return false; // 실패
            }

            exteriorDoorCount = subDoorCount; // 실내 서브문 수 = 외부 서브문 수 (1:1 규칙)
            ModulePlanConfig config = BuildConfig(); // 배치 규칙
            Plan = SocketDungeonPlanner.Plan(library.BuildDefinitions(), config, seed); // 배치

            if (Plan == null) // 배치 실패
            {
                FailureReason = $"시드 {seed}에서 규칙을 만족하는 배치를 찾지 못함"; // 사유
                Debug.LogError($"[Project I] 모듈 던전 생성 실패 / {FailureReason}", this); // 오류
                return false; // 실패
            }

            float bottomY = ComputeExteriorBottomY(); // 외부 최저 높이
            InteriorTopY = bottomY - surfaceClearance; // 실내 최고 높이 한계
            generatedRoot = new GameObject(GeneratedRootName).transform; // 생성물 루트
            generatedRoot.SetParent(transform, false); // 생성기 아래 (환경 씬 소속)
            generatedRoot.position = new Vector3(originXZ.x, InteriorTopY - (DungeonModuleMetrics.FloorStep * Mathf.Max(1, Plan.FloorCount)) - DungeonModuleMetrics.WallThickness, originXZ.y); // 지하 위치 (전체 층 높이를 지형 아래로)
            generatedRoot.rotation = Quaternion.identity; // 격자 방향
            SpawnModules(); // 모듈 배치
            SpawnFills(); // 문·금 간 벽·외부 문
            SpawnContent(new System.Random(unchecked(seed ^ 0x5EED1234))); // 회수품·열쇠
            GeneratedMaxY = ComputeMaxY(); // 최고 높이

            if (GeneratedMaxY > InteriorTopY + 0.01f) // 지형 관통 위험
            {
                FailureReason = $"생성물 최고 높이 {GeneratedMaxY:F2}가 한계 {InteriorTopY:F2}를 넘음"; // 사유
                Debug.LogError($"[Project I] 모듈 던전 생성 취소 / {FailureReason}", this); // 오류
                Clear(); // 제거
                return false; // 실패
            }

            IsGenerated = true; // 성공
            Debug.Log($"[Project I] 모듈 던전 생성 / Seed={seed} / 시도 {Plan.Attempt + 1} / 모듈 {Plan.Modules.Count} (방 {Plan.CountOfRole(ModuleRole.Room)} · 복도 {Plan.CountOfRole(ModuleRole.Corridor)}) / 층 {WorldCell.FloorName(Plan.MinFloor)}~{WorldCell.FloorName(Plan.MaxFloor)} · 세로형 {Plan.CountOfRole(ModuleRole.Vertical)} / 보스방 {(Plan.BossIndex >= 0 ? "있음" : "없음")} / 비밀방 {Plan.SecretIndices.Count} / 문 있음 {spawnedDoors.Count} · 문 없음 {openPassageCount} · 벽으로 막힘 {spawnedPlugs.Count} / 회수품 {spawnedLoot.Count} / 외부 연결 문 {interiorDoors.Count} / 최고 높이 {GeneratedMaxY:F1} < 외부 최저 {bottomY:F1}", this); // 결과
            return true; // 성공
        }

        public void Clear() // 생성물 제거
        {
            IsGenerated = false; // 상태 초기화
            Plan = null; // 배치 초기화
            FailureReason = string.Empty; // 사유 초기화
            spawnedModules.Clear(); // 목록 비우기
            spawnedDoors.Clear(); // 목록 비우기
            spawnedBreakables.Clear(); // 목록 비우기
            interiorDoors.Clear(); // 목록 비우기
            spawnedPlugs.Clear(); // 목록 비우기
            openPassageCount = 0; // 집계 초기화
            spawnedLoot.Clear(); // 목록 비우기

            if (generatedRoot != null) // 생성물 존재
            {
                DestroyObject(generatedRoot.gameObject); // 제거
                generatedRoot = null; // 초기화
            }

            for (int index = transform.childCount - 1; index >= 0; index--) // 남은 생성물 정리
            {
                Transform child = transform.GetChild(index); // 자식

                if (child.name == GeneratedRootName) // 생성물 루트
                {
                    DestroyObject(child.gameObject); // 제거
                }
            }
        }

        public ModulePlanConfig BuildConfig() // 배치 규칙 구성
        {
            return new ModulePlanConfig
            {
                TargetModules = targetModules, // 목표
                MinModules = minModules, // 최소
                ExteriorDoorCount = exteriorDoorCount, // 서브문
                EnableBoss = enableBoss, // 보스방
                SecretCount = secretRoomCount, // 비밀방
                GridRadius = gridRadius, // 반경
                FloorsAbove = floorsAbove, // 위층 수
                FloorsBelow = floorsBelow, // 아래층 수
            }; // 반환
        }

        public Vector3 CellToLocal(CellPoint cell) // 격자 칸 → 생성물 루트 로컬 위치 (1층 바닥 기준)
        {
            return CellToLocal(cell, 0); // 기본 층
        }

        public Vector3 CellToLocal(CellPoint cell, int floor) // 격자 칸 + 층 → 생성물 루트 로컬 위치 (칸 한가운데 바닥)
        {
            float size = ModuleDoorway.CellSize; // 칸 크기
            return new Vector3((cell.X + 0.5f) * size, FloorLocalY(floor), (cell.Y + 0.5f) * size); // 위치
        }

        public float FloorLocalY(int floor) // 층 바닥의 로컬 높이
        {
            return (floor - (Plan == null ? 0 : Plan.MinFloor)) * DungeonModuleMetrics.FloorStep; // 최저층을 0으로 맞춤
        }

        public Vector3 ModuleCenterWorld(PlacedModule module) // 모듈 중심 월드 위치 (바닥 높이, 반드시 모듈이 차지한 칸 위)
        {
            Vector3 sum = Vector3.zero; // 합

            foreach (CellPoint cell in module.Cells) // 칸 순회
            {
                sum += CellToLocal(cell, module.Floor); // 누적
            }

            Vector3 average = sum / Mathf.Max(1, module.Cells.Count); // 평균 (ㄷ자·L자 방은 빈 곳에 떨어질 수 있음)
            CellPoint best = module.Cells[0]; // 가장 가까운 칸
            float bestDistance = float.MaxValue; // 거리

            foreach (CellPoint cell in module.Cells) // 칸 순회
            {
                float distance = (CellToLocal(cell, module.Floor) - average).sqrMagnitude; // 거리
                best = distance < bestDistance ? cell : best; // 갱신
                bestDistance = distance < bestDistance ? distance : bestDistance; // 갱신
            }

            return generatedRoot.TransformPoint(CellToLocal(best, module.Floor)); // 실제 칸 한가운데로 보정
        }

        public PlacedModule FindModuleAt(Vector3 worldPosition) // 월드 위치가 속한 모듈
        {
            if (Plan == null || generatedRoot == null) // 생성 전
            {
                return null; // 없음
            }

            Vector3 local = generatedRoot.InverseTransformPoint(worldPosition); // 로컬 위치
            float size = ModuleDoorway.CellSize; // 칸 크기
            CellPoint cell = new CellPoint(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.z / size)); // 칸
            int floor = Mathf.FloorToInt((local.y + FloorProbeTolerance) / DungeonModuleMetrics.FloorStep) + Plan.MinFloor; // 층 (바닥에 딱 붙은 물건이 한 층 아래로 판정되지 않게 여유를 둠)

            foreach (PlacedModule module in Plan.Modules) // 모듈 순회
            {
                if (floor < module.Floor || floor > module.Floor + (module.Definition.FloorSpan - 1)) // 층이 다름
                {
                    continue; // 다음
                }

                foreach (CellPoint occupied in module.Cells) // 칸 순회
                {
                    if (occupied.X == cell.X && occupied.Y == cell.Y) // 일치
                    {
                        return module; // 반환
                    }
                }
            }

            return null; // 밖
        }

        private void SpawnModules() // 배치 결과대로 모듈 프리팹 생성
        {
            foreach (PlacedModule placed in Plan.Modules) // 모듈 순회
            {
                DungeonModule prefab = library.Find(placed.Definition.Id); // 프리팹

                if (prefab == null) // 없음
                {
                    Debug.LogError($"[Project I] 모듈 프리팹을 찾지 못함 / {placed.Definition.Id}", this); // 오류
                    continue; // 다음
                }

                DungeonModule instance = Instantiate(prefab, generatedRoot); // 생성
                instance.name = $"Module_{placed.Index:00}_{placed.Definition.Id}"; // 이름
                instance.transform.localRotation = Quaternion.Euler(0f, -90f * placed.QuarterTurns, 0f); // 90° 단위 회전 (격자 회전과 같은 방향)
                instance.transform.localPosition = OriginToLocal(placed); // 위치
                spawnedModules.Add(instance); // 등록
            }
        }

        private Vector3 OriginToLocal(PlacedModule placed) // 모듈 원점을 로컬 위치로 변환
        {
            float size = ModuleDoorway.CellSize; // 칸 크기
            return new Vector3(placed.Origin.X * size, FloorLocalY(placed.Floor), placed.Origin.Y * size); // 위치 (층 높이 포함)
        }

        private void SpawnFills() // 연결된 출입구에 문·금 간 벽·외부 문 생성
        {
            int exteriorIndex = 0; // 외부 문 번호

            foreach (PlacedModule placed in Plan.Modules) // 모듈 순회
            {
                DungeonModule instance = FindInstance(placed.Index); // 실제 오브젝트

                if (instance == null) // 없음
                {
                    continue; // 다음
                }

                for (int socketIndex = 0; socketIndex < placed.Sockets.Count; socketIndex++) // 출입구 순회
                {
                    PlacedSocket socket = placed.Sockets[socketIndex]; // 출입구
                    DungeonSocket socketObject = instance.SocketAt(socketIndex); // 출입구 오브젝트

                    if (socketObject == null) // 없음
                    {
                        continue; // 다음
                    }

                    if (socket.Fill == PassageFill.Exterior) // 외부 씬 문
                    {
                        SpawnExteriorDoor(socketObject, exteriorIndex++); // 생성
                        continue; // 다음
                    }

                    if (socket.ConnectedModule < 0) // 연결되지 않은 출입구는 벽으로 막음 (프리팹에 뚫려 있는 구멍이 밖으로 이어지지 않게)
                    {
                        socketObject.SetFrameVisible(false); // 문틀도 숨겨 통짜 벽처럼 보이게 함
                        SpawnWallPlug(socketObject, placed.Index, socketIndex); // 막음
                        continue; // 다음
                    }

                    if (socket.ConnectedModule < placed.Index) // 상대가 이미 처리
                    {
                        continue; // 다음
                    }

                    GameObject prefab = library.FillPrefab(socket.Fill); // 채움 프리팹

                    if (prefab == null) // 문 없이 뚫린 통로
                    {
                        openPassageCount++; // 집계
                        continue; // 다음
                    }

                    GameObject fill = Instantiate(prefab, socketObject.DoorAnchor.position, socketObject.DoorAnchor.rotation, generatedRoot); // 생성
                    fill.name = $"{prefab.name}_{placed.Index:00}_{socketIndex:00}"; // 이름
                    RegisterFill(fill, socket.Fill); // 등록
                }
            }
        }

        private void SpawnContent(System.Random random) // 회수품·열쇠 생성
        {
            int maxDepth = 1; // 최대 깊이

            foreach (PlacedModule module in Plan.Modules) // 모듈 순회
            {
                maxDepth = module.Depth > maxDepth ? module.Depth : maxDepth; // 갱신
            }

            SpawnKey(random); // 열쇠 (잠긴 문 앞쪽에만 놓임)

            foreach (PlacedModule module in Plan.Modules) // 모듈 순회
            {
                if (module.Role == ModuleRole.Corridor || module.Role == ModuleRole.Vertical || module.Role == ModuleRole.Entrance) // 복도·세로형 방·시작 방은 제외
                {
                    continue; // 다음
                }

                bool always = module.Role == ModuleRole.Boss || module.Role == ModuleRole.Secret; // 보스방·비밀방은 반드시

                if (!always && random.NextDouble() > lootChancePerRoom) // 확률
                {
                    continue; // 다음
                }

                float ratio = always ? 1f : module.Depth / (float)maxDepth; // 깊이 비율 (보스방·비밀방은 가장 깊은 등급)
                InteriorLootEntry entry = PickLoot(ratio, random); // 회수품 선택

                if (entry != null) // 후보 확인
                {
                    SpawnItem(entry.definition, module, random, random.Next(entry.minValue, entry.maxValue + 1)); // 생성
                }
            }
        }

        private void SpawnKey(System.Random random) // 잠긴 문을 지나지 않고 갈 수 있는 방에 열쇠를 놓음
        {
            if (keyDefinition == null) // 열쇠 없음
            {
                return; // 종료
            }

            bool hasLocked = false; // 잠긴 문 존재 여부

            foreach (PlacedSocket socket in Plan.AllSockets()) // 출입구 순회
            {
                hasLocked |= socket.Fill == PassageFill.LockedDoor; // 확인
            }

            if (!hasLocked) // 잠긴 문이 없으면 열쇠도 불필요
            {
                return; // 종료
            }

            PlacedModule best = null; // 가장 깊은 방

            foreach (int index in ReachableWithoutLockedDoors()) // 잠긴 문 앞쪽 모듈 순회
            {
                PlacedModule module = Plan.Module(index); // 모듈

                if (module.Role != ModuleRole.Room) // 일반 방에만
                {
                    continue; // 다음
                }

                best = best == null || module.Depth > best.Depth ? module : best; // 갱신
            }

            if (best == null) // 놓을 방 없음
            {
                return; // 종료
            }

            SpawnItem(keyDefinition, best, random, -1); // 열쇠 생성
        }

        private List<int> ReachableWithoutLockedDoors() // 시작 방에서 잠긴 문·금 간 벽을 지나지 않고 갈 수 있는 모듈
        {
            List<int> reachable = new List<int>(); // 결과
            HashSet<int> visited = new HashSet<int> { Plan.EntranceIndex }; // 방문
            Queue<int> queue = new Queue<int>(); // 탐색
            queue.Enqueue(Plan.EntranceIndex); // 시작

            while (queue.Count > 0) // 너비 우선 탐색
            {
                int current = queue.Dequeue(); // 현재
                reachable.Add(current); // 등록

                foreach (PlacedSocket socket in Plan.Module(current).Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0 || socket.Fill == PassageFill.LockedDoor || socket.Fill == PassageFill.Breakable) // 연결 없음·막힌 연결
                    {
                        continue; // 다음
                    }

                    if (visited.Add(socket.ConnectedModule)) // 처음 방문
                    {
                        queue.Enqueue(socket.ConnectedModule); // 등록
                    }
                }
            }

            return reachable; // 반환
        }

        private InteriorLootEntry PickLoot(float depthRatio, System.Random random) // 깊이 비율에 맞는 회수품 가중 선택
        {
            List<InteriorLootEntry> candidates = new List<InteriorLootEntry>(); // 후보
            int total = 0; // 가중 합

            foreach (InteriorLootEntry entry in lootTable) // 표 순회
            {
                bool usable = entry != null && entry.definition != null && depthRatio >= entry.minDepthRatio && depthRatio <= entry.maxDepthRatio && entry.weight > 0; // 구간 확인

                if (usable) // 후보
                {
                    candidates.Add(entry); // 등록
                    total += entry.weight; // 합계
                }
            }

            if (candidates.Count == 0) // 후보 없음
            {
                return null; // 없음
            }

            int roll = random.Next(total); // 추첨

            foreach (InteriorLootEntry entry in candidates) // 누적 비교
            {
                roll -= entry.weight; // 차감

                if (roll < 0) // 선택
                {
                    return entry; // 반환
                }
            }

            return candidates[candidates.Count - 1]; // 안전 반환
        }

        private void SpawnItem(ItemDefinition definition, PlacedModule module, System.Random random, int value) // 모듈 안 칸 위에 아이템 생성
        {
            if (definition == null || definition.RecoveryPrefab == null) // 정의 확인
            {
                return; // 생성 불가
            }

            CellPoint cell = PickInnerCell(module, random); // 벽에서 떨어진 칸
            float jitter = ModuleDoorway.CellSize * 0.18f; // 칸 안 흔들기
            Vector3 local = CellToLocal(cell, module.Floor) + new Vector3(((float)random.NextDouble() * 2f - 1f) * jitter, 0.6f, ((float)random.NextDouble() * 2f - 1f) * jitter); // 로컬 위치
            Vector3 world = generatedRoot.TransformPoint(local); // 월드 위치
            GameObject instance = Instantiate(definition.RecoveryPrefab, world, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f)); // 위치 지정 생성
            instance.transform.SetParent(generatedRoot, true); // 환경 씬 소속
            instance.name = $"{definition.DisplayName}_M{module.Index:00}"; // 이름
            RecoverableValue recoverable = instance.GetComponent<RecoverableValue>(); // 가격

            if (recoverable != null && value > 0) // 회수품 가치 지정
            {
                recoverable.Configure(value); // 깊이별 가치
            }

            WorldItem item = instance.GetComponent<WorldItem>(); // WorldItem

            if (item != null) // 확인
            {
                spawnedLoot.Add(item); // 등록
            }
        }

        private CellPoint PickInnerCell(PlacedModule module, System.Random random) // 네 이웃이 모두 같은 모듈인 칸 우선 (벽에 끼는 것 방지)
        {
            HashSet<CellPoint> cells = new HashSet<CellPoint>(module.Cells); // 빠른 조회
            List<CellPoint> inner = new List<CellPoint>(); // 안쪽 칸

            foreach (CellPoint cell in module.Cells) // 칸 순회
            {
                bool surrounded = true; // 둘러싸임 여부

                foreach (GridDirection direction in GridDirections.All) // 네 방향
                {
                    surrounded &= cells.Contains(cell.Step(direction)); // 확인
                }

                if (surrounded) // 안쪽 칸
                {
                    inner.Add(cell); // 등록
                }
            }

            List<CellPoint> source = inner.Count > 0 ? inner : new List<CellPoint>(module.Cells); // 없으면 전체
            return source[random.Next(source.Count)]; // 무작위 선택
        }

        private void SpawnWallPlug(DungeonSocket socketObject, int moduleIndex, int socketIndex) // 연결되지 않은 출입구를 막는 벽
        {
            GameObject prefab = library.WallPlugPrefab; // 프리팹

            if (prefab == null) // 없음
            {
                Debug.LogWarning("[Project I] 막음 벽 프리팹이 없어 출입구를 막지 못했습니다. Build Module Set 을 다시 실행하세요.", this); // 경고
                return; // 종료
            }

            GameObject plug = Instantiate(prefab, socketObject.DoorAnchor.position, socketObject.DoorAnchor.rotation, generatedRoot); // 생성
            plug.name = $"Wall_Plug_{moduleIndex:00}_{socketIndex:00}"; // 이름
            spawnedPlugs.Add(plug); // 등록
        }

        private void SpawnExteriorDoor(DungeonSocket socketObject, int index) // 외부 씬과 잇는 순간이동 문
        {
            GameObject prefab = library.ExteriorDoorPrefab; // 프리팹

            if (prefab == null) // 없음
            {
                return; // 종료
            }

            GameObject door = Instantiate(prefab, socketObject.DoorAnchor.position, socketObject.DoorAnchor.rotation, generatedRoot); // 생성
            DungeonDoorKind kind = index == 0 ? DungeonDoorKind.Main : DungeonDoorKind.Sub; // 정문·서브문
            int subIndex = index == 0 ? 0 : index - 1; // 서브문 번호
            door.name = kind == DungeonDoorKind.Main ? "InteriorMainDoor" : $"InteriorSubDoor_{subIndex + 1}"; // 이름
            DungeonTeleportDoor teleport = door.GetComponentInChildren<DungeonTeleportDoor>(true); // 순간이동 문
            Transform arrival = door.transform.Find("ArrivalPoint"); // 도착 지점

            if (teleport != null) // 확인
            {
                teleport.Configure(DungeonDoorSide.Interior, kind, subIndex, arrival); // 구성
                interiorDoors.Add(teleport); // 등록
            }
        }

        private void RegisterFill(GameObject fill, PassageFill kind) // 생성한 채움을 목록에 등록
        {
            DungeonDoor door = fill.GetComponentInChildren<DungeonDoor>(true); // 문

            if (door != null) // 문
            {
                spawnedDoors.Add(door); // 등록
                return; // 종료
            }

            BreakableWall breakable = fill.GetComponentInChildren<BreakableWall>(true); // 금 간 벽

            if (breakable != null) // 금 간 벽
            {
                spawnedBreakables.Add(breakable); // 등록
            }
        }

        private DungeonModule FindInstance(int placedIndex) // 배치 번호로 생성된 모듈 조회
        {
            return placedIndex >= 0 && placedIndex < spawnedModules.Count ? spawnedModules[placedIndex] : null; // 반환
        }

        private float ComputeExteriorBottomY() // 외부 씬 지형의 최저 높이
        {
            Transform reference = exteriorReference != null ? exteriorReference : transform; // 기준
            float lowest = reference.position.y; // 기본
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None); // 지형

            foreach (Terrain terrain in terrains) // 지형 순회
            {
                lowest = Mathf.Min(lowest, terrain.transform.position.y); // 최저
            }

            return lowest; // 반환
        }

        private float ComputeMaxY() // 생성물 최고 높이
        {
            float highest = float.MinValue; // 최고

            foreach (Renderer renderer in generatedRoot.GetComponentsInChildren<Renderer>(true)) // 렌더러 순회
            {
                highest = Mathf.Max(highest, renderer.bounds.max.y); // 최고
            }

            return highest <= float.MinValue ? generatedRoot.position.y : highest; // 반환
        }

        private static void DestroyObject(GameObject target) // 실행 중·에디터 모두에서 제거
        {
            if (Application.isPlaying) // 실행 중
            {
                Destroy(target); // 제거
                return; // 종료
            }

            DestroyImmediate(target); // 즉시 제거
        }

        private void Start() // 환경 씬 로드 직후 생성
        {
            if (Application.isPlaying) // 플레이 중에만 자동 생성
            {
                Generate(ResolveSeed()); // 캠페인·일차 기준 생성
            }
        }

        public static ModuleDungeonGenerator FindInScene(Scene scene) // 씬의 모듈 생성기 조회
        {
            if (!scene.IsValid() || !scene.isLoaded) // 씬 확인
            {
                return null; // 없음
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                ModuleDungeonGenerator generator = root.GetComponentInChildren<ModuleDungeonGenerator>(true); // 조회

                if (generator != null) // 발견
                {
                    return generator; // 반환
                }
            }

            return null; // 없음
        }

        public int ResolveSeed() // 같은 캠페인·일차·지역이면 같은 시드
        {
            if (seedOverride != 0) // 테스트 고정 시드
            {
                return seedOverride; // 반환
            }

            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스
            int campaign = service == null ? 1 : service.CampaignSeed; // 캠페인 시드
            int day = service == null ? 1 : service.CurrentDay; // 일차
            return DungeonSeed.For(campaign, day, gameObject.scene.name); // 결정적 시드
        }

        public bool UseDoor(DungeonTeleportDoor door) // 짝이 되는 문으로 플레이어 순간이동
        {
            if (!IsGenerated || door == null) // 생성 확인
            {
                return false; // 실패
            }

            List<DungeonTeleportDoor> targets = door.Side == DungeonDoorSide.Exterior ? interiorDoors : exteriorDoors; // 반대편 문 목록
            DungeonTeleportDoor target = targets.Find(candidate => candidate != null && candidate.Kind == door.Kind && candidate.SubIndex == door.SubIndex); // 같은 종류·번호

            if (target == null) // 짝 누락
            {
                return false; // 실패
            }

            PersistentMapLoader loader = PersistentMapLoader.Instance; // 순간이동 담당
            return loader != null && loader.RequestPlayerTeleport(target.ArrivalPosition, target.ArrivalRotation); // 암전 이동
        }

        public DungeonTeleportDoor FindDoor(DungeonDoorSide side, DungeonDoorKind kind, int subIndex) // 문 조회 (테스트·진단용)
        {
            List<DungeonTeleportDoor> doors = side == DungeonDoorSide.Exterior ? exteriorDoors : interiorDoors; // 목록
            return doors.Find(door => door != null && door.Kind == kind && (kind == DungeonDoorKind.Main || door.SubIndex == subIndex)); // 조회
        }

        private void CollectExteriorDoors() // 같은 씬의 외부 문 수집 (생성물 제외)
        {
            exteriorDoors.Clear(); // 초기화

            foreach (GameObject root in gameObject.scene.GetRootGameObjects()) // 루트 순회
            {
                foreach (DungeonTeleportDoor door in root.GetComponentsInChildren<DungeonTeleportDoor>(true)) // 문 순회
                {
                    if (door.Side == DungeonDoorSide.Exterior) // 외부 문만
                    {
                        exteriorDoors.Add(door); // 등록
                    }
                }
            }
        }

        private bool ValidateExteriorDoors(out int subDoorCount) // 외부 정문 1개 + 서브문 번호 0..N-1 연속·중복 없음
        {
            subDoorCount = 0; // 기본값
            int mainCount = exteriorDoors.FindAll(door => door.Kind == DungeonDoorKind.Main).Count; // 정문 수
            List<DungeonTeleportDoor> subs = exteriorDoors.FindAll(door => door.Kind == DungeonDoorKind.Sub); // 서브문

            if (mainCount != 1) // 정문 1개
            {
                FailureReason = $"외부 정문 {mainCount}개 (정확히 1개 필요)"; // 사유
                return false; // 실패
            }

            HashSet<int> indices = new HashSet<int>(); // 번호 검사

            foreach (DungeonTeleportDoor sub in subs) // 서브문 순회
            {
                if (sub.SubIndex < 0 || sub.SubIndex >= subs.Count || !indices.Add(sub.SubIndex)) // 0..N-1 연속·중복 없음
                {
                    FailureReason = $"외부 서브문 번호 {sub.SubIndex} 중복 또는 범위 밖 (0~{subs.Count - 1} 필요)"; // 사유
                    return false; // 실패
                }
            }

            subDoorCount = subs.Count; // 외부 서브문 수 = 실내 서브문 수
            return true; // 통과
        }

        private void Reset() // 컴포넌트 추가 시 기본값
        {
            exteriorReference = transform; // 기준
        }
    }

    public static class DungeonModuleMetrics // 모듈 공통 치수
    {
        public const float WallThickness = 0.25f; // 벽·바닥·천장 두께
        public const float RoomHeight = 4f; // 방 높이
        public const float ModuleTotalHeight = RoomHeight + (WallThickness * 2f); // 바닥 아래부터 천장 위까지
        public const float FloorStep = RoomHeight + (WallThickness * 2f); // 한 층 높이 (4.50m)
    }
}
