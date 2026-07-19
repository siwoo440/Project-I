using System.Collections; // 지연 검증 코루틴 사용
using System.Collections.Generic; // 오류와 경고 목록 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // F3 패널 전환과 F8 재검증 입력 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VerticalSliceValidator : MonoBehaviour // 싱글 수직 슬라이스 필수 설정 검증
    {
        [Header("핵심 진행")] // Inspector 핵심 진행 참조 구분
        [SerializeField] DungeonGenerator dungeonGenerator; // 절차적 던전 생성기
        [SerializeField] DungeonTimeSystem dungeonTimeSystem; // 던전 제한시간 시스템
        [SerializeField] Wagon wagon; // 보물 적재와 탈출용 마차
        [SerializeField] WagonCargo wagonCargo; // 마차 적재 상호작용 지점
        [SerializeField] WagonLever wagonLever; // 마차 탈출 상호작용 지점

        [Header("플레이어")] // Inspector 플레이어 참조 구분
        [SerializeField] PlayerController playerController; // 플레이어 이동과 생존 상태
        [SerializeField] InventorySystem inventorySystem; // 플레이어 인벤토리
        [SerializeField] LightSystem lightSystem; // 플레이어 밝기 시스템
        [SerializeField] PlayerStatusEffectSystem statusEffectSystem; // 출혈과 둔화 상태 시스템
        [SerializeField] PlayerDamageFeedback damageFeedback; // 플레이어 피격 피드백

        [Header("자동 생성")] // Inspector 자동 생성 매니저 참조 구분
        [SerializeField] MonsterSpawnManager monsterSpawnManager; // 일반 몬스터 스폰 매니저
        [SerializeField] TreasureSpawnManager treasureSpawnManager; // 보물과 미믹 스폰 매니저
        [SerializeField] TrapSpawnManager trapSpawnManager; // 함정 스폰 매니저
        [SerializeField] StalkerSpawnManager stalkerSpawnManager; // 스토커 스폰 매니저
        [SerializeField] GimmickMonsterSpawnManager gimmickSpawnManager; // 고스트와 웃는 석상 스폰 매니저

        [Header("기타 시스템")] // Inspector 기타 필수 시스템 참조 구분
        [SerializeField] AudioManager audioManager; // 3D 효과음 시스템

        [Header("검증 설정")] // Inspector 검증 기준 설정 구분
        [SerializeField] int minimumRoomCount = 8; // 정상 던전으로 인정할 최소 방 수
        [SerializeField] float validationDelay = 0.5f; // 자동 생성 완료 후 검증 대기시간
        [SerializeField] bool showValidationPanel = true; // 임시 검증 패널 표시 여부

        readonly List<string> errors = new List<string>(); // 진행 불가 오류 목록
        readonly List<string> warnings = new List<string>(); // 확인이 필요한 경고 목록

        bool generationCompleted; // 던전 생성 완료 여부
        bool validationFinished; // 현재 검증 완료 여부
        bool validationScheduled; // 지연 검증 예약 여부
        int generatedRoomCount; // 현재 생성된 방 수

        public bool IsValid => validationFinished && errors.Count == 0; // 현재 수직 슬라이스 통과 여부
        public int ErrorCount => errors.Count; // 현재 진행 불가 오류 수
        public int WarningCount => warnings.Count; // 현재 확인 경고 수

        void Awake() // 수직 슬라이스 필수 참조 자동 검색
        {
            ResolveReferences(); // Scene의 필수 시스템 참조 검색
        }

        void OnEnable() // 던전 생성 완료 이벤트 구독
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationCompleted += HandleDungeonGenerated; // 던전 생성 완료 이벤트 연결
            }
        }

        void Start() // Scene 시작 후 초기 검증 예약
        {
            ScheduleValidation(); // 초기 Scene 설정과 생성 결과 검증 예약
        }

        void OnDisable() // 던전 생성 완료 이벤트 구독 해제
        {
            if (dungeonGenerator != null) // 던전 생성기 존재 여부 확인
            {
                dungeonGenerator.GenerationCompleted -= HandleDungeonGenerated; // 던전 생성 완료 이벤트 연결 해제
            }
        }

        void Update() // 검증 패널 전환과 수동 재검증 입력 처리
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null) // 키보드 연결 여부 확인
            {
                return; // 키보드 입력 처리 중단
            }

            if (keyboard.f3Key.wasPressedThisFrame) // F3 입력 여부 확인
            {
                showValidationPanel = !showValidationPanel; // 검증 패널 표시 상태 전환

                string panelState = showValidationPanel ? "표시" : "숨김"; // 현재 패널 상태 문구 계산
                Debug.Log($"[VerticalSlice] 검증 패널 {panelState}"); // 변경된 패널 상태 출력
            }

            if (keyboard.f8Key.wasPressedThisFrame) // F8 입력 여부 확인
            {
                ValidateVerticalSlice(); // 현재 Scene과 생성 결과 즉시 재검증
            }
        }

        void HandleDungeonGenerated() // 던전 생성 완료 결과 처리
        {
            generationCompleted = true; // 던전 생성 완료 상태 저장
            ScheduleValidation(); // 스폰 매니저 처리 후 지연 검증 예약
        }

        void ScheduleValidation() // 중복을 방지하며 지연 검증 예약
        {
            if (validationScheduled) // 기존 검증 예약 여부 확인
            {
                return; // 중복 코루틴 시작 방지
            }

            validationScheduled = true; // 검증 예약 상태 저장
            StartCoroutine(ValidateAfterDelay()); // 생성 완료 대기 후 검증 시작
        }

        IEnumerator ValidateAfterDelay() // 자동 생성 매니저 처리 완료 후 검증
        {
            float safeDelay = Mathf.Max(0f, validationDelay); // 검증 대기시간 안전값 계산

            if (safeDelay > 0f) // 실제 대기시간 존재 여부 확인
            {
                yield return new WaitForSecondsRealtime(safeDelay); // Time Scale과 관계없는 대기 적용
            }

            validationScheduled = false; // 검증 예약 상태 초기화
            ValidateVerticalSlice(); // 전체 수직 슬라이스 검증 실행
        }

        public void ValidateVerticalSlice() // 필수 시스템과 현재 생성 결과 검증
        {
            ResolveReferences(); // 누락되거나 변경된 참조 다시 검색
            errors.Clear(); // 기존 오류 목록 초기화
            warnings.Clear(); // 기존 경고 목록 초기화
            generatedRoomCount = CountGeneratedRooms(); // 현재 던전 방 수 계산

            CheckRequiredReference(dungeonGenerator, "DungeonGenerator"); // 던전 생성기 필수 여부 확인
            CheckRequiredReference(dungeonTimeSystem, "DungeonTimeSystem"); // 제한시간 시스템 필수 여부 확인
            CheckRequiredReference(wagon, "Wagon"); // 마차 필수 여부 확인
            CheckRequiredReference(wagonCargo, "WagonCargo"); // 적재 지점 필수 여부 확인
            CheckRequiredReference(wagonLever, "WagonLever"); // 탈출 레버 필수 여부 확인
            CheckRequiredReference(playerController, "PlayerController"); // 플레이어 제어 필수 여부 확인
            CheckRequiredReference(inventorySystem, "InventorySystem"); // 인벤토리 필수 여부 확인
            CheckRequiredReference(lightSystem, "LightSystem"); // 밝기 시스템 필수 여부 확인
            CheckRequiredReference(statusEffectSystem, "PlayerStatusEffectSystem"); // 상태 이상 시스템 필수 여부 확인
            CheckRequiredReference(damageFeedback, "PlayerDamageFeedback"); // 피격 피드백 필수 여부 확인
            CheckRequiredReference(monsterSpawnManager, "MonsterSpawnManager"); // 일반 몬스터 스폰 필수 여부 확인
            CheckRequiredReference(treasureSpawnManager, "TreasureSpawnManager"); // 보물 스폰 필수 여부 확인
            CheckRequiredReference(trapSpawnManager, "TrapSpawnManager"); // 함정 스폰 필수 여부 확인
            CheckRequiredReference(stalkerSpawnManager, "StalkerSpawnManager"); // 스토커 스폰 필수 여부 확인
            CheckRequiredReference(gimmickSpawnManager, "GimmickMonsterSpawnManager"); // 기믹 몬스터 스폰 필수 여부 확인
            CheckRequiredReference(audioManager, "AudioManager"); // 효과음 시스템 필수 여부 확인

            ValidateDungeonGeneration(); // 던전 생성 결과 검사
            ValidateSpawnResults(); // 자동 스폰 결과 검사
            ValidateAudioListener(); // AudioListener 개수 검사

            validationFinished = true; // 전체 검증 완료 상태 저장
            PrintValidationResult(); // Console에 검증 결과 출력
        }

        void ResolveReferences() // Scene에서 누락된 필수 참조 자동 검색
        {
            if (dungeonGenerator == null) // 던전 생성기 연결 여부 확인
            {
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // Scene에서 던전 생성기 검색
            }

            if (dungeonTimeSystem == null) // 제한시간 시스템 연결 여부 확인
            {
                dungeonTimeSystem = FindFirstObjectByType<DungeonTimeSystem>(); // Scene에서 제한시간 시스템 검색
            }

            if (wagon == null) // 마차 연결 여부 확인
            {
                wagon = FindFirstObjectByType<Wagon>(); // Scene에서 마차 검색
            }

            if (wagonCargo == null) // 마차 적재 지점 연결 여부 확인
            {
                wagonCargo = FindFirstObjectByType<WagonCargo>(); // Scene에서 마차 적재 지점 검색
            }

            if (wagonLever == null) // 마차 레버 연결 여부 확인
            {
                wagonLever = FindFirstObjectByType<WagonLever>(); // Scene에서 마차 탈출 레버 검색
            }

            if (playerController == null) // 플레이어 연결 여부 확인
            {
                playerController = FindFirstObjectByType<PlayerController>(); // Scene에서 플레이어 검색
            }

            if (inventorySystem == null) // 인벤토리 연결 여부 확인
            {
                inventorySystem = FindFirstObjectByType<InventorySystem>(); // Scene에서 인벤토리 검색
            }

            if (lightSystem == null) // 밝기 시스템 연결 여부 확인
            {
                lightSystem = FindFirstObjectByType<LightSystem>(); // Scene에서 밝기 시스템 검색
            }

            if (statusEffectSystem == null) // 상태 이상 시스템 연결 여부 확인
            {
                statusEffectSystem = FindFirstObjectByType<PlayerStatusEffectSystem>(); // Scene에서 상태 이상 시스템 검색
            }

            if (damageFeedback == null) // 플레이어 피격 피드백 연결 여부 확인
            {
                damageFeedback = FindFirstObjectByType<PlayerDamageFeedback>(); // Scene에서 피격 피드백 검색
            }

            if (monsterSpawnManager == null) // 일반 몬스터 스폰 매니저 연결 여부 확인
            {
                monsterSpawnManager = FindFirstObjectByType<MonsterSpawnManager>(); // Scene에서 일반 몬스터 스폰 매니저 검색
            }

            if (treasureSpawnManager == null) // 보물 스폰 매니저 연결 여부 확인
            {
                treasureSpawnManager = FindFirstObjectByType<TreasureSpawnManager>(); // Scene에서 보물 스폰 매니저 검색
            }

            if (trapSpawnManager == null) // 함정 스폰 매니저 연결 여부 확인
            {
                trapSpawnManager = FindFirstObjectByType<TrapSpawnManager>(); // Scene에서 함정 스폰 매니저 검색
            }

            if (stalkerSpawnManager == null) // 스토커 스폰 매니저 연결 여부 확인
            {
                stalkerSpawnManager = FindFirstObjectByType<StalkerSpawnManager>(); // Scene에서 스토커 스폰 매니저 검색
            }

            if (gimmickSpawnManager == null) // 기믹 몬스터 스폰 매니저 연결 여부 확인
            {
                gimmickSpawnManager = FindFirstObjectByType<GimmickMonsterSpawnManager>(); // Scene에서 기믹 몬스터 스폰 매니저 검색
            }

            if (audioManager == null) // 효과음 시스템 연결 여부 확인
            {
                audioManager = FindFirstObjectByType<AudioManager>(); // Scene에서 AudioManager 검색
            }
        }

        void CheckRequiredReference(Object target, string systemName) // 필수 Unity 오브젝트 연결 여부 검사
        {
            if (target == null) // 전달된 필수 오브젝트 존재 여부 확인
            {
                errors.Add($"{systemName} 없음"); // 진행 불가 오류 목록에 추가
            }
        }

        int CountGeneratedRooms() // 현재 DungeonGenerator의 유효한 방 수 계산
        {
            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                return 0; // 생성된 방 없음 반환
            }

            int roomCount = 0; // 유효한 방 수 초기화

            foreach (Room room in dungeonGenerator.PlacedRooms) // 현재 생성된 모든 방 순회
            {
                if (room != null) // 유효한 방 오브젝트인지 확인
                {
                    roomCount++; // 유효한 방 수 증가
                }
            }

            return roomCount; // 최종 방 수 반환
        }

        void ValidateDungeonGeneration() // 생성된 방과 시작 방 검사
        {
            if (dungeonGenerator == null) // 던전 생성기 존재 여부 확인
            {
                return; // 던전 생성 검사 중단
            }

            if (!generationCompleted && dungeonGenerator.StartRoom != null) // 이벤트 이전에 방이 생성되었는지 확인
            {
                generationCompleted = true; // 생성 완료 상태 보정
            }

            if (!generationCompleted) // 던전 생성 완료 여부 확인
            {
                errors.Add("던전 생성 완료 이벤트 미수신"); // 생성 완료 오류 추가
            }

            if (dungeonGenerator.StartRoom == null) // 시작 방 생성 여부 확인
            {
                errors.Add("시작 방 없음"); // 시작 방 누락 오류 추가
            }

            if (generatedRoomCount < minimumRoomCount) // 최소 방 수 충족 여부 확인
            {
                errors.Add($"방 수 부족: {generatedRoomCount}/{minimumRoomCount}"); // 방 수 부족 오류 추가
            }
        }

        void ValidateSpawnResults() // 핵심 자동 생성 결과 검사
        {
            if (monsterSpawnManager != null && monsterSpawnManager.ActiveMonsterCount <= 0) // 일반 몬스터 생성 여부 확인
            {
                warnings.Add("자동 생성 일반 몬스터 0마리"); // 일반 몬스터 없음 경고 추가
            }

            if (treasureSpawnManager != null && treasureSpawnManager.ActiveTreasureCount <= 0) // 일반 보물 생성 여부 확인
            {
                warnings.Add("자동 생성 일반 보물 0개"); // 일반 보물 없음 경고 추가
            }

            if (trapSpawnManager != null && trapSpawnManager.ActiveTrapCount <= 0) // 함정 생성 여부 확인
            {
                warnings.Add("자동 생성 함정 0개"); // 함정 없음 경고 추가
            }
        }

        void ValidateAudioListener() // Scene의 AudioListener 중복 여부 검사
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None); // Scene의 모든 AudioListener 검색

            if (listeners.Length == 0) // AudioListener 없음 여부 확인
            {
                errors.Add("AudioListener 없음"); // 오디오 출력 불가 오류 추가
            }
            else if (listeners.Length > 1) // AudioListener 중복 여부 확인
            {
                errors.Add($"AudioListener 중복: {listeners.Length}개"); // 오디오 중복 오류 추가
            }
        }

        void PrintValidationResult() // Console에 전체 검증 결과 출력
        {
            if (errors.Count == 0) // 진행 불가 오류 없음 여부 확인
            {
                Debug.Log($"[VerticalSlice] 필수 검증 통과 — 경고 {warnings.Count}개"); // 검증 통과 결과 출력
            }
            else // 진행 불가 오류 존재
            {
                Debug.LogError($"[VerticalSlice] 검증 실패 — 오류 {errors.Count}개, 경고 {warnings.Count}개"); // 검증 실패 결과 출력
            }

            foreach (string error in errors) // 모든 오류 목록 순회
            {
                Debug.LogError($"[VerticalSlice] 오류: {error}"); // 개별 오류 출력
            }

            foreach (string warning in warnings) // 모든 경고 목록 순회
            {
                Debug.LogWarning($"[VerticalSlice] 경고: {warning}"); // 개별 경고 출력
            }
        }

        string GetOutcomeText() // 현재 던전 진행 결과 문구 계산
        {
            if (wagon != null && wagon.HasLeft) // 마차 탈출 완료 여부 확인
            {
                return "탈출 성공"; // 탈출 성공 문구 반환
            }

            if (dungeonTimeSystem != null && dungeonTimeSystem.HasFailed) // 제한시간 유기 실패 여부 확인
            {
                return "탈출 실패"; // 탈출 실패 문구 반환
            }

            if (playerController != null && playerController.IsDead) // 플레이어 최종 사망 여부 확인
            {
                return "플레이어 사망"; // 플레이어 사망 문구 반환
            }

            return "진행 중"; // 기본 진행 상태 반환
        }

        void OnGUI() // 수직 슬라이스 검증 결과와 현재 런 정보 표시
        {
            if (!showValidationPanel) // 검증 패널 표시 여부 확인
            {
                return; // 패널 표시 중단
            }

            float width = 350f; // 검증 패널 너비
            float x = Screen.width - width - 10f; // 검증 패널 오른쪽 위치
            float y = 40f; // 검증 패널 위쪽 위치
            float height = 235f + (errors.Count + warnings.Count) * 18f; // 오류와 경고를 포함한 패널 높이 계산

            GUI.Box(new Rect(x, y, width, height), "25일차 수직 슬라이스 검증"); // 검증 패널 배경 표시

            string validationText = validationFinished ? IsValid ? "통과" : "실패" : "검증 중"; // 현재 검증 상태 문구 계산
            GUI.Label(new Rect(x + 10f, y + 25f, width - 20f, 20f), $"필수 검증: {validationText} / 오류 {errors.Count} / 경고 {warnings.Count}"); // 검증 상태 표시
            GUI.Label(new Rect(x + 10f, y + 45f, width - 20f, 20f), $"진행 결과: {GetOutcomeText()}"); // 현재 던전 결과 표시
            GUI.Label(new Rect(x + 10f, y + 65f, width - 20f, 20f), $"생성 방: {generatedRoomCount}개"); // 생성된 방 수 표시
            GUI.Label(new Rect(x + 10f, y + 85f, width - 20f, 20f), $"몬스터: {(monsterSpawnManager != null ? monsterSpawnManager.ActiveMonsterCount : 0)}"); // 일반 몬스터 수 표시
            GUI.Label(new Rect(x + 10f, y + 105f, width - 20f, 20f), $"보물: {(treasureSpawnManager != null ? treasureSpawnManager.ActiveTreasureCount : 0)} / 미믹: {(treasureSpawnManager != null ? treasureSpawnManager.ActiveMimicCount : 0)}"); // 보물과 미믹 수 표시
            GUI.Label(new Rect(x + 10f, y + 125f, width - 20f, 20f), $"함정: {(trapSpawnManager != null ? trapSpawnManager.ActiveTrapCount : 0)} / 스토커: {(stalkerSpawnManager != null ? stalkerSpawnManager.ActiveStalkerCount : 0)}"); // 함정과 스토커 수 표시
            GUI.Label(new Rect(x + 10f, y + 145f, width - 20f, 20f), $"고스트: {(gimmickSpawnManager != null ? gimmickSpawnManager.ActiveGhostCount : 0)} / 웃는 석상: {(gimmickSpawnManager != null ? gimmickSpawnManager.ActiveStatueCount : 0)}"); // 기믹 몬스터 수 표시
            GUI.Label(new Rect(x + 10f, y + 165f, width - 20f, 20f), $"마차: {(wagon != null ? wagon.SecuredCount : 0)}개 / {(wagon != null ? wagon.SecuredValue : 0)}골드"); // 마차 적재 결과 표시
            GUI.Label(new Rect(x + 10f, y + 185f, width - 20f, 20f), $"남은 시간: {(dungeonTimeSystem != null ? dungeonTimeSystem.RemainingSeconds : 0f):F0}초"); // 제한시간 잔여량 표시
            GUI.Label(new Rect(x + 10f, y + 205f, width - 20f, 20f), "[F3] 패널 숨김 / [F8] 현재 상태 재검증"); 
            // 패널 전환과 재검증 조작 안내 표시

            float issueY = y + 225f; // 오류와 경고 표시 시작 위치 계산

            foreach (string error in errors) // 모든 오류 목록 순회
            {
                GUI.Label(new Rect(x + 10f, issueY, width - 20f, 18f), $"오류: {error}"); // 개별 오류 표시
                issueY += 18f; // 다음 오류 표시 위치 이동
            }

            foreach (string warning in warnings) // 모든 경고 목록 순회
            {
                GUI.Label(new Rect(x + 10f, issueY, width - 20f, 18f), $"경고: {warning}"); // 개별 경고 표시
                issueY += 18f; // 다음 경고 표시 위치 이동
            }
        }
    }
}