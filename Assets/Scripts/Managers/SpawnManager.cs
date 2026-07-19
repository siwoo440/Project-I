using System.Collections.Generic; // 생성된 몬스터 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// 던전 생성 완료 후 각 방에 몬스터를 자동 배치.
    /// 시작 방은 제외하며 방이 어두울수록 더 많은 몬스터를 생성.
    /// 보물과 함정 스폰은 이후 작업에서 확장.
    /// </summary>
    public class SpawnManager : MonoBehaviour // 몬스터 자동 스폰 관리 컴포넌트
    {
        [Header("필수 참조")] // 필수 참조 설정 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 던전 생성기 참조
        [SerializeField] MonsterAI[] monsterPrefabs; // 자동 생성할 몬스터 프리팹 목록

        [Header("방별 몬스터 수")] // 몬스터 수 설정 구분
        [SerializeField] int minMonstersPerRoom = 1; // 방별 최소 기본 몬스터 수
        [SerializeField] int maxMonstersPerRoom = 2; // 방별 최대 기본 몬스터 수
        [SerializeField] bool skipStartingRoom = true; // 시작 방 스폰 제외 여부

        [Header("스폰 위치")] // 스폰 위치 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽에서 떨어질 최소 거리
        [SerializeField] float spawnHeight = 1.1f; // 바닥 위 몬스터 생성 높이
        [SerializeField] float collisionCheckRadius = 0.45f; // 장애물 확인 반경
        [SerializeField] int positionSearchAttempts = 12; // 방별 위치 검색 최대 횟수
        [SerializeField] float playerSafeDistance = 4f; // 플레이어 주변 스폰 금지 거리

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 자동 스폰 수 표시 여부

        readonly List<MonsterAI> spawnedMonsters = new List<MonsterAI>(); // 자동 생성된 몬스터 목록
        PlayerController player; // 플레이어 위치 확인용 참조

        public int ActiveMonsterCount // 현재 살아 있는 자동 스폰 몬스터 수
        {
            get // 살아 있는 몬스터 개수 계산
            {
                int activeCount = 0; // 활성 몬스터 합계 초기화

                foreach (MonsterAI monster in spawnedMonsters) // 생성된 몬스터 목록 순회
                {
                    if (monster != null) // 몬스터 생존 여부 확인
                    {
                        activeCount++; // 활성 몬스터 수 증가
                    }
                }

                return activeCount; // 최종 활성 몬스터 수 반환
            }
        }

        void Awake() // 필수 오브젝트 참조 초기화
        {
            if (dungeonGenerator == null) // DungeonGenerator 수동 연결 여부 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // 현재 씬에서 던전 생성기 검색
            }

            player = FindFirstObjectByType<PlayerController>(); // 현재 씬에서 플레이어 검색
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 생성 완료 처리 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 생성 완료 처리 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 후 몬스터 스폰 시작
        {
            SpawnMonsters(); // 전체 방 몬스터 자동 배치
        }

        public void SpawnMonsters() // 생성된 모든 방에 몬스터 배치
        {
            ClearSpawnedMonsters(); // 이전 자동 스폰 몬스터 제거

            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                Debug.LogError("[SpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 오류 출력
                return; // 자동 스폰 중단
            }

            if (monsterPrefabs == null || monsterPrefabs.Length == 0) // 몬스터 프리팹 등록 여부 확인
            {
                Debug.LogError("[SpawnManager] Monster Prefabs가 비어 있습니다."); // 프리팹 오류 출력
                return; // 자동 스폰 중단
            }

            int totalSpawned = 0; // 이번 생성의 몬스터 합계 초기화

            foreach (Room room in dungeonGenerator.PlacedRooms) // 생성된 모든 방 순회
            {
                if (room == null) // 삭제되거나 비어 있는 방 확인
                {
                    continue; // 현재 방 처리 건너뛰기
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방 제외 설정 확인
                {
                    continue; // 시작 방 스폰 건너뛰기
                }

                float brightnessMultiplier = GetBrightnessMultiplier(room); // 방 밝기 기반 스폰 배율 계산
                int safeMinimum = Mathf.Max(0, minMonstersPerRoom); // 최소 몬스터 수 음수 방지
                int safeMaximum = Mathf.Max(safeMinimum, maxMonstersPerRoom); // 최대 몬스터 수 범위 보정
                int baseCount = Random.Range(safeMinimum, safeMaximum + 1); // 방별 기본 몬스터 수 결정
                int spawnCount = Mathf.CeilToInt(baseCount * brightnessMultiplier); // 밝기 배율 적용 몬스터 수 계산

                for (int i = 0; i < spawnCount; i++) // 방에 생성할 몬스터 수만큼 반복
                {
                    if (!TryFindSpawnPosition(room, out Vector3 spawnPosition)) // 안전한 스폰 위치 검색
                    {
                        Debug.LogWarning($"[SpawnManager] {room.name}: 안전한 스폰 위치를 찾지 못했습니다."); // 위치 검색 실패 출력
                        continue; // 현재 몬스터 생성 건너뛰기
                    }

                    MonsterAI prefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Length)]; // 무작위 몬스터 프리팹 선택

                    if (prefab == null) // 선택된 프리팹 존재 여부 확인
                    {
                        continue; // 비어 있는 프리팹 건너뛰기
                    }

                    Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 무작위 Y축 회전 계산
                    MonsterAI monster = Instantiate(prefab, spawnPosition, rotation, transform); // 몬스터 프리팹 생성

                    spawnedMonsters.Add(monster); // 자동 생성 몬스터 목록에 추가
                    totalSpawned++; // 이번 생성 몬스터 합계 증가
                }
            }

            Debug.Log($"[SpawnManager] 몬스터 자동 스폰 완료: {totalSpawned}마리"); // 전체 스폰 결과 출력
        }

        float GetBrightnessMultiplier(Room room) // 방 밝기에 따른 몬스터 수 배율 계산
        {
            LightRoom lightRoom = room.GetComponentInChildren<LightRoom>(); // 방의 밝기 컴포넌트 검색
            float brightness = lightRoom != null ? lightRoom.FixedBrightness : 0f; // 현재 방 밝기 가져오기

            if (brightness >= 81f) // 매우 밝은 방 확인
            {
                return 0.5f; // 몬스터 수 50% 적용
            }

            if (brightness >= 61f) // 밝은 방 확인
            {
                return 0.8f; // 몬스터 수 80% 적용
            }

            if (brightness >= 41f) // 보통 밝기 방 확인
            {
                return 1f; // 몬스터 수 100% 적용
            }

            if (brightness >= 21f) // 어두운 방 확인
            {
                return 1.5f; // 몬스터 수 150% 적용
            }

            return 2f; // 칠흑 방 몬스터 수 200% 적용
        }

        bool TryFindSpawnPosition(Room room, out Vector3 spawnPosition) // 방 내부의 안전한 스폰 위치 검색
        {
            spawnPosition = room.transform.position + Vector3.up * spawnHeight; // 위치 검색 실패 시 기본값 설정

            LightRoom lightRoom = room.GetComponentInChildren<LightRoom>(); // 방의 밝기 영역 검색

            if (lightRoom == null) // LightRoom 존재 여부 확인
            {
                return false; // 방 범위를 알 수 없어 검색 실패
            }

            Collider roomZone = lightRoom.GetComponent<Collider>(); // LightRoom의 방 범위 Collider 가져오기

            if (roomZone == null) // 방 범위 Collider 존재 여부 확인
            {
                return false; // 방 범위를 알 수 없어 검색 실패
            }

            Bounds bounds = roomZone.bounds; // 방의 월드 공간 범위 가져오기
            float marginX = Mathf.Min(wallMargin, bounds.extents.x * 0.5f); // X축 안전 여백 보정
            float marginZ = Mathf.Min(wallMargin, bounds.extents.z * 0.5f); // Z축 안전 여백 보정
            int safeAttempts = Mathf.Max(1, positionSearchAttempts); // 최소 검색 횟수 보장

            for (int attempt = 0; attempt < safeAttempts; attempt++) // 안전한 위치를 찾을 때까지 반복
            {
                float x = Random.Range(bounds.min.x + marginX, bounds.max.x - marginX); // 방 내부 무작위 X 위치 계산
                float z = Random.Range(bounds.min.z + marginZ, bounds.max.z - marginZ); // 방 내부 무작위 Z 위치 계산
                Vector3 rayOrigin = new Vector3(x, bounds.max.y + 5f, z); // 바닥 탐색 Ray 시작 위치 계산
                float rayDistance = bounds.size.y + 10f; // 바닥 탐색 Ray 거리 계산
                Vector3 candidate; // 현재 검사할 스폰 위치

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Ignore)) // 방 바닥 탐색
                {
                    candidate = hit.point + Vector3.up * spawnHeight; // 바닥 위 스폰 높이 적용
                }
                else // 바닥 Raycast 실패 처리
                {
                    candidate = new Vector3(x, room.transform.position.y + spawnHeight, z); // 방 높이 기준 대체 위치 계산
                }

                if (player != null && Vector3.Distance(candidate, player.transform.position) < playerSafeDistance) // 플레이어 안전거리 확인
                {
                    continue; // 플레이어와 가까운 위치 제외
                }

                if (Physics.CheckSphere(candidate, collisionCheckRadius, ~0, QueryTriggerInteraction.Ignore)) // 장애물 겹침 여부 확인
                {
                    continue; // 장애물이 있는 위치 제외
                }

                spawnPosition = candidate; // 안전한 위치 결과 저장
                return true; // 위치 검색 성공 반환
            }

            return false; // 모든 위치 검색 실패 반환
        }

        void ClearSpawnedMonsters() // 기존 자동 생성 몬스터 제거
        {
            foreach (MonsterAI monster in spawnedMonsters) // 기존 자동 생성 몬스터 순회
            {
                if (monster != null) // 몬스터 오브젝트 존재 여부 확인
                {
                    Destroy(monster.gameObject); // 기존 몬스터 오브젝트 제거
                }
            }

            spawnedMonsters.Clear(); // 자동 생성 몬스터 목록 초기화
        }

        void OnGUI() // 자동 스폰 디버그 정보 표시
        {
            if (!showDebug) // 디버그 표시 설정 확인
            {
                return; // 디버그 UI 표시 중단
            }

            GUI.Label(new Rect(10f, 190f, 640f, 20f), $"자동 스폰 몬스터: {ActiveMonsterCount}마리"); // 현재 활성 몬스터 수 표시
        }
    }
}