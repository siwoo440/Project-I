using System; // 직렬화·난수 기능 사용
using System.Collections.Generic; // 목록 기능 사용
using ProjectI.Economy; // 회수품 가격 참조
using ProjectI.Generation; // 순수 C# 생성 핵심 참조
using ProjectI.Items; // ItemDefinition 참조
using ProjectI.Loop; // 맵 로더 순간이동 참조
using ProjectI.Persistence; // 캠페인 시드·일차 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [Serializable]
    public sealed class InteriorLootEntry // 깊이 구간별 회수품 후보
    {
        public ItemDefinition definition; // 생성할 아이템 정의
        public int minValue = 200; // 최소 가치
        public int maxValue = 400; // 최대 가치
        [Range(0f, 1f)] public float minDepthRatio; // 사용 가능한 최소 깊이 비율
        [Range(0f, 1f)] public float maxDepthRatio = 1f; // 사용 가능한 최대 깊이 비율
        public int weight = 1; // 선택 가중치
    }

    [DisallowMultipleComponent] // 환경 씬당 하나
    public sealed class ProceduralInteriorGenerator : MonoBehaviour // 외부 씬 지하에 다층 실내 던전을 생성하고 외부 문과 1:1로 연결
    {
        private const string GeneratedRootName = "Day27_GeneratedInterior"; // 생성물 루트 이름

        [Header("생성 규칙")]
        [SerializeField] private int minRooms = 14; // 1층 최소 방 수
        [SerializeField] private int maxRooms = 20; // 1층 최대 방 수
        [SerializeField] private int gridMinX = -3; // 격자 최소 X
        [SerializeField] private int gridMaxX = 3; // 격자 최대 X
        [SerializeField] private int gridMaxY = 5; // 격자 최대 Y
        [SerializeField] private bool enableLockedDoor = true; // 잠긴 문 사용
        [SerializeField] private int seedOverride; // 0이 아니면 고정 시드 (테스트용)

        [Header("층 (메인 방 = 1층)")]
        [SerializeField] private int floorsAbove = 2; // 메인 방 위로 생성할 층 수
        [SerializeField] private int floorsBelow = 2; // 메인 방 아래로 생성할 층 수
        [SerializeField] private int minOtherFloorRooms = 6; // 1층 외 각 층 최소 방 수
        [SerializeField] private int maxOtherFloorRooms = 11; // 1층 외 각 층 최대 방 수
        [SerializeField] private VerticalKind forcedVerticalKind = VerticalKind.None; // 테스트용: None이 아니면 모든 세로형 방을 이 종류로 고정

        [Header("크기 (m)")]
        [SerializeField] private float cellSize = 9f; // 격자 칸 크기
        [SerializeField] private float roomSize = 7f; // 방 내부 크기
        [SerializeField] private float corridorSize = 3.5f; // 복도 내부 크기
        [SerializeField] private float roomHeight = 4f; // 방 높이
        [SerializeField] private float wallThickness = 0.4f; // 벽·바닥·천장 두께
        [SerializeField] private float doorWidth = 2.2f; // 방 문 폭
        [SerializeField] private float doorHeight = 2.8f; // 방 문 높이
        [SerializeField] private float surfaceClearance = 12f; // 최상층 천장부터 외부 씬 최저 지형까지 여유 깊이
        [SerializeField] private Vector2 gridOriginXZ = new Vector2(0f, -38f); // 시작 방 중심의 월드 XZ (지형 아래)

        [Header("계단·사다리")]
        [SerializeField] private float stairWidth = 2.6f; // 계단 폭
        [SerializeField] private float stairStepHeight = 0.3f; // 계단 한 칸 높이
        [SerializeField] private float stairEntryInset = 0.9f; // 계단 앞 평지 길이
        [SerializeField] private float stairRampThickness = 0.4f; // 계단 경사 충돌체 두께
        [SerializeField] private float ladderHoleWidth = 1.6f; // 사다리 구멍 폭
        [SerializeField] private float ladderHoleDepth = 1.3f; // 사다리 구멍 깊이
        [SerializeField] private float ladderTopOverhang = 1f; // 사다리가 위층 바닥 위로 올라오는 길이

        [Header("재질")]
        [SerializeField] private Material floorMaterial; // 바닥
        [SerializeField] private Material wallMaterial; // 벽·천장
        [SerializeField] private Material doorMaterial; // 출입문
        [SerializeField] private Material lockedDoorMaterial; // 잠긴 문
        [SerializeField] private Material stairMaterial; // 계단 (없으면 바닥 재질)
        [SerializeField] private Material ladderMaterial; // 사다리 (없으면 출입문 재질)

        [Header("아이템")]
        [SerializeField] private ItemDefinition keyDefinition; // 열쇠 정의
        [SerializeField] private InteriorLootEntry[] lootTable = Array.Empty<InteriorLootEntry>(); // 회수품 표

        private readonly List<DungeonTeleportDoor> exteriorDoors = new List<DungeonTeleportDoor>(); // 외부 문
        private readonly List<DungeonTeleportDoor> interiorDoors = new List<DungeonTeleportDoor>(); // 실내 문
        private readonly List<WorldItem> spawnedLoot = new List<WorldItem>(); // 생성 회수품
        private readonly List<DungeonLadder> ladders = new List<DungeonLadder>(); // 생성 사다리
        private Transform generatedRoot; // 생성물 루트
        private int baseFloor; // 생성물 루트 높이에 해당하는 층 (= 최저 층)

        public DungeonLayout Layout { get; private set; } // 생성 그래프
        public bool IsGenerated { get; private set; } // 생성 성공 여부
        public int Seed { get; private set; } // 사용 시드
        public float ExteriorBottomY { get; private set; } // 외부 씬 최저 높이
        public float InteriorTopY { get; private set; } // 실내 생성물 최고 높이 한계
        public float GeneratedMaxY { get; private set; } // 실제 생성물 최고 높이
        public string FailureReason { get; private set; } = string.Empty; // 실패 사유
        public IReadOnlyList<DungeonTeleportDoor> ExteriorDoors => exteriorDoors; // 외부 문 공개
        public IReadOnlyList<DungeonTeleportDoor> InteriorDoors => interiorDoors; // 실내 문 공개
        public IReadOnlyList<WorldItem> SpawnedLoot => spawnedLoot; // 생성 회수품 공개
        public IReadOnlyList<DungeonLadder> Ladders => ladders; // 사다리 공개
        public Transform GeneratedRoot => generatedRoot; // 생성물 루트 공개
        public float CellSize => cellSize; // 격자 크기 공개
        public float RoomHeight => roomHeight; // 방 높이 공개
        public float FloorStep => roomHeight + (wallThickness * 2f); // 한 층 높이 (바닥 위 → 위층 바닥 위)
        public int SeedOverride => seedOverride; // 고정 시드 공개 (0이면 캠페인·일차 기준)
        public VerticalKind ForcedVerticalKind => forcedVerticalKind; // 고정 세로형 방 종류 공개
        public int FloorsAboveSetting => floorsAbove; // 위층 수 공개
        public int FloorsBelowSetting => floorsBelow; // 지하층 수 공개

        private void Start() // 환경 씬 로드 직후 생성
        {
            if (Application.isPlaying) // 플레이 중에만 자동 생성
            {
                Generate(ResolveSeed()); // 캠페인·일차 기준 생성
            }
        }

        public static ProceduralInteriorGenerator FindInScene(Scene scene) // 씬의 생성기 조회
        {
            if (!scene.IsValid() || !scene.isLoaded) // 씬 확인
            {
                return null; // 없음
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                ProceduralInteriorGenerator generator = root.GetComponentInChildren<ProceduralInteriorGenerator>(true); // 조회

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

        public void Configure(Material floor, Material wall, Material door, Material lockedDoor, ItemDefinition key, InteriorLootEntry[] loot) // 에디터 구성
        {
            floorMaterial = floor; // 바닥
            wallMaterial = wall; // 벽
            doorMaterial = door; // 출입문
            lockedDoorMaterial = lockedDoor; // 잠긴 문
            keyDefinition = key; // 열쇠
            lootTable = loot ?? Array.Empty<InteriorLootEntry>(); // 회수품 표
        }

        public void ConfigureStairMaterials(Material stair, Material ladder) // 에디터 구성 (계단·사다리 재질)
        {
            stairMaterial = stair; // 계단
            ladderMaterial = ladder; // 사다리
        }

        public void ConfigureTestOverrides(int seed, VerticalKind forcedKind) // 테스트용 고정 시드·세로형 방 종류 지정 (0·None이면 해제)
        {
            seedOverride = seed; // 고정 시드
            forcedVerticalKind = forcedKind; // 고정 종류
        }

        public void ConfigureFloorRange(int above, int below) // 테스트용 층 수 지정
        {
            floorsAbove = Mathf.Max(0, above); // 위층 수
            floorsBelow = Mathf.Max(0, below); // 지하층 수
        }

        public DungeonGenerationConfig BuildConfig(int subDoorCount) // 씬 설정 → 생성 규칙
        {
            return new DungeonGenerationConfig // 규칙 생성
            {
                MinRooms = minRooms, // 1층 최소 방
                MaxRooms = maxRooms, // 1층 최대 방
                MinOtherFloorRooms = minOtherFloorRooms, // 다른 층 최소 방
                MaxOtherFloorRooms = maxOtherFloorRooms, // 다른 층 최대 방
                FloorsAbove = Mathf.Max(0, floorsAbove), // 위층 수
                FloorsBelow = Mathf.Max(0, floorsBelow), // 지하층 수
                ForcedVerticalKind = forcedVerticalKind, // 테스트용 세로형 방 종류 고정
                GridMinX = gridMinX, // 격자
                GridMaxX = gridMaxX, // 격자
                GridMaxY = gridMaxY, // 격자
                EnableLockedDoor = enableLockedDoor, // 잠긴 문
                SubDoorCount = subDoorCount // 외부 서브문 수와 동일
            };
        }

        public bool Generate(int seed) // 실내 던전 생성 (이전 생성물은 제거)
        {
            Clear(); // 이전 생성물 제거
            Seed = seed; // 시드 기록
            CollectExteriorDoors(); // 외부 문 수집

            if (!ValidateExteriorDoors(out int subDoorCount)) // 외부 문 구성 확인
            {
                Debug.LogError($"[Project I] 실내 던전 생성 취소 / {FailureReason}", this); // 오류
                return false; // 실패
            }

            DungeonGenerationConfig config = BuildConfig(subDoorCount); // 규칙
            Layout = DungeonLayoutGenerator.Generate(config, seed); // 그래프 생성

            if (Layout == null) // 생성 실패
            {
                FailureReason = $"시드 {seed}에서 규칙을 만족하는 구조를 찾지 못함"; // 사유
                Debug.LogError($"[Project I] 실내 던전 생성 실패 / {FailureReason}", this); // 오류
                return false; // 실패
            }

            ExteriorBottomY = ComputeExteriorBottomY(); // 외부 최저 높이
            InteriorTopY = ExteriorBottomY - surfaceClearance; // 실내 최고 높이 한계
            baseFloor = Layout.MinFloor; // 최저 층 기준
            int floorSpan = Layout.MaxFloor - Layout.MinFloor; // 층 높이 차
            float rootY = InteriorTopY - ((floorSpan * FloorStep) + roomHeight + wallThickness); // 최저 층 바닥 높이 (최상층 천장이 한계에 닿도록)
            generatedRoot = new GameObject(GeneratedRootName).transform; // 생성물 루트
            generatedRoot.SetParent(transform, false); // 생성기 아래 (환경 씬 소속)
            generatedRoot.position = new Vector3(gridOriginXZ.x, rootY, gridOriginXZ.y); // 지하 위치
            generatedRoot.rotation = Quaternion.identity; // 격자 방향 고정
            System.Random random = new System.Random(unchecked(seed ^ 0x5EED1234)); // 배치용 결정적 난수

            foreach (RoomNode room in Layout.Rooms) // 방 생성
            {
                if (room.IsVertical) // 세로형 방
                {
                    BuildVerticalRoom(room); // 계단통·사다리 방·수직 통로
                }
                else // 일반 방·복도
                {
                    BuildRoom(room); // 바닥·벽·천장·조명
                }
            }

            for (int index = 0; index < Layout.Doors.Count; index++) // 방 사이 통로
            {
                BuildPassage(Layout.Doors[index]); // 통로·잠긴 문
            }

            BuildInteriorDoor(Layout.Room(Layout.StartRoomId), Layout.MainDoorWall, DungeonDoorKind.Main, 0); // 실내 정문

            foreach (SubDoorPlacement sub in Layout.SubDoors) // 실내 서브문
            {
                BuildInteriorDoor(Layout.Room(sub.RoomId), sub.Wall, DungeonDoorKind.Sub, sub.Index); // 번호별 생성
            }

            SpawnContent(random); // 회수품·열쇠

            if (!VerifyUnderground()) // 지형 관통 최종 확인
            {
                Debug.LogError($"[Project I] 실내 던전 생성 취소 / {FailureReason}", this); // 오류
                Clear(); // 생성물 제거
                return false; // 실패
            }

            IsGenerated = true; // 성공
            Debug.Log($"[Project I] 실내 던전 생성 / Seed={seed} / 시도 {Layout.Attempt + 1} / 방 {Layout.Rooms.Count} / 층 {GridPoint.FloorName(Layout.MinFloor)}~{GridPoint.FloorName(Layout.MaxFloor)} / 세로형 방 {CountVerticalRooms()} / 서브문 {Layout.SubDoors.Count} (외부 {subDoorCount}) / 회수품 {spawnedLoot.Count} / 최고 높이 {GeneratedMaxY:F1} < 외부 최저 {ExteriorBottomY:F1}", this); // 결과
            return true; // 성공
        }

        public void Clear() // 생성물 제거
        {
            IsGenerated = false; // 상태 초기화
            Layout = null; // 그래프 초기화
            FailureReason = string.Empty; // 사유 초기화
            interiorDoors.Clear(); // 실내 문 목록
            spawnedLoot.Clear(); // 회수품 목록
            ladders.Clear(); // 사다리 목록

            for (int index = transform.childCount - 1; index >= 0; index--) // 기존 루트 제거
            {
                Transform child = transform.GetChild(index); // 자식

                if (child.name != GeneratedRootName) // 생성물 루트만
                {
                    continue; // 다음
                }

                if (Application.isPlaying) // 플레이 중
                {
                    Destroy(child.gameObject); // 제거
                }
                else // 에디터 미리보기
                {
                    DestroyImmediate(child.gameObject); // 즉시 제거
                }
            }

            generatedRoot = null; // 참조 초기화
        }

        public int CountVerticalRooms() // 세로형 방 수
        {
            int count = 0; // 결과

            if (Layout != null) // 생성 확인
            {
                foreach (RoomNode _ in Layout.VerticalRooms) // 순회
                {
                    count++; // 집계
                }
            }

            return count; // 반환
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

        public float FloorWorldY(int floor) // 층 바닥 월드 높이
        {
            return generatedRoot == null ? 0f : generatedRoot.position.y + FloorLocalY(floor); // 변환
        }

        public Vector3 RoomCenterWorld(RoomNode room) // 방 중심 월드 위치 (방 기준 층 바닥 높이)
        {
            return generatedRoot == null ? Vector3.zero : generatedRoot.TransformPoint(CellCenter(room.Cell)); // 변환
        }

        public RoomNode FindRoomAt(Vector3 worldPosition) // 월드 위치가 속한 방 (없으면 null)
        {
            if (Layout == null || generatedRoot == null) // 생성 확인
            {
                return null; // 없음
            }

            Vector3 local = generatedRoot.InverseTransformPoint(worldPosition); // 격자 좌표계
            int floorIndex = Mathf.FloorToInt((local.y + 1f) / FloorStep); // 층 번호 (바닥 아래 1m 여유)
            int floor = baseFloor + floorIndex; // 실제 층
            float floorLocalY = FloorLocalY(floor); // 해당 층 바닥 높이

            if (local.y < floorLocalY - 1f || local.y > floorLocalY + roomHeight + 1f) // 층 높이 범위
            {
                return null; // 없음
            }

            int cellX = Mathf.RoundToInt(local.x / cellSize); // 칸 X
            int cellY = Mathf.RoundToInt(local.z / cellSize); // 칸 Y
            RoomNode found = null; // 결과

            foreach (RoomNode room in Layout.Rooms) // 방 순회
            {
                if (room.Cell.X == cellX && room.Cell.Y == cellY && room.OccupiesFloor(floor)) // 칸·층 확인
                {
                    found = room; // 발견
                    break; // 종료
                }
            }

            if (found == null) // 방 없음
            {
                return null; // 없음
            }

            float half = HalfSize(found) + 0.5f; // 방 반크기 + 여유
            float centerX = found.Cell.X * cellSize; // 방 중심 X
            float centerZ = found.Cell.Y * cellSize; // 방 중심 Z
            return Mathf.Abs(local.x - centerX) <= half && Mathf.Abs(local.z - centerZ) <= half ? found : null; // 방 내부 여부
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

        private float ComputeExteriorBottomY() // 외부 씬(생성물 제외)의 가장 낮은 지형·충돌체·렌더러 높이
        {
            Physics.SyncTransforms(); // 충돌체 경계값 최신화
            float bottom = float.MaxValue; // 최저 높이

            foreach (GameObject root in gameObject.scene.GetRootGameObjects()) // 루트 순회
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true)) // Terrain 바닥면 (높이맵 0)
                {
                    bottom = Mathf.Min(bottom, terrain.GetPosition().y); // 최저 갱신
                }

                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) // 충돌체
                {
                    if (!BelongsToGenerated(collider.transform)) // 생성물 제외
                    {
                        bottom = Mathf.Min(bottom, collider.bounds.min.y); // 최저 갱신
                    }
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) // 렌더러
                {
                    if (!BelongsToGenerated(renderer.transform)) // 생성물 제외
                    {
                        bottom = Mathf.Min(bottom, renderer.bounds.min.y); // 최저 갱신
                    }
                }
            }

            return bottom == float.MaxValue ? 0f : bottom; // 결과
        }

        private bool BelongsToGenerated(Transform target) // 생성물 여부
        {
            return generatedRoot != null && target.IsChildOf(generatedRoot); // 루트 하위 확인
        }

        private bool VerifyUnderground() // 모든 생성물이 외부 최저 높이보다 여유 깊이 아래인지 확인
        {
            Physics.SyncTransforms(); // 방금 만든 충돌체의 경계값을 최신 위치로 동기화
            GeneratedMaxY = float.MinValue; // 초기화
            string highest = string.Empty; // 가장 높은 생성물 이름

            foreach (Renderer renderer in generatedRoot.GetComponentsInChildren<Renderer>(true)) // 렌더러
            {
                if (renderer.bounds.max.y > GeneratedMaxY) // 최고 갱신
                {
                    GeneratedMaxY = renderer.bounds.max.y; // 높이
                    highest = renderer.name; // 이름
                }
            }

            foreach (Collider collider in generatedRoot.GetComponentsInChildren<Collider>(true)) // 충돌체
            {
                if (collider.bounds.max.y > GeneratedMaxY) // 최고 갱신
                {
                    GeneratedMaxY = collider.bounds.max.y; // 높이
                    highest = collider.name; // 이름
                }
            }

            if (GeneratedMaxY > InteriorTopY + 0.01f) // 한계 초과
            {
                FailureReason = $"생성물 최고 높이 {GeneratedMaxY:F2}({highest})가 한계 {InteriorTopY:F2}를 넘음 (지형 관통 위험)"; // 사유
                return false; // 실패
            }

            return true; // 통과
        }

        private float FloorLocalY(int floor) // 층 바닥의 루트 로컬 높이
        {
            return (floor - baseFloor) * FloorStep; // 층 간격
        }

        private Vector3 CellCenter(GridPoint cell) // 격자 칸 중심 (루트 로컬, 해당 층 바닥 높이)
        {
            return new Vector3(cell.X * cellSize, FloorLocalY(cell.Floor), cell.Y * cellSize); // 로컬 위치
        }

        private Vector3 CellCenterAtFloor(GridPoint cell, int floor) // 격자 칸 중심 (지정 층 바닥 높이)
        {
            return new Vector3(cell.X * cellSize, FloorLocalY(floor), cell.Y * cellSize); // 로컬 위치
        }

        private float HalfSize(RoomNode room) // 방 내부 반크기
        {
            if (room.Kind == RoomKind.Corridor || room.Vertical == VerticalKind.Shaft) // 복도·좁은 수직 통로
            {
                return corridorSize * 0.5f; // 좁은 반크기
            }

            return roomSize * 0.5f; // 방 반크기
        }

        private static Vector3 DirectionVector(GridDirection direction) // 격자 방향 → 월드 방향
        {
            switch (direction) // 방향별
            {
                case GridDirection.North: return Vector3.forward; // +Z
                case GridDirection.East: return Vector3.right; // +X
                case GridDirection.South: return Vector3.back; // -Z
                default: return Vector3.left; // -X
            }
        }

        private static string RoomObjectName(RoomNode room) // 방 GameObject 이름
        {
            string kind = room.IsVertical ? room.Vertical.ToString() : room.Kind.ToString(); // 종류 이름
            return $"Room_{room.Id:00}_{room.Cell.X}_{room.Cell.Y}_{GridPoint.FloorName(room.Cell.Floor)}_{kind}"; // 이름
        }

        private void BuildRoom(RoomNode room) // 일반 방·복도 바닥·천장·벽·조명
        {
            Transform roomRoot = new GameObject(RoomObjectName(room)).transform; // 방 루트
            roomRoot.SetParent(generatedRoot, false); // 생성물 루트 아래
            roomRoot.localPosition = CellCenter(room.Cell); // 방 중심 (층 높이 포함)
            float half = HalfSize(room); // 반크기
            float t = wallThickness; // 두께
            float span = (half * 2f) + (t * 2f); // 벽 포함 전체 폭
            CreateBox(roomRoot, "Floor", new Vector3(0f, -t * 0.5f, 0f), new Vector3(span, t, span), floorMaterial); // 바닥
            CreateBox(roomRoot, "Ceiling", new Vector3(0f, roomHeight + (t * 0.5f), 0f), new Vector3(span, t, span), wallMaterial); // 천장
            BuildWalls(roomRoot, room, half, roomHeight); // 4면 벽
            AddRoomLight(roomRoot, roomHeight - 0.6f, half, room.Kind == RoomKind.Corridor ? 2.4f : 3.2f); // 방 조명
        }

        private void BuildVerticalRoom(RoomNode room) // 두 층을 잇는 세로형 방 (계단통·사다리 방·수직 통로)
        {
            Transform roomRoot = new GameObject(RoomObjectName(room)).transform; // 방 루트
            roomRoot.SetParent(generatedRoot, false); // 생성물 루트 아래
            roomRoot.localPosition = CellCenter(room.Cell); // 아래층 바닥 기준
            float half = HalfSize(room); // 반크기
            float t = wallThickness; // 두께
            float span = (half * 2f) + (t * 2f); // 벽 포함 전체 폭
            float totalHeight = FloorStep + roomHeight; // 두 층 높이
            CreateBox(roomRoot, "Floor", new Vector3(0f, -t * 0.5f, 0f), new Vector3(span, t, span), floorMaterial); // 아래층 바닥
            CreateBox(roomRoot, "Ceiling", new Vector3(0f, totalHeight + (t * 0.5f), 0f), new Vector3(span, t, span), wallMaterial); // 위층 천장
            BuildWalls(roomRoot, room, half, totalHeight); // 아래·위층 문 구멍이 있는 벽

            if (room.Vertical == VerticalKind.Stairwell) // 계단통
            {
                BuildStairwell(roomRoot, room, half); // 계단 + 경사 충돌체 + 위층 바닥
            }
            else // 사다리 방·수직 통로
            {
                BuildLadderWell(roomRoot, room, half); // 사다리 + 구멍이 있는 위층 바닥
            }

            AddRoomLight(roomRoot, FloorStep - 0.7f, half, 2.6f); // 아래층 조명
            AddRoomLight(roomRoot, totalHeight - 0.7f, half, 2.6f); // 위층 조명
        }

        private void BuildWalls(Transform roomRoot, RoomNode room, float half, float totalHeight) // 4면 벽 (문 구멍 높이 목록 기준)
        {
            foreach (GridDirection direction in GridDirections.All) // 4방향
            {
                List<float> openings = new List<float>(); // 문 구멍 바닥 높이

                if (room.IsVertical) // 세로형 방
                {
                    if (direction == room.LowerWall) // 아래층 문
                    {
                        openings.Add(0f); // 아래층 높이
                    }

                    if (direction == room.UpperWall) // 위층 문
                    {
                        openings.Add(FloorStep); // 위층 높이
                    }
                }
                else if (Layout.HasDoorOnWall(room.Id, direction)) // 일반 방 문
                {
                    openings.Add(0f); // 바닥 높이
                }

                BuildWall(roomRoot, direction, half, totalHeight, openings); // 벽 생성
            }
        }

        private void BuildWall(Transform roomRoot, GridDirection direction, float half, float totalHeight, List<float> openings) // 방 한 면 벽 (구멍 0~2개)
        {
            float t = wallThickness; // 두께
            float length = (half * 2f) + (t * 2f); // 벽 길이 (모서리 포함)
            Vector3 normal = DirectionVector(direction); // 벽 바깥 방향
            Vector3 along = new Vector3(Mathf.Abs(normal.z), 0f, Mathf.Abs(normal.x)); // 벽 길이 방향
            Vector3 wallBase = normal * (half + (t * 0.5f)); // 벽 중심 (바닥 기준)

            if (openings == null || openings.Count == 0) // 막힌 벽
            {
                CreateBox(roomRoot, $"Wall_{direction}", wallBase + (Vector3.up * (totalHeight * 0.5f)), WallSize(along, length, totalHeight, t), wallMaterial); // 한 판
                return; // 종료
            }

            float segment = (length - doorWidth) * 0.5f; // 문 양옆 벽 길이
            float offset = (doorWidth * 0.5f) + (segment * 0.5f); // 옆 벽 중심 거리
            CreateBox(roomRoot, $"Wall_{direction}_L", wallBase + (along * offset) + (Vector3.up * (totalHeight * 0.5f)), WallSize(along, segment, totalHeight, t), wallMaterial); // 왼쪽
            CreateBox(roomRoot, $"Wall_{direction}_R", wallBase - (along * offset) + (Vector3.up * (totalHeight * 0.5f)), WallSize(along, segment, totalHeight, t), wallMaterial); // 오른쪽
            openings.Sort(); // 낮은 구멍부터
            float cursor = 0f; // 채울 시작 높이

            for (int index = 0; index < openings.Count; index++) // 구멍 사이 채우기
            {
                float openingBase = openings[index]; // 구멍 바닥

                if (openingBase - cursor > 0.01f) // 채울 구간 존재
                {
                    float fillHeight = openingBase - cursor; // 높이
                    CreateBox(roomRoot, $"Wall_{direction}_Fill{index}", wallBase + (Vector3.up * (cursor + (fillHeight * 0.5f))), WallSize(along, doorWidth, fillHeight, t), wallMaterial); // 문 사이 벽
                }

                cursor = openingBase + doorHeight; // 다음 시작
            }

            if (totalHeight - cursor > 0.01f) // 문 위 벽
            {
                float topHeight = totalHeight - cursor; // 높이
                CreateBox(roomRoot, $"Wall_{direction}_Top", wallBase + (Vector3.up * (cursor + (topHeight * 0.5f))), WallSize(along, doorWidth, topHeight, t), wallMaterial); // 문 위
            }
        }

        private static Vector3 WallSize(Vector3 along, float length, float height, float thickness) // 방향별 벽 크기
        {
            return along.x > 0.5f ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length); // X 방향 또는 Z 방향
        }

        private static Vector3 PlaneSize(Vector3 axis, float axisLength, float crossLength, float height) // 축 방향 길이 · 직각 방향 길이로 상자 크기 계산
        {
            return Mathf.Abs(axis.x) > 0.5f ? new Vector3(axisLength, height, crossLength) : new Vector3(crossLength, height, axisLength); // 축이 X인지 Z인지
        }

        private void AddRoomLight(Transform roomRoot, float localY, float half, float intensity) // 방 점광원
        {
            GameObject lightObject = new GameObject("RoomLight"); // 조명
            lightObject.transform.SetParent(roomRoot, false); // 방 아래
            lightObject.transform.localPosition = new Vector3(0f, localY, 0f); // 천장 아래
            Light light = lightObject.AddComponent<Light>(); // 점광원
            light.type = LightType.Point; // 점광원
            light.range = half * 3.2f; // 범위
            light.intensity = intensity; // 밝기
            light.color = new Color(1f, 0.72f, 0.45f); // 따뜻한 횃불색
            light.shadows = LightShadows.None; // 성능용 그림자 없음
        }

        private GridDirection PickStairSide(RoomNode room) // 계단을 붙일 측면 벽 (위층 문이 있는 벽은 피함)
        {
            GridDirection perpendicularA = (GridDirection)(((int)room.ClimbWall + 1) % 4); // 직각 벽 A
            GridDirection perpendicularB = GridDirections.Opposite(perpendicularA); // 직각 벽 B

            if (room.UpperWall == perpendicularA) // 위층 문이 A에 있음
            {
                return perpendicularB; // B에 계단
            }

            if (room.UpperWall == perpendicularB) // 위층 문이 B에 있음
            {
                return perpendicularA; // A에 계단
            }

            return room.Id % 2 == 0 ? perpendicularA : perpendicularB; // 결정적 선택
        }

        private void BuildStairwell(Transform roomRoot, RoomNode room, float half) // 계단통: 한쪽에 계단, 나머지에 위층 바닥
        {
            Vector3 runAxis = DirectionVector(room.ClimbWall); // 계단 아래쪽 벽 방향
            Vector3 sideAxis = DirectionVector(PickStairSide(room)); // 계단이 붙는 측면
            float t = wallThickness; // 두께
            float span = (half * 2f) + (t * 2f); // 벽 포함 전체 폭
            float rise = FloorStep; // 올라가는 높이
            float width = Mathf.Min(stairWidth, (half * 2f) - 2f); // 계단 폭 (위층 통로 확보)
            float startU = half - stairEntryInset; // 계단 시작 (아래층 문 쪽)
            float endU = -half; // 계단 끝 (반대 벽)
            float runLength = startU - endU; // 수평 길이
            int steps = Mathf.Max(2, Mathf.CeilToInt(rise / Mathf.Max(0.1f, stairStepHeight))); // 계단 수
            float stepRise = rise / steps; // 한 칸 높이
            float tread = runLength / steps; // 한 칸 깊이
            Material stair = stairMaterial != null ? stairMaterial : floorMaterial; // 계단 재질
            Transform stairRoot = new GameObject("Stairs").transform; // 계단 루트
            stairRoot.SetParent(roomRoot, false); // 방 아래
            stairRoot.localPosition = Vector3.zero; // 방 중심 기준
            float sideCenter = half - (width * 0.5f); // 계단 중심 (측면 쪽)

            for (int index = 0; index < steps; index++) // 디딤판 (보이는 부분, 충돌체 없음)
            {
                float centerU = startU - (tread * (index + 0.5f)); // 디딤판 중심
                float height = stepRise * (index + 1); // 디딤판 높이 (바닥부터)
                Vector3 center = (runAxis * centerU) + (sideAxis * sideCenter) + (Vector3.up * (height * 0.5f)); // 중심
                CreateBox(stairRoot, $"Step_{index:00}", center, PlaneSize(runAxis, tread, width, height), stair, false); // 계단 한 칸
            }

            Vector3 ascend = ((-runAxis * runLength) + (Vector3.up * rise)).normalized; // 올라가는 방향
            Quaternion rampRotation = Quaternion.LookRotation(ascend, Vector3.up); // 경사 회전
            float rampLength = Mathf.Sqrt((rise * rise) + (runLength * runLength)); // 경사 길이
            Vector3 surfaceMid = (runAxis * ((startU + endU) * 0.5f)) + (sideAxis * sideCenter) + (Vector3.up * (rise * 0.5f)); // 경사면 중심
            GameObject ramp = new GameObject("StairRamp"); // 경사 충돌체 (보이지 않음, 걸림 없이 오르기)
            ramp.transform.SetParent(stairRoot, false); // 계단 아래
            ramp.transform.localRotation = rampRotation; // 회전
            ramp.transform.localPosition = surfaceMid + ((rampRotation * Vector3.down) * (stairRampThickness * 0.5f)); // 경사면 아래로 두께 절반
            BoxCollider rampCollider = ramp.AddComponent<BoxCollider>(); // 충돌체
            rampCollider.size = new Vector3(width, stairRampThickness, rampLength); // 크기
            float slabWidth = (half * 2f) - width; // 위층 바닥 폭

            if (slabWidth > 0.5f) // 위층 바닥 생성 가능
            {
                Vector3 slabCenter = (sideAxis * (-width * 0.5f)) + (Vector3.up * (FloorStep - (t * 0.5f))); // 위층 바닥 중심
                CreateBox(roomRoot, "UpperFloor", slabCenter, PlaneSize(runAxis, span, slabWidth, t), floorMaterial); // 위층 바닥
                float railLength = (half * 2f) - 2f; // 난간 길이 (계단 위쪽 내림 구간 제외)

                if (railLength > 0.5f) // 난간 생성 가능
                {
                    Vector3 railCenter = (runAxis * (half - (railLength * 0.5f))) + (sideAxis * (half - width - 0.1f)) + (Vector3.up * (FloorStep + 0.3f)); // 난간 중심
                    CreateBox(roomRoot, "UpperRail", railCenter, PlaneSize(runAxis, railLength, 0.2f, 0.6f), wallMaterial); // 추락 방지 난간
                }
            }
        }

        private void BuildLadderWell(Transform roomRoot, RoomNode room, float half) // 사다리 방·수직 통로: 사다리와 구멍이 있는 위층 바닥
        {
            Vector3 wallAxis = DirectionVector(room.ClimbWall); // 사다리가 붙는 벽 방향
            Vector3 sideAxis = DirectionVector((GridDirection)(((int)room.ClimbWall + 1) % 4)); // 직각 방향
            float t = wallThickness; // 두께
            float holeWidth = Mathf.Min(ladderHoleWidth, (half * 2f) - 1.2f); // 구멍 폭
            float holeDepth = Mathf.Min(ladderHoleDepth, half * 0.8f); // 구멍 깊이
            float mainLength = (half * 2f) + t - holeDepth; // 구멍 뒤쪽 전체 바닥 길이
            float mainCenterU = (-t - holeDepth) * 0.5f; // 구멍 뒤쪽 바닥 중심
            float slabY = FloorStep - (t * 0.5f); // 위층 바닥 높이
            CreateBox(roomRoot, "UpperFloor_Main", (wallAxis * mainCenterU) + (Vector3.up * slabY), PlaneSize(wallAxis, mainLength, (half * 2f) + (t * 2f), t), floorMaterial); // 구멍 뒤쪽 바닥
            float sideLength = holeDepth + t; // 구멍 옆 바닥 길이
            float sideCenterU = half + ((t - holeDepth) * 0.5f); // 구멍 옆 바닥 중심
            float sideWidth = half + t - (holeWidth * 0.5f); // 구멍 옆 바닥 폭
            float sideCenterV = ((holeWidth * 0.5f) + half + t) * 0.5f; // 구멍 옆 바닥 중심 (직각 방향)

            if (sideWidth > 0.1f) // 구멍 옆 바닥 생성 가능
            {
                CreateBox(roomRoot, "UpperFloor_SideA", (wallAxis * sideCenterU) + (sideAxis * sideCenterV) + (Vector3.up * slabY), PlaneSize(wallAxis, sideLength, sideWidth, t), floorMaterial); // 한쪽
                CreateBox(roomRoot, "UpperFloor_SideB", (wallAxis * sideCenterU) - (sideAxis * sideCenterV) + (Vector3.up * slabY), PlaneSize(wallAxis, sideLength, sideWidth, t), floorMaterial); // 반대쪽
            }

            Material ladderMat = ladderMaterial != null ? ladderMaterial : doorMaterial; // 사다리 재질
            Transform ladderRoot = new GameObject("Ladder").transform; // 사다리 루트
            ladderRoot.SetParent(roomRoot, false); // 방 아래
            ladderRoot.localPosition = wallAxis * (half - 0.08f); // 벽 안쪽 면
            ladderRoot.localRotation = Quaternion.LookRotation(-wallAxis, Vector3.up); // 앞(+Z)이 방 안쪽
            float ladderHeight = FloorStep + ladderTopOverhang; // 사다리 전체 길이 (위층 바닥보다 길게)
            CreateBox(ladderRoot, "Rail_L", new Vector3(-0.35f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMat); // 왼쪽 기둥
            CreateBox(ladderRoot, "Rail_R", new Vector3(0.35f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMat); // 오른쪽 기둥
            int rungs = Mathf.Max(2, Mathf.FloorToInt((ladderHeight - 0.3f) / 0.32f)); // 가로대 수

            for (int index = 0; index < rungs; index++) // 가로대
            {
                float y = 0.25f + (index * 0.32f); // 높이
                CreateBox(ladderRoot, $"Rung_{index:00}", new Vector3(0f, y, 0.09f), new Vector3(0.8f, 0.06f, 0.09f), ladderMat); // 가로대
            }

            Transform bottomPoint = CreatePoint(ladderRoot, "ClimbBottom", new Vector3(0f, 0.05f, 0.68f)); // 아래 끝
            Transform topPoint = CreatePoint(ladderRoot, "ClimbTop", new Vector3(0f, FloorStep + 0.15f, 0.68f)); // 위 끝
            Transform exitTop = CreatePoint(ladderRoot, "ExitTop", new Vector3(0f, FloorStep + 0.1f, holeDepth + 0.75f)); // 위에서 내려서는 위치
            Transform exitBottom = CreatePoint(ladderRoot, "ExitBottom", new Vector3(0f, 0.1f, 1.1f)); // 아래에서 내려서는 위치
            GameObject body = new GameObject("LadderBody"); // 상호작용 본체
            body.transform.SetParent(ladderRoot, false); // 사다리 아래
            body.transform.localPosition = new Vector3(0f, ladderHeight * 0.5f, 0.16f); // 사다리 면
            BoxCollider bodyCollider = body.AddComponent<BoxCollider>(); // 시선 상호작용용
            bodyCollider.size = new Vector3(0.9f, ladderHeight, 0.12f); // 사다리 전체를 덮음
            DungeonLadder ladder = body.AddComponent<DungeonLadder>(); // 사다리 기능
            ladder.Configure(bottomPoint, topPoint, exitTop, exitBottom); // 기준점 연결
            ladders.Add(ladder); // 등록
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition) // 기준점 생성
        {
            Transform point = new GameObject(name).transform; // 빈 오브젝트
            point.SetParent(parent, false); // 부모
            point.localPosition = localPosition; // 위치
            point.localRotation = Quaternion.identity; // 회전
            return point; // 반환
        }

        private void BuildPassage(DoorEdge door) // 두 방 사이 통로 (잠긴 문 포함, 문이 놓인 층 기준)
        {
            RoomNode a = Layout.Room(door.A); // 방 A
            RoomNode b = Layout.Room(door.B); // 방 B
            Vector3 direction = DirectionVector(door.FromA); // A → B
            float t = wallThickness; // 두께
            Vector3 start = CellCenterAtFloor(a.Cell, door.Floor) + (direction * (HalfSize(a) + t)); // A 벽 바깥면
            Vector3 end = CellCenterAtFloor(b.Cell, door.Floor) - (direction * (HalfSize(b) + t)); // B 벽 바깥면
            float length = Vector3.Distance(start, end); // 통로 길이
            Transform passage = new GameObject($"Passage_{door.A:00}_{door.B:00}_{GridPoint.FloorName(door.Floor)}{(door.IsLocked ? "_Locked" : string.Empty)}").transform; // 통로 루트
            passage.SetParent(generatedRoot, false); // 생성물 루트 아래
            passage.localPosition = (start + end) * 0.5f; // 중간
            passage.localRotation = Quaternion.LookRotation(direction); // 통로 방향 = 로컬 +Z
            float overlap = t * 2f; // 방 벽과 이음새가 뜨지 않도록 겹침
            CreateBox(passage, "Floor", new Vector3(0f, (-t * 0.5f) - 0.01f, 0f), new Vector3(doorWidth + (t * 2f), t, length + overlap), floorMaterial); // 바닥
            CreateBox(passage, "Ceiling", new Vector3(0f, doorHeight + (t * 0.5f), 0f), new Vector3(doorWidth + (t * 2f), t, length + overlap), wallMaterial); // 천장
            CreateBox(passage, "Side_L", new Vector3(-(doorWidth + t) * 0.5f, doorHeight * 0.5f, 0f), new Vector3(t, doorHeight, length + overlap), wallMaterial); // 왼쪽 벽
            CreateBox(passage, "Side_R", new Vector3((doorWidth + t) * 0.5f, doorHeight * 0.5f, 0f), new Vector3(t, doorHeight, length + overlap), wallMaterial); // 오른쪽 벽

            if (!door.IsLocked) // 잠긴 문 여부
            {
                return; // 일반 통로 종료
            }

            GameObject panel = CreateBox(passage, "LockedDoor", new Vector3(0f, doorHeight * 0.5f, 0f), new Vector3(doorWidth, doorHeight, 0.25f), lockedDoorMaterial); // 통로를 막는 문
            LockedRoomDoor locked = panel.AddComponent<LockedRoomDoor>(); // 잠긴 문 기능
            locked.Configure(keyDefinition == null ? "key.basic" : keyDefinition.ItemId); // 열쇠 ID
        }

        private void BuildInteriorDoor(RoomNode room, GridDirection wall, DungeonDoorKind kind, int subIndex) // 실내 정문·서브문 (벽 안쪽 면)
        {
            Transform roomRoot = generatedRoot.Find(RoomObjectName(room)); // 방 루트
            Vector3 normal = DirectionVector(wall); // 벽 바깥 방향
            float half = HalfSize(room); // 방 반크기
            Transform doorRoot = new GameObject(kind == DungeonDoorKind.Main ? "InteriorMainDoor" : $"InteriorSubDoor_{subIndex + 1}").transform; // 문 루트
            doorRoot.SetParent(roomRoot, false); // 방 아래
            doorRoot.localPosition = normal * (half - 0.07f); // 벽 안쪽 면 바로 앞
            doorRoot.localRotation = Quaternion.LookRotation(-normal); // 방 안쪽을 바라봄
            GameObject panel = CreateBox(doorRoot, "DoorPanel", new Vector3(0f, 1.2f, 0f), new Vector3(1.6f, 2.4f, 0.12f), doorMaterial); // 문짝
            CreateBox(doorRoot, "Frame_L", new Vector3(-0.9f, 1.3f, 0.02f), new Vector3(0.18f, 2.6f, 0.2f), wallMaterial); // 문틀
            CreateBox(doorRoot, "Frame_R", new Vector3(0.9f, 1.3f, 0.02f), new Vector3(0.18f, 2.6f, 0.2f), wallMaterial); // 문틀
            CreateBox(doorRoot, "Frame_Top", new Vector3(0f, 2.55f, 0.02f), new Vector3(1.98f, 0.18f, 0.2f), wallMaterial); // 문틀
            Transform arrival = CreatePoint(doorRoot, "ArrivalPoint", new Vector3(0f, 0.05f, 1.4f)); // 도착 지점 (문 앞 1.4m)
            DungeonTeleportDoor teleport = panel.AddComponent<DungeonTeleportDoor>(); // 순간이동 문
            teleport.Configure(DungeonDoorSide.Interior, kind, subIndex, arrival); // 짝 번호
            interiorDoors.Add(teleport); // 등록
            GameObject markerLight = new GameObject("DoorLight"); // 출입문 표시등
            markerLight.transform.SetParent(doorRoot, false); // 문 아래
            markerLight.transform.localPosition = new Vector3(0f, 2.9f, 0.4f); // 문 위
            Light light = markerLight.AddComponent<Light>(); // 점광원
            light.type = LightType.Point; // 점광원
            light.range = 4f; // 범위
            light.intensity = 1.6f; // 밝기
            light.color = kind == DungeonDoorKind.Main ? new Color(0.55f, 1f, 0.6f) : new Color(0.55f, 0.75f, 1f); // 정문 녹색·서브문 청색
            light.shadows = LightShadows.None; // 그림자 없음
        }

        private void SpawnContent(System.Random random) // 회수품·열쇠 생성
        {
            int maxDepth = Mathf.Max(1, Layout.MaxDepth); // 최대 깊이

            foreach (RoomNode room in Layout.Rooms) // 방 순회
            {
                if (room.IsVertical) // 세로형 방은 제외 (구멍·계단에 떨어지는 것 방지)
                {
                    continue; // 다음 방
                }

                if ((room.Content & RoomContent.Key) != 0 && keyDefinition != null) // 열쇠 방
                {
                    SpawnItem(keyDefinition, room, random, -1); // 열쇠 생성
                }

                if ((room.Content & RoomContent.Loot) == 0) // 회수품 방 여부
                {
                    continue; // 다음 방
                }

                float ratio = room.Depth / (float)maxDepth; // 깊이 비율
                InteriorLootEntry entry = PickLoot(ratio, random); // 회수품 선택

                if (entry != null) // 후보 확인
                {
                    SpawnItem(entry.definition, room, random, random.Next(entry.minValue, entry.maxValue + 1)); // 생성
                }
            }
        }

        private InteriorLootEntry PickLoot(float depthRatio, System.Random random) // 깊이 비율에 맞는 회수품 가중 선택
        {
            List<InteriorLootEntry> candidates = new List<InteriorLootEntry>(); // 후보
            int total = 0; // 가중 합

            foreach (InteriorLootEntry entry in lootTable) // 표 순회
            {
                if (entry?.definition != null && depthRatio >= entry.minDepthRatio && depthRatio <= entry.maxDepthRatio && entry.weight > 0) // 구간 확인
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

        private void SpawnItem(ItemDefinition definition, RoomNode room, System.Random random, int value) // 방 안 무작위 위치에 아이템 생성
        {
            if (definition == null || definition.RecoveryPrefab == null) // 정의 확인
            {
                return; // 생성 불가
            }

            float range = Mathf.Max(0f, HalfSize(room) - 1.2f); // 벽에서 떨어진 범위
            Vector3 local = CellCenter(room.Cell) + new Vector3(((float)random.NextDouble() * 2f - 1f) * range, 0.6f, ((float)random.NextDouble() * 2f - 1f) * range); // 방 안 위치
            Vector3 world = generatedRoot.TransformPoint(local); // 월드 위치
            GameObject instance = Instantiate(definition.RecoveryPrefab, world, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f)); // 위치 지정 생성 (Rigidbody 보간 문제 방지)
            instance.transform.SetParent(generatedRoot, true); // 환경 씬 소속
            instance.name = $"{definition.DisplayName}_R{room.Id:00}"; // 이름
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

        private GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material) // 기본 상자 생성 (충돌체 포함)
        {
            return CreateBox(parent, name, localPosition, size, material, true); // 충돌체 포함
        }

        private GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool withCollider) // 기본 상자 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상자
            box.name = name; // 이름
            box.transform.SetParent(parent, false); // 부모
            box.transform.localPosition = localPosition; // 위치
            box.transform.localRotation = Quaternion.identity; // 회전
            box.transform.localScale = size; // 크기

            if (material != null) // 재질 확인
            {
                box.GetComponent<Renderer>().sharedMaterial = material; // 재질
            }

            if (!withCollider) // 충돌체 제거 (계단 디딤판은 경사 충돌체가 담당)
            {
                Collider collider = box.GetComponent<Collider>(); // 충돌체

                if (collider != null) // 확인
                {
                    if (Application.isPlaying) // 플레이 중
                    {
                        Destroy(collider); // 제거
                    }
                    else // 에디터 미리보기
                    {
                        DestroyImmediate(collider); // 즉시 제거
                    }
                }
            }

            return box; // 반환
        }
    }
}
