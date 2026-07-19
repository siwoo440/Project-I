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

        [Header("보물 스폰")] // Inspector에서 보물 스폰 설정을 구분
        [SerializeField] Treasure[] treasurePrefabs; // 자동으로 생성할 보물 프리팹 목록
        [SerializeField] int minTreasuresPerRoom = 0; // 방 하나에 생성할 최소 기본 보물 수
        [SerializeField] int maxTreasuresPerRoom = 1; // 방 하나에 생성할 최대 기본 보물 수
        [SerializeField] bool skipStartingRoomForTreasures = true; // 시작 방의 보물 생성을 건너뛸지 결정
        [SerializeField] float treasureSpawnHeight = 0.5f; // 바닥으로부터 보물을 생성할 높이
        [SerializeField] float treasureCollisionCheckRadius = 0.35f; // 보물 생성 위치의 충돌 검사 반경
        [SerializeField] float treasurePlayerSafeDistance = 2f; // 플레이어 주변에 보물이 생성되지 않는 거리
        [SerializeField][Min(0f)] float treasureRiskMultiplier = 1f; // 이후 리스크 시스템에서 사용할 보상 배율

        [Header("스폰 위치")] // 스폰 위치 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽에서 떨어질 최소 거리
        [SerializeField] float spawnHeight = 1.1f; // 바닥 위 몬스터 생성 높이
        [SerializeField] float collisionCheckRadius = 0.45f; // 장애물 확인 반경
        [SerializeField] int positionSearchAttempts = 12; // 방별 위치 검색 최대 횟수
        [SerializeField] float playerSafeDistance = 4f; // 플레이어 주변 스폰 금지 거리

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 자동 스폰 수 표시 여부

        readonly List<MonsterAI> spawnedMonsters = new List<MonsterAI>(); // 자동 생성된 몬스터 목록
        readonly List<Treasure> spawnedTreasures = new List<Treasure>(); // 현재 SpawnManager가 생성한 보물 목록
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
        public int ActiveTreasureCount // 현재 남아 있는 자동 생성 보물 수 반환
        {
            get // 보물 목록에서 유효한 보물 수 계산
            {
                int count = 0; // 유효한 보물 수를 저장

                foreach (Treasure treasure in spawnedTreasures) // 생성된 모든 보물을 순회
                {
                    if (treasure != null) // 보물이 파괴되지 않고 존재하는지 확인
                    {
                        count++; // 유효한 보물 수 증가
                    }
                }

                return count; // 계산된 보물 수 반환
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

        void HandleDungeonGenerated() // 던전 생성 완료 이벤트를 받았을 때 자동 스폰 실행
        {
            SpawnMonsters(); // 생성된 방에 몬스터 자동 배치
            SpawnTreasures(); // 생성된 방에 보물 자동 배치
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
                    if (!TryFindSpawnPosition(room, spawnHeight, collisionCheckRadius, playerSafeDistance, out Vector3 spawnPosition)) 
                        // 몬스터가 배치될 안전한 위치 검색
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

        public void SpawnTreasures() // 생성된 던전 방에 보물을 자동 배치
        {
            ClearSpawnedTreasures(); // 이전에 자동 생성된 보물 제거

            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                Debug.LogError("[SpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 보물 생성을 중단
            }

            if (treasurePrefabs == null || treasurePrefabs.Length == 0) // 보물 프리팹 목록이 비어 있는지 확인
            {
                Debug.LogError("[SpawnManager] Treasure Prefabs가 비어 있습니다."); // 프리팹 누락 오류 출력
                return; // 보물 생성을 중단
            }

            int totalSpawned = 0; // 이번 생성에서 배치된 전체 보물 수 저장

            foreach (Room room in dungeonGenerator.PlacedRooms) // 던전에 생성된 모든 방을 순회
            {
                if (room == null) // 방이 파괴되었거나 유효하지 않은지 확인
                {
                    continue; // 현재 방을 건너뛰고 다음 방 확인
                }

                if (skipStartingRoomForTreasures && room == dungeonGenerator.StartRoom) // 시작 방을 제외하도록 설정했는지 확인
                {
                    continue; // 시작 방의 보물 생성을 건너뜀
                }

                float brightnessMultiplier = GetBrightnessMultiplier(room); // 방 밝기에 따른 생성 및 가치 배율 계산
                int baseSpawnCount = Random.Range(minTreasuresPerRoom, maxTreasuresPerRoom + 1); // 방의 기본 보물 수 무작위 결정
                int spawnCount = Mathf.CeilToInt(baseSpawnCount * brightnessMultiplier); // 어두운 방일수록 보물 생성 수 증가

                for (int i = 0; i < spawnCount; i++) // 결정된 보물 수만큼 반복
                {
                    if (!TryFindSpawnPosition(room, treasureSpawnHeight, treasureCollisionCheckRadius, treasurePlayerSafeDistance, out Vector3 spawnPosition)) // 보물이 배치될 안전한 위치 검색
                    {
                        continue; // 적절한 위치를 찾지 못하면 현재 보물 생성을 건너뜀
                    }

                    Treasure selectedPrefab = treasurePrefabs[Random.Range(0, treasurePrefabs.Length)]; // 목록에서 보물 프리팹 하나를 무작위 선택

                    if (selectedPrefab == null) // 선택된 프리팹이 비어 있는지 확인
                    {
                        continue; // 비어 있는 프리팹은 생성하지 않음
                    }

                    Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 보물의 Y축 방향을 무작위 결정
                    Treasure spawnedTreasure = Instantiate(selectedPrefab, spawnPosition, spawnRotation, transform); // 보물을 SpawnManager 자식으로 생성
                    float finalRiskMultiplier = Mathf.Max(0f, treasureRiskMultiplier); // 리스크 배율이 음수가 되지 않도록 제한
                    spawnedTreasure.GenerateValue(brightnessMultiplier, finalRiskMultiplier); // 밝기와 리스크 배율을 적용해 보물 가치 고정
                    spawnedTreasures.Add(spawnedTreasure); // 생성된 보물을 관리 목록에 추가
                    totalSpawned++; // 전체 생성된 보물 수 증가
                }
            }

            Debug.Log($"[SpawnManager] 보물 자동 생성 완료 — {totalSpawned}개"); // 최종 보물 생성 결과 출력
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

        bool TryFindSpawnPosition(Room room, float objectHeight, float objectCollisionRadius, float safeDistance, out Vector3 spawnPosition) // 방 내부에서 안전한 생성 위치 검색
        {
            spawnPosition = Vector3.zero; // 위치 검색 실패에 대비해 기본값 설정

            LightRoom lightRoom = room.GetComponent<LightRoom>(); // 방 오브젝트에서 밝기 영역 컴포넌트 검색

            if (lightRoom == null) // 방 루트에 LightRoom이 없는지 확인
            {
                lightRoom = room.GetComponentInChildren<LightRoom>(); // 방 자식 오브젝트에서도 LightRoom 검색
            }

            if (lightRoom == null) // 방에서 LightRoom을 찾지 못했는지 확인
            {
                Debug.LogWarning($"[SpawnManager] {room.name}에 LightRoom이 없어 스폰 위치를 찾을 수 없습니다."); // 설정 누락 경고 출력
                return false; // 위치 검색 실패 반환
            }

            Collider roomCollider = lightRoom.GetComponent<Collider>(); // LightRoom 영역을 나타내는 Collider 검색

            if (roomCollider == null) // LightRoom에 Collider가 없는지 확인
            {
                Debug.LogWarning($"[SpawnManager] {room.name}의 LightRoom에 Collider가 없습니다."); // Collider 누락 경고 출력
                return false; // 위치 검색 실패 반환
            }

            Bounds bounds = roomCollider.bounds; // 방 영역 Collider의 월드 좌표 경계 가져오기
            float minX = bounds.min.x + wallMargin; // 서쪽 벽에서 여백을 둔 최소 X 좌표 계산
            float maxX = bounds.max.x - wallMargin; // 동쪽 벽에서 여백을 둔 최대 X 좌표 계산
            float minZ = bounds.min.z + wallMargin; // 남쪽 벽에서 여백을 둔 최소 Z 좌표 계산
            float maxZ = bounds.max.z - wallMargin; // 북쪽 벽에서 여백을 둔 최대 Z 좌표 계산

            if (minX >= maxX || minZ >= maxZ) // 벽 여백 적용 후 사용할 공간이 남아 있는지 확인
            {
                Debug.LogWarning($"[SpawnManager] {room.name}의 스폰 가능 영역이 너무 작습니다."); // 방 크기 설정 오류 경고 출력
                return false; // 위치 검색 실패 반환
            }

            for (int attempt = 0; attempt < positionSearchAttempts; attempt++) // 설정된 횟수만큼 무작위 위치 검색 반복
            {
                float randomX = Random.Range(minX, maxX); // 방 내부의 무작위 X 좌표 선택
                float randomZ = Random.Range(minZ, maxZ); // 방 내부의 무작위 Z 좌표 선택
                Vector3 rayOrigin = new Vector3(randomX, bounds.max.y + 2f, randomZ); // 천장 위쪽에서 바닥을 향할 Ray 시작점 생성

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit floorHit, bounds.size.y + 5f, Physics.AllLayers, QueryTriggerInteraction.Ignore)) // Trigger를 제외하고 아래 방향에서 실제 바닥 검색
                {
                    continue; // 바닥을 찾지 못하면 다른 위치 검색
                }

                if (floorHit.collider == roomCollider && roomCollider.isTrigger) // Ray가 바닥 대신 LightRoom Trigger에 맞았는지 확인
                {
                    RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, bounds.size.y + 5f); // Ray 경로의 모든 충돌 정보 가져오기
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); // 가까운 충돌부터 확인하도록 거리순 정렬
                    bool foundFloor = false; // 실제 바닥을 찾았는지 저장

                    foreach (RaycastHit hit in hits) // Ray에 감지된 모든 충돌을 순회
                    {
                        if (hit.collider == roomCollider) // 현재 충돌체가 LightRoom Trigger인지 확인
                        {
                            continue; // LightRoom Trigger는 바닥 판정에서 제외
                        }

                        floorHit = hit; // 처음 발견한 실제 충돌체를 바닥으로 지정
                        foundFloor = true; // 실제 바닥을 찾았다고 기록
                        break; // 더 이상 충돌체를 확인하지 않음
                    }

                    if (!foundFloor) // LightRoom 이외의 바닥을 찾지 못했는지 확인
                    {
                        continue; // 다른 위치 검색
                    }
                }

                Vector3 candidate = floorHit.point + Vector3.up * objectHeight; // 바닥에서 대상 높이만큼 올린 생성 후보 위치 계산

                if (player != null && Vector3.Distance(candidate, player.transform.position) < safeDistance) // 후보 위치가 플레이어와 너무 가까운지 확인
                {
                    continue; // 플레이어 주변 위치를 제외
                }

                if (Physics.CheckSphere(candidate, objectCollisionRadius, Physics.AllLayers, QueryTriggerInteraction.Ignore)) // Trigger를 제외한 실제 장애물과의 충돌 확인
                {
                    continue; // 다른 오브젝트와 겹치는 위치를 제외
                }

                if (!lightRoom.Contains(candidate)) // 후보 위치가 실제 LightRoom 영역 안에 있는지 확인
                {
                    continue; // 방 영역 밖 위치를 제외
                }

                spawnPosition = candidate; // 검증을 통과한 위치를 최종 생성 위치로 저장
                return true; // 위치 검색 성공 반환
            }

            return false; // 모든 시도에서 적절한 위치를 찾지 못했음을 반환
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
        public void ClearSpawnedTreasures() // SpawnManager가 자동 생성한 기존 보물 제거
        {
            foreach (Treasure treasure in spawnedTreasures) // 생성된 모든 보물을 순회
            {
                if (treasure != null) // 보물 오브젝트가 아직 존재하는지 확인
                {
                    Destroy(treasure.gameObject); // 보물 게임 오브젝트 제거
                }
            }

            spawnedTreasures.Clear(); // 보물 관리 목록 초기화
        }

        void OnGUI() // 자동 스폰 상태를 임시 화면에 표시
        {
            if (!showDebug) // 디버그 표시가 꺼져 있는지 확인
            {
                return; // 화면 표시를 중단
            }

            GUI.Label(new Rect(10f, 190f, 300f, 25f), $"자동 생성 몬스터: {ActiveMonsterCount}"); // 현재 생성된 몬스터 수 표시
            GUI.Label(new Rect(10f, 215f, 300f, 25f), $"자동 생성 보물: {ActiveTreasureCount}"); // 현재 생성된 보물 수 표시
        }
    }
}