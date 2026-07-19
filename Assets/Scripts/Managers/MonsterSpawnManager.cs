using System.Collections.Generic; // 생성된 몬스터 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class MonsterSpawnManager : MonoBehaviour // 던전의 몬스터 자동 생성을 담당
    {
        [Header("필수 참조")] // Inspector 필수 참조 설정 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 생성 완료 이벤트와 방 목록을 제공하는 던전 생성기
        [SerializeField] MonsterAI[] monsterPrefabs; // 자동 생성할 몬스터 프리팹 목록

        [Header("방별 몬스터 수")] // 방별 몬스터 수 설정 구분
        [SerializeField] int minMonstersPerRoom = 1; // 방 하나의 최소 기본 몬스터 수
        [SerializeField] int maxMonstersPerRoom = 2; // 방 하나의 최대 기본 몬스터 수
        [SerializeField] bool skipStartingRoom = true; // 시작 방에서 몬스터를 생성하지 않을지 결정

        [Header("스폰 위치")] // 몬스터 위치 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽으로부터 떨어질 최소 거리
        [SerializeField] float spawnHeight = 1.1f; // 바닥에서 몬스터를 생성할 높이
        [SerializeField] float collisionCheckRadius = 0.45f; // 몬스터 생성 위치의 충돌 검사 반경
        [SerializeField] int positionSearchAttempts = 30; // 방 안에서 위치를 검색할 최대 횟수
        [SerializeField] float playerSafeDistance = 4f; // 플레이어 주변 몬스터 생성 금지 거리

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 현재 몬스터 수를 화면에 표시할지 결정

        readonly List<MonsterAI> spawnedMonsters = new List<MonsterAI>(); // 이 매니저가 생성한 몬스터 목록
        Transform player; // 플레이어 위치를 확인하기 위한 Transform 참조

        public int ActiveMonsterCount // 현재 존재하는 자동 생성 몬스터 수 반환
        {
            get // 유효한 몬스터 개수 계산
            {
                int count = 0; // 유효한 몬스터 수 초기화

                foreach (MonsterAI monster in spawnedMonsters) // 생성된 몬스터 목록 순회
                {
                    if (monster != null) // 몬스터 오브젝트가 아직 존재하는지 확인
                    {
                        count++; // 유효한 몬스터 수 증가
                    }
                }

                return count; // 계산된 몬스터 수 반환
            }
        }

        void Awake() // 던전 생성기와 플레이어 참조 초기화
        {
            if (dungeonGenerator == null) // DungeonGenerator가 Inspector에 연결되지 않았는지 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // 현재 씬에서 DungeonGenerator 자동 검색
            }

            PlayerController playerController = FindFirstObjectByType<PlayerController>(); // 현재 씬에서 PlayerController 검색

            if (playerController != null) // 플레이어를 찾았는지 확인
            {
                player = playerController.transform; // 플레이어의 Transform 저장
            }
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted += ClearSpawnedMonsters; // 새 던전 생성 전에 기존 몬스터 정리
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 던전 생성 완료 시 몬스터 생성 메서드 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted -= ClearSpawnedMonsters; // 기존 몬스터 정리 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 등록했던 몬스터 생성 메서드 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 이벤트 처리
        {
            SpawnMonsters(); // 생성된 던전 방에 몬스터 자동 배치
        }

        public void SpawnMonsters() // 생성된 모든 던전 방에 몬스터 자동 배치
        {
            ClearSpawnedMonsters(); // 이전에 자동 생성한 몬스터 제거

            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                Debug.LogError("[MonsterSpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 몬스터 생성을 중단
            }

            if (monsterPrefabs == null || monsterPrefabs.Length == 0) // 몬스터 프리팹 목록이 비어 있는지 확인
            {
                Debug.LogError("[MonsterSpawnManager] Monster Prefabs가 비어 있습니다."); // 프리팹 누락 오류 출력
                return; // 몬스터 생성을 중단
            }

            int totalSpawned = 0; // 이번에 생성된 전체 몬스터 수 저장

            foreach (Room room in dungeonGenerator.PlacedRooms) // 생성된 던전의 모든 방을 순회
            {
                if (room == null) // 방 오브젝트가 유효하지 않은지 확인
                {
                    continue; // 현재 방을 건너뜀
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방을 제외하도록 설정했는지 확인
                {
                    continue; // 시작 방의 몬스터 생성을 건너뜀
                }

                float brightnessMultiplier = DungeonSpawnUtility.GetBrightnessMultiplier(room); // 방 밝기에 따른 몬스터 수 배율 계산
                int safeMinimum = Mathf.Max(0, minMonstersPerRoom); // 최소 몬스터 수가 음수가 되지 않도록 제한
                int safeMaximum = Mathf.Max(safeMinimum, maxMonstersPerRoom); // 최대값이 최소값보다 작지 않도록 제한
                int baseCount = Random.Range(safeMinimum, safeMaximum + 1); // 방의 기본 몬스터 수 무작위 결정
                DungeonRouteData selectedRoute = DungeonSelectionManager.Instance != null // 던전 선택 매니저 존재 여부 확인
                    ? DungeonSelectionManager.Instance.SelectedRoute // 현재 선택한 던전 경로 가져오기
                    : null; // 선택 경로 없음 처리

                float routeMonsterMultiplier = selectedRoute != null // 선택 경로 존재 여부 확인
                    ? Mathf.Max(0f, selectedRoute.MonsterSpawnMultiplier) // 선택 경로의 몬스터 생성 배율 적용
                    : 1f; // 기본 몬스터 생성 배율 적용

                int spawnCount = Mathf.CeilToInt(baseCount * brightnessMultiplier * routeMonsterMultiplier); // 밝기와 선택 경로를 적용한 몬스터 수 계산

                for (int i = 0; i < spawnCount; i++) // 최종 몬스터 수만큼 생성 반복
                {
                    bool foundPosition = DungeonSpawnUtility.TryFindSpawnPosition( // 공용 위치 검색 기능 실행
                        room, // 현재 몬스터를 생성할 방 전달
                        player, // 플레이어 Transform 전달
                        wallMargin, // 벽 여백 전달
                        spawnHeight, // 몬스터 생성 높이 전달
                        collisionCheckRadius, // 몬스터 충돌 검사 반경 전달
                        positionSearchAttempts, // 위치 검색 횟수 전달
                        playerSafeDistance, // 플레이어 안전거리 전달
                        out Vector3 spawnPosition); // 검색된 생성 위치 저장

                    if (!foundPosition) // 안전한 위치를 찾지 못했는지 확인
                    {
                        Debug.LogWarning($"[MonsterSpawnManager] {room.name}: 안전한 몬스터 위치를 찾지 못했습니다."); // 위치 검색 실패 경고 출력
                        continue; // 현재 몬스터 생성을 건너뜀
                    }

                    MonsterAI selectedPrefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Length)]; // 목록에서 몬스터 프리팹 무작위 선택

                    if (selectedPrefab == null) // 선택된 프리팹이 비어 있는지 확인
                    {
                        continue; // 비어 있는 프리팹 생성을 건너뜀
                    }

                    Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 몬스터의 무작위 Y축 회전 계산
                    MonsterAI spawnedMonster = Instantiate(selectedPrefab, spawnPosition, spawnRotation, transform); // 몬스터를 현재 매니저의 자식으로 생성
                    spawnedMonsters.Add(spawnedMonster); // 생성된 몬스터를 관리 목록에 추가
                    totalSpawned++; // 전체 생성 수 증가
                }
            }

            Debug.Log($"[MonsterSpawnManager] 몬스터 자동 생성 완료 — {totalSpawned}마리"); // 최종 생성 결과 출력
        }

        public void ClearSpawnedMonsters() // 이 매니저가 자동 생성한 몬스터 제거
        {
            foreach (MonsterAI monster in spawnedMonsters) // 생성된 몬스터 목록 순회
            {
                if (monster != null) // 몬스터가 아직 존재하는지 확인
                {
                    monster.gameObject.SetActive(false); // 지연 삭제 전에 몬스터와 Collider 즉시 비활성화
                    Destroy(monster.gameObject); // 몬스터 게임 오브젝트 제거 예약
                }
            }

            spawnedMonsters.Clear(); // 몬스터 관리 목록 초기화
        }

        void OnGUI() // 몬스터 자동 생성 상태를 임시 화면에 표시
        {
            if (!showDebug) // 디버그 표시가 꺼져 있는지 확인
            {
                return; // 화면 표시 중단
            }

            GUI.Label(new Rect(10f, 190f, 300f, 25f), $"자동 생성 몬스터: {ActiveMonsterCount}"); // 현재 생성된 몬스터 수 표시
        }
    }
}