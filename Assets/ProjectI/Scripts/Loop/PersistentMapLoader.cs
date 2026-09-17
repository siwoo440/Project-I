using System; // 복구 결과 콜백 사용
using System.Collections; // Coroutine 이동 절차 사용
using System.Collections.Generic; // 실패 상실 목록 기능 참조
using ProjectI.Economy; // 회수품 가치 참조
using ProjectI.Items; // 플레이어 운반 기능 참조
using ProjectI.Persistence; // 사무소 안전 체크포인트 저장 서비스 참조
using ProjectI.Player; // 플레이어 이동 추락 판정 초기화 참조
using ProjectI.Wagon; // 마차 CargoArea 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // Additive 씬 로드·언로드 기능 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    [DisallowMultipleComponent] // Persistent 맵 로더 중복 방지
    public sealed class PersistentMapLoader : MonoBehaviour // 마차 씬을 유지하면서 환경 씬만 교체
    {
        private const string OfficeSceneName = "01_Office"; // 사무소 환경 씬 이름
        private const string TestDungeonSceneName = "02_TestDungeon"; // 테스트 던전 환경 씬 이름
        private static PersistentMapLoader instance; // 현재 Persistent 로더 단일 참조

        [SerializeField] private Transform wagonRoot; // 이동 기준 Persistent Wagon 루트
        [SerializeField] private Transform playerRoot; // Persistent Player 루트
        [SerializeField] private CanvasGroup fadeGroup; // 암전 전환 UI
        [SerializeField] private WagonCargoPersistence cargoPersistence; // 실제 Cargo GameObject 보존 관리자
        [SerializeField] private float fadeDuration = 0.75f; // 암전 시간
        [SerializeField] private float arrivalDuration = 2.25f; // 마차 진입 이동 시간
        [SerializeField] private TravelDestination initialDestination = TravelDestination.Office; // 최초 환경 목적지
        [SerializeField] private float boardingMargin = 0.4f; // 탑승 판정 시 적재칸 박스 바깥 허용 여유
        [SerializeField] private float expeditionFailureDelay = 3f; // 전원 사망 후 실패 귀환까지 보여주는 시간
        [SerializeField] private float rideWalkSpeed = 2.6f; // 마차 진입 연출 동안 적재칸 안에서 걷는 속도
        [SerializeField] private float ridePadding = 0.55f; // 적재칸 가장자리에서 띄울 여유
        private OfficeWorldItemKeeper officeItemKeeper; // Office 언로드 동안 사무소 WorldItem 보관 관리자
        private ExpeditionReportTracker reportTracker; // 원정 출발·귀환 물건 비교 기록기
        private WagonTravelBellInteractable travelBell; // 현재 마차 이동 종
        private WagonTravelCover travelCover; // 이동 중 바깥을 가리는 마차 천막
        private bool playerAttachedToWagon; // 이동 중 Player를 마차에 붙였는지 여부
        private bool failureInProgress; // 원정 실패 귀환 진행 여부
        private float officeReviveElapsed; // 사무소 사망 후 부활 대기 시간
        private TravelDestination currentDestination; // 현재 로드된 환경 목적지
        private bool isTransitioning; // 환경 교체 진행 여부

        public static PersistentMapLoader Instance => instance; // 전역 맵 로더 공개
        public TravelDestination CurrentDestination => currentDestination; // 현재 목적지 공개
        public bool IsTransitioning => isTransitioning; // 이동 진행 상태 공개
        public WagonCargoPersistence CargoPersistence => cargoPersistence; // Cargo 보존 관리자 공개
        public OfficeWorldItemKeeper OfficeItemKeeper => officeItemKeeper; // 사무소 아이템 보관 관리자 공개
        public ExpeditionReportTracker ReportTracker => reportTracker; // 원정 결과 기록기 공개
        public WagonTravelCover TravelCover => travelCover; // 마차 천막 공개
        public bool IsPlayerAttachedToWagon => playerAttachedToWagon; // 이동 중 마차 동승 상태 공개
        public bool IsExpeditionFailing => failureInProgress; // 원정 실패 귀환 진행 여부 공개

        private void Awake() // Persistent 로더 초기화
        {
            if (instance != null && instance != this) // 중복 로더 존재 여부 확인
            {
                Destroy(gameObject); // 중복 Persistent 시스템 제거
                return; // 추가 초기화 중단
            }

            instance = this; // 현재 로더 등록
            currentDestination = initialDestination; // 초기 목적지 설정
            officeItemKeeper = GetComponent<OfficeWorldItemKeeper>(); // 기존 보관 관리자 조회

            if (officeItemKeeper == null) // 씬에 보관 관리자가 없는지 확인
            {
                officeItemKeeper = gameObject.AddComponent<OfficeWorldItemKeeper>(); // 런타임에 같은 Persistent 오브젝트로 추가
            }

            reportTracker = GetComponent<ExpeditionReportTracker>(); // 기존 원정 결과 기록기 조회

            if (reportTracker == null) // 씬에 기록기가 없는지 확인
            {
                reportTracker = gameObject.AddComponent<ExpeditionReportTracker>(); // 런타임에 같은 Persistent 오브젝트로 추가
            }

            BindPersistentReferences(); // Player/Wagon 참조 연결
            SetFadeImmediate(1f); // 최초 맵 준비 전 화면 암전
        }

        private IEnumerator Start() // 최초 Office Additive 준비
        {
            yield return BootstrapInitialMap(); // 초기 환경 로드 완료까지 대기
        }

        private void OnDestroy() // 로더 제거 정리
        {
            UnbindBell(); // 종 이벤트 해제

            if (instance == this) // 현재 전역 로더인지 확인
            {
                instance = null; // 전역 참조 제거
            }
        }

        public void Configure(Transform targetWagonRoot, Transform targetPlayerRoot, CanvasGroup targetFadeGroup) // Editor 자동 구성용 참조 지정
        {
            wagonRoot = targetWagonRoot; // Wagon 참조 저장
            playerRoot = targetPlayerRoot; // Player 참조 저장
            fadeGroup = targetFadeGroup; // Fade UI 저장
            BindPersistentReferences(); // 누락 참조 보정
        }

        public void ConfigureCargoPersistence(WagonCargoPersistence targetCargoPersistence) // Cargo 보존 관리자 지정
        {
            cargoPersistence = targetCargoPersistence; // 관리자 참조 저장
            BindPersistentReferences(); // 연관 참조 보정
        }

        public void CaptureRuntimeOfficeState() // Office 언로드 전 경제 상태 보존
        {
            DailySnapshotService.Instance?.CaptureRuntimeOfficeState(); // Snapshot 서비스에 현재 Office 상태 전달
        }

        public void RestoreRuntimeOfficeState() // Office 재로드 후 경제 상태 복원
        {
            DailySnapshotService.Instance?.RestoreRuntimeOfficeState(); // Snapshot 서비스의 런타임 Office 상태 적용
        }

        public IEnumerator LoadDestinationForRecovery(TravelDestination targetDestination, Action<bool> onCompleted) // Snapshot 복구 전에 안전한 환경 준비
        {
            if (isTransitioning) // 다른 맵 전환 진행 여부 확인
            {
                onCompleted?.Invoke(false); // 복구 준비 실패 전달
                yield break; // 중복 전환 중단
            }

            isTransitioning = true; // 복구 환경 준비 잠금
            SetFadeImmediate(1f); // 복구 중 화면 가림
            CaptureRuntimeOfficeState(); // 기존 Office 상태가 있다면 메모리 보존
            string targetSceneName = GetSceneName(targetDestination); // 목표 환경 씬 이름 계산
            Scene targetScene = SceneManager.GetSceneByName(targetSceneName); // 현재 로드 상태 조회

            if (!targetScene.IsValid() || !targetScene.isLoaded) // 목표 환경이 아직 없는지 확인
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive); // 목표 환경 Additive 로드

                if (loadOperation == null) // 로드 요청 자체 실패 여부 확인
                {
                    Debug.LogError($"[Project I] Snapshot 복구용 맵 로드 실패 / Scene={targetSceneName}", this); // 실패 원인 로그
                    isTransitioning = false; // 전환 잠금 해제
                    SetFadeImmediate(0f); // 기존 화면 복원
                    onCompleted?.Invoke(false); // 호출자에게 실패 전달
                    yield break; // 기존 아이템을 건드리기 전에 종료
                }

                yield return loadOperation; // 실제 씬 로드 완료 대기
                targetScene = SceneManager.GetSceneByName(targetSceneName); // 로드된 씬 재조회
                EnvironmentSceneGuard.SanitizeLoadedEnvironment(targetScene, gameObject.scene, this); // 새로 로드한 환경 씬에 섞인 전역 시스템 차단
            }

            MapTravelAnchor anchor = FindAnchor(targetScene, targetDestination); // 목표 마차 정차 지점 조회

            if (anchor == null || !anchor.IsConfigured || anchor.StopPoint == null) // 복구 배치 지점 유효성 확인
            {
                Debug.LogError($"[Project I] Snapshot 복구용 WagonStopPoint 누락 / Scene={targetSceneName}", this); // 구성 오류 로그
                isTransitioning = false; // 전환 잠금 해제
                SetFadeImmediate(0f); // 기존 화면 복원
                onCompleted?.Invoke(false); // 호출자에게 실패 전달
                yield break; // 기존 아이템 삭제 전에 종료
            }

            if (targetScene.IsValid() && targetScene.isLoaded) // 목표 환경 최종 유효성 확인
            {
                SceneManager.SetActiveScene(targetScene); // 이후 생성 오브젝트 기준 씬을 목표 환경으로 지정
            }

            string otherSceneName = targetDestination == TravelDestination.Office ? TestDungeonSceneName : OfficeSceneName; // 반대 환경 이름 계산
            Scene otherScene = SceneManager.GetSceneByName(otherSceneName); // 반대 환경 조회

            if (otherScene.IsValid() && otherScene.isLoaded) // 반대 환경이 남아있는지 확인
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(otherScene); // 반대 환경만 언로드

                if (unloadOperation != null) // 언로드 요청 성공 여부 확인
                {
                    yield return unloadOperation; // 정리 완료 대기
                }
            }

            TeleportPersistentGroup(anchor.StopPoint.position, anchor.StopPoint.rotation); // Persistent Wagon/Player를 안전 정차 지점으로 이동
            PlacePlayerAtSpawn(anchor); // 34일차: 시작 위치가 있으면 플레이어를 그 자리에 세움
            currentDestination = targetDestination; // 현재 환경 목적지 갱신
            RestoreRuntimeOfficeState(); // Office라면 경제 상태 복원
            BindBell(); // 이동 종 참조 재확인
            isTransitioning = false; // 복구 환경 준비 완료
            SetFadeImmediate(0f); // 화면 표시 복원
            onCompleted?.Invoke(true); // 파괴적 Snapshot 복구를 진행해도 됨을 전달
            Debug.Log($"[Project I] Snapshot 복구용 환경 준비 완료 / Map={targetSceneName}", this); // 준비 완료 로그
        }

        private IEnumerator BootstrapInitialMap() // Persistent 시작 시 최초 Office 준비
        {
            BindPersistentReferences(); // 참조 보정
            string initialSceneName = GetSceneName(initialDestination); // 최초 환경 이름 계산
            Scene initialScene = SceneManager.GetSceneByName(initialSceneName); // 로드 여부 조회

            if (!initialScene.isLoaded) // 최초 환경 미로드 확인
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(initialSceneName, LoadSceneMode.Additive); // 최초 환경 Additive 로드

                if (loadOperation == null) // 로드 요청 실패 여부 확인
                {
                    Debug.LogError($"[Project I] 초기 맵 로드 실패 / Scene={initialSceneName} / Build Settings(Build Profiles 씬 목록)에 00_WagonPersistent·01_Office·02_TestDungeon 등록 여부를 확인하세요.", this); // 실패 원인 안내
                    yield break; // 초기화 중단
                }

                yield return loadOperation; // 환경 로드 완료 대기
                initialScene = SceneManager.GetSceneByName(initialSceneName); // 로드 씬 재조회
                EnvironmentSceneGuard.SanitizeLoadedEnvironment(initialScene, gameObject.scene, this); // 새로 로드한 환경 씬에 섞인 전역 시스템 차단
            }

            if (!initialScene.IsValid() || !initialScene.isLoaded) // 최종 환경 유효성 확인
            {
                Debug.LogError($"[Project I] 초기 맵을 찾지 못했습니다 / Scene={initialSceneName}", this); // 구성 오류 로그
                yield break; // 초기화 중단
            }

            SceneManager.SetActiveScene(initialScene); // 최초 Office를 활성 환경으로 지정
            MapTravelAnchor anchor = FindAnchor(initialScene, initialDestination); // 초기 정차 지점 조회

            if (anchor != null && anchor.StopPoint != null) // 정차 지점 존재 여부 확인
            {
                TeleportPersistentGroup(anchor.StopPoint.position, anchor.StopPoint.rotation); // Wagon/Player를 정차 위치로 이동
                PlacePlayerAtSpawn(anchor); // 34일차: 시작 위치가 있으면 플레이어를 그 자리에 세움
            }

            currentDestination = initialDestination; // 현재 목적지 기록
            RestoreRuntimeOfficeState(); // 초기 Office 경제 상태 연결
            BindBell(); // 종 이벤트 연결
            yield return FadeTo(0f, fadeDuration); // 초기 암전 해제
            Debug.Log($"[Project I] 24일차 2단계 초기 맵 준비 / Persistent + {initialSceneName}", this); // 준비 완료 로그
        }

        public string GetTravelBlockReason() // 현재 마차 종으로 이동할 수 없는 이유 반환 (가능하면 null)
        {
            if (isTransitioning) // 이동 진행 여부 확인
            {
                return "마차 이동 중"; // 중복 이동 차단 사유
            }

            if (!IsPlayerAboard()) // 플레이어 탑승 여부 확인
            {
                return "마차 창고 안에 탑승한 뒤 종을 울리세요"; // 탑승 판정 실패 사유
            }

            if (currentDestination != TravelDestination.Office) // 던전에서 귀환하는 경우인지 확인
            {
                return null; // 귀환은 항상 허용
            }

            DailySnapshotService snapshotService = DailySnapshotService.Instance; // 일차 저장 서비스 조회

            if (snapshotService == null || !snapshotService.IsInitialized || snapshotService.IsRestoreInProgress || snapshotService.IsDayCompletionInProgress) // 저장 시스템 준비 여부 확인
            {
                return "사무소 기록 정리 중 — 잠시 후 다시 시도하세요"; // 저장 준비 전 출발 차단 사유
            }

            if (snapshotService.DayPhase == ExpeditionDayPhase.Returned) // 오늘 원정을 이미 다녀왔는지 확인
            {
                return $"오늘 원정 완료 — 일차 마감 장부에서 {snapshotService.CurrentDay}일차를 마감하세요"; // 하루 1회 원정 사유
            }

            return null; // 출발 가능
        }

        public bool RequestPlayerTeleport(Vector3 position, Quaternion rotation) // 짧은 암전과 함께 플레이어 순간이동 (던전 출입구용)
        {
            BindPersistentReferences(); // 최신 참조 확보

            if (isTransitioning || playerRoot == null) // 이동 중 또는 플레이어 누락 확인
            {
                return false; // 요청 거부
            }

            StartCoroutine(PlayerTeleportRoutine(position, rotation)); // 순간이동 절차 시작
            return true; // 요청 수락
        }

        private IEnumerator PlayerTeleportRoutine(Vector3 position, Quaternion rotation) // 암전 → 위치 이동 → 암전 해제
        {
            isTransitioning = true; // 이동 중 종·저장·중복 순간이동 차단
            yield return FadeTo(1f, 0.25f); // 짧은 암전
            CharacterController controller = playerRoot.GetComponentInChildren<CharacterController>(); // 이동 충돌체
            bool controllerWasEnabled = controller != null && controller.enabled; // 원래 활성 상태

            if (controller != null) // 충돌체 확인
            {
                controller.enabled = false; // 순간이동 중 충돌 보정 방지
            }

            playerRoot.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f)); // 좌우 방향만 적용해 이동
            Physics.SyncTransforms(); // 물리 위치 즉시 반영
            NotifyPlayerTeleported(); // 높이 변화를 추락 피해로 계산하지 않도록 초기화

            if (controller != null) // 충돌체 확인
            {
                controller.enabled = controllerWasEnabled; // 원래 상태 복원
            }

            yield return null; // 한 프레임 안정화
            yield return FadeTo(0f, 0.25f); // 암전 해제
            isTransitioning = false; // 이동 완료
        }

        private void PlacePlayerAtSpawn(MapTravelAnchor anchor) // 시작·불러오기 때 플레이어를 정해진 시작 위치에 세움 (마차 이동 중에는 사용하지 않음)
        {
            if (anchor == null || anchor.PlayerSpawnPoint == null || playerRoot == null || (wagonRoot != null && playerRoot.IsChildOf(wagonRoot))) // 시작 위치·별도 플레이어 확인
            {
                return; // 기존 마차 기준 위치 유지
            }

            CharacterController controller = playerRoot.GetComponentInChildren<CharacterController>(); // 이동 충돌체
            bool controllerWasEnabled = controller != null && controller.enabled; // 원래 활성 상태

            if (controller != null) // 충돌체 확인
            {
                controller.enabled = false; // 순간이동 중 충돌 보정 방지
            }

            Transform spawn = anchor.PlayerSpawnPoint; // 시작 위치
            playerRoot.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f)); // 좌우 방향만 적용
            Physics.SyncTransforms(); // 물리 위치 즉시 반영
            NotifyPlayerTeleported(); // 추락 판정 기준 초기화

            if (controller != null) // 충돌체 확인
            {
                controller.enabled = controllerWasEnabled; // 원래 상태 복원
            }
        }

        private void NotifyPlayerTeleported() // 플레이어 이동 컴포넌트에 순간이동 알림 (추락 판정 기준 초기화)
        {
            PlayerMovement movement = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerMovement>(); // 이동 컴포넌트
            movement?.NotifyTeleported(); // 추락 판정 초기화
        }

        public bool IsPlayerAboard() // 플레이어가 마차 적재 창고 안에 있는지 판정
        {
            BindPersistentReferences(); // 최신 참조 확보
            WagonCargoArea cargoArea = wagonRoot == null ? null : wagonRoot.GetComponentInChildren<WagonCargoArea>(true); // 적재칸 조회
            BoxCollider cargoTrigger = cargoArea == null ? null : cargoArea.GetComponent<BoxCollider>(); // 적재칸 판정 박스 조회

            if (playerRoot == null || cargoTrigger == null) // 판정 기준 누락 확인
            {
                return true; // 구성 누락 시 이동 자체는 막지 않음
            }

            Vector3 chest = playerRoot.position + Vector3.up * 0.9f; // 발 위치 대신 몸통 중심으로 판정
            Vector3 local = cargoTrigger.transform.InverseTransformPoint(chest) - cargoTrigger.center; // 적재칸 로컬 좌표 변환
            Vector3 lossy = cargoTrigger.transform.lossyScale; // 월드 여유를 로컬 단위로 바꾸기 위한 배율
            Vector3 half = cargoTrigger.size * 0.5f; // 적재칸 반크기
            Vector3 margin = new Vector3(boardingMargin / Mathf.Max(0.01f, lossy.x), boardingMargin / Mathf.Max(0.01f, lossy.y), boardingMargin / Mathf.Max(0.01f, lossy.z)); // 로컬 단위 여유
            return Mathf.Abs(local.x) <= half.x + margin.x && Mathf.Abs(local.y) <= half.y + margin.y && Mathf.Abs(local.z) <= half.z + margin.z; // 세 축 모두 탑승 범위 안인지 반환
        }

        private void HandleTravelRequested() // 마차 종 이동 요청 처리
        {
            string blockReason = GetTravelBlockReason(); // 이동 불가 사유 확인

            if (blockReason != null) // 이동할 수 없는 상태인지 확인
            {
                Debug.LogWarning($"[Project I] 마차 이동 거부 / {blockReason}", this); // 거부 사유 로그
                return; // 이동 차단
            }

            TravelDestination targetDestination = currentDestination == TravelDestination.Office // 현재 목적지 기준 반대 환경 계산
                ? TravelDestination.TestDungeon // Office에서는 Dungeon으로 이동
                : TravelDestination.Office; // Dungeon에서는 Office로 복귀
            StartCoroutine(TravelRoutine(targetDestination)); // 환경 교체 절차 시작
        }

        private void Update() // 원정 실패(전원 사망) 감지
        {
            CheckExpeditionFailure(); // 던전에서 사망했는지 확인
        }

        private void CheckExpeditionFailure() // 던전 체류 중 전원 사망 확인 · 사무소 사망은 비용 없이 부활
        {
            if (failureInProgress || isTransitioning) // 이미 처리 중인지 확인
            {
                return; // 확인 불필요
            }

            if (playerRoot == null) // 참조 확인
            {
                BindPersistentReferences(); // 참조 보정
            }

            PlayerDeathController death = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerDeathController>(true); // 사망 상태 조회

            if (death == null || !death.IsDead) // 생존 확인
            {
                officeReviveElapsed = 0f; // 사무소 부활 대기 초기화
                return; // 처리 불필요
            }

            if (currentDestination != TravelDestination.Office) // 던전에서 사망한 경우
            {
                BeginExpeditionFailure(); // 원정 실패 처리 시작
                return; // 종료
            }

            officeReviveElapsed += Time.deltaTime; // 사무소 사망 대기 누적

            if (officeReviveElapsed < Mathf.Max(0f, expeditionFailureDelay)) // 대기 확인
            {
                return; // 더 기다림
            }

            officeReviveElapsed = 0f; // 대기 초기화
            ReviveAtWagon(); // 설계 문서 3.13 — 사무소에서는 비용 없이 마차에서 부활
        }

        public bool BeginExpeditionFailure() // 원정 실패 귀환 시작 (설계 문서 6.13)
        {
            if (failureInProgress || isTransitioning || currentDestination == TravelDestination.Office) // 시작 가능 여부 확인
            {
                return false; // 거부
            }

            StartCoroutine(ExpeditionFailureRoutine()); // 실패 귀환 절차 시작
            return true; // 수락
        }

        private IEnumerator ExpeditionFailureRoutine() // 전원 사망 → 소지품·적재 상실 → 사무소 강제 귀환 → 부활
        {
            failureInProgress = true; // 중복 처리 차단
            Debug.LogWarning("[Project I] 원정 실패 — 전원 사망으로 원정을 종료합니다.", this); // 실패 안내
            float elapsed = 0f; // 대기 시간

            while (elapsed < Mathf.Max(0f, expeditionFailureDelay)) // 쓰러진 모습을 잠시 보여줌
            {
                elapsed += Time.deltaTime; // 시간 누적
                yield return null; // 다음 프레임
            }

            yield return TravelRoutine(TravelDestination.Office, true); // 실패 귀환 이동
            failureInProgress = false; // 처리 완료
        }

        private int DiscardExpeditionBelongings(out int lostValue) // 실패 시 소지품·마차 적재를 모두 잃음 (공동 보관함은 유지)
        {
            lostValue = 0; // 잃은 가치 합계
            int lostCount = 0; // 잃은 물건 수
            WagonSharedStorage sharedStorage = wagonRoot == null ? null : wagonRoot.GetComponentInChildren<WagonSharedStorage>(true); // 공동 보관함 조회
            Transform keepRoot = sharedStorage == null ? null : sharedStorage.StorageRoot; // 유지 대상 루트
            PlayerInventory inventory = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerInventory>(true); // 플레이어 인벤토리
            List<WorldItem> doomed = new List<WorldItem>(); // 제거 대상

            if (inventory != null) // 소지품 상실
            {
                for (int slot = 0; slot < inventory.SlotCount; slot++) // 슬롯 순회
                {
                    WorldItem item = inventory.GetItem(slot); // 슬롯 아이템

                    if (item != null && !doomed.Contains(item)) // 유효·중복 확인
                    {
                        doomed.Add(item); // 제거 대상 등록
                    }
                }
            }

            foreach (WorldItem item in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // 마차·던전 물건 상실
            {
                if (item == null || doomed.Contains(item)) // 유효·중복 확인
                {
                    continue; // 다음
                }

                if (keepRoot != null && item.transform.IsChildOf(keepRoot)) // 공동 보관함 물건은 유지
                {
                    continue; // 다음
                }

                if (playerRoot != null && item.transform.IsChildOf(playerRoot)) // 손에 든 물건·사망 시 떨어뜨린 물건
                {
                    doomed.Add(item); // 제거 대상 등록
                    continue; // 다음
                }

                if (wagonRoot != null && item.transform.IsChildOf(wagonRoot)) // 마차에 실린 물건
                {
                    doomed.Add(item); // 제거 대상 등록
                    continue; // 다음
                }

                if (item.gameObject.scene != gameObject.scene) // 던전 환경 씬에 남은 물건 (사무소 물건은 Persistent에 보관 중)
                {
                    doomed.Add(item); // 제거 대상 등록
                }
            }

            foreach (WorldItem item in doomed) // 제거 실행
            {
                if (item == null) // 유효 확인
                {
                    continue; // 다음
                }

                RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 회수 가치
                lostValue += recoverable != null ? recoverable.Value : 0; // 가치 합계
                lostCount++; // 개수 집계
                Destroy(item.gameObject); // 실제 제거
            }

            Debug.LogWarning($"[Project I] 원정 실패 상실 / 물건 {lostCount}개 · 가치 {lostValue} (공동 보관함 {(sharedStorage == null ? 0 : sharedStorage.StoredCount)}개 유지)", this); // 상실 기록
            return lostCount; // 잃은 물건 수 반환
        }

        private void ReviveAtWagon() // 마차 적재칸 안에서 플레이어 부활
        {
            PlayerDeathController death = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerDeathController>(true); // 사망 상태 조회

            if (death == null || !death.IsDead) // 부활 필요 여부 확인
            {
                return; // 종료
            }

            WagonCargoArea cargoArea = wagonRoot == null ? null : wagonRoot.GetComponentInChildren<WagonCargoArea>(true); // 적재칸 조회
            BoxCollider box = cargoArea == null ? null : cargoArea.GetComponent<BoxCollider>(); // 적재칸 박스
            Vector3 position = box != null // 부활 위치 계산
                ? box.transform.TransformPoint(box.center) - (Vector3.up * (box.size.y * Mathf.Abs(box.transform.lossyScale.y) * 0.5f)) + (Vector3.up * 0.25f) // 적재칸 바닥 위
                : wagonRoot.position + (Vector3.up * 1.2f); // 폴백 위치
            Quaternion rotation = wagonRoot == null ? Quaternion.identity : Quaternion.Euler(0f, wagonRoot.eulerAngles.y, 0f); // 마차 방향
            death.Revive(position, rotation); // 부활
        }

        private IEnumerator TravelRoutine(TravelDestination targetDestination, bool failureReturn = false) // 실제 Office↔Dungeon 이동 절차
        {
            if (currentDestination == TravelDestination.Office && targetDestination == TravelDestination.TestDungeon) // 새 원정을 시작하는 순간인지 확인
            {
                DailySnapshotService snapshotService = DailySnapshotService.Instance; // 사무소 안전 체크포인트 서비스 조회

                if (snapshotService == null || !snapshotService.SaveSafeOfficeCheckpoint()) // 출발 직전 최신 Office 데이터 저장 성공 여부 확인
                {
                    Debug.LogError("[Project I] 사무소 안전 체크포인트 저장 실패 / 던전 출발을 취소합니다.", this); // 안전 저장 실패 안내
                    yield break; // 복구 기준 없이 던전에 진입하지 않음
                }
            }

            isTransitioning = true; // 실제 환경 교체 잠금 시작
            BindPersistentReferences(); // 최신 Persistent 참조 확보
            int lostCount = 0; // 실패로 잃은 물건 수
            int lostValue = 0; // 실패로 잃은 가치

            if (!failureReturn) // 정상 이동인지 확인
            {
                AttachPlayerToWagon(); // 이동 중에도 마차 안에서 자유롭게 움직이도록 마차에 태움
                yield return CloseTravelCover(); // 천막을 쳐서 바깥이 보이지 않게 함
            }

            yield return FadeTo(1f, fadeDuration); // 화면 완전 암전

            if (failureReturn) // 원정 실패 귀환인지 확인
            {
                lostCount = DiscardExpeditionBelongings(out lostValue); // 소지품·마차 적재 상실 (공동 보관함 유지)
            }

            CaptureRuntimeOfficeState(); // Office를 떠나기 전 경제 상태 보존

            if (currentDestination == TravelDestination.Office) // 원정 출발인지 확인
            {
                reportTracker?.BeginExpedition(wagonRoot); // 가져가는 물건 기록
            }
            else // 던전에서 귀환하는 경우
            {
                reportTracker?.CompleteExpedition(wagonRoot); // 가져오는 물건과 비교해 원정 결과 계산
            }

            cargoPersistence?.CaptureCargoForTravel(); // 마차 안 실제 WorldItem을 같은 GameObject로 고정

            string previousSceneName = GetSceneName(currentDestination); // 기존 환경 씬 이름 저장
            string targetSceneName = GetSceneName(targetDestination); // 목적지 환경 이름 계산
            Scene targetScene = SceneManager.GetSceneByName(targetSceneName); // 목적지 로드 상태 조회

            if (!targetScene.isLoaded) // 목적지가 아직 미로드인지 확인
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive); // 목적지 Additive 로드

                if (loadOperation == null) // 로드 요청 실패 여부 확인
                {
                    Debug.LogError($"[Project I] 목적지 맵 로드 실패 / Scene={targetSceneName}", this); // 실패 로그
                    cargoPersistence?.ReleaseCargoAfterTravel(); // 잠근 Cargo 물리 원상 복구
                    DetachPlayerFromWagon(); // 마차 동승 해제
                    yield return FadeTo(0f, fadeDuration); // 기존 화면 복원
                    yield return OpenTravelCover(); // 천막 걷기
                    isTransitioning = false; // 이동 잠금 해제
                    yield break; // 기존 환경 유지
                }

                yield return loadOperation; // 목적지 로드 완료 대기
                targetScene = SceneManager.GetSceneByName(targetSceneName); // 로드 씬 재조회
                EnvironmentSceneGuard.SanitizeLoadedEnvironment(targetScene, gameObject.scene, this); // 새로 로드한 환경 씬에 섞인 전역 시스템 차단
            }

            MapTravelAnchor targetAnchor = FindAnchor(targetScene, targetDestination); // 목적지 Entry/Stop 지점 조회

            if (targetAnchor == null || !targetAnchor.IsConfigured) // 이동 지점 구성 확인
            {
                Debug.LogError($"[Project I] 목적지 Wagon Entry/Stop 지점 누락 / Scene={targetSceneName}", this); // 구성 오류 로그
                cargoPersistence?.ReleaseCargoAfterTravel(); // Cargo 물리 복구
                DetachPlayerFromWagon(); // 마차 동승 해제
                yield return FadeTo(0f, fadeDuration); // 화면 복원
                yield return OpenTravelCover(); // 천막 걷기
                isTransitioning = false; // 이동 잠금 해제
                yield break; // 환경 교체 중단
            }

            TeleportPersistentGroup(targetAnchor.EntryPoint.position, targetAnchor.EntryPoint.rotation); // 암전 중 마차를 진입 지점으로 이동

            if (targetScene.IsValid() && targetScene.isLoaded) // 목적지 최종 유효성 확인
            {
                SceneManager.SetActiveScene(targetScene); // 목적지를 현재 활성 환경으로 지정
            }

            if (targetDestination == TravelDestination.Office) // 사무소로 돌아오는 경우인지 확인
            {
                officeItemKeeper?.RestoreIntoScene(targetScene); // 씬 기본 아이템 대신 떠날 때의 실제 사무소 아이템 복귀
            }

            Scene previousScene = SceneManager.GetSceneByName(previousSceneName); // 기존 환경 조회

            if (previousScene.IsValid() && previousScene.isLoaded && previousScene.name != targetSceneName) // 기존 환경이 별도로 남아있는지 확인
            {
                if (previousSceneName == OfficeSceneName) // 사무소를 떠나는 경우인지 확인
                {
                    officeItemKeeper?.StashFromScene(previousScene); // 언로드 전에 사무소 실제 아이템을 같은 GameObject로 보관
                }

                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene); // 기존 환경만 언로드

                if (unloadOperation != null) // 언로드 요청 성공 여부 확인
                {
                    yield return unloadOperation; // 정리 완료 대기
                }
            }

            currentDestination = targetDestination; // 현재 환경 목적지 갱신

            if (targetDestination == TravelDestination.Office) // 사무소 귀환인지 확인
            {
                DailySnapshotService.Instance?.MarkExpeditionReturned(); // 오늘 원정 귀환 완료 단계로 전환
            }
            else // 던전 도착인 경우
            {
                DailySnapshotService.Instance?.MarkExpeditionDeparted(); // 오늘 원정 진행 단계로 전환
            }

            RestoreRuntimeOfficeState(); // Office 도착이면 경제 상태 복원

            if (failureReturn) // 원정 실패 귀환인지 확인
            {
                ReviveAtWagon(); // 마차 안에서 부활 (사무소 진입 연출을 함께 탐)
                AttachPlayerToWagon(); // 진입 연출 동안 마차와 함께 이동
                reportTracker?.MarkFailed(lostCount, lostValue); // 원정 결과에 실패 기록
            }

            yield return MovePersistentGroup(targetAnchor); // Entry에서 Stop까지 실제 마차 진입 연출
            BindBell(); // 이동 종 참조 재연결
            DetachPlayerFromWagon(); // 도착 후 플레이어를 다시 독립 루트로 복귀
            yield return OpenTravelCover(); // 도착했으므로 천막을 걷어 바깥이 보이게 함
            isTransitioning = false; // 전체 이동 완료

            if (currentDestination == TravelDestination.Office) // 원정에서 안전 구역으로 귀환했는지 확인
            {
                DailySnapshotService.Instance?.SaveSafeOfficeCheckpoint(); // 귀환 직후 상태를 즉시 Current에 기록
            }

            Debug.Log($"[Project I] 맵 교체 완료 / Persistent 유지 / Map={targetSceneName}", this); // 이동 완료 로그
        }

        private IEnumerator MovePersistentGroup(MapTravelAnchor anchor) // Entry에서 Stop까지 Wagon/Player 이동
        {
            if (wagonRoot == null || anchor == null || anchor.EntryPoint == null || anchor.StopPoint == null) // 필수 이동 참조 확인
            {
                cargoPersistence?.ReleaseCargoAfterTravel(); // Cargo 물리 복구
                yield return FadeTo(0f, fadeDuration); // 화면 복원
                yield break; // 이동 중단
            }

            Vector3 startPosition = anchor.EntryPoint.position; // 시작 위치 저장
            Quaternion startRotation = anchor.EntryPoint.rotation; // 시작 회전 저장
            Vector3 endPosition = anchor.StopPoint.position; // 정차 위치 저장
            Quaternion endRotation = anchor.StopPoint.rotation; // 정차 회전 저장
            bool playerIsWagonChild = playerRoot != null && playerRoot.IsChildOf(wagonRoot); // Player가 Wagon 자식인지 확인
            bool ridePlayer = playerRoot != null && !playerIsWagonChild; // 마차 진입 연출 동안 탑승 이동 사용 여부
            Quaternion previousWagonRotation = wagonRoot.rotation; // 직전 프레임 마차 회전
            Vector3 rideLocalPosition = ridePlayer ? wagonRoot.InverseTransformPoint(playerRoot.position) : Vector3.zero; // 마차 기준 탑승 위치
            PlayerMovement rideMovement = ridePlayer ? playerRoot.GetComponentInChildren<PlayerMovement>() : null; // 이동 컴포넌트
            CharacterController rideController = ridePlayer ? playerRoot.GetComponentInChildren<CharacterController>() : null; // 이동 충돌체
            bool movementWasEnabled = rideMovement != null && rideMovement.enabled; // 원래 상태
            bool controllerWasEnabled = rideController != null && rideController.enabled; // 원래 상태

            if (ridePlayer) // 탑승 이동 시작 (지형·구조물 충돌을 무시하고 적재칸 안에서만 움직임)
            {
                rideLocalPosition = ClampToCargo(rideLocalPosition); // 적재칸 안으로 보정

                if (rideMovement != null) // 이동 컴포넌트
                {
                    rideMovement.enabled = false; // 중력·충돌 이동 정지 (시점은 그대로 자유)
                }

                if (rideController != null) // 충돌체
                {
                    rideController.enabled = false; // 사무소 입구 구조물에 걸리지 않도록 해제
                }
            }

            float duration = Mathf.Max(0.1f, arrivalDuration); // 안전한 이동 시간 계산
            float elapsed = 0f; // 이동 경과 시간 초기화

            while (elapsed < duration) // 정차 지점까지 이동
            {
                elapsed += Time.deltaTime; // 프레임 경과 누적
                float normalized = Mathf.Clamp01(elapsed / duration); // 0~1 진행률 계산
                float eased = Mathf.SmoothStep(0f, 1f, normalized); // 부드러운 가감속 적용
                wagonRoot.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, eased), Quaternion.Slerp(startRotation, endRotation, eased)); // Wagon 이동

                if (ridePlayer) // 탑승 이동
                {
                    rideLocalPosition = StepRidePosition(rideLocalPosition, Time.deltaTime); // 입력만큼 마차 안에서 이동
                    playerRoot.position = wagonRoot.TransformPoint(rideLocalPosition); // 마차와 함께 이동
                    playerRoot.rotation = (wagonRoot.rotation * Quaternion.Inverse(previousWagonRotation)) * playerRoot.rotation; // 마차가 돈 만큼만 몸도 돌림 (시점은 자유)
                }

                previousWagonRotation = wagonRoot.rotation; // 다음 프레임 기준 갱신
                cargoPersistence?.SyncCapturedCargoToWagon(); // 실제 Cargo GameObject 위치 동기화
                Physics.SyncTransforms(); // autoSyncTransforms가 꺼져 있어 이동한 충돌체 위치를 매 프레임 반영

                if (fadeGroup != null) // Fade UI 존재 여부 확인
                {
                    fadeGroup.alpha = 1f - normalized; // 마차 진입과 함께 화면 밝힘
                }

                yield return null; // 다음 프레임 진행
            }

            wagonRoot.SetPositionAndRotation(endPosition, endRotation); // 최종 정차 Transform 확정

            if (ridePlayer) // 탑승 이동 종료
            {
                playerRoot.position = wagonRoot.TransformPoint(rideLocalPosition); // 최종 위치 확정

                if (rideController != null) // 충돌체 복구
                {
                    rideController.enabled = controllerWasEnabled; // 원래 상태
                }

                if (rideMovement != null) // 이동 컴포넌트 복구
                {
                    rideMovement.enabled = movementWasEnabled; // 원래 상태
                }

                Physics.SyncTransforms(); // 위치 물리 반영
                NotifyPlayerTeleported(); // 도착 위치 기준으로 추락 판정 초기화
            }

            cargoPersistence?.SyncCapturedCargoToWagon(); // Cargo 최종 위치 확정
            Physics.SyncTransforms(); // 최종 위치 물리 반영
            cargoPersistence?.ReleaseCargoAfterTravel(); // Cargo Rigidbody 상태 원복
            SetFadeImmediate(0f); // 화면 완전히 표시
        }

        private void TeleportPersistentGroup(Vector3 targetPosition, Quaternion targetRotation) // 암전 중 Wagon/Player 즉시 이동
        {
            BindPersistentReferences(); // 최신 참조 확보

            if (wagonRoot == null) // Wagon 누락 확인
            {
                Debug.LogError("[Project I] Persistent Wagon을 찾지 못했습니다.", this); // 구성 오류 로그
                return; // 이동 중단
            }

            bool playerIsWagonChild = playerRoot != null && playerRoot.IsChildOf(wagonRoot); // Player 계층 관계 확인
            Vector3 playerLocalPosition = Vector3.zero; // 별도 Player 상대 위치
            Quaternion playerLocalRotation = Quaternion.identity; // 별도 Player 상대 회전

            if (playerRoot != null && !playerIsWagonChild) // Player가 Wagon 자식이 아닌지 확인
            {
                playerLocalPosition = wagonRoot.InverseTransformPoint(playerRoot.position); // 상대 위치 저장
                playerLocalRotation = Quaternion.Inverse(wagonRoot.rotation) * playerRoot.rotation; // 상대 회전 저장
            }

            wagonRoot.SetPositionAndRotation(targetPosition, targetRotation); // Wagon 즉시 이동

            if (playerRoot != null && !playerIsWagonChild) // 별도 Player 루트 동기화
            {
                playerRoot.SetPositionAndRotation(wagonRoot.TransformPoint(playerLocalPosition), wagonRoot.rotation * playerLocalRotation); // Player 즉시 이동
            }

            Physics.SyncTransforms(); // 마차와 함께 움직인 충돌체 위치 즉시 반영
            NotifyPlayerTeleported(); // 맵 간 높이 차이를 추락 피해로 계산하지 않도록 초기화 (마차 자식일 때 포함)
            cargoPersistence?.SyncCapturedCargoToWagon(); // 이동 중 고정 Cargo 동기화
        }

        private IEnumerator FadeTo(float targetAlpha, float duration) // 화면 Fade Coroutine
        {
            if (fadeGroup == null) // Fade UI 누락 확인
            {
                yield break; // Fade 없이 진행
            }

            float startAlpha = fadeGroup.alpha; // 현재 투명도 저장
            float safeDuration = Mathf.Max(0.01f, duration); // 최소 Fade 시간 보장
            float elapsed = 0f; // 경과 시간 초기화
            fadeGroup.blocksRaycasts = true; // 전환 중 입력 차단

            while (elapsed < safeDuration) // 목표 투명도까지 보간
            {
                elapsed += Time.unscaledDeltaTime; // 게임 시간 배율과 무관하게 진행
                float normalized = Mathf.Clamp01(elapsed / safeDuration); // 진행률 계산
                fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalized); // 투명도 보간
                yield return null; // 다음 프레임 진행
            }

            fadeGroup.alpha = targetAlpha; // 최종 투명도 확정
            fadeGroup.blocksRaycasts = targetAlpha > 0.01f; // 화면이 보이면 입력 차단 해제
        }

        private void SetFadeImmediate(float alpha) // Fade 상태 즉시 설정
        {
            if (fadeGroup == null) // Fade UI 누락 확인
            {
                return; // 설정 중단
            }

            fadeGroup.alpha = Mathf.Clamp01(alpha); // 안전 범위 투명도 적용
            fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f; // 암전 상태 입력 차단
        }

        private void AttachPlayerToWagon() // 이동 중 플레이어를 마차와 함께 옮기는 상태로 전환 (시점·이동은 자유)
        {
            playerAttachedToWagon = playerRoot != null && wagonRoot != null; // 동승 상태 기록 (계층은 바꾸지 않음)
        }

        private void DetachPlayerFromWagon() // 도착 후 동승 상태 해제
        {
            playerAttachedToWagon = false; // 상태 해제
        }

        private Vector3 ClampToCargo(Vector3 wagonLocalPosition) // 탑승 이동 범위를 적재칸 안으로 제한
        {
            WagonCargoArea cargoArea = wagonRoot == null ? null : wagonRoot.GetComponentInChildren<WagonCargoArea>(true); // 적재칸
            BoxCollider box = cargoArea == null ? null : cargoArea.GetComponent<BoxCollider>(); // 판정 박스

            if (box == null) // 구성 누락
            {
                return wagonLocalPosition; // 제한 없음
            }

            Vector3 center = wagonRoot.InverseTransformPoint(box.transform.TransformPoint(box.center)); // 마차 기준 중심
            Vector3 size = wagonRoot.InverseTransformVector(box.transform.TransformVector(box.size)); // 마차 기준 크기
            float marginX = Mathf.Max(0.1f, (Mathf.Abs(size.x) * 0.5f) - ridePadding); // 좌우 여유
            float marginZ = Mathf.Max(0.1f, (Mathf.Abs(size.z) * 0.5f) - ridePadding); // 앞뒤 여유
            Vector3 clamped = wagonLocalPosition; // 결과
            clamped.x = Mathf.Clamp(clamped.x, center.x - marginX, center.x + marginX); // 좌우 제한
            clamped.z = Mathf.Clamp(clamped.z, center.z - marginZ, center.z + marginZ); // 앞뒤 제한
            return clamped; // 반환
        }

        private Vector3 StepRidePosition(Vector3 wagonLocalPosition, float deltaTime) // 마차 안에서 입력만큼 걸어 다니기 (구조물 충돌 무시)
        {
            PlayerInputReader reader = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerInputReader>(); // 입력
            PlayerHealth health = playerRoot == null ? null : playerRoot.GetComponentInChildren<PlayerHealth>(); // 체력

            if (reader == null || wagonRoot == null || (health != null && health.IsDead)) // 조작 불가
            {
                return ClampToCargo(wagonLocalPosition); // 위치 유지
            }

            Vector2 input = reader.Move; // 이동 입력
            Vector3 localInput = new Vector3(input.x, 0f, input.y); // 로컬 이동 벡터

            if (localInput.sqrMagnitude > 1f) // 대각선 보정
            {
                localInput.Normalize(); // 정규화
            }

            if (localInput.sqrMagnitude < 0.0001f) // 입력 없음
            {
                return ClampToCargo(wagonLocalPosition); // 위치 유지
            }

            Vector3 worldDirection = playerRoot.rotation * localInput; // 보는 방향 기준
            Vector3 wagonDirection = Quaternion.Inverse(wagonRoot.rotation) * worldDirection; // 마차 기준 방향
            wagonDirection.y = 0f; // 수평 이동만
            return ClampToCargo(wagonLocalPosition + (wagonDirection * (rideWalkSpeed * deltaTime))); // 제한 적용
        }

        private IEnumerator CloseTravelCover() // 출발 전 천막 치기
        {
            BindPersistentReferences(); // 최신 참조 확보

            if (travelCover == null) // 천막 누락 확인
            {
                yield break; // 연출 없이 진행
            }

            yield return travelCover.CloseRoutine(); // 닫힘 완료까지 대기
        }

        private IEnumerator OpenTravelCover() // 도착 후 천막 걷기
        {
            if (travelCover == null) // 천막 누락 확인
            {
                yield break; // 연출 없이 진행
            }

            yield return travelCover.OpenRoutine(); // 열림 완료까지 대기
        }

        private void BindPersistentReferences() // Player/Wagon/Cargo 참조 자동 연결
        {
            if (wagonRoot == null) // Wagon 루트 누락 확인
            {
                WagonCargoArea cargoArea = UnityEngine.Object.FindFirstObjectByType<WagonCargoArea>(); // 현재 Persistent CargoArea 조회

                if (cargoArea != null) // CargoArea 존재 여부 확인
                {
                    wagonRoot = cargoArea.transform.root; // Wagon 루트 설정
                }
            }

            if (cargoPersistence == null && wagonRoot != null) // Cargo 보존 관리자 누락 확인
            {
                cargoPersistence = wagonRoot.GetComponentInChildren<WagonCargoPersistence>(true); // Wagon 계층에서 조회
            }

            if (travelCover == null && wagonRoot != null) // 마차 천막 누락 확인
            {
                travelCover = wagonRoot.GetComponentInChildren<WagonTravelCover>(true); // Wagon 계층에서 조회
            }

            if (playerRoot == null) // Player 루트 누락 확인
            {
                PlayerCarryController carryController = UnityEngine.Object.FindFirstObjectByType<PlayerCarryController>(); // Persistent Player 조회

                if (carryController != null) // Player 존재 여부 확인
                {
                    playerRoot = carryController.transform.root; // Player 루트 설정
                }
            }
        }

        private void BindBell() // Persistent Wagon 종 이벤트 연결
        {
            BindPersistentReferences(); // 참조 보정
            WagonTravelBellInteractable nextBell = wagonRoot != null // Wagon 계층 존재 여부 확인
                ? wagonRoot.GetComponentInChildren<WagonTravelBellInteractable>(true) // Wagon 내부 종 조회
                : UnityEngine.Object.FindFirstObjectByType<WagonTravelBellInteractable>(); // 전역 종 폴백 조회

            if (travelBell == nextBell) // 이미 올바른 종인지 확인
            {
                return; // 중복 구독 방지
            }

            UnbindBell(); // 이전 종 이벤트 해제
            travelBell = nextBell; // 새 종 저장

            if (travelBell != null) // 유효 종 확인
            {
                travelBell.TravelRequested += HandleTravelRequested; // 이동 요청 이벤트 구독
            }
            else // 종 누락 상태
            {
                Debug.LogWarning("[Project I] Persistent Wagon의 이동 종을 찾지 못했습니다.", this); // 진단 로그
            }
        }

        private void UnbindBell() // 종 이벤트 해제
        {
            if (travelBell != null) // 기존 종 존재 여부 확인
            {
                travelBell.TravelRequested -= HandleTravelRequested; // 이벤트 구독 해제
                travelBell = null; // 참조 제거
            }
        }

        private static string GetSceneName(TravelDestination destination) // 목적지 enum에서 환경 씬 이름 계산
        {
            return destination == TravelDestination.TestDungeon ? TestDungeonSceneName : OfficeSceneName; // 두 환경 이름 반환
        }

        private static MapTravelAnchor FindAnchor(Scene scene, TravelDestination destination) // 특정 환경의 마차 이동 지점 조회
        {
            if (!scene.IsValid() || !scene.isLoaded) // 대상 씬 유효성 확인
            {
                return null; // 검색 실패
            }

            GameObject[] roots = scene.GetRootGameObjects(); // 대상 씬 루트 목록 조회

            foreach (GameObject root in roots) // 모든 루트 순회
            {
                MapTravelAnchor[] anchors = root.GetComponentsInChildren<MapTravelAnchor>(true); // 하위 Anchor 전체 조회

                foreach (MapTravelAnchor anchor in anchors) // Anchor 순회
                {
                    if (anchor != null && anchor.Destination == destination) // 원하는 목적지 Anchor인지 확인
                    {
                        return anchor; // 첫 일치 Anchor 반환
                    }
                }
            }

            return null; // 대상 Anchor 없음
        }

        private void OnValidate() // Inspector 수치 안전 보정
        {
            fadeDuration = Mathf.Max(0.1f, fadeDuration); // Fade 최소 시간 보장
            arrivalDuration = Mathf.Max(0.1f, arrivalDuration); // 도착 이동 최소 시간 보장
        }
    }
}
