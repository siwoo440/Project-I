using System.Collections.Generic; // 스폰 후보 방과 생성된 스토커 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class StalkerSpawnManager : MonoBehaviour // 던전당 확률적으로 한 마리의 스토커를 생성
    {
        [Header("필수 참조")] // Inspector 필수 참조 설정 구분
        [Tooltip("생성 완료 이벤트와 방 목록을 제공하는 던전 생성기")] [SerializeField] DungeonGenerator dungeonGenerator; // 생성 완료 이벤트와 방 목록을 제공하는 던전 생성기
        [Tooltip("자동 생성할 스토커 프리팹")] [SerializeField] Stalker stalkerPrefab; // 자동 생성할 스토커 프리팹

        [Header("출현 설정")] // Inspector 스토커 출현 설정 구분
        [Tooltip("던전당 스토커가 출현할 확률")] [SerializeField][Range(0f, 1f)] float spawnChance = 0.2f; // 던전당 스토커가 출현할 확률
        [Tooltip("시작 방을 스토커 생성 후보에서 제외할지 결정")] [SerializeField] bool skipStartingRoom = true; // 시작 방을 스토커 생성 후보에서 제외할지 결정

        [Header("스폰 위치")] // Inspector 스토커 위치 설정 구분
        [Tooltip("벽으로부터 떨어질 최소 거리")] [SerializeField] float wallMargin = 1.5f; // 벽으로부터 떨어질 최소 거리
        [Tooltip("장애물 검사를 실행할 바닥 위 높이")] [SerializeField] float positionSearchHeight = 1.1f; // 장애물 검사를 실행할 바닥 위 높이
        [Tooltip("스토커 루트를 바닥에서 띄울 높이")] [SerializeField] float groundOffset = 0f; // 스토커 루트를 바닥에서 띄울 높이
        [Tooltip("스토커 생성 위치의 충돌 검사 반경")] [SerializeField] float collisionCheckRadius = 0.45f; // 스토커 생성 위치의 충돌 검사 반경
        [Tooltip("각 방에서 안전한 위치를 검색할 최대 횟수")] [SerializeField] int positionSearchAttempts = 30; // 각 방에서 안전한 위치를 검색할 최대 횟수
        [Tooltip("플레이어 주변 스토커 생성 금지 거리")] [SerializeField] float playerSafeDistance = 8f; // 플레이어 주변 스토커 생성 금지 거리

        [Header("디버그")] // Inspector 디버그 설정 구분
        [Tooltip("스토커 생성 상태를 화면에 표시할지 결정")] [SerializeField] bool showDebug = true; // 스토커 생성 상태를 화면에 표시할지 결정

        readonly List<Stalker> spawnedStalkers = new List<Stalker>(); // 이 매니저가 생성한 스토커 목록
        PlayerController player; // 스토커의 목표로 지정할 플레이어

        public int ActiveStalkerCount // 현재 존재하는 자동 생성 스토커 수 반환
        {
            get // 유효한 스토커 개수 계산
            {
                int count = 0; // 유효한 스토커 수 초기화

                foreach (Stalker stalker in spawnedStalkers) // 생성된 스토커 목록 순회
                {
                    if (stalker != null) // 스토커 오브젝트가 아직 존재하는지 확인
                    {
                        count++; // 유효한 스토커 수 증가
                    }
                }

                return count; // 계산된 스토커 수 반환
            }
        }

        void Awake() // 던전 생성기와 플레이어 참조 초기화
        {
            if (dungeonGenerator == null) // DungeonGenerator가 Inspector에 연결되지 않았는지 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // 현재 Scene에서 DungeonGenerator 자동 검색
            }

            player = FindFirstObjectByType<PlayerController>(); // 현재 Scene에서 스토커의 목표 플레이어 검색
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted += ClearSpawnedStalkers; // 새 던전 생성 전에 기존 스토커 정리
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 던전 생성 완료 시 스토커 생성 메서드 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted -= ClearSpawnedStalkers; // 기존 스토커 정리 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 등록했던 스토커 생성 메서드 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 이벤트 처리
        {
            SpawnStalker(); // 출현 확률을 확인하고 스토커 생성
        }

        public void SpawnStalker() // 던전당 최대 한 마리의 스토커 생성 시도
        {
            ClearSpawnedStalkers(); // 이전 던전에서 자동 생성한 스토커 제거

            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                Debug.LogError("[StalkerSpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 스토커 생성 중단
            }

            if (stalkerPrefab == null) // 스토커 프리팹이 연결되지 않았는지 확인
            {
                Debug.LogError("[StalkerSpawnManager] Stalker Prefab이 연결되지 않았습니다."); // 프리팹 누락 오류 출력
                return; // 스토커 생성 중단
            }

            if (player == null) // 플레이어 참조가 없는지 확인
            {
                player = FindFirstObjectByType<PlayerController>(); // 현재 Scene에서 플레이어 다시 검색
            }

            if (player == null) // 다시 검색해도 플레이어가 없는지 확인
            {
                Debug.LogError("[StalkerSpawnManager] PlayerController를 찾을 수 없습니다."); // 플레이어 누락 오류 출력
                return; // 스토커 생성 중단
            }

            float safeSpawnChance = Mathf.Clamp01(spawnChance); // 출현 확률을 0에서 1 사이로 제한

            if (Random.value > safeSpawnChance) // 이번 던전에서 스토커 출현 확률에 실패했는지 확인
            {
                Debug.Log($"[StalkerSpawnManager] 스토커 미출현 — 확률 {safeSpawnChance * 100f:F0}%"); // 미출현 결과 출력
                return; // 스토커를 생성하지 않고 종료
            }

            List<Room> candidateRooms = new List<Room>(); // 스토커 생성 후보 방 목록 생성

            foreach (Room room in dungeonGenerator.PlacedRooms) // 현재 던전에 생성된 모든 방 순회
            {
                if (room == null || !room.AllowAutomaticSpawning) // 방 유효성과 자동 스폰 허용 상태 확인
                {
                    continue; // 계단 방과 유효하지 않은 방 제외
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방을 제외하도록 설정했는지 확인
                {
                    continue; // 시작 방을 후보에서 제외
                }

                candidateRooms.Add(room); // 조건을 통과한 방을 스토커 후보 목록에 추가
            }

            if (candidateRooms.Count == 0) // 스토커를 생성할 후보 방이 없는지 확인
            {
                Debug.LogWarning("[StalkerSpawnManager] 스토커를 생성할 후보 방이 없습니다."); // 후보 방 없음 경고 출력
                return; // 스토커 생성 중단
            }

            int randomStartIndex = Random.Range(0, candidateRooms.Count); // 후보 검색을 시작할 무작위 방 인덱스 결정

            for (int i = 0; i < candidateRooms.Count; i++) // 모든 후보 방을 최대 한 번씩 검색
            {
                int roomIndex = (randomStartIndex + i) % candidateRooms.Count; // 목록 범위를 순환하는 현재 방 인덱스 계산
                Room candidateRoom = candidateRooms[roomIndex]; // 현재 스토커 생성 후보 방 가져오기

                bool foundPosition = DungeonSpawnUtility.TryFindSpawnPosition( // 공용 안전 위치 검색 실행
                    candidateRoom, // 현재 스토커를 생성할 후보 방 전달
                    player.transform, // 플레이어 안전거리 확인용 Transform 전달
                    wallMargin, // 벽 여백 전달
                    positionSearchHeight, // 장애물 검사 높이 전달
                    collisionCheckRadius, // 충돌 검사 반경 전달
                    positionSearchAttempts, // 위치 검색 횟수 전달
                    playerSafeDistance, // 플레이어 주변 생성 금지 거리 전달
                    out Vector3 searchPosition); // 검색된 스토커 위치 저장

                if (!foundPosition) // 현재 방에서 안전한 위치를 찾지 못했는지 확인
                {
                    continue; // 다음 후보 방 검색
                }

                float heightAdjustment = groundOffset - positionSearchHeight; // 검색 위치를 실제 바닥 위치로 보정할 높이 계산
                Vector3 spawnPosition = searchPosition + Vector3.up * heightAdjustment; // 스토커 루트가 바닥에 놓일 최종 위치 계산
                Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 스토커의 무작위 Y축 회전 계산
                Stalker spawnedStalker = Instantiate(stalkerPrefab, spawnPosition, spawnRotation, transform); // 스토커를 현재 매니저 자식으로 생성

                spawnedStalker.Activate(player); // 생성된 스토커의 목표를 현재 플레이어로 지정
                spawnedStalkers.Add(spawnedStalker); // 생성된 스토커를 관리 목록에 추가

                Debug.Log($"[StalkerSpawnManager] 스토커 출현 — {candidateRoom.name}"); // 스토커 생성 방 정보 출력
                return; // 한 마리를 생성했으므로 추가 생성 중단
            }

            Debug.LogWarning("[StalkerSpawnManager] 모든 후보 방에서 안전한 스토커 위치를 찾지 못했습니다."); // 전체 위치 검색 실패 경고 출력
        }

        public void ClearSpawnedStalkers() // 이 매니저가 자동 생성한 기존 스토커 제거
        {
            foreach (Stalker stalker in spawnedStalkers) // 생성된 스토커 목록 순회
            {
                if (stalker != null) // 스토커 오브젝트가 아직 존재하는지 확인
                {
                    stalker.gameObject.SetActive(false); // 지연 삭제 전에 스토커와 Collider 즉시 비활성화
                    Destroy(stalker.gameObject); // 스토커 게임 오브젝트 제거 예약
                }
            }

            spawnedStalkers.Clear(); // 스토커 관리 목록 초기화
        }

        void OnGUI() // 스토커 자동 생성 상태를 임시 화면에 표시
        {
            if (!showDebug || !DebugUIToggleController.SpawnInfoVisible) // Inspector 설정과 F4 표시 상태 확인
            {
                return; // 자동 스폰 디버그 정보 표시 중단
            }

            GUI.Label(new Rect(10f, 290f, 300f, 25f), $"자동 생성 스토커: {ActiveStalkerCount}"); // 현재 생성된 스토커 수 표시
        }
    }
}