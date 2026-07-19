using System.Collections.Generic; // 생성 후보 방 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class GimmickMonsterSpawnManager : MonoBehaviour // 고스트와 웃는 석상의 독립 출현 관리
    {
        [Header("필수 참조")] // Inspector 필수 참조 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 던전 생성 완료 이벤트 제공자
        [SerializeField] Ghost ghostPrefab; // 자동 생성할 고스트 프리팹
        [SerializeField] SmilingStatue smilingStatuePrefab; // 자동 생성할 웃는 석상 프리팹

        [Header("독립 출현 확률")] // Inspector 출현 확률 구분
        [SerializeField][Range(0f, 1f)] float ghostSpawnChance = 0.3f; // 던전당 고스트 출현 확률
        [SerializeField][Range(0f, 1f)] float statueSpawnChance = 0.4f; // 던전당 웃는 석상 출현 확률
        [SerializeField] bool skipStartingRoom = true; // 시작 방을 후보에서 제외할지 결정

        [Header("스폰 위치")] // Inspector 위치 검색 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽에서 떨어질 최소 거리
        [SerializeField] float positionSearchHeight = 1.2f; // 장애물 검사 중심 높이
        [SerializeField] float groundOffset = 0f; // 프리팹 루트 바닥 높이 보정
        [SerializeField] float collisionCheckRadius = 0.5f; // 생성 위치 충돌 검사 반경
        [SerializeField] int positionSearchAttempts = 30; // 방마다 위치 검색 횟수
        [SerializeField] float playerSafeDistance = 6f; // 플레이어 주변 생성 금지 거리

        [Header("디버그")] // Inspector 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 임시 OnGUI 표시 여부

        PlayerController player; // 두 기믹 몬스터의 목표 플레이어
        Ghost spawnedGhost; // 현재 자동 생성된 고스트
        SmilingStatue spawnedStatue; // 현재 자동 생성된 웃는 석상

        public int ActiveGhostCount => spawnedGhost != null ? 1 : 0; // 현재 고스트 수 반환
        public int ActiveStatueCount => spawnedStatue != null ? 1 : 0; // 현재 웃는 석상 수 반환

        void Awake() // 던전 생성기와 플레이어 참조 초기화
        {
            if (dungeonGenerator == null) // DungeonGenerator 연결 여부 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // Scene에서 던전 생성기 자동 검색
            }

            player = FindFirstObjectByType<PlayerController>(); // Scene에서 플레이어 자동 검색
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationStarted += ClearSpawnedMonsters; // 새 던전 생성 전에 기존 기믹 몬스터 정리
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 생성 완료 이벤트 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationStarted -= ClearSpawnedMonsters; // 기존 기믹 몬스터 정리 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 생성 완료 이벤트 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 후 기믹 몬스터 출현 처리
        {
            SpawnGimmickMonsters(); // 고스트와 웃는 석상 독립 생성 실행
        }

        public void SpawnGimmickMonsters() // 기믹 몬스터의 독립 확률 출현 실행
        {
            ClearSpawnedMonsters(); // 이전 던전에서 생성한 기믹 몬스터 제거

            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                Debug.LogError("[GimmickSpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 생성 처리 중단
            }

            if (player == null) // 플레이어 참조 존재 여부 확인
            {
                player = FindFirstObjectByType<PlayerController>(); // Scene에서 플레이어 다시 검색
            }

            if (player == null) // 플레이어 재검색 결과 확인
            {
                Debug.LogError("[GimmickSpawnManager] PlayerController를 찾을 수 없습니다."); // 플레이어 누락 오류 출력
                return; // 생성 처리 중단
            }

            SpawnGhost(); // 고스트 독립 출현 처리
            SpawnSmilingStatue(); // 웃는 석상 독립 출현 처리
        }

        void SpawnGhost() // 고스트의 던전당 독립 출현 처리
        {
            if (ghostPrefab == null) // 고스트 프리팹 연결 여부 확인
            {
                Debug.LogError("[GimmickSpawnManager] Ghost Prefab이 연결되지 않았습니다."); // 프리팹 누락 오류 출력
                return; // 고스트 생성 중단
            }

            float safeChance = Mathf.Clamp01(ghostSpawnChance); // 고스트 출현 확률 제한

            if (safeChance <= 0f || Random.value >= safeChance) // 고스트 출현 확률 성공 여부 확인
            {
                Debug.Log($"[GimmickSpawnManager] 고스트 미출현 — 확률 {safeChance * 100f:F0}%"); // 미출현 결과 출력
                return; // 고스트 생성 중단
            }

            if (!TryFindSpawnPosition(out Vector3 position, out Room room)) // 고스트 안전 위치 검색
            {
                Debug.LogWarning("[GimmickSpawnManager] 고스트 생성 위치를 찾지 못했습니다."); // 위치 검색 실패 출력
                return; // 고스트 생성 중단
            }

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 고스트 초기 회전 계산
            spawnedGhost = Instantiate(ghostPrefab, position, rotation, transform); // 고스트 프리팹 생성
            spawnedGhost.Activate(player); // 고스트 목표 플레이어 지정
            Debug.Log($"[GimmickSpawnManager] 고스트 출현 — {room.name}"); // 고스트 생성 결과 출력
        }

        void SpawnSmilingStatue() // 웃는 석상의 던전당 독립 출현 처리
        {
            if (smilingStatuePrefab == null) // 웃는 석상 프리팹 연결 여부 확인
            {
                Debug.LogError("[GimmickSpawnManager] Smiling Statue Prefab이 연결되지 않았습니다."); // 프리팹 누락 오류 출력
                return; // 웃는 석상 생성 중단
            }

            float safeChance = Mathf.Clamp01(statueSpawnChance); // 웃는 석상 출현 확률 제한

            if (safeChance <= 0f || Random.value >= safeChance) // 웃는 석상 출현 확률 성공 여부 확인
            {
                Debug.Log($"[GimmickSpawnManager] 웃는 석상 미출현 — 확률 {safeChance * 100f:F0}%"); // 미출현 결과 출력
                return; // 웃는 석상 생성 중단
            }

            if (!TryFindSpawnPosition(out Vector3 position, out Room room)) // 웃는 석상 안전 위치 검색
            {
                Debug.LogWarning("[GimmickSpawnManager] 웃는 석상 생성 위치를 찾지 못했습니다."); // 위치 검색 실패 출력
                return; // 웃는 석상 생성 중단
            }

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 웃는 석상 초기 회전 계산
            spawnedStatue = Instantiate(smilingStatuePrefab, position, rotation, transform); // 웃는 석상 프리팹 생성
            spawnedStatue.Activate(player); // 웃는 석상 목표 플레이어 지정
            Debug.Log($"[GimmickSpawnManager] 웃는 석상 출현 — {room.name}"); // 웃는 석상 생성 결과 출력
        }

        bool TryFindSpawnPosition(out Vector3 spawnPosition, out Room selectedRoom) // 모든 후보 방에서 안전한 위치 검색
        {
            spawnPosition = Vector3.zero; // 실패 대비 기본 위치 설정
            selectedRoom = null; // 실패 대비 선택 방 초기화
            List<Room> candidateRooms = new List<Room>(); // 생성 후보 방 목록 생성

            foreach (Room room in dungeonGenerator.PlacedRooms) // 현재 생성된 모든 방 순회
            {
                if (room == null) // 방 유효성 확인
                {
                    continue; // 유효하지 않은 방 제외
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방 제외 설정 확인
                {
                    continue; // 시작 방 제외
                }

                candidateRooms.Add(room); // 후보 방 목록 추가
            }

            if (candidateRooms.Count == 0) // 후보 방 존재 여부 확인
            {
                return false; // 위치 검색 실패 반환
            }

            int startIndex = Random.Range(0, candidateRooms.Count); // 무작위 검색 시작 인덱스 계산

            for (int i = 0; i < candidateRooms.Count; i++) // 모든 후보 방 검색
            {
                int roomIndex = (startIndex + i) % candidateRooms.Count; // 순환형 방 인덱스 계산
                Room room = candidateRooms[roomIndex]; // 현재 후보 방 가져오기

                bool foundPosition = DungeonSpawnUtility.TryFindSpawnPosition( // 공용 안전 위치 검색 실행
                    room, // 현재 후보 방
                    player.transform, // 플레이어 안전거리 기준
                    wallMargin, // 벽 여백
                    positionSearchHeight, // 장애물 검사 높이
                    collisionCheckRadius, // 충돌 검사 반경
                    positionSearchAttempts, // 위치 검색 횟수
                    playerSafeDistance, // 플레이어 안전거리
                    out Vector3 searchPosition); // 검색된 위치 저장

                if (!foundPosition) // 현재 방 위치 검색 결과 확인
                {
                    continue; // 다음 후보 방 검색
                }

                float heightAdjustment = groundOffset - positionSearchHeight; // 검색 높이에서 루트 높이로 보정
                spawnPosition = searchPosition + Vector3.up * heightAdjustment; // 최종 프리팹 위치 계산
                selectedRoom = room; // 생성 방 저장
                return true; // 위치 검색 성공 반환
            }

            return false; // 전체 후보 방 위치 검색 실패 반환
        }

        public void ClearSpawnedMonsters() // 자동 생성된 기믹 몬스터 제거
        {
            if (spawnedGhost != null) // 기존 고스트 존재 여부 확인
            {
                spawnedGhost.gameObject.SetActive(false); // 지연 삭제 전에 고스트와 Collider 즉시 비활성화
                Destroy(spawnedGhost.gameObject); // 기존 고스트 제거 예약
            }

            if (spawnedStatue != null) // 기존 웃는 석상 존재 여부 확인
            {
                spawnedStatue.gameObject.SetActive(false); // 지연 삭제 전에 웃는 석상과 Collider 즉시 비활성화
                Destroy(spawnedStatue.gameObject); // 기존 웃는 석상 제거 예약
            }

            spawnedGhost = null; // 고스트 참조 초기화
            spawnedStatue = null; // 웃는 석상 참조 초기화
        }

        void OnGUI() // 자동 생성 상태 임시 표시
        {
            if (!showDebug) // 디버그 표시 여부 확인
            {
                return; // 화면 표시 중단
            }

            GUI.Label(new Rect(10f, 315f, 300f, 25f), $"자동 생성 고스트: {ActiveGhostCount}"); // 고스트 수 표시
            GUI.Label(new Rect(10f, 340f, 300f, 25f), $"자동 생성 웃는 석상: {ActiveStatueCount}"); // 웃는 석상 수 표시
        }
    }
}