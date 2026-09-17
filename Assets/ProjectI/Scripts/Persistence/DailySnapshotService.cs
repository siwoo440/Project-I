using System; // 문자열·콜백·숫자 변환 기능 사용
using System.Collections; // Coroutine 복구 절차 사용
using System.Collections.Generic; // 아이템 중복 캡처 집합 사용
using ProjectI.Economy; // 회수품 가격 상태 사용
using ProjectI.Items; // WorldItem과 복구 데이터 사용
using ProjectI.Loop; // Persistent 맵 목적지와 로더 사용
using ProjectI.Wagon; // CargoArea 위치 판정 사용
using UnityEngine; // 유니티 컴포넌트와 Object 기능 사용
using UnityEngine.SceneManagement; // 아이템 씬 소속 복구 기능 사용

namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    [DisallowMultipleComponent] // Persistent 씬에 저장 서비스 중복 부착 방지
    public sealed class DailySnapshotService : MonoBehaviour // 사무소 안전 체크포인트와 완료 일차 롤백 관리자
    {
        private static DailySnapshotService instance; // 현재 Persistent 저장 서비스 단일 참조
        [SerializeField] private int currentDay = 1; // 현재 플레이 중인 일차 번호
        [SerializeField] private bool initializeOnStart = true; // Persistent 시작 시 안전 체크포인트 자동 복구 여부
        [SerializeField] private float officeAutosaveInterval = 2f; // 사무소 체류 중 Current 자동 저장 간격
        private DailySnapshotStore store; // 로컬 SHA-256 저장소
        private EconomySnapshotData runtimeEconomy = new EconomySnapshotData(); // Office가 언로드되어도 유지할 현재 경제 상태
        private IRemoteDailySnapshotStore remoteStore; // 향후 서버 백업 구현 연결 지점
        private bool initialized; // 최초 저장·복구 초기화 완료 여부
        private bool restoreInProgress; // 중복 복구 방지 상태
        private bool dayCompletionInProgress; // 일차 확정 중 중복 저장 방지 상태
        private float nextOfficeAutosaveTime; // 다음 사무소 자동 저장 시각
        private ExpeditionDayPhase dayPhase = ExpeditionDayPhase.OfficePrep; // 오늘 원정 진행 단계
        private int campaignSeed; // 절차적 던전 시드 기준값 (캠페인마다 고정)

        public static DailySnapshotService Instance => instance; // 전역 저장 서비스 공개
        public static event Action OfficeStateReloaded; // 38일차: 하루 마감·복구로 사무소 상태가 바뀜 (협동 참가자 전체 목록 다시 받기)
        public int CurrentDay => currentDay; // UI·다음 날 시스템용 현재 일차 공개
        public bool IsInitialized => initialized; // 저장 시스템 준비 완료 여부 공개
        public bool IsRestoreInProgress => restoreInProgress; // 복구 진행 여부 공개
        public bool IsDayCompletionInProgress => dayCompletionInProgress; // 일차 마감 처리 여부 공개
        public ExpeditionDayPhase DayPhase => dayPhase; // 오늘 원정 진행 단계 공개
        public bool CanDepartToday => initialized && !restoreInProgress && !dayCompletionInProgress && dayPhase == ExpeditionDayPhase.OfficePrep; // 오늘 원정 출발 가능 여부 공개
        public int CampaignSeed => EnsureCampaignSeed(); // 캠페인 시드 공개 (같은 캠페인·일차 → 같은 던전)

        private void Awake() // Persistent 저장 서비스 초기화
        {
            if (instance != null && instance != this) // 중복 서비스 존재 여부 확인
            {
                Destroy(this); // 뒤늦게 생성된 중복 컴포넌트 제거
                return; // 중복 초기화 중단
            }

            instance = this; // 현재 서비스를 단일 인스턴스로 등록
            currentDay = Mathf.Max(1, currentDay); // 일차 최소값 보정
            officeAutosaveInterval = Mathf.Max(0.5f, officeAutosaveInterval); // 과도한 디스크 쓰기 방지 최소 간격 설정
            store = new DailySnapshotStore(); // 로컬 저장소 생성
            nextOfficeAutosaveTime = Time.unscaledTime + officeAutosaveInterval; // 첫 자동 저장 시점 예약
        }

        private IEnumerator Start() // Additive 초기 Office 준비 후 안전 데이터 적용
        {
            if (!initializeOnStart) // 자동 초기화 비활성 여부 확인
            {
                initialized = true; // 외부에서 직접 사용할 수 있도록 준비 상태만 설정
                yield break; // 자동 복구 수행 안 함
            }

            yield return WaitForMapLoaderReady(); // 초기 Office 준비 완료 대기

            if (ProjectI.Net.NetworkSession.IsGuest) // 37일차 참가자: 자기 저장을 읽거나 쓰지 않고 방장 기록을 따름
            {
                CaptureRuntimeOfficeState(); // 현재 사무소 경제 상태 보관
                initialized = true; // 준비 완료
                Debug.Log("[Project I] 협동 참가 — 방장의 일차 기록을 따릅니다 (저장 안 함)", this); // 안내
                yield break; // 디스크 복구 생략
            }

            yield return InitializeFromDisk(); // Current 또는 이전 정상 완료 일차에서 Office 복구
            nextOfficeAutosaveTime = Time.unscaledTime + officeAutosaveInterval; // 복구 직후 자동 저장 타이머 재설정
        }

        private void Update() // 사무소 체류 중 안전 체크포인트 주기 저장
        {
            if (!initialized || restoreInProgress || dayCompletionInProgress) // 저장 가능한 런타임 상태인지 확인
            {
                return; // 초기화·복구·일차 확정 중 자동 저장 차단
            }

            if (Time.unscaledTime < nextOfficeAutosaveTime) // 다음 저장 시각 도달 여부 확인
            {
                return; // 아직 저장 간격이 남음
            }

            nextOfficeAutosaveTime = Time.unscaledTime + officeAutosaveInterval; // 다음 자동 저장 시각 선예약

            if (IsSafeOfficeRuntime()) // 현재 실제 안전 구역이 Office인지 확인
            {
                SaveSafeOfficeCheckpoint(false); // 사무소 현재 상태를 조용히 Current에 갱신
            }
        }

        private void OnApplicationPause(bool pauseStatus) // 앱 일시정지 직전 Office 상태 보존
        {
            if (pauseStatus) // 실제 일시정지 진입인지 확인
            {
                SaveSafeOfficeCheckpoint(false); // Office일 때만 마지막 안전 상태 저장
            }
        }

        private void OnApplicationQuit() // 정상 종료 직전 Office 상태 보존
        {
            SaveSafeOfficeCheckpoint(false); // Dungeon에서는 저장하지 않고 Office에서만 Current 갱신
        }

        private void OnDestroy() // 서비스 제거 시 단일 참조 정리
        {
            if (instance == this) // 현재 등록 인스턴스인지 확인
            {
                instance = null; // 전역 참조 해제
            }
        }

        private void OnValidate() // Inspector 자동 저장 간격 보정
        {
            currentDay = Mathf.Max(1, currentDay); // 일차 최소값 보장
            officeAutosaveInterval = Mathf.Max(0.5f, officeAutosaveInterval); // 자동 저장 최소 간격 보장
        }

        public void SetRemoteStore(IRemoteDailySnapshotStore targetRemoteStore) // 향후 서버 백업 구현 연결
        {
            remoteStore = targetRemoteStore; // 실제 백엔드 구현체 저장
        }

        public void CaptureRuntimeOfficeState() // Office 맵 언로드 직전 경제 상태를 Persistent 메모리에 보관
        {
            EconomySnapshotData latest = new EconomySnapshotData(); // 새 경제 캡처 버퍼 생성

            if (Day23SnapshotBridge.CaptureEconomy(latest)) // 현재 Office 경제 오브젝트가 존재하는지 확인
            {
                runtimeEconomy = latest; // 최신 공동 자금·채무 상태 보존
            }
        }

        public void RestoreRuntimeOfficeState() // Office 맵이 다시 로드된 뒤 Persistent 경제 상태 재적용
        {
            if (runtimeEconomy == null || !runtimeEconomy.hasData) // 저장된 경제 상태 존재 여부 확인
            {
                CaptureRuntimeOfficeState(); // 첫 Office 로드라면 현재 씬 값을 초기 런타임 상태로 캡처
                return; // 별도 덮어쓰기 없음
            }

            Day23SnapshotBridge.RestoreEconomy(runtimeEconomy); // 기존 Office 경제 컴포넌트에 메모리 상태 복원
        }

        public bool SaveSafeOfficeCheckpoint() // 던전 출발 직전 또는 명시 저장용 안전 체크포인트 API
        {
            return SaveSafeOfficeCheckpoint(true); // 실패 원인을 로그로 남기며 저장 시도
        }

        public bool SaveCurrentDayStart() // 기존 호출 호환용 현재 일차 시작 저장 API
        {
            return SaveSafeOfficeCheckpoint(); // 새 규칙에서는 사무소 안전 체크포인트와 동일하게 처리
        }

        private int EnsureCampaignSeed() // 캠페인 시드가 없으면 새로 생성
        {
            if (campaignSeed == 0) // 새 게임 또는 이전 버전 저장 확인
            {
                campaignSeed = (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) | 1; // 0이 아닌 양수 시드
            }

            return campaignSeed; // 시드 반환
        }

        public void MarkExpeditionDeparted() // 마차가 던전에 도착해 오늘 원정이 시작됐음을 기록
        {
            dayPhase = ExpeditionDayPhase.OnExpedition; // 런타임 전용 원정 단계 (던전에서는 저장하지 않음)
        }

        public void MarkExpeditionReturned() // 마차가 사무소로 귀환했음을 기록
        {
            dayPhase = ExpeditionDayPhase.Returned; // 일차 마감 전까지 재출발 금지 단계
        }

        public void ApplyGuestState(int day, ExpeditionDayPhase phase, int seed) // 37일차 참가자: 방장의 일차·단계·시드 적용
        {
            if (!ProjectI.Net.NetworkSession.IsGuest) // 참가자만
            {
                return; // 생략
            }

            currentDay = Mathf.Max(1, day); // 일차
            dayPhase = phase; // 단계

            if (seed != 0) // 방장 시드
            {
                campaignSeed = seed; // 같은 던전
            }
        }

        public void CompleteCurrentDay() // 사무소에서 현재 일차를 완료 일차로 확정
        {
            if (ProjectI.Net.NetworkSession.IsGuest) // 참가자
            {
                ProjectI.UI.GameHud.ShowNotice("하루 마감은 방장만 할 수 있습니다", 3f); // 안내
                return; // 거부
            }

            if (!initialized || restoreInProgress || dayCompletionInProgress) // 저장 서비스 준비와 중복 실행 여부 확인
            {
                Debug.LogWarning("[Project I] 일차 Snapshot 저장 불가 / 초기화·복구·일차 확정 진행 중", this); // 호출 시점 안내
                return; // 중복 저장 차단
            }

            dayCompletionInProgress = true; // 일차 완료 절차 중 자동 저장 잠금
            StartCoroutine(CompleteCurrentDayRoutine()); // 불변 완료 백업과 다음 일차 Current 생성 시작
        }

        public void RestoreMostRecentDailySnapshot() // 사용자가 가장 최근 정상 완료 일차로 직접 롤백
        {
            if (ProjectI.Net.NetworkSession.IsGuest) // 참가자는 기록을 바꾸지 않음
            {
                return; // 거부
            }

            if (restoreInProgress) // 이미 복구 중인지 확인
            {
                return; // 중복 롤백 차단
            }

            if (store == null) // 저장소 초기화 여부 확인
            {
                store = new DailySnapshotStore(); // 늦은 호출에도 저장소 생성
            }

            if (!store.TryReadLatestValidDailySnapshot(out DailySnapshotData snapshot, out string path)) // 최신 정상 Office 완료 일차 조회
            {
                Debug.LogError("[Project I] 복구 가능한 정상 이전 일차 Snapshot이 없습니다.", this); // 백업 없음 로그 출력
                return; // 복구 중단
            }

            snapshot.currentDay = snapshot.completedDay + 1; // 완료 일차 다음 날을 다시 시작하도록 계산
            snapshot.activeDestination = "Office"; // 진행 중 던전은 없었던 것으로 처리하고 Office로 강제 복구
            snapshot.dayPhase = ExpeditionDayPhase.OfficePrep; // 다음 날 준비 단계에서 다시 시작
            Debug.LogWarning($"[Project I] 이전 일차 롤백 시작 / Source={path} / RestartDay={snapshot.currentDay} / Start=Office", this); // 복구 출처 안내
            StartCoroutine(RestoreSnapshotRoutine(snapshot, true, null)); // Office 전체 상태 복구 후 Current 재생성
        }

        private bool SaveSafeOfficeCheckpoint(bool logFailure) // 현재 Office 상태를 Current로 갱신하는 공통 구현
        {
            if (ProjectI.Net.NetworkSession.IsGuest) // 37일차 참가자: 저장하지 않음 (출발 절차는 계속 진행)
            {
                return initialized; // 준비됐으면 성공으로 처리
            }

            if (!initialized || restoreInProgress || dayCompletionInProgress) // 안전하게 캡처할 수 있는 상태인지 확인
            {
                if (logFailure) // 명시 저장 호출인지 확인
                {
                    Debug.LogWarning("[Project I] 사무소 체크포인트 저장 대기 / 저장 시스템이 아직 준비되지 않았습니다.", this); // 저장 거부 이유 안내
                }

                return false; // 저장 불가 반환
            }

            if (!IsSafeOfficeRuntime()) // 현재 환경이 Office인지 확인
            {
                return false; // Dungeon 진행 중에는 Current를 절대 갱신하지 않음
            }

            int lastCompletedDay = Mathf.Max(0, currentDay - 1); // 현재 원정 일차 직전 완료 일차 계산

            if (!TryCaptureSnapshot(currentDay, lastCompletedDay, out DailySnapshotData snapshot)) // 현재 Office 전체 상태 캡처
            {
                if (logFailure) // 명시 저장 호출인지 확인
                {
                    Debug.LogError("[Project I] 사무소 안전 체크포인트 캡처 실패 / 복구 정의 누락 여부를 확인하세요.", this); // 실패 안내
                }

                return false; // 불완전 상태 저장 차단
            }

            snapshot.activeDestination = "Office"; // Current는 항상 Office 복구용 데이터로 고정
            bool written = store.WriteCurrent(snapshot); // SHA-256 Current 안전 교체 저장

            if (!written && logFailure) // 디스크 쓰기 실패 여부 확인
            {
                Debug.LogError("[Project I] 사무소 안전 체크포인트 파일 저장 실패 / 던전 출발을 진행하지 않는 것이 안전합니다.", this); // 실패 안내
            }

            return written; // 저장 성공 여부 반환
        }

        private IEnumerator InitializeFromDisk() // 게임 시작 시 안전 Current 또는 이전 완료 일차로 Office 복구
        {
            if (store == null) // 저장소 누락 확인
            {
                store = new DailySnapshotStore(); // 기본 로컬 저장소 생성
            }

            Scene office = SceneManager.GetSceneByName("01_Office"); // 안전 시작점 Office 로드 상태 조회

            if (!office.IsValid() || !office.isLoaded) // 초기 Office 준비 실패 여부 확인
            {
                Debug.LogError("[Project I] 01_Office가 로드되지 않아 저장·복구 초기화를 중단합니다 / 기존 저장 파일은 변경하지 않습니다. Build Settings(Build Profiles 씬 목록)를 확인하세요.", this); // 실제 원인 안내
                yield break; // initialized를 올리지 않아 자동 저장·출발 저장을 차단
            }

            bool currentExists = store.CurrentFileExists(); // Current 파일 실제 존재 여부 확인
            bool currentReadable = store.TryReadCurrent(out DailySnapshotData current, out string currentReason); // Current 무결성 검사

            if (currentReadable && DailySnapshotStore.IsOfficeSnapshot(current)) // 정상 사무소 안전 체크포인트인지 확인
            {
                current.activeDestination = "Office"; // 이전 버전 필드 변형 방지
                currentDay = Mathf.Max(1, current.currentDay); // 저장된 현재 일차 복구
                bool restored = false; // 복구 성공 상태 초기화
                yield return RestoreSnapshotRoutine(current, false, success => restored = success); // Office 상태 적용

                if (restored) // 전체 복구 성공 여부 확인
                {
                    initialized = true; // 초기 복구 완료 표시
                    yield break; // 이전 일차 폴백 불필요
                }

                currentReason = "Current Office 복구 실패"; // 다음 폴백 로그용 이유 갱신
            }
            else if (currentReadable) // 파일은 정상이지만 Dungeon 진행 상태 등 안전 Current가 아닌 경우
            {
                currentReason = "Current가 Office 안전 체크포인트가 아님"; // 던전 중간 데이터 사용 금지 이유 기록
            }

            if (store.TryReadLatestValidDailySnapshot(out DailySnapshotData fallback, out string fallbackPath)) // Current 누락·손상 시 가장 최근 Office 완료 일차 검색
            {
                fallback.currentDay = fallback.completedDay + 1; // 이전 완료 일차 다음 날부터 다시 시작
                fallback.activeDestination = "Office"; // 진행 중 원정은 없었던 것으로 처리
                fallback.dayPhase = ExpeditionDayPhase.OfficePrep; // 다음 날 준비 단계에서 다시 시작
                Debug.LogWarning($"[Project I] Current 데이터 {(currentExists ? "사용 불가" : "누락")} / 이유={currentReason} / 이전 완료 일차 복구={fallbackPath}", this); // 자동 폴백 이유 출력
                bool restored = false; // 폴백 복구 결과 초기화
                yield return RestoreSnapshotRoutine(fallback, true, success => restored = success); // 이전 완료 상태를 Office에 적용하고 새 Current 생성

                if (restored) // 폴백 복구 성공 여부 확인
                {
                    initialized = true; // 초기 복구 완료 표시
                    yield break; // 새 게임 초기화 불필요
                }
            }

            currentDay = Mathf.Max(1, currentDay); // 최초 새 게임 일차 보정
            CaptureRuntimeOfficeState(); // 현재 Office 초기 경제 상태 메모리 보관
            initialized = true; // 최초 Current 생성을 위해 준비 상태 선반영

            if (!SaveSafeOfficeCheckpoint()) // 최초 Day 1 Office 안전 상태 Current 저장 시도
            {
                Debug.LogWarning("[Project I] 최초 Office Current Snapshot 생성 실패 / ItemDefinition 누락 여부를 확인하세요.", this); // 초기 백업 실패 안내
            }
        }

        private IEnumerator CompleteCurrentDayRoutine() // 완료 일차 불변 Office Snapshot과 다음 일차 Current 생성
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 여행 로더 조회

            if (loader == null || loader.IsTransitioning || loader.CurrentDestination != TravelDestination.Office) // 안전한 Office 상태인지 확인
            {
                Debug.LogWarning("[Project I] 일차 완료는 마차 이동이 끝난 사무소에서만 가능합니다.", this); // 안전 규칙 안내
                dayCompletionInProgress = false; // 일차 완료 잠금 해제
                yield break; // Dungeon 또는 전환 중 저장 차단
            }

            if (dayPhase != ExpeditionDayPhase.Returned) // 오늘 원정을 다녀왔는지 확인
            {
                Debug.LogWarning($"[Project I] {currentDay}일차 마감 불가 / 오늘 원정을 다녀온 뒤 마감할 수 있습니다.", this); // 하루 1회 원정 규칙 안내
                dayCompletionInProgress = false; // 일차 완료 잠금 해제
                yield break; // 원정 없이 날짜 넘기기 차단
            }

            int completedDay = currentDay; // 이번에 확정할 일차 번호 저장
            DailySnapshotData completedSnapshot = null; // 완료 상태 데이터 초기화

            if (store.TryReadDailySnapshot(completedDay, out DailySnapshotData existing, out _) && DailySnapshotStore.IsOfficeSnapshot(existing)) // 이전 저장 시도에서 완료 백업이 이미 만들어졌는지 확인
            {
                completedSnapshot = existing; // 정상 기존 완료 백업을 그대로 재사용
            }
            else // 아직 정상 완료 백업이 없는 경우
            {
                CaptureRuntimeOfficeState(); // 최신 Office 경제 상태 갱신

                if (!TryCaptureSnapshot(completedDay, completedDay, out completedSnapshot)) // 현재 Office 전체 상태를 완료 일차로 캡처
                {
                    Debug.LogError($"[Project I] Day {completedDay} Snapshot 생성 실패 / 복구 정의가 없는 아이템이 존재합니다.", this); // 불완전 백업 생성 차단
                    dayCompletionInProgress = false; // 일차 완료 잠금 해제
                    yield break; // 기존 정상 상태 유지
                }

                completedSnapshot.activeDestination = "Office"; // 완료 백업은 반드시 사무소 상태로 고정

                if (!store.WriteImmutableDailySnapshot(completedSnapshot)) // 완료 일차 불변 백업 최초 기록
                {
                    Debug.LogError($"[Project I] Day {completedDay} 완료 Snapshot 저장 실패 / 다음 일차로 넘어가지 않습니다.", this); // 저장 실패 안내
                    dayCompletionInProgress = false; // 일차 완료 잠금 해제
                    yield break; // currentDay 변경 금지
                }
            }

            DailySnapshotData nextDayStart = CloneSnapshot(completedSnapshot); // 완료 상태를 다음 일차 시작용 Current로 복제
            nextDayStart.currentDay = completedDay + 1; // 다음 원정 일차 번호 설정
            nextDayStart.completedDay = completedDay; // 직전 정상 완료 일차 기록
            nextDayStart.activeDestination = "Office"; // 다음 일차도 사무소에서 시작하도록 고정
            nextDayStart.dayPhase = ExpeditionDayPhase.OfficePrep; // 다음 일차는 준비 단계에서 시작

            if (!store.WriteCurrent(nextDayStart)) // 다음 일차 시작 Current 저장 성공 여부 확인
            {
                Debug.LogError($"[Project I] Day {completedDay + 1} 사무소 시작 데이터 저장 실패 / 현재 일차를 유지합니다.", this); // 문제 1 방지 로그
                dayCompletionInProgress = false; // 재시도 가능하도록 잠금 해제
                yield break; // currentDay를 올리지 않아 같은 완료 절차를 다시 시도 가능
            }

            currentDay = nextDayStart.currentDay; // Current 저장까지 성공한 뒤에만 실제 일차 증가
            dayPhase = ExpeditionDayPhase.OfficePrep; // 새 일차 원정 준비 단계 시작
            runtimeEconomy = CloneEconomy(nextDayStart.economy); // 다음 일차 사무소 경제 상태 동기화
            string envelopeText = store.ReadEnvelopeTextForRemote(completedDay); // 향후 서버 업로드에 사용할 완료 백업 원문 조회

            if (remoteStore != null && !string.IsNullOrWhiteSpace(envelopeText)) // 실제 서버 구현 연결 여부 확인
            {
                remoteStore.Upload($"Day_{completedDay:000}", envelopeText); // 동일 완료 Snapshot 서버 백업 요청
            }

            nextOfficeAutosaveTime = Time.unscaledTime + officeAutosaveInterval; // 다음 Office 자동 저장 시각 재설정
            dayCompletionInProgress = false; // 일차 완료 절차 종료
            OfficeStateReloaded?.Invoke(); // 협동 알림
            Debug.Log($"[Project I] 일차 완료 확정 / CompletedDay={completedDay} / RestartSafePoint=Office / NextDay={currentDay}", this); // 완료 결과 로그
            yield return null; // Coroutine 정상 종료 프레임 반환
        }

        private bool TryCaptureSnapshot(int targetCurrentDay, int completedDay, out DailySnapshotData snapshot) // 현재 로드 상태를 하나의 Snapshot으로 캡처
        {
            snapshot = new DailySnapshotData // 새 스냅샷 루트 생성
            {
                currentDay = Mathf.Max(1, targetCurrentDay), // 시작 일차 기록
                completedDay = Mathf.Max(0, completedDay), // 직전 또는 이번 완료 일차 기록
                activeDestination = GetCurrentDestinationName(), // 현재 환경 목적지 기록
                dayPhase = dayPhase == ExpeditionDayPhase.OnExpedition ? ExpeditionDayPhase.OfficePrep : dayPhase, // 원정 중 단계는 저장하지 않음
                campaignSeed = EnsureCampaignSeed(), // 캠페인 시드 기록
                economy = CloneEconomy(runtimeEconomy) // Office가 없어도 Persistent 경제 메모리 복사
            };

            CaptureRuntimeOfficeState(); // Office가 현재 로드됐으면 가장 최신 경제 상태로 갱신
            snapshot.economy = CloneEconomy(runtimeEconomy); // 갱신된 경제 상태 최종 저장
            HashSet<WorldItem> captured = new HashSet<WorldItem>(); // 위치별 중복 캡처 방지 집합
            snapshot.selectedQuickSlot = Day23SnapshotBridge.CaptureInventory(snapshot.items, captured); // 빠른 슬롯 아이템과 선택 위치 저장
            Day23SnapshotBridge.CaptureOfficeStorage(snapshot.items, captured); // 사무소 영구 보관 단상 저장
            WagonCargoArea cargoArea = FindFirst<WagonCargoArea>(); // Persistent Wagon CargoArea 조회
            Transform wagonRoot = cargoArea == null ? null : cargoArea.transform.root; // Wagon 로컬 좌표 기준 확보
            WorldItem[] worldItems = UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로드된 모든 WorldItem 조회
            bool complete = true; // 모든 아이템에 복구 Definition이 있는지 추적

            foreach (WorldItem item in worldItems) // 남은 월드·Cargo 아이템 순회
            {
                if (item == null || captured.Contains(item)) // 중복 또는 파괴 대상 확인
                {
                    continue; // 이미 위치별 캡처된 아이템 제외
                }

                RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 판매 상태 조회

                if (recoverable != null && recoverable.IsSold) // 정상 판매 완료 아이템인지 확인
                {
                    continue; // 판매된 물건은 복구 대상에서 제외
                }

                ItemInstanceData data = Day23SnapshotBridge.CreateBaseItemData(item); // 공통 개별 데이터 캡처

                if (string.IsNullOrWhiteSpace(data.itemId) || ItemRegistry.Find(data.itemId) == null) // 복구 가능한 ItemDefinition 존재 여부 확인
                {
                    Debug.LogError($"[Project I] Snapshot 캡처 거부 / ItemDefinition 누락 / Item={item.DisplayName} / Object={item.name}", item); // 불완전 백업 원인 출력
                    complete = false; // 전체 Snapshot 저장 금지 표시
                    continue; // 복구 불가능한 레코드는 파일에 넣지 않음
                }

                if (cargoArea != null && wagonRoot != null && cargoArea.IsSecured(item)) // 실제 마차 짐칸에 확보된 아이템인지 확인
                {
                    data.location = SnapshotItemLocation.WagonCargo; // Persistent Cargo 위치 기록
                    data.position = wagonRoot.InverseTransformPoint(item.transform.position); // Wagon 기준 로컬 위치 기록
                    data.rotation = Quaternion.Inverse(wagonRoot.rotation) * item.transform.rotation; // Wagon 기준 로컬 회전 기록
                }
                else // 일반 환경 월드 아이템 처리
                {
                    data.location = SnapshotItemLocation.World; // 환경 바닥 위치 기록
                    data.sceneName = item.gameObject.scene.name; // 원래 환경 씬 이름 기록
                    data.position = item.transform.position; // 월드 위치 기록
                    data.rotation = item.transform.rotation; // 월드 회전 기록
                }

                snapshot.items.Add(data); // 개별 아이템 데이터 추가
                captured.Add(item); // 중복 캡처 방지 등록
            }

            foreach (ItemInstanceData data in snapshot.items) // 인벤토리·단상 포함 전체 저장 항목 검증
            {
                if (data == null || string.IsNullOrWhiteSpace(data.itemId) || ItemRegistry.Find(data.itemId) == null) // 복구 정의 누락 여부 확인
                {
                    complete = false; // 불완전 Snapshot으로 판정
                }
            }

            return complete; // 모든 실제 아이템을 복구 가능한 경우에만 저장 허용
        }

        private IEnumerator RestoreSnapshotRoutine(DailySnapshotData snapshot, bool writeCurrentAfterRestore, Action<bool> onCompleted) // 안전 Snapshot을 Office에 복원
        {
            if (snapshot == null || restoreInProgress) // 복구 데이터와 중복 실행 확인
            {
                onCompleted?.Invoke(false); // 호출자에게 실패 전달
                yield break; // 복구 중단
            }

            restoreInProgress = true; // 다른 저장·복구 요청 잠금
            snapshot.activeDestination = "Office"; // 진행 중 Dungeon은 없었던 것으로 처리
            PersistentMapLoader loader = PersistentMapLoader.Instance; // Persistent 여행 로더 조회

            if (loader == null) // 환경 준비 담당 로더 존재 여부 확인
            {
                Debug.LogError("[Project I] Snapshot 복구 취소 / PersistentMapLoader 누락 / 기존 아이템은 유지합니다.", this); // 안전 취소 로그
                restoreInProgress = false; // 복구 잠금 해제
                onCompleted?.Invoke(false); // 실패 전달
                yield break; // 기존 아이템 삭제 금지
            }

            while (loader.IsTransitioning) // 현재 이동이 끝날 때까지 대기
            {
                yield return null; // 다음 프레임 재확인
            }

            bool mapReady = false; // Office 복구 환경 준비 결과 초기화
            yield return loader.LoadDestinationForRecovery(TravelDestination.Office, success => mapReady = success); // Office를 먼저 완전히 준비

            if (!mapReady) // Office 씬 또는 정차 지점 준비 실패 여부 확인
            {
                Debug.LogError("[Project I] Snapshot 복구 취소 / Office 준비 실패 / 기존 아이템은 삭제하지 않았습니다.", this); // 문제 2 방지 로그
                restoreInProgress = false; // 복구 잠금 해제
                onCompleted?.Invoke(false); // 실패 전달
                yield break; // 파괴적 복구 단계 진입 금지
            }

            Scene environmentScene = SceneManager.GetSceneByName("01_Office"); // 복구 대상 Office 환경 씬 조회

            if (!environmentScene.IsValid() || !environmentScene.isLoaded) // 콜백 이후에도 Office가 유효한지 최종 확인
            {
                Debug.LogError("[Project I] Snapshot 복구 취소 / Office 씬 최종 검증 실패 / 기존 아이템 유지", this); // 안전 중단 로그
                restoreInProgress = false; // 복구 잠금 해제
                onCompleted?.Invoke(false); // 실패 전달
                yield break; // 기존 아이템 삭제 금지
            }

            runtimeEconomy = CloneEconomy(snapshot.economy); // 저장된 경제 상태를 Persistent 메모리에 먼저 복원
            loader.OfficeItemKeeper?.DiscardStash(); // Snapshot이 사무소 상태를 대체하므로 이동 중 보관 상태 폐기
            Day23SnapshotBridge.ClearInventoryForRestore(); // 빠른 슬롯의 기존 참조 제거
            Day23SnapshotBridge.ClearOfficeStorageForRestore(); // 단상 기존 참조 제거
            WorldItem[] oldItems = UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 실제 WorldItem 전체 조회

            foreach (WorldItem oldItem in oldItems) // 기존 런타임 아이템 순회
            {
                if (oldItem != null) // 유효 아이템 확인
                {
                    Destroy(oldItem.gameObject); // Office 준비 성공 이후에만 기존 상태 제거
                }
            }

            yield return null; // Destroy 예약 반영 후 새 아이템 생성
            ItemRegistry.Reload(); // 최신 ItemDefinition 캐시 갱신
            Scene persistentScene = gameObject.scene; // 저장 서비스가 속한 00_WagonPersistent 씬 조회
            WagonCargoArea cargoArea = FindFirst<WagonCargoArea>(); // 현재 Persistent Wagon CargoArea 조회
            Transform wagonRoot = cargoArea == null ? null : cargoArea.transform.root; // Cargo 로컬 좌표 기준 확보
            int failedItems = 0; // 복구 실패 개수 집계

            foreach (ItemInstanceData data in snapshot.items) // 저장된 모든 개별 아이템 순회
            {
                if (data == null || data.isSold) // 잘못된 레코드 또는 정상 판매 완료 상태 확인
                {
                    continue; // 실제 물건을 생성하지 않음
                }

                WorldItem item = ItemFactory.SpawnForRecovery(data); // 복구 전용 Factory로 실제 아이템 생성

                if (item == null) // Prefab 또는 Definition 복구 실패 확인
                {
                    failedItems++; // 실패 집계 증가
                    continue; // 다음 아이템 복구 시도
                }

                GameObject itemObject = item.gameObject; // 씬 이동용 루트 GameObject 참조

                switch (data.location) // 저장된 위치 종류에 따라 동일 상태 복원
                {
                    case SnapshotItemLocation.PlayerInventory: // 플레이어 빠른 슬롯 복구
                        MoveRootToScene(itemObject, persistentScene); // 플레이어와 같은 Persistent 씬으로 이동

                        if (!Day23SnapshotBridge.RestoreInventoryItem(item, data.slotIndex)) // 정확한 슬롯 복구 시도
                        {
                            failedItems++; // 슬롯 복구 실패 기록
                            Destroy(itemObject); // 떠도는 중복 아이템 제거
                        }
                        break; // 인벤토리 복구 종료
                    case SnapshotItemLocation.OfficeStorage: // 사무소 단상 복구
                        MoveRootToScene(itemObject, environmentScene); // Office 환경 씬으로 이동

                        if (!Day23SnapshotBridge.RestoreOfficeStorageItem(item, data.storageKey)) // 저장 당시 단상 복구 시도
                        {
                            failedItems++; // 단상 복구 실패 기록
                            Destroy(itemObject); // 잘못 배치될 아이템 제거
                        }
                        break; // 단상 복구 종료
                    case SnapshotItemLocation.WagonCargo: // 마차 짐칸 복구
                        MoveRootToScene(itemObject, persistentScene); // Cargo를 Persistent 씬 소속으로 유지

                        if (wagonRoot == null) // Wagon 기준 존재 여부 확인
                        {
                            failedItems++; // Cargo 위치 복구 실패 기록
                            Destroy(itemObject); // 기준 없는 아이템 제거
                            break; // Cargo 복구 종료
                        }

                        PlaceRestoredItem(item, wagonRoot.TransformPoint(data.position), wagonRoot.rotation * data.rotation); // Wagon 로컬 위치·회전 복원
                        break; // Cargo 복구 종료
                    default: // 일반 Office WorldItem 복구
                        MoveRootToScene(itemObject, environmentScene); // 안전 체크포인트는 항상 Office 환경에 배치
                        PlaceRestoredItem(item, data.position, data.rotation); // 저장 당시 Office 월드 위치·회전 복구
                        break; // World 복구 종료
                }
            }

            Physics.SyncTransforms(); // 복구 위치를 물리 엔진에 즉시 반영
            yield return null; // Trigger와 인벤토리 상태가 생성 아이템을 인식할 프레임 제공
            Day23SnapshotBridge.FinalizeInventorySelection(snapshot.selectedQuickSlot); // 저장 당시 선택 슬롯 화면 복구
            RestoreRuntimeOfficeState(); // 공동 자금·채무 상태 복구
            currentDay = Mathf.Max(1, snapshot.currentDay); // 저장된 다음 원정 일차 적용
            dayPhase = snapshot.dayPhase == ExpeditionDayPhase.OnExpedition ? ExpeditionDayPhase.OfficePrep : snapshot.dayPhase; // 저장된 원정 단계 적용 (원정 중은 준비 단계로 복구)
            campaignSeed = snapshot.campaignSeed; // 저장된 캠페인 시드 적용 (0이면 다음 조회 때 생성)
            snapshot.campaignSeed = EnsureCampaignSeed(); // 이전 버전 파일도 시드를 가진 상태로 저장
            bool currentWritten = true; // Current 갱신 결과 기본값 설정

            if (failedItems == 0 && writeCurrentAfterRestore) // 전체 아이템 복구 성공 시 새 정상 Current 작성 여부 확인
            {
                currentWritten = store.WriteCurrent(snapshot); // 복구된 Office 시작점을 새 Current로 저장
            }

            bool success = failedItems == 0 && currentWritten; // 전체 복구 성공 조건 계산

            if (!success) // 일부 아이템 또는 Current 저장 실패 여부 확인
            {
                Debug.LogError($"[Project I] Snapshot 복구 부분 실패 / 실패 아이템={failedItems} / CurrentWritten={currentWritten}", this); // 실패 진단
            }
            else // 전체 복구 성공
            {
                Debug.Log($"[Project I] Snapshot 전체 복구 완료 / Day={currentDay} / Start=Office / Items={snapshot.items.Count}", this); // 정상 복구 결과 출력
            }

            restoreInProgress = false; // 저장·복구 잠금 해제
            nextOfficeAutosaveTime = Time.unscaledTime + officeAutosaveInterval; // 복구 직후 자동 저장 타이머 재설정
            OfficeStateReloaded?.Invoke(); // 협동 알림
            onCompleted?.Invoke(success); // 초기화 호출자에게 최종 결과 전달
        }

        private IEnumerator WaitForMapLoaderReady() // PersistentMapLoader의 초기 환경 준비 대기
        {
            float timeout = 60f; // 느린 디스크에서도 초기 Office 로드를 기다리되 무한 대기는 방지
            float elapsed = 0f; // 경과 시간 초기화

            while (elapsed < timeout) // 제한 시간 동안 준비 상태 검사
            {
                PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 맵 로더 조회

                if (loader != null && !loader.IsTransitioning) // 로더 존재 여부 확인
                {
                    Scene office = SceneManager.GetSceneByName("01_Office"); // 안전 시작점인 Office 씬 조회

                    if (office.IsValid() && office.isLoaded) // Office가 준비됐는지 확인
                    {
                        yield break; // 저장·복구 시작 가능
                    }
                }

                elapsed += Time.unscaledDeltaTime; // 대기 시간 누적
                yield return null; // 다음 프레임 재확인
            }

            Debug.LogWarning("[Project I] Snapshot 초기화 대기 시간 초과 / Office 상태를 다시 확인합니다.", this); // 초기 맵 누락 진단
        }

        private bool IsSafeOfficeRuntime() // 현재 런타임이 안전 저장 가능한 Office 상태인지 확인
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 환경 로더 조회

            if (loader == null || loader.IsTransitioning || loader.CurrentDestination != TravelDestination.Office) // Office 정차 상태 여부 확인
            {
                return false; // Dungeon·전환 중에는 저장 금지
            }

            Scene office = SceneManager.GetSceneByName("01_Office"); // 실제 Office 씬 조회
            return office.IsValid() && office.isLoaded; // Office가 실제 로드된 경우만 안전 구역 인정
        }

        private string GetCurrentDestinationName() // 현재 Additive 환경 목적지를 저장 문자열로 변환
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 맵 로더 조회
            return loader != null && loader.CurrentDestination == TravelDestination.TestDungeon ? "TestDungeon" : "Office"; // 안정 문자열 반환
        }

        private static DailySnapshotData CloneSnapshot(DailySnapshotData source) // 완료 백업을 다음 일차 Current로 깊은 복사
        {
            if (source == null) // 원본 누락 확인
            {
                return new DailySnapshotData(); // 빈 안전 데이터 반환
            }

            string json = JsonUtility.ToJson(source, false); // 직렬화 가능한 전체 Snapshot을 JSON으로 복사
            DailySnapshotData clone = JsonUtility.FromJson<DailySnapshotData>(json); // 별도 인스턴스로 역직렬화
            clone.items ??= new List<ItemInstanceData>(); // 아이템 목록 누락 안전 보정
            clone.economy ??= new EconomySnapshotData(); // 경제 상태 누락 안전 보정
            return clone; // 독립 복사본 반환
        }

        private static EconomySnapshotData CloneEconomy(EconomySnapshotData source) // 런타임 경제 메모리와 파일 데이터를 분리 복사
        {
            if (source == null) // 원본 누락 확인
            {
                return new EconomySnapshotData(); // 빈 경제 데이터 반환
            }

            return new EconomySnapshotData // 값 복사본 생성
            {
                hasData = source.hasData, // 경제 데이터 존재 여부 복사
                sharedFunds = source.sharedFunds, // 공동 자금 복사
                saleMultiplier = source.saleMultiplier, // 판매 배율 복사
                debtPhaseIndex = source.debtPhaseIndex, // 채무 단계 복사
                paidInCurrentPhase = source.paidInCurrentPhase // 현재 단계 납부액 복사
            };
        }

        private static void PlaceRestoredItem(WorldItem item, Vector3 position, Quaternion rotation) // 복구 아이템의 Transform과 Rigidbody 위치를 함께 지정
        {
            item.transform.SetPositionAndRotation(position, rotation); // 표시 위치 지정
            Rigidbody body = item.Body != null ? item.Body : item.GetComponent<Rigidbody>(); // 아이템 Rigidbody 조회

            if (body == null) // Rigidbody 없는 아이템 확인
            {
                return; // Transform만으로 충분
            }

            body.position = position; // 물리 위치도 지정해야 보간(Interpolate)이 Prefab 원본 위치로 되돌리지 않음
            body.rotation = rotation; // 물리 회전 지정

            if (!body.isKinematic) // Dynamic Rigidbody 여부 확인
            {
                body.linearVelocity = Vector3.zero; // 복구 순간 속도 제거
                body.angularVelocity = Vector3.zero; // 복구 순간 회전 속도 제거
            }
        }

        private static void MoveRootToScene(GameObject target, Scene scene) // 복구 아이템 루트를 지정 씬으로 안전 이동
        {
            if (target == null || !scene.IsValid() || !scene.isLoaded) // 대상과 씬 유효성 확인
            {
                return; // 씬 이동 중단
            }

            target.transform.SetParent(null, true); // MoveGameObjectToScene 요구사항에 맞게 루트로 분리

            if (target.scene != scene) // 이미 올바른 씬인지 확인
            {
                SceneManager.MoveGameObjectToScene(target, scene); // 동일 실제 GameObject 씬 소속 변경
            }
        }

        private static T FindFirst<T>() where T : UnityEngine.Object // 비활성 포함 첫 컴포넌트 조회
        {
            T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로드된 전체 대상 조회
            return objects != null && objects.Length > 0 ? objects[0] : null; // 첫 대상 또는 null 반환
        }
    }
}
