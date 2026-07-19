using System.Collections.Generic; // 생성된 보물 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class TreasureSpawnManager : MonoBehaviour // 던전의 보물 자동 생성을 담당
    {
        [Header("필수 참조")] // Inspector 필수 참조 설정 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 생성 완료 이벤트와 방 목록을 제공하는 던전 생성기
        [SerializeField] Treasure[] treasurePrefabs; // 자동 생성할 보물 프리팹 목록

        [Header("미믹 스폰")] // Inspector 미믹 스폰 설정 구분
        [SerializeField] Mimic mimicPrefab; // 일반 보물 대신 생성할 미믹 프리팹
        [SerializeField][Range(0f, 1f)] float mimicChance = 0.15f; // 보물 생성 기회가 미믹으로 교체될 확률
        [SerializeField] float mimicGroundOffset = 0f; // 미믹 루트를 바닥에서 띄울 높이

        [Header("방별 보물 수")] // 방별 보물 수 설정 구분
        [SerializeField] int minTreasuresPerRoom = 0; // 방 하나의 최소 기본 보물 수
        [SerializeField] int maxTreasuresPerRoom = 1; // 방 하나의 최대 기본 보물 수
        [SerializeField] bool skipStartingRoom = true; // 시작 방에서 보물을 생성하지 않을지 결정

        [Header("스폰 위치")] // 보물 위치 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽으로부터 떨어질 최소 거리
        [SerializeField] float spawnHeight = 0.5f; // 바닥에서 보물을 생성할 높이
        [SerializeField] float collisionCheckRadius = 0.35f; // 보물 생성 위치의 충돌 검사 반경
        [SerializeField] int positionSearchAttempts = 30; // 방 안에서 위치를 검색할 최대 횟수
        [SerializeField] float playerSafeDistance = 2f; // 플레이어 주변 보물 생성 금지 거리

        [Header("가치 설정")] // 보물 가치 설정 구분
        [SerializeField][Min(0f)] float riskMultiplier = 1f; // 이후 리스크 시스템에서 적용할 보상 배율

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 현재 보물 수를 화면에 표시할지 결정

        readonly List<Treasure> spawnedTreasures = new List<Treasure>(); // 이 매니저가 생성한 보물 목록
        readonly List<Mimic> spawnedMimics = new List<Mimic>(); // TreasureSpawnManager가 생성한 미믹 목록
        Transform player; // 플레이어 위치를 확인하기 위한 Transform 참조

        public int ActiveTreasureCount // 현재 존재하는 자동 생성 보물 수 반환
        {
            get // 유효한 보물 개수 계산
            {
                int count = 0; // 유효한 보물 수 초기화

                foreach (Treasure treasure in spawnedTreasures) // 생성된 보물 목록 순회
                {
                    if (treasure != null) // 보물 오브젝트가 아직 존재하는지 확인
                    {
                        count++; // 유효한 보물 수 증가
                    }
                }

                return count; // 계산된 보물 수 반환
            }
        }

        public int ActiveMimicCount // 현재 존재하는 자동 생성 미믹 수 반환
        {
            get // 유효한 미믹 개수 계산
            {
                int count = 0; // 유효한 미믹 수 초기화

                foreach (Mimic mimic in spawnedMimics) // 생성된 미믹 목록 순회
                {
                    if (mimic != null) // 미믹 오브젝트가 아직 존재하는지 확인
                    {
                        count++; // 유효한 미믹 수 증가
                    }
                }

                return count; // 계산된 미믹 수 반환
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
                dungeonGenerator.GenerationStarted += ClearSpawnedTreasures; // 새 던전 생성 전에 기존 보물과 미믹 정리
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 던전 생성 완료 시 보물 생성 메서드 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted -= ClearSpawnedTreasures; // 기존 보물 정리 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 등록했던 보물 생성 메서드 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 이벤트 처리
        {
            SpawnTreasures(); // 생성된 던전 방에 보물 자동 배치
        }

        public void SpawnTreasures() // 생성된 모든 던전 방에 보물 자동 배치
        {
            ClearSpawnedTreasures(); // 이전에 자동 생성한 보물 제거

            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                Debug.LogError("[TreasureSpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 보물 생성을 중단
            }

            if (treasurePrefabs == null || treasurePrefabs.Length == 0) // 보물 프리팹 목록이 비어 있는지 확인
            {
                Debug.LogError("[TreasureSpawnManager] Treasure Prefabs가 비어 있습니다."); // 프리팹 누락 오류 출력
                return; // 보물 생성을 중단
            }

            int totalSpawned = 0; // 이번에 생성된 전체 보물 수 저장
            int totalMimicsSpawned = 0; // 이번에 생성된 전체 미믹 수 저장

            foreach (Room room in dungeonGenerator.PlacedRooms) // 생성된 던전의 모든 방을 순회
            {
                if (room == null) // 방 오브젝트가 유효하지 않은지 확인
                {
                    continue; // 현재 방을 건너뜀
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방을 제외하도록 설정했는지 확인
                {
                    continue; // 시작 방의 보물 생성을 건너뜀
                }

                float brightnessMultiplier = DungeonSpawnUtility.GetBrightnessMultiplier(room); // 방 밝기에 따른 보물 수와 가치 배율 계산
                int safeMinimum = Mathf.Max(0, minTreasuresPerRoom); // 최소 보물 수가 음수가 되지 않도록 제한
                int safeMaximum = Mathf.Max(safeMinimum, maxTreasuresPerRoom); // 최대값이 최소값보다 작지 않도록 제한
                int baseCount = Random.Range(safeMinimum, safeMaximum + 1); // 방의 기본 보물 수 무작위 결정
                int spawnCount = Mathf.CeilToInt(baseCount * brightnessMultiplier); // 밝기 배율을 적용한 최종 보물 수 계산

                for (int i = 0; i < spawnCount; i++) // 최종 보물 수만큼 생성 반복
                {
                    bool foundPosition = DungeonSpawnUtility.TryFindSpawnPosition( // 공용 위치 검색 기능 실행
                        room, // 현재 보물을 생성할 방 전달
                        player, // 플레이어 Transform 전달
                        wallMargin, // 벽 여백 전달
                        spawnHeight, // 보물 생성 높이 전달
                        collisionCheckRadius, // 보물 충돌 검사 반경 전달
                        positionSearchAttempts, // 위치 검색 횟수 전달
                        playerSafeDistance, // 플레이어 안전거리 전달
                        out Vector3 spawnPosition); // 검색된 생성 위치 저장

                    if (!foundPosition) // 안전한 위치를 찾지 못했는지 확인
                    {
                        Debug.LogWarning($"[TreasureSpawnManager] {room.name}: 안전한 보물 위치를 찾지 못했습니다."); // 위치 검색 실패 경고 출력
                        continue; // 현재 보물 생성을 건너뜀
                    }

                    bool shouldSpawnMimic = mimicPrefab != null && Random.value < Mathf.Clamp01(mimicChance); // 현재 보물 생성 기회를 미믹으로 교체할지 결정

                    if (shouldSpawnMimic) // 미믹 생성이 선택되었는지 확인
                    {
                        float heightAdjustment = mimicGroundOffset - spawnHeight; // 보물용 검색 높이를 미믹 바닥 높이로 보정
                        Vector3 mimicPosition = spawnPosition + Vector3.up * heightAdjustment; // 미믹 루트가 바닥에 놓일 최종 위치 계산
                        Quaternion mimicRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 미믹의 무작위 Y축 회전 계산
                        Mimic spawnedMimic = Instantiate(mimicPrefab, mimicPosition, mimicRotation, transform); // 미믹을 TreasureSpawnManager 자식으로 생성

                        spawnedMimics.Add(spawnedMimic); // 생성된 미믹을 관리 목록에 추가
                        totalMimicsSpawned++; // 전체 생성된 미믹 수 증가
                        continue; // 현재 생성 기회에서 일반 보물 생성 방지
                    }

                    Treasure selectedPrefab = treasurePrefabs[Random.Range(0, treasurePrefabs.Length)]; // 목록에서 보물 프리팹 무작위 선택

                    if (selectedPrefab == null) // 선택된 프리팹이 비어 있는지 확인
                    {
                        continue; // 비어 있는 프리팹 생성을 건너뜀
                    }

                    Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 보물의 무작위 Y축 회전 계산
                    Treasure spawnedTreasure = Instantiate(selectedPrefab, spawnPosition, spawnRotation, transform); // 보물을 현재 매니저의 자식으로 생성
                    float finalRiskMultiplier = Mathf.Max(0f, riskMultiplier); // 리스크 배율이 음수가 되지 않도록 제한
                    spawnedTreasure.GenerateValue(brightnessMultiplier, finalRiskMultiplier); // 밝기와 리스크 배율을 적용해 보물 가치 결정
                    spawnedTreasures.Add(spawnedTreasure); // 생성된 보물을 관리 목록에 추가
                    totalSpawned++; // 전체 생성 수 증가
                }
            }

            Debug.Log($"[TreasureSpawnManager] 자동 생성 완료 — 보물 {totalSpawned}개, 미믹 {totalMimicsSpawned}마리");
            // 보물과 미믹 생성 결과 출력
        }

        public void ClearSpawnedTreasures() // 이 매니저가 자동 생성한 보물 제거
        {
            foreach (Treasure treasure in spawnedTreasures) // 생성된 보물 목록 순회
            {
                if (treasure != null) // 보물이 아직 존재하는지 확인
                {
                    treasure.gameObject.SetActive(false); // 지연 삭제 전에 보물과 Collider 즉시 비활성화
                    Destroy(treasure.gameObject); // 보물 게임 오브젝트 제거 예약
                }
            }

            spawnedTreasures.Clear(); // 보물 관리 목록 초기화

            foreach (Mimic mimic in spawnedMimics) // 기존 자동 생성 미믹 목록 순회
            {
                if (mimic != null) // 미믹 오브젝트가 아직 존재하는지 확인
                {
                    mimic.gameObject.SetActive(false); // 지연 삭제 전에 미믹과 Collider 즉시 비활성화
                    Destroy(mimic.gameObject); // 미믹 게임 오브젝트 제거 예약
                }
            }

            spawnedMimics.Clear(); // 자동 생성 미믹 목록 초기화


        }

        void OnGUI() // 보물 자동 생성 상태를 임시 화면에 표시
        {
            if (!showDebug) // 디버그 표시가 꺼져 있는지 확인
            {
                return; // 화면 표시 중단
            }

            GUI.Label(new Rect(10f, 215f, 300f, 25f), $"자동 생성 보물: {ActiveTreasureCount}"); 
            // 현재 생성된 보물 수 표시
            GUI.Label(new Rect(10f, 265f, 300f, 25f), $"자동 생성 미믹: {ActiveMimicCount}"); 
            // 현재 생성된 미믹 수 표시

        }
    }
}