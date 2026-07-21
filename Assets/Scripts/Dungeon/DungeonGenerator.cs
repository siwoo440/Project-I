using System.Collections.Generic; // 목록과 좌표별 방 저장 기능
using UnityEngine; // Unity 기본 기능

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonGenerator : MonoBehaviour // 다층 절차적 던전 생성기
    {
        [Header("일반 방 프리팹")] // 일반 방 설정 구분
        [SerializeField] private Room startingRoomPrefab; // 시작 방 전용 Room_Open 프리팹
        [SerializeField] private Room[] roomPrefabs; // 일반 방 프리팹 목록

        [Header("수직 연결 방 프리팹")] // 계단 방 설정 구분
        [SerializeField] private Room[] stairUpRoomPrefabs; // 위층 계단 방 목록
        [SerializeField] private Room[] stairDownRoomPrefabs; // 아래층 계단 방 목록
        [SerializeField] private Room[] stairBothRoomPrefabs; // 양방향 계단 방 목록

        [Header("생성 크기")] // 던전 크기 설정 구분
        [SerializeField] private int roomCount = 12; // 기본 목표 방 개수
        [SerializeField] private float cellSize = 12f; // 방의 수평 격자 크기
        [SerializeField] private float floorHeight = 4f; // 층 사이 높이
        [SerializeField] private Transform player; // 이동 대상 플레이어

        [Header("층 설정")] // 층 생성 설정 구분
        [SerializeField] private int minimumFloor = -1; // 가장 낮은 층
        [SerializeField] private int maximumFloor = 1; // 가장 높은 층
        [SerializeField][Range(0f, 0.5f)] private float verticalConnectionChance = 0.18f; // 수직 이동 확률
        [SerializeField] private bool guaranteeBasement = true; // 지하층 생성 보장
        [SerializeField] private bool guaranteeUpperFloor = true; // 위층 생성 보장
        [SerializeField] private int minimumRoomsPerExtraFloor = 2; // 추가 층 최소 방 개수

        [Header("시드")] // 시드 설정 구분
        [SerializeField] private bool randomSeed = true; // 무작위 시드 사용 여부
        [SerializeField] private int seed; // 현재 생성 시드

        private readonly Dictionary<Vector3Int, Room> placed = new Dictionary<Vector3Int, Room>(); // 좌표별 생성 방
        private readonly List<Vector3Int> layoutCells = new List<Vector3Int>(); // 생성 예정 좌표
        private readonly List<Connection> connections = new List<Connection>(); // 방 연결 정보

        private static readonly Vector3Int[] HorizontalDirections = // 수평 이동 방향 목록
        {
            new Vector3Int(0, 0, 1), // 북쪽 방향
            new Vector3Int(1, 0, 0), // 동쪽 방향
            new Vector3Int(0, 0, -1), // 남쪽 방향
            new Vector3Int(-1, 0, 0) // 서쪽 방향
        };

        private struct Connection // 두 방의 연결 정보
        {
            public Vector3Int First; // 첫 번째 방 좌표
            public Vector3Int Second; // 두 번째 방 좌표

            public Connection(Vector3Int first, Vector3Int second) // 연결 정보 생성
            {
                First = first; // 첫 번째 좌표 저장
                Second = second; // 두 번째 좌표 저장
            }
        }

        public IEnumerable<Room> PlacedRooms => placed.Values; // 생성된 모든 방 반환

        public Room StartRoom // 시작 방 반환
        {
            get
            {
                placed.TryGetValue(Vector3Int.zero, out Room startRoom); // 원점의 방 검색
                return startRoom; // 검색된 시작 방 반환
            }
        }

        public event System.Action GenerationStarted; // 생성 시작 이벤트
        public event System.Action GenerationCompleted; // 생성 완료 이벤트

        public bool IsGenerating { get; private set; } // 생성 진행 상태
        public int GenerationCount { get; private set; } // 생성 완료 횟수
        public int CurrentSeed => seed; // 현재 시드 반환

        private int SafeMinimumFloor => Mathf.Min(minimumFloor, maximumFloor); // 정렬된 최소 층
        private int SafeMaximumFloor => Mathf.Max(minimumFloor, maximumFloor); // 정렬된 최대 층

        private void Start() // Scene 시작 처리
        {
            Generate(); // 던전 자동 생성
        }

        public void Generate() // 전체 던전 생성
        {
            if (IsGenerating) // 중복 생성 확인
            {
                Debug.LogWarning("[Dungeon] 이미 던전을 생성 중입니다."); // 중복 생성 경고
                return; // 중복 생성 중단
            }

            if (startingRoomPrefab == null) // 시작 방 프리팹 확인
            {
                Debug.LogError("[Dungeon] Starting Room Prefab에 Room_Open이 연결되지 않았습니다."); // 시작 방 누락 오류
                return; // 생성 중단
            }

            if (roomPrefabs == null || roomPrefabs.Length == 0) // 일반 방 목록 확인
            {
                Debug.LogError("[Dungeon] 일반 Room Prefabs가 비어 있습니다."); // 일반 방 누락 오류
                return; // 생성 중단
            }

            if (!ValidateVerticalPrefabs()) // 계단 방 목록 검사
            {
                return; // 계단 방 누락 시 생성 중단
            }

            IsGenerating = true; // 생성 상태 활성화
            GenerationStarted?.Invoke(); // 생성 시작 이벤트 호출
            ClearPreviousRooms(); // 기존 방 제거
            layoutCells.Clear(); // 기존 좌표 초기화
            connections.Clear(); // 기존 연결 초기화

            DungeonRouteData selectedRoute = DungeonSelectionManager.Instance != null // 선택 관리자 확인
                ? DungeonSelectionManager.Instance.SelectedRoute // 선택한 경로 가져오기
                : null; // 선택 경로 없음

            int targetRoomCount = selectedRoute != null // 선택 경로 확인
                ? Mathf.Max(8, selectedRoute.RoomCount) // 경로 방 개수 적용
                : Mathf.Max(8, roomCount); // 기본 방 개수 적용

            if (randomSeed) // 무작위 시드 확인
            {
                seed = Random.Range(int.MinValue, int.MaxValue); // 새 무작위 시드 생성
            }

            if (selectedRoute != null) // 선택 경로 확인
            {
                seed = unchecked(seed + selectedRoute.SeedOffset); // 경로별 시드 오프셋 적용
            }

            Random.InitState(seed); // Unity 무작위 상태 초기화
            BuildRandomLayout(targetRoomCount); // 기본 던전 좌표 생성

            if (guaranteeUpperFloor && SafeMaximumFloor >= 1) // 위층 보장 설정 확인
            {
                EnsureFloorExists(1, minimumRoomsPerExtraFloor); // 위층 생성 보장
            }

            if (guaranteeBasement && SafeMinimumFloor <= -1) // 지하층 보장 설정 확인
            {
                EnsureFloorExists(-1, minimumRoomsPerExtraFloor); // 지하층 생성 보장
            }

            if (!PlaceAllRooms()) // 방 배치 결과 확인
            {
                IsGenerating = false; // 생성 상태 해제
                return; // 배치 실패 처리
            }

            OpenAllConnections(); // 방 연결부 개방
            MovePlayerToStartRoom(); // 플레이어 시작 위치 배치

            GenerationCount++; // 생성 완료 횟수 증가
            IsGenerating = false; // 생성 상태 해제
            GenerationCompleted?.Invoke(); // 생성 완료 이벤트 호출

            string routeName = selectedRoute != null // 경로 존재 여부 확인
                ? selectedRoute.DisplayName // 선택 경로 이름 사용
                : "기본 던전"; // 기본 던전 이름 사용

            int basementCount = CountCellsAtFloor(-1); // 지하층 방 개수
            int groundCount = CountCellsAtFloor(0); // 지상층 방 개수
            int upperCount = CountCellsAtFloor(1); // 위층 방 개수

            Debug.Log( // 생성 결과 로그 출력
                $"[Dungeon] {routeName} 다층 생성 완료 — " + // 경로 이름 출력
                $"지하 {basementCount}개 / 지상 {groundCount}개 / 2층 {upperCount}개 " + // 층별 개수 출력
                $"(전체 {placed.Count}개, seed {seed}, 생성 {GenerationCount}회)"); // 전체 결과 출력
        }

        private bool ValidateVerticalPrefabs() // 계단 방 프리팹 검사
        {
            bool needsVerticalRooms = // 계단 방 필요 여부
                guaranteeBasement || // 지하층 보장 여부
                guaranteeUpperFloor || // 위층 보장 여부
                verticalConnectionChance > 0f; // 수직 연결 확률 여부

            if (!needsVerticalRooms) // 계단 방 불필요 확인
            {
                return true; // 단층 생성 허용
            }

            if (stairUpRoomPrefabs == null || stairUpRoomPrefabs.Length == 0) // 위층 계단 목록 확인
            {
                Debug.LogError("[Dungeon] Stair Up Room Prefabs가 비어 있습니다."); // 위층 계단 누락 오류
                return false; // 검사 실패
            }

            if (stairDownRoomPrefabs == null || stairDownRoomPrefabs.Length == 0) // 아래층 계단 목록 확인
            {
                Debug.LogError("[Dungeon] Stair Down Room Prefabs가 비어 있습니다."); // 아래층 계단 누락 오류
                return false; // 검사 실패
            }

            return true; // 검사 성공
        }

        private void ClearPreviousRooms() // 기존 방 제거
        {
            foreach (Room room in placed.Values) // 기존 생성 방 순회
            {
                if (room == null) // 제거된 방 확인
                {
                    continue; // 다음 방 처리
                }

                room.gameObject.SetActive(false); // Collider 즉시 비활성화
                Destroy(room.gameObject); // 방 오브젝트 제거 예약
            }

            placed.Clear(); // 생성 방 정보 초기화
        }

        private void BuildRandomLayout(int targetRoomCount) // 무작위 던전 좌표 생성
        {
            Vector3Int currentCell = Vector3Int.zero; // 시작 좌표 설정
            layoutCells.Add(currentCell); // 시작 좌표 등록

            int guard = 0; // 반복 방지 횟수
            int safeTargetCount = Mathf.Max(1, targetRoomCount); // 안전한 목표 개수

            while (layoutCells.Count < safeTargetCount && guard++ < safeTargetCount * 60) // 목표 개수까지 반복
            {
                Vector3Int direction; // 선택 방향
                bool wantsVertical = Random.value < Mathf.Clamp01(verticalConnectionChance); // 수직 이동 확률 판정

                if (wantsVertical && TryPickVerticalDirection(currentCell, out Vector3Int verticalDirection)) // 수직 방향 검색
                {
                    direction = verticalDirection; // 수직 방향 적용
                }
                else
                {
                    direction = HorizontalDirections[Random.Range(0, HorizontalDirections.Length)]; // 수평 방향 선택
                }

                Vector3Int nextCell = currentCell + direction; // 다음 좌표 계산

                if (nextCell.y < SafeMinimumFloor || nextCell.y > SafeMaximumFloor) // 층 범위 확인
                {
                    continue; // 범위 밖 좌표 제외
                }

                bool isVertical = direction.y != 0; // 수직 연결 여부

                if (isVertical && IsStartCell(currentCell)) // 시작 방에서 수직 이동 확인
                {
                    continue; // 시작 방 수직 연결 금지
                }

                if (isVertical && IsStartCell(nextCell)) // 시작 방으로 수직 이동 확인
                {
                    continue; // 시작 방 수직 연결 금지
                }

                if (isVertical && (HasVerticalConnection(currentCell) || HasVerticalConnection(nextCell))) // 계단 중복 확인
                {
                    continue; // 중복 계단 제외
                }

                if (!layoutCells.Contains(nextCell)) // 새로운 좌표 확인
                {
                    layoutCells.Add(nextCell); // 새 방 좌표 등록
                }

                AddConnection(currentCell, nextCell); // 두 방 연결 등록
                currentCell = nextCell; // 현재 좌표 갱신
            }
        }

        private bool TryPickVerticalDirection(Vector3Int currentCell, out Vector3Int direction) // 수직 방향 선택
        {
            direction = Vector3Int.zero; // 기본 방향 초기화

            if (IsStartCell(currentCell)) // 시작 방 여부 확인
            {
                return false; // 시작 방 수직 연결 금지
            }

            bool canGoUp = currentCell.y < SafeMaximumFloor; // 위층 이동 가능 여부
            bool canGoDown = currentCell.y > SafeMinimumFloor; // 아래층 이동 가능 여부

            if (!canGoUp && !canGoDown) // 수직 이동 불가능 확인
            {
                return false; // 선택 실패
            }

            if (canGoUp && canGoDown) // 양방향 이동 가능 확인
            {
                direction = Random.value < 0.5f // 무작위 방향 판정
                    ? Vector3Int.up // 위층 방향
                    : Vector3Int.down; // 아래층 방향

                return true; // 선택 성공
            }

            direction = canGoUp // 위층 이동 가능 여부
                ? Vector3Int.up // 위층 방향
                : Vector3Int.down; // 아래층 방향

            return true; // 선택 성공
        }

        private void EnsureFloorExists(int targetFloor, int requiredRoomCount) // 지정 층 생성 보장
        {
            if (targetFloor < SafeMinimumFloor || targetFloor > SafeMaximumFloor) // 층 범위 확인
            {
                return; // 범위 밖 처리 중단
            }

            int safeRequiredCount = Mathf.Max(1, requiredRoomCount); // 안전한 최소 개수

            if (CountCellsAtFloor(targetFloor) == 0) // 지정 층 방 존재 여부 확인
            {
                int sourceFloor = targetFloor > 0 // 목표 층 방향 확인
                    ? targetFloor - 1 // 아래 인접 층 선택
                    : targetFloor + 1; // 위 인접 층 선택

                if (!TryFindVerticalAnchor( // 계단 위치 검색
                    sourceFloor, // 시작 층 전달
                    targetFloor, // 목표 층 전달
                    out Vector3Int sourceCell, // 시작 좌표 받기
                    out Vector3Int targetCell)) // 목표 좌표 받기
                {
                    Debug.LogWarning($"[Dungeon] {targetFloor}층 연결용 계단 위치를 찾지 못했습니다."); // 계단 검색 실패 경고
                    return; // 층 보장 중단
                }

                layoutCells.Add(targetCell); // 새 층 첫 방 추가
                AddConnection(sourceCell, targetCell); // 층 사이 연결 등록
            }

            ExpandFloorHorizontally(targetFloor, safeRequiredCount); // 지정 층 수평 확장
        }

        private bool TryFindVerticalAnchor( // 수직 연결 위치 검색
            int sourceFloor, // 기존 층
            int targetFloor, // 목표 층
            out Vector3Int sourceCell, // 선택된 기존 방 좌표
            out Vector3Int targetCell) // 계산된 목표 방 좌표
        {
            sourceCell = Vector3Int.zero; // 실패 대비 좌표 초기화
            targetCell = Vector3Int.zero; // 실패 대비 좌표 초기화

            List<Vector3Int> candidates = new List<Vector3Int>(); // 계단 시작 후보 목록

            foreach (Vector3Int cell in layoutCells) // 전체 방 좌표 순회
            {
                if (cell.y != sourceFloor) // 시작 층 확인
                {
                    continue; // 다른 층 제외
                }

                if (IsStartCell(cell)) // 시작 방 좌표 확인
                {
                    continue; // 시작 방 계단 후보 제외
                }

                if (HasVerticalConnection(cell)) // 기존 계단 확인
                {
                    continue; // 계단 중복 방 제외
                }

                Vector3Int verticalTarget = new Vector3Int( // 목표 층 좌표 생성
                    cell.x, // 동일한 X 좌표
                    targetFloor, // 목표 층 Y 좌표
                    cell.z); // 동일한 Z 좌표

                if (IsStartCell(verticalTarget)) // 목표가 시작 방인지 확인
                {
                    continue; // 시작 방 연결 제외
                }

                if (layoutCells.Contains(verticalTarget)) // 목표 위치 중복 확인
                {
                    continue; // 중복 위치 제외
                }

                candidates.Add(cell); // 유효한 후보 추가
            }

            if (candidates.Count == 0) // 후보 존재 여부 확인
            {
                return false; // 검색 실패
            }

            sourceCell = candidates[Random.Range(0, candidates.Count)]; // 후보 무작위 선택

            targetCell = new Vector3Int( // 목표 방 좌표 계산
                sourceCell.x, // 기존 X 좌표
                targetFloor, // 목표 층 Y 좌표
                sourceCell.z); // 기존 Z 좌표

            return true; // 검색 성공
        }

        private void ExpandFloorHorizontally(int targetFloor, int requiredRoomCount) // 층 수평 확장
        {
            int guard = 0; // 반복 방지 횟수

            while (CountCellsAtFloor(targetFloor) < requiredRoomCount && guard++ < requiredRoomCount * 40) // 최소 방 수까지 반복
            {
                List<Vector3Int> floorCells = GetCellsAtFloor(targetFloor); // 해당 층 방 목록

                if (floorCells.Count == 0) // 기준 방 존재 여부 확인
                {
                    return; // 확장 중단
                }

                Vector3Int sourceCell = floorCells[Random.Range(0, floorCells.Count)]; // 기준 방 무작위 선택
                Vector3Int direction = HorizontalDirections[Random.Range(0, HorizontalDirections.Length)]; // 방향 무작위 선택
                Vector3Int targetCell = sourceCell + direction; // 새로운 방 좌표 계산

                if (layoutCells.Contains(targetCell)) // 위치 중복 확인
                {
                    continue; // 중복 위치 제외
                }

                layoutCells.Add(targetCell); // 새 방 좌표 추가
                AddConnection(sourceCell, targetCell); // 방 연결 등록
            }
        }

        private List<Vector3Int> GetCellsAtFloor(int floor) // 지정 층 좌표 반환
        {
            List<Vector3Int> floorCells = new List<Vector3Int>(); // 결과 목록 생성

            foreach (Vector3Int cell in layoutCells) // 전체 좌표 순회
            {
                if (cell.y == floor) // 지정 층 확인
                {
                    floorCells.Add(cell); // 결과 목록 추가
                }
            }

            return floorCells; // 결과 반환
        }

        private void AddConnection(Vector3Int first, Vector3Int second) // 방 연결 추가
        {
            if (first == second) // 같은 방 연결 확인
            {
                return; // 자기 연결 방지
            }

            foreach (Connection connection in connections) // 기존 연결 순회
            {
                bool sameOrder = connection.First == first && connection.Second == second; // 동일 순서 확인
                bool reverseOrder = connection.First == second && connection.Second == first; // 반대 순서 확인

                if (sameOrder || reverseOrder) // 기존 연결 확인
                {
                    return; // 중복 연결 방지
                }
            }

            connections.Add(new Connection(first, second)); // 새 연결 추가
        }

        private bool HasVerticalConnection(Vector3Int cell) // 수직 연결 확인
        {
            foreach (Connection connection in connections) // 전체 연결 순회
            {
                if (connection.First != cell && connection.Second != cell) // 관련 연결 확인
                {
                    continue; // 관련 없는 연결 제외
                }

                Vector3Int other = connection.First == cell // 현재 좌표 위치 확인
                    ? connection.Second // 두 번째 좌표 선택
                    : connection.First; // 첫 번째 좌표 선택

                if (other.y != cell.y) // 다른 층 연결 확인
                {
                    return true; // 수직 연결 존재
                }
            }

            return false; // 수직 연결 없음
        }

        private bool HasConnectionDirection(Vector3Int cell, Room.Dir direction) // 방향별 연결 확인
        {
            foreach (Connection connection in connections) // 전체 연결 순회
            {
                if (connection.First == cell) // 첫 번째 방 확인
                {
                    if (GetDirection(connection.First, connection.Second) == direction) // 연결 방향 확인
                    {
                        return true; // 연결 존재
                    }
                }
                else if (connection.Second == cell) // 두 번째 방 확인
                {
                    if (GetDirection(connection.Second, connection.First) == direction) // 반대 기준 방향 확인
                    {
                        return true; // 연결 존재
                    }
                }
            }

            return false; // 연결 없음
        }

        private bool PlaceAllRooms() // 모든 방 프리팹 배치
        {
            foreach (Vector3Int cell in layoutCells) // 전체 생성 좌표 순회
            {
                Room selectedPrefab; // 배치할 방 프리팹

                if (IsStartCell(cell)) // 시작 좌표 확인
                {
                    selectedPrefab = startingRoomPrefab; // Room_Open 강제 선택
                }
                else
                {
                    bool connectsUp = HasConnectionDirection(cell, Room.Dir.Up); // 위층 연결 확인
                    bool connectsDown = HasConnectionDirection(cell, Room.Dir.Down); // 아래층 연결 확인
                    selectedPrefab = SelectRoomPrefab(connectsUp, connectsDown); // 연결 형태별 방 선택
                }

                if (selectedPrefab == null) // 프리팹 유효성 확인
                {
                    Debug.LogError($"[Dungeon] {cell} 좌표에 사용할 Room 프리팹이 없습니다."); // 프리팹 누락 오류
                    return false; // 전체 배치 실패
                }

                Vector3 worldPosition = new Vector3( // 월드 좌표 계산
                    cell.x * cellSize, // X 위치 계산
                    cell.y * floorHeight, // Y 위치 계산
                    cell.z * cellSize); // Z 위치 계산

                Room createdRoom = Instantiate( // 방 프리팹 생성
                    selectedPrefab, // 선택된 프리팹
                    worldPosition, // 계산된 월드 위치
                    Quaternion.identity, // 기본 회전값
                    transform); // 생성기 자식 설정

                createdRoom.name = $"{selectedPrefab.name}_F{cell.y}_{cell.x}_{cell.z}"; // 생성 방 이름 지정
                placed[cell] = createdRoom; // 좌표별 생성 방 저장
            }

            return true; // 전체 배치 성공
        }

        private Room SelectRoomPrefab(bool connectsUp, bool connectsDown) // 연결 상태별 방 선택
        {
            if (connectsUp && connectsDown) // 양방향 계단 확인
            {
                return PickRandomRoom(stairBothRoomPrefabs); // 양방향 계단 방 선택
            }

            if (connectsUp) // 위층 계단 확인
            {
                return PickRandomRoom(stairUpRoomPrefabs); // 위층 계단 방 선택
            }

            if (connectsDown) // 아래층 계단 확인
            {
                return PickRandomRoom(stairDownRoomPrefabs); // 아래층 계단 방 선택
            }

            return PickRandomRoom(roomPrefabs); // 일반 방 무작위 선택
        }

        private Room PickRandomRoom(Room[] prefabs) // 유효한 방 무작위 선택
        {
            if (prefabs == null || prefabs.Length == 0) // 목록 존재 여부 확인
            {
                return null; // 선택 실패
            }

            int startIndex = Random.Range(0, prefabs.Length); // 검색 시작 번호 선택

            for (int i = 0; i < prefabs.Length; i++) // 전체 목록 검사
            {
                int index = (startIndex + i) % prefabs.Length; // 순환형 번호 계산

                if (prefabs[index] != null) // 유효한 프리팹 확인
                {
                    return prefabs[index]; // 프리팹 반환
                }
            }

            return null; // 유효한 프리팹 없음
        }

        private void OpenAllConnections() // 모든 연결부 개방
        {
            foreach (Connection connection in connections) // 전체 연결 순회
            {
                if (!placed.TryGetValue(connection.First, out Room firstRoom)) // 첫 번째 방 검색
                {
                    continue; // 없는 방 제외
                }

                if (!placed.TryGetValue(connection.Second, out Room secondRoom)) // 두 번째 방 검색
                {
                    continue; // 없는 방 제외
                }

                Room.Dir firstDirection = GetDirection(connection.First, connection.Second); // 첫 번째 방 방향 계산
                Room.Dir secondDirection = Room.Opposite(firstDirection); // 반대 방향 계산

                firstRoom.OpenSide(firstDirection); // 첫 번째 방 연결부 개방
                secondRoom.OpenSide(secondDirection); // 두 번째 방 연결부 개방
            }
        }

        private Room.Dir GetDirection(Vector3Int from, Vector3Int to) // 좌표 차이를 방향으로 변환
        {
            Vector3Int difference = to - from; // 좌표 차이 계산

            if (difference.y > 0) // 위층 방향 확인
            {
                return Room.Dir.Up; // 위층 방향 반환
            }

            if (difference.y < 0) // 아래층 방향 확인
            {
                return Room.Dir.Down; // 아래층 방향 반환
            }

            if (difference.z > 0) // 북쪽 방향 확인
            {
                return Room.Dir.N; // 북쪽 방향 반환
            }

            if (difference.x > 0) // 동쪽 방향 확인
            {
                return Room.Dir.E; // 동쪽 방향 반환
            }

            if (difference.z < 0) // 남쪽 방향 확인
            {
                return Room.Dir.S; // 남쪽 방향 반환
            }

            return Room.Dir.W; // 서쪽 방향 반환
        }

        private bool IsStartCell(Vector3Int cell) // 시작 좌표 여부 확인
        {
            return cell == Vector3Int.zero; // 원점 비교 결과 반환
        }

        private void MovePlayerToStartRoom() // 플레이어 시작 방 배치
        {
            Room startRoom = StartRoom; // 생성된 시작 방 가져오기

            if (player == null) // 플레이어 참조 확인
            {
                Debug.LogWarning("[Dungeon] Player가 연결되지 않아 시작 위치로 이동하지 못했습니다."); // 플레이어 누락 경고
                return; // 이동 중단
            }

            if (startRoom == null) // 시작 방 존재 여부 확인
            {
                Debug.LogWarning("[Dungeon] 생성된 시작 방을 찾지 못했습니다."); // 시작 방 누락 경고
                return; // 이동 중단
            }

            Transform spawnPoint = FindPlayerSpawnPoint(startRoom); // 시작 위치 오브젝트 검색

            Vector3 targetPosition = spawnPoint != null // 시작 위치 존재 여부 확인
                ? spawnPoint.position // 지정된 시작 위치 사용
                : startRoom.transform.position + Vector3.up * 1.1f; // 방 중앙 위쪽 사용

            Quaternion targetRotation = spawnPoint != null // 시작 회전 존재 여부 확인
                ? spawnPoint.rotation // 지정된 시작 회전 사용
                : player.rotation; // 기존 플레이어 회전 유지

            CharacterController characterController = player.GetComponent<CharacterController>(); // CharacterController 검색
            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>(); // Rigidbody 검색

            if (characterController != null) // CharacterController 존재 여부 확인
            {
                characterController.enabled = false; // 순간 이동 중 충돌 비활성화
            }

            if (playerRigidbody != null && !playerRigidbody.isKinematic) // 물리 Rigidbody 확인
            {
                playerRigidbody.linearVelocity = Vector3.zero; // 선형 이동 속도 초기화
                playerRigidbody.angularVelocity = Vector3.zero; // 회전 속도 초기화
            }

            player.SetPositionAndRotation(targetPosition, targetRotation); // 시작 위치와 회전 적용

            if (characterController != null) // CharacterController 존재 여부 재확인
            {
                characterController.enabled = true; // 충돌 기능 복구
            }
        }

        private Transform FindPlayerSpawnPoint(Room startRoom) // 시작 위치 오브젝트 검색
        {
            Transform[] childTransforms = startRoom.GetComponentsInChildren<Transform>(true); // 모든 자식 Transform 검색

            foreach (Transform childTransform in childTransforms) // 자식 Transform 순회
            {
                if (childTransform.name == "PlayerSpawnPoint") // 시작 위치 이름 확인
                {
                    return childTransform; // 시작 위치 반환
                }
            }

            Debug.LogWarning("[Dungeon] Room_Open에 PlayerSpawnPoint가 없어 방 중앙을 사용합니다."); // 시작 위치 누락 경고
            return null; // 검색 실패 반환
        }

        private int CountCellsAtFloor(int floor) // 지정 층 방 개수 계산
        {
            int count = 0; // 개수 초기화

            foreach (Vector3Int cell in layoutCells) // 전체 좌표 순회
            {
                if (cell.y == floor) // 지정 층 확인
                {
                    count++; // 방 개수 증가
                }
            }

            return count; // 최종 개수 반환
        }
    }
}