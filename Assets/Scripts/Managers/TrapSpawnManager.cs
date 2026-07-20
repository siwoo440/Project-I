using System.Collections.Generic; // 생성된 함정 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class TrapSpawnManager : MonoBehaviour // 던전의 함정 자동 생성을 담당
    {
        [Header("필수 참조")] // Inspector 필수 참조 설정 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 생성 완료 이벤트와 방 목록을 제공하는 던전 생성기
        [SerializeField] Trap[] trapPrefabs; // 자동 생성할 함정 프리팹 목록

        [Header("방별 함정 수")] // 방별 함정 수 설정 구분
        [SerializeField] int minTrapsPerRoom = 0; // 방 하나의 최소 기본 함정 수
        [SerializeField] int maxTrapsPerRoom = 1; // 방 하나의 최대 기본 함정 수
        [SerializeField] bool skipStartingRoom = true; // 시작 방에서 함정을 생성하지 않을지 결정
        [SerializeField] bool useBrightnessMultiplier = true; // 어두운 방에서 함정 수를 증가시킬지 결정

        [Header("스폰 위치")] // 함정 위치 설정 구분
        [SerializeField] float wallMargin = 1.5f; // 벽으로부터 떨어질 최소 거리
        [SerializeField] float positionSearchHeight = 0.4f; // 장애물 검사를 실행할 바닥 위 높이
        [SerializeField] float groundOffset = 0f; // 함정 루트를 바닥에서 띄울 높이
        [SerializeField] float collisionCheckRadius = 0.3f; // 함정 위치의 장애물 검사 반경
        [SerializeField] int positionSearchAttempts = 30; // 방 안에서 위치를 검색할 최대 횟수
        [SerializeField] float playerSafeDistance = 3f; // 플레이어 주변 함정 생성 금지 거리

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 현재 함정 수를 화면에 표시할지 결정

        readonly List<Trap> spawnedTraps = new List<Trap>(); // 이 매니저가 생성한 함정 목록
        Transform player; // 플레이어 위치를 확인하기 위한 Transform 참조

        public int ActiveTrapCount // 현재 존재하는 자동 생성 함정 수 반환
        {
            get // 유효한 함정 개수 계산
            {
                int count = 0; // 유효한 함정 수 초기화

                foreach (Trap trap in spawnedTraps) // 생성된 함정 목록 순회
                {
                    if (trap != null) // 함정 오브젝트가 아직 존재하는지 확인
                    {
                        count++; // 유효한 함정 수 증가
                    }
                }

                return count; // 계산된 함정 수 반환
            }
        }

        void Awake() // 던전 생성기와 플레이어 참조 초기화
        {
            if (dungeonGenerator == null) // DungeonGenerator가 연결되지 않았는지 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // 현재 Scene에서 DungeonGenerator 검색
            }

            PlayerController playerController = FindFirstObjectByType<PlayerController>(); // 현재 Scene에서 플레이어 검색

            if (playerController != null) // 플레이어를 찾았는지 확인
            {
                player = playerController.transform; // 플레이어 Transform 저장
            }
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted += ClearSpawnedTraps; // 새 던전 생성 전에 기존 함정 정리
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 던전 생성 완료 시 함정 생성 연결
            }
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기가 존재하는지 확인
            {
                dungeonGenerator.GenerationStarted -= ClearSpawnedTraps; // 기존 함정 정리 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 등록했던 함정 생성 연결 해제
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 이벤트 처리
        {
            SpawnTraps(); // 생성된 방에 함정 자동 배치
        }

        public void SpawnTraps() // 생성된 모든 던전 방에 함정 자동 배치
        {
            ClearSpawnedTraps(); // 이전에 자동 생성한 함정 제거

            if (dungeonGenerator == null) // 던전 생성기 참조가 없는지 확인
            {
                Debug.LogError("[TrapSpawnManager] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 함정 생성을 중단
            }

            if (trapPrefabs == null || trapPrefabs.Length == 0) // 함정 프리팹 목록이 비어 있는지 확인
            {
                Debug.LogError("[TrapSpawnManager] Trap Prefabs가 비어 있습니다."); // 프리팹 누락 오류 출력
                return; // 함정 생성을 중단
            }

            int totalSpawned = 0; // 이번에 생성된 전체 함정 수 저장

            foreach (Room room in dungeonGenerator.PlacedRooms) // 생성된 던전의 모든 방을 순회
            {
                if (room == null) // 방 오브젝트가 유효하지 않은지 확인
                {
                    continue; // 현재 방을 건너뜀
                }

                if (skipStartingRoom && room == dungeonGenerator.StartRoom) // 시작 방을 제외하도록 설정했는지 확인
                {
                    continue; // 시작 방의 함정 생성을 건너뜀
                }

                int safeMinimum = Mathf.Max(0, minTrapsPerRoom); // 최소 함정 수가 음수가 되지 않도록 제한
                int safeMaximum = Mathf.Max(safeMinimum, maxTrapsPerRoom); // 최대값이 최소값보다 작지 않도록 제한
                int baseCount = Random.Range(safeMinimum, safeMaximum + 1); // 방의 기본 함정 수 무작위 결정
                float brightnessMultiplier = useBrightnessMultiplier ? DungeonSpawnUtility.GetBrightnessMultiplier(room) : 1f; // 밝기 설정에 따른 함정 수 배율 결정
                DungeonRouteData selectedRoute = DungeonSelectionManager.Instance != null // 던전 선택 매니저 존재 여부 확인
                    ? DungeonSelectionManager.Instance.SelectedRoute // 현재 선택한 던전 경로 가져오기
                    : null; // 선택 경로 없음 처리

                float routeTrapMultiplier = selectedRoute != null // 선택 경로 존재 여부 확인
                    ? Mathf.Max(0f, selectedRoute.TrapSpawnMultiplier) // 선택 경로의 함정 생성 배율 적용
                    : 1f; // 기본 함정 생성 배율 적용

                int spawnCount = Mathf.CeilToInt(baseCount * brightnessMultiplier * routeTrapMultiplier); // 밝기와 선택 경로를 적용한 함정 수 계산

                for (int i = 0; i < spawnCount; i++) // 최종 함정 수만큼 생성 반복
                {
                    bool foundPosition = DungeonSpawnUtility.TryFindSpawnPosition( // 공용 위치 검색 기능 실행
                        room, // 현재 함정을 생성할 방 전달
                        player, // 플레이어 Transform 전달
                        wallMargin, // 벽 여백 전달
                        positionSearchHeight, // 장애물 검사 높이 전달
                        collisionCheckRadius, // 충돌 검사 반경 전달
                        positionSearchAttempts, // 위치 검색 횟수 전달
                        playerSafeDistance, // 플레이어 안전거리 전달
                        out Vector3 searchPosition); // 검색된 위치 저장

                    if (!foundPosition) // 안전한 위치를 찾지 못했는지 확인
                    {
                        Debug.LogWarning($"[TrapSpawnManager] {room.name}: 안전한 함정 위치를 찾지 못했습니다."); // 위치 검색 실패 경고 출력
                        continue; // 현재 함정 생성을 건너뜀
                    }

                    Trap selectedPrefab = trapPrefabs[Random.Range(0, trapPrefabs.Length)]; // 목록에서 함정 프리팹 무작위 선택

                    if (selectedPrefab == null) // 선택된 프리팹이 비어 있는지 확인
                    {
                        continue; // 비어 있는 프리팹 생성을 건너뜀
                    }

                    float heightAdjustment = groundOffset - positionSearchHeight; // 검색 위치를 실제 바닥 위치로 옮길 높이 계산
                    Vector3 spawnPosition = searchPosition + Vector3.up * heightAdjustment; // 함정 루트가 바닥에 놓이도록 최종 위치 보정
                    Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 함정의 무작위 Y축 회전 계산
                    Trap spawnedTrap = Instantiate(selectedPrefab, spawnPosition, spawnRotation, transform); // 함정을 현재 매니저 자식으로 생성
                    spawnedTraps.Add(spawnedTrap); // 생성된 함정을 관리 목록에 추가
                    totalSpawned++; // 전체 생성 수 증가
                }
            }

            Debug.Log($"[TrapSpawnManager] 함정 자동 생성 완료 — {totalSpawned}개"); // 최종 생성 결과 출력
        }

        public void ClearSpawnedTraps() // 이 매니저가 자동 생성한 함정 제거
        {
            foreach (Trap trap in spawnedTraps) // 생성된 함정 목록 순회
            {
                if (trap != null) // 함정이 아직 존재하는지 확인
                {
                    trap.gameObject.SetActive(false); // 지연 삭제 전에 함정과 Collider 즉시 비활성화
                    Destroy(trap.gameObject); // 함정 게임 오브젝트 제거 예약
                }
            }

            spawnedTraps.Clear(); // 함정 관리 목록 초기화
        }

        void OnGUI() // 함정 자동 생성 상태를 임시 화면에 표시
        {
            if (!showDebug || !DebugUIToggleController.SpawnInfoVisible) // Inspector 설정과 F4 표시 상태 확인
            {
                return; // 자동 스폰 디버그 정보 표시 중단
            }

            GUI.Label(new Rect(10f, 240f, 300f, 25f), $"자동 생성 함정: {ActiveTrapCount}"); // 현재 생성된 함정 수 표시
        }
    }
}