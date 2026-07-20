using System.Collections; // 자동 반복 생성 코루틴 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // Keyboard 저수준 입력 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class RuntimePerformanceMonitor : MonoBehaviour // 성능과 반복 던전 생성 안정성을 검사
    {
        [Header("필수 참조")] // Inspector 참조 설정 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 반복 생성할 던전 생성기
        [SerializeField] MonsterSpawnManager monsterSpawnManager; // 일반 몬스터 개수 확인
        [SerializeField] TreasureSpawnManager treasureSpawnManager; // 보물과 미믹 개수 확인
        [SerializeField] TrapSpawnManager trapSpawnManager; // 함정 개수 확인
        [SerializeField] StalkerSpawnManager stalkerSpawnManager; // 스토커 개수 확인
        [SerializeField] GimmickMonsterSpawnManager gimmickSpawnManager; // 기믹 몬스터 개수 확인

        [Header("성능 측정")] // Inspector 성능 측정 설정 구분
        [SerializeField][Min(0.1f)] float sampleInterval = 0.5f; // FPS와 메모리 갱신 간격
        [SerializeField][Min(1f)] float memoryGrowthWarningMB = 16f; // 생성 1회 메모리 증가 경고 기준
        [SerializeField] bool showPanel = false; // Scene 시작 시 성능 검사 패널을 숨김

        [Header("자동 반복 생성")] // 반복 생성 검사 설정 구분
        [SerializeField][Min(1)] int automaticTestIterations = 10; // 자동 생성 반복 횟수
        [SerializeField][Min(0.2f)] float automaticTestInterval = 1f; // 반복 생성 사이의 실시간 대기시간

        float sampleElapsedTime; // 현재 성능 표본 누적시간
        int sampleFrameCount; // 현재 성능 표본 프레임 수
        float averageFps; // 최근 표본 평균 FPS
        float averageFrameMilliseconds; // 최근 표본 평균 프레임 시간
        float longestFrameMilliseconds; // Play 이후 가장 긴 프레임 시간
        long managedMemoryBytes; // 현재 관리 메모리 사용량
        long generationMemoryBefore; // 던전 생성 직전 관리 메모리
        long lastGenerationMemoryDelta; // 마지막 던전 생성 메모리 변화량
        int completedGenerationCount; // 감시 중 완료된 던전 생성 횟수
        int lastRoomLeftoverCount; // 마지막 검사에서 발견한 잔류 방 수
        int lastSpawnLeftoverCount; // 마지막 검사에서 발견한 잔류 스폰 오브젝트 수
        int maximumRecordedLeftovers; // 자동 검사 중 발견한 최대 잔류 개수
        bool automaticTestRunning; // 자동 반복 생성 검사 진행 여부

        void Awake() // 성능 검사 참조와 시작 패널 상태 초기화
        {
            showPanel = false; // 기존 Scene 직렬화 값과 관계없이 시작 시 성능 패널 숨김
            ResolveReferences(); // Scene의 검사 대상 자동 검색
            managedMemoryBytes = System.GC.GetTotalMemory(false); // 초기 관리 메모리 측정
        }

        void OnEnable() // 던전 생성 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationStarted += HandleGenerationStarted; // 생성 직전 메모리 측정 연결
                dungeonGenerator.GenerationCompleted += HandleGenerationCompleted; // 생성 완료 안정성 검사 연결
            }
        }

        void OnDisable() // 던전 생성 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationStarted -= HandleGenerationStarted; // 생성 시작 이벤트 연결 해제
                dungeonGenerator.GenerationCompleted -= HandleGenerationCompleted; // 생성 완료 이벤트 연결 해제
            }
        }

        void Update() // 성능 표본과 검사 키 입력 처리
        {
            float currentFrameMilliseconds = Time.unscaledDeltaTime * 1000f; // 현재 프레임 시간을 밀리초로 계산
            longestFrameMilliseconds = Mathf.Max(longestFrameMilliseconds, currentFrameMilliseconds); // 가장 긴 프레임 시간 갱신
            sampleElapsedTime += Time.unscaledDeltaTime; // 표본 실시간 누적
            sampleFrameCount++; // 표본 프레임 수 증가

            if (sampleElapsedTime >= sampleInterval) // 성능 표본 갱신시간 도달 여부 확인
            {
                averageFps = sampleFrameCount / Mathf.Max(0.0001f, sampleElapsedTime); // 표본 평균 FPS 계산
                averageFrameMilliseconds = 1000f / Mathf.Max(0.01f, averageFps); // 표본 평균 프레임 시간 계산
                managedMemoryBytes = System.GC.GetTotalMemory(false); // 현재 관리 메모리 사용량 측정
                sampleElapsedTime = 0f; // 표본 누적시간 초기화
                sampleFrameCount = 0; // 표본 프레임 수 초기화
            }

            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null) // 키보드 연결 여부 확인
            {
                return; // 키 입력 처리 중단
            }

            if (keyboard.f9Key.wasPressedThisFrame && !automaticTestRunning) // F9 수동 생성 입력 확인
            {
                RegenerateDungeon(); // 던전 한 번 재생성
            }

            if (keyboard.f10Key.wasPressedThisFrame) // F10 패널 표시 입력 확인
            {
                showPanel = !showPanel; // 성능 패널 표시 상태 전환
            }

            if (keyboard.f11Key.wasPressedThisFrame && !automaticTestRunning) // F11 자동 검사 입력 확인
            {
                StartCoroutine(RunAutomaticTest()); // 자동 반복 생성 검사 시작
            }
        }

        void ResolveReferences() // Scene에서 누락된 검사 대상 자동 검색
        {
            dungeonGenerator ??= FindFirstObjectByType<DungeonGenerator>(); // 던전 생성기 검색
            monsterSpawnManager ??= FindFirstObjectByType<MonsterSpawnManager>(); // 몬스터 스폰 매니저 검색
            treasureSpawnManager ??= FindFirstObjectByType<TreasureSpawnManager>(); // 보물 스폰 매니저 검색
            trapSpawnManager ??= FindFirstObjectByType<TrapSpawnManager>(); // 함정 스폰 매니저 검색
            stalkerSpawnManager ??= FindFirstObjectByType<StalkerSpawnManager>(); // 스토커 스폰 매니저 검색
            gimmickSpawnManager ??= FindFirstObjectByType<GimmickMonsterSpawnManager>(); // 기믹 스폰 매니저 검색
        }

        void HandleGenerationStarted() // 던전 생성 직전 메모리 기록
        {
            generationMemoryBefore = System.GC.GetTotalMemory(false); // 생성 직전 관리 메모리 저장
        }

        void HandleGenerationCompleted() // 던전 생성 완료 후 한 프레임 지연 검사 예약
        {
            completedGenerationCount++; // 완료된 던전 생성 횟수 증가
            StartCoroutine(CaptureGenerationResult()); // 지연 삭제가 끝난 다음 잔류 상태 검사
        }

        IEnumerator CaptureGenerationResult() // 지연 삭제 완료 후 방과 스폰 오브젝트 검사
        {
            yield return null; // Destroy 예약이 처리되도록 다음 프레임까지 대기

            int validRoomCount = CountValidRooms(); // 생성기 목록의 정상 방 수 계산
            int roomChildCount = dungeonGenerator != null ? dungeonGenerator.transform.childCount : 0; // 생성기 자식 방 개수 확인
            lastRoomLeftoverCount = Mathf.Max(0, roomChildCount - validRoomCount); // 목록에 없는 잔류 방 개수 계산

            int expectedSpawnCount = CountExpectedSpawns(); // 스폰 매니저가 관리하는 정상 개수 계산
            int actualSpawnChildCount = CountSpawnManagerChildren(); // 스폰 매니저 Hierarchy 자식 개수 계산
            lastSpawnLeftoverCount = Mathf.Max(0, actualSpawnChildCount - expectedSpawnCount); // 관리 목록에 없는 잔류 개수 계산

            managedMemoryBytes = System.GC.GetTotalMemory(false); // 생성 후 관리 메모리 측정
            lastGenerationMemoryDelta = managedMemoryBytes - generationMemoryBefore; // 생성 전후 메모리 변화 계산
            int totalLeftovers = lastRoomLeftoverCount + lastSpawnLeftoverCount; // 전체 잔류 개수 계산
            maximumRecordedLeftovers = Mathf.Max(maximumRecordedLeftovers, totalLeftovers); // 최대 잔류 개수 갱신

            if (totalLeftovers > 0) // 잔류 오브젝트 발견 여부 확인
            {
                Debug.LogWarning($"[Performance] 잔류 오브젝트 발견 — 방 {lastRoomLeftoverCount}, 스폰 {lastSpawnLeftoverCount}"); // 잔류 결과 경고 출력
            }
            else // 잔류 오브젝트가 없는 경우
            {
                Debug.Log($"[Performance] 생성 {completedGenerationCount}회 검사 통과 — 잔류 오브젝트 0개"); // 정상 검사 결과 출력
            }

            float memoryDeltaMB = lastGenerationMemoryDelta / 1048576f; // 메모리 변화량을 MB로 변환

            if (memoryDeltaMB > memoryGrowthWarningMB) // 메모리 증가 경고 기준 초과 여부 확인
            {
                Debug.LogWarning($"[Performance] 생성 메모리 증가 확인 — {memoryDeltaMB:F2} MB"); // 메모리 증가 경고 출력
            }
        }

        void RegenerateDungeon() // 던전을 한 번 수동 재생성
        {
            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                Debug.LogError("[Performance] DungeonGenerator가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 던전 재생성 중단
            }

            if (dungeonGenerator.IsGenerating) // 던전 생성 진행 여부 확인
            {
                return; // 생성 중 추가 요청 방지
            }

            dungeonGenerator.Generate(); // 새로운 던전 생성 실행
        }

        IEnumerator RunAutomaticTest() // 설정된 횟수만큼 던전을 자동 반복 생성
        {
            automaticTestRunning = true; // 자동 검사 진행 상태 활성화
            maximumRecordedLeftovers = 0; // 이전 최대 잔류 결과 초기화
            long testStartMemory = System.GC.GetTotalMemory(false); // 자동 검사 시작 메모리 저장

            for (int i = 0; i < automaticTestIterations; i++) // 설정된 반복 횟수만큼 실행
            {
                RegenerateDungeon(); // 던전 재생성 실행
                yield return new WaitForSecondsRealtime(automaticTestInterval); // 지연 삭제와 검사가 끝날 때까지 실시간 대기
            }

            yield return null; // 마지막 지연 삭제 완료 대기

            long testEndMemory = System.GC.GetTotalMemory(false); // 자동 검사 종료 메모리 측정
            float memoryDifferenceMB = (testEndMemory - testStartMemory) / 1048576f; // 전체 메모리 변화량 계산
            automaticTestRunning = false; // 자동 검사 진행 상태 해제

            Debug.Log($"[Performance] 자동 반복 검사 완료 — {automaticTestIterations}회, 최대 잔류 {maximumRecordedLeftovers}개, 메모리 변화 {memoryDifferenceMB:F2} MB"); // 자동 검사 최종 결과 출력
        }

        int CountValidRooms() // DungeonGenerator 목록의 정상 방 개수 계산
        {
            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                return 0; // 정상 방 없음 반환
            }

            int count = 0; // 정상 방 개수 초기화

            foreach (Room room in dungeonGenerator.PlacedRooms) // 생성된 방 목록 순회
            {
                if (room != null) // 정상 방 오브젝트인지 확인
                {
                    count++; // 정상 방 개수 증가
                }
            }

            return count; // 정상 방 개수 반환
        }

        int CountExpectedSpawns() // 스폰 매니저 관리 목록의 정상 개수 계산
        {
            int count = 0; // 정상 스폰 개수 초기화
            count += monsterSpawnManager != null ? monsterSpawnManager.ActiveMonsterCount : 0; // 일반 몬스터 수 추가
            count += treasureSpawnManager != null ? treasureSpawnManager.ActiveTreasureCount : 0; // 일반 보물 수 추가
            count += treasureSpawnManager != null ? treasureSpawnManager.ActiveMimicCount : 0; // 미믹 수 추가
            count += trapSpawnManager != null ? trapSpawnManager.ActiveTrapCount : 0; // 함정 수 추가
            count += stalkerSpawnManager != null ? stalkerSpawnManager.ActiveStalkerCount : 0; // 스토커 수 추가
            count += gimmickSpawnManager != null ? gimmickSpawnManager.ActiveGhostCount : 0; // 고스트 수 추가
            count += gimmickSpawnManager != null ? gimmickSpawnManager.ActiveStatueCount : 0; // 웃는 석상 수 추가

            return count; // 전체 정상 스폰 개수 반환
        }

        int CountSpawnManagerChildren() // 각 스폰 매니저의 직접 자식 개수 계산
        {
            int count = 0; // 전체 자식 개수 초기화
            count += monsterSpawnManager != null ? monsterSpawnManager.transform.childCount : 0; // 몬스터 자식 개수 추가
            count += treasureSpawnManager != null ? treasureSpawnManager.transform.childCount : 0; // 보물 자식 개수 추가
            count += trapSpawnManager != null ? trapSpawnManager.transform.childCount : 0; // 함정 자식 개수 추가
            count += stalkerSpawnManager != null ? stalkerSpawnManager.transform.childCount : 0; // 스토커 자식 개수 추가
            count += gimmickSpawnManager != null ? gimmickSpawnManager.transform.childCount : 0; // 기믹 몬스터 자식 개수 추가

            return count; // 전체 스폰 자식 개수 반환
        }

        void OnGUI() // 성능과 반복 생성 검사 결과를 임시 화면에 표시
        {
            if (!showPanel) // 패널 표시 여부 확인
            {
                return; // 화면 표시 중단
            }

            float memoryMB = managedMemoryBytes / 1048576f; // 현재 관리 메모리를 MB로 변환
            float generationDeltaMB = lastGenerationMemoryDelta / 1048576f; // 마지막 생성 메모리 변화를 MB로 변환
            string testState = automaticTestRunning ? "자동 검사 중" : "대기"; // 자동 검사 상태 문구 계산

            GUI.Box(new Rect(10f, 350f, 390f, 190f), "26일차 성능·재생성 검사"); // 성능 패널 배경 표시
            GUI.Label(new Rect(20f, 375f, 370f, 20f), $"FPS: {averageFps:F1} / 평균 {averageFrameMilliseconds:F2} ms / 최대 {longestFrameMilliseconds:F2} ms"); // 프레임 성능 표시
            GUI.Label(new Rect(20f, 395f, 370f, 20f), $"관리 메모리: {memoryMB:F2} MB / 마지막 생성 변화: {generationDeltaMB:F2} MB"); // 메모리 상태 표시
            GUI.Label(new Rect(20f, 415f, 370f, 20f), $"완료 생성: {completedGenerationCount}회 / 상태: {testState}"); // 생성 횟수와 검사 상태 표시
            GUI.Label(new Rect(20f, 435f, 370f, 20f), $"잔류 방: {lastRoomLeftoverCount} / 잔류 스폰: {lastSpawnLeftoverCount}"); // 잔류 오브젝트 표시
            GUI.Label(new Rect(20f, 455f, 370f, 20f), $"오디오 활성: {(AudioManager.Instance != null ? AudioManager.Instance.ActiveSourceCount : 0)} / 대기: {(AudioManager.Instance != null ? AudioManager.Instance.AvailableSourceCount : 0)}"); // 오디오 풀 상태 표시
            GUI.Label(new Rect(20f, 475f, 370f, 20f), $"파티클 활성: {(ParticleEffectPool.Instance != null ? ParticleEffectPool.Instance.ActiveEffectCount : 0)} / 대기: {(ParticleEffectPool.Instance != null ? ParticleEffectPool.Instance.AvailableEffectCount : 0)}"); // 파티클 풀 상태 표시
            GUI.Label(new Rect(20f, 500f, 370f, 20f), "[F9] 1회 생성 / [F10] 패널 / [F11] 자동 10회 검사"); // 성능 검사 조작 안내
        }
    }
}