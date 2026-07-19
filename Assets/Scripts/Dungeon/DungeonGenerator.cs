using System.Collections.Generic; // 방 배치 Dictionary와 IEnumerable 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// 그리드 랜덤 워크 방식으로 절차적 던전을 생성.
    /// 방 프리팹을 격자에 배치하고 이동 방향의 벽을 열어 방 사이 통로를 연결.
    /// 생성 완료 후 플레이어를 시작 방으로 이동하고 완료 이벤트를 전달.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour // 절차적 던전 생성 컴포넌트
    {
        [Header("생성 설정")] // 던전 생성 설정 구분
        [SerializeField] Room[] roomPrefabs; // 무작위로 배치할 방 프리팹 목록
        [SerializeField] int roomCount = 8; // 생성할 목표 방 개수
        [SerializeField] float cellSize = 12f; // 방 하나가 차지하는 격자 크기
        [SerializeField] Transform player; // 시작 방으로 이동시킬 플레이어

        [Header("시드")] // 무작위 시드 설정 구분
        [SerializeField] bool randomSeed = true; // 실행할 때마다 무작위 시드 사용 여부
        [SerializeField] int seed = 0; // 고정 생성에 사용할 시드값

        readonly Dictionary<Vector2Int, Room> placed = new Dictionary<Vector2Int, Room>(); // 격자 좌표별 생성된 방 목록

        public IEnumerable<Room> PlacedRooms => placed.Values; // 현재 생성된 모든 방 반환

        public Room StartRoom // 던전 시작 방 반환
        {
            get // 원점 좌표의 방 검색
            {
                placed.TryGetValue(Vector2Int.zero, out Room startRoom); // 원점에 생성된 방 가져오기
                return startRoom; // 시작 방 또는 null 반환
            }
        }

        public event System.Action GenerationStarted; // 기존 런타임 오브젝트 정리를 요청하는 생성 시작 이벤트
        public event System.Action GenerationCompleted; // 새로운 던전 생성 완료 이벤트

        public bool IsGenerating { get; private set; } // 현재 던전 생성 진행 여부
        public int GenerationCount { get; private set; } // 현재 Play에서 완료한 던전 생성 횟수
        public int CurrentSeed => seed; // 현재 던전 생성에 사용한 시드

        static readonly Vector2Int[] DirectionVectors = // 방 연결 방향별 격자 이동값
        {
            new Vector2Int(0, 1), // 북쪽 이동값
            new Vector2Int(1, 0), // 동쪽 이동값
            new Vector2Int(0, -1), // 남쪽 이동값
            new Vector2Int(-1, 0) // 서쪽 이동값
        };

        void Start() // 씬 시작 시 던전 자동 생성
        {
            Generate(); // 던전 생성 실행
        }

        public void Generate() // 방 생성과 연결 및 플레이어 배치 실행
        {
            if (IsGenerating) // 이미 던전을 생성 중인지 확인
            {
                Debug.LogWarning("[Dungeon] 이미 던전을 생성 중입니다."); // 중복 생성 요청 경고 출력
                return; // 중복 던전 생성 중단
            }

            if (roomPrefabs == null || roomPrefabs.Length == 0) // 방 프리팹 등록 여부 확인
            {
                Debug.LogError("[Dungeon] roomPrefabs가 비어 있습니다."); // 방 프리팹 오류 출력
                return; // 생성 상태를 변경하지 않고 던전 생성 중단
            }

            IsGenerating = true; // 유효성 검사 통과 후 던전 생성 상태 활성화
            GenerationStarted?.Invoke(); // 스폰 매니저에 기존 오브젝트 정리 요청


            foreach (Room room in placed.Values) // 기존 생성 방 순회
            {
                if (room != null) // 기존 방 오브젝트 존재 여부 확인
                {
                    room.gameObject.SetActive(false); // 지연 삭제 전에 기존 방과 Collider 즉시 비활성화
                    Destroy(room.gameObject); // 기존 방 오브젝트 제거 예약
                }
            }

            placed.Clear(); // 기존 방 좌표 목록 초기화

            if (randomSeed) // 무작위 시드 사용 여부 확인
            {
                seed = Random.Range(int.MinValue, int.MaxValue); // 새로운 무작위 시드 생성
            }

            Random.InitState(seed); // 던전 생성용 무작위 상태 초기화

            Vector2Int currentCell = Vector2Int.zero; // 시작 격자 좌표 설정

            PlaceRoom(currentCell); // 시작 방 생성

            int guard = 0; // 무한 반복 방지 횟수 초기화

            while (placed.Count < roomCount && guard++ < roomCount * 20) // 목표 방 수 또는 최대 시도 횟수까지 반복
            {
                int directionIndex = Random.Range(0, 4); // 무작위 이동 방향 선택
                Vector2Int nextCell = currentCell + DirectionVectors[directionIndex]; // 다음 방 격자 좌표 계산

                if (!placed.ContainsKey(nextCell)) // 다음 좌표에 방이 없는지 확인
                {
                    PlaceRoom(nextCell); // 새로운 방 생성
                }

                Connect(currentCell, (Room.Dir)directionIndex, nextCell); // 현재 방과 다음 방 사이 벽 열기
                currentCell = nextCell; // 현재 격자 좌표 갱신
            }

            if (player != null) // 플레이어 참조 존재 여부 확인
            {
                CharacterController characterController = player.GetComponent<CharacterController>(); // 플레이어 CharacterController 가져오기

                if (characterController != null) // CharacterController 존재 여부 확인
                {
                    characterController.enabled = false; // 순간 위치 이동을 위해 컨트롤러 비활성화
                }

                player.position = new Vector3(0f, 1.1f, 0f); // 플레이어를 시작 방 원점으로 이동

                if (characterController != null) // CharacterController 존재 여부 다시 확인
                {
                    characterController.enabled = true; // 위치 이동 후 컨트롤러 재활성화
                }
            }

            GenerationCount++; // 완료된 던전 생성 횟수 증가
            IsGenerating = false; // 던전 생성 진행 상태 해제
            GenerationCompleted?.Invoke(); // 모든 스폰 매니저에 생성 완료 전달
            Debug.Log($"[Dungeon] 생성 완료 — 방 {placed.Count}개 (seed {seed}, 생성 {GenerationCount}회)"); 
            // 던전 생성 결과 출력
        }

        void PlaceRoom(Vector2Int cell) // 지정한 격자 좌표에 방 생성
        {
            if (placed.ContainsKey(cell)) // 해당 좌표의 기존 방 존재 여부 확인
            {
                return; // 중복 방 생성 방지
            }

            Room prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)]; // 무작위 방 프리팹 선택
            Vector3 position = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize); // 격자 좌표를 월드 위치로 변환
            Room createdRoom = Instantiate(prefab, position, Quaternion.identity, transform); // 방 프리팹 생성

            placed[cell] = createdRoom; // 생성된 방을 좌표 목록에 저장
        }

        void Connect(Vector2Int firstCell, Room.Dir direction, Vector2Int secondCell) // 두 방 사이 통로 연결
        {
            if (placed.TryGetValue(firstCell, out Room firstRoom)) // 첫 번째 방 존재 여부 확인
            {
                firstRoom.OpenSide(direction); // 첫 번째 방의 이동 방향 벽 열기
            }

            if (placed.TryGetValue(secondCell, out Room secondRoom)) // 두 번째 방 존재 여부 확인
            {
                secondRoom.OpenSide(Room.Opposite(direction)); // 두 번째 방의 반대 방향 벽 열기
            }
        }
    }
}