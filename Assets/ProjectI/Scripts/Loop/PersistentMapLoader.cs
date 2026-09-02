using System; // 복구 결과 콜백 사용
using System.Collections; // Coroutine 이동 절차 사용
using ProjectI.Items; // 플레이어 운반 기능 참조
using ProjectI.Persistence; // 사무소 안전 체크포인트 저장 서비스 참조
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
        private WagonTravelBellInteractable travelBell; // 현재 마차 이동 종
        private TravelDestination currentDestination; // 현재 로드된 환경 목적지
        private bool isTransitioning; // 환경 교체 진행 여부

        public static PersistentMapLoader Instance => instance; // 전역 맵 로더 공개
        public TravelDestination CurrentDestination => currentDestination; // 현재 목적지 공개
        public bool IsTransitioning => isTransitioning; // 이동 진행 상태 공개
        public WagonCargoPersistence CargoPersistence => cargoPersistence; // Cargo 보존 관리자 공개

        private void Awake() // Persistent 로더 초기화
        {
            if (instance != null && instance != this) // 중복 로더 존재 여부 확인
            {
                Destroy(gameObject); // 중복 Persistent 시스템 제거
                return; // 추가 초기화 중단
            }

            instance = this; // 현재 로더 등록
            currentDestination = initialDestination; // 초기 목적지 설정
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
                    Debug.LogError($"[Project I] 초기 맵 로드 실패 / Scene={initialSceneName}", this); // 실패 로그
                    yield break; // 초기화 중단
                }

                yield return loadOperation; // 환경 로드 완료 대기
                initialScene = SceneManager.GetSceneByName(initialSceneName); // 로드 씬 재조회
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
            }

            currentDestination = initialDestination; // 현재 목적지 기록
            RestoreRuntimeOfficeState(); // 초기 Office 경제 상태 연결
            BindBell(); // 종 이벤트 연결
            yield return FadeTo(0f, fadeDuration); // 초기 암전 해제
            Debug.Log($"[Project I] 24일차 2단계 초기 맵 준비 / Persistent + {initialSceneName}", this); // 준비 완료 로그
        }

        private void HandleTravelRequested() // 마차 종 이동 요청 처리
        {
            if (isTransitioning) // 이미 이동 중인지 확인
            {
                return; // 중복 이동 차단
            }

            TravelDestination targetDestination = currentDestination == TravelDestination.Office // 현재 목적지 기준 반대 환경 계산
                ? TravelDestination.TestDungeon // Office에서는 Dungeon으로 이동
                : TravelDestination.Office; // Dungeon에서는 Office로 복귀
            StartCoroutine(TravelRoutine(targetDestination)); // 환경 교체 절차 시작
        }

        private IEnumerator TravelRoutine(TravelDestination targetDestination) // 실제 Office↔Dungeon 이동 절차
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
            yield return FadeTo(1f, fadeDuration); // 화면 완전 암전
            BindPersistentReferences(); // 최신 Persistent 참조 확보
            CaptureRuntimeOfficeState(); // Office를 떠나기 전 경제 상태 보존
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
                    yield return FadeTo(0f, fadeDuration); // 기존 화면 복원
                    isTransitioning = false; // 이동 잠금 해제
                    yield break; // 기존 환경 유지
                }

                yield return loadOperation; // 목적지 로드 완료 대기
                targetScene = SceneManager.GetSceneByName(targetSceneName); // 로드 씬 재조회
            }

            MapTravelAnchor targetAnchor = FindAnchor(targetScene, targetDestination); // 목적지 Entry/Stop 지점 조회

            if (targetAnchor == null || !targetAnchor.IsConfigured) // 이동 지점 구성 확인
            {
                Debug.LogError($"[Project I] 목적지 Wagon Entry/Stop 지점 누락 / Scene={targetSceneName}", this); // 구성 오류 로그
                cargoPersistence?.ReleaseCargoAfterTravel(); // Cargo 물리 복구
                yield return FadeTo(0f, fadeDuration); // 화면 복원
                isTransitioning = false; // 이동 잠금 해제
                yield break; // 환경 교체 중단
            }

            TeleportPersistentGroup(targetAnchor.EntryPoint.position, targetAnchor.EntryPoint.rotation); // 암전 중 마차를 진입 지점으로 이동

            if (targetScene.IsValid() && targetScene.isLoaded) // 목적지 최종 유효성 확인
            {
                SceneManager.SetActiveScene(targetScene); // 목적지를 현재 활성 환경으로 지정
            }

            Scene previousScene = SceneManager.GetSceneByName(previousSceneName); // 기존 환경 조회

            if (previousScene.IsValid() && previousScene.isLoaded && previousScene.name != targetSceneName) // 기존 환경이 별도로 남아있는지 확인
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene); // 기존 환경만 언로드

                if (unloadOperation != null) // 언로드 요청 성공 여부 확인
                {
                    yield return unloadOperation; // 정리 완료 대기
                }
            }

            currentDestination = targetDestination; // 현재 환경 목적지 갱신
            RestoreRuntimeOfficeState(); // Office 도착이면 경제 상태 복원
            yield return MovePersistentGroup(targetAnchor); // Entry에서 Stop까지 실제 마차 진입 연출
            BindBell(); // 이동 종 참조 재연결
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
            Vector3 playerLocalPosition = Vector3.zero; // 별도 Player의 Wagon 상대 위치
            Quaternion playerLocalRotation = Quaternion.identity; // 별도 Player의 Wagon 상대 회전

            if (playerRoot != null && !playerIsWagonChild) // Player가 별도 Persistent 루트인지 확인
            {
                playerLocalPosition = wagonRoot.InverseTransformPoint(playerRoot.position); // Wagon 상대 위치 저장
                playerLocalRotation = Quaternion.Inverse(wagonRoot.rotation) * playerRoot.rotation; // Wagon 상대 회전 저장
            }

            float duration = Mathf.Max(0.1f, arrivalDuration); // 안전한 이동 시간 계산
            float elapsed = 0f; // 이동 경과 시간 초기화

            while (elapsed < duration) // 정차 지점까지 이동
            {
                elapsed += Time.deltaTime; // 프레임 경과 누적
                float normalized = Mathf.Clamp01(elapsed / duration); // 0~1 진행률 계산
                float eased = Mathf.SmoothStep(0f, 1f, normalized); // 부드러운 가감속 적용
                wagonRoot.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, eased), Quaternion.Slerp(startRotation, endRotation, eased)); // Wagon 이동

                if (playerRoot != null && !playerIsWagonChild) // 별도 Player 루트 이동 필요 여부 확인
                {
                    playerRoot.SetPositionAndRotation(wagonRoot.TransformPoint(playerLocalPosition), wagonRoot.rotation * playerLocalRotation); // Player를 Wagon과 함께 이동
                }

                cargoPersistence?.SyncCapturedCargoToWagon(); // 실제 Cargo GameObject 위치 동기화

                if (fadeGroup != null) // Fade UI 존재 여부 확인
                {
                    fadeGroup.alpha = 1f - normalized; // 마차 진입과 함께 화면 밝힘
                }

                yield return null; // 다음 프레임 진행
            }

            wagonRoot.SetPositionAndRotation(endPosition, endRotation); // 최종 정차 Transform 확정

            if (playerRoot != null && !playerIsWagonChild) // 별도 Player 루트 최종 정렬
            {
                playerRoot.SetPositionAndRotation(wagonRoot.TransformPoint(playerLocalPosition), wagonRoot.rotation * playerLocalRotation); // Player 최종 위치 확정
            }

            cargoPersistence?.SyncCapturedCargoToWagon(); // Cargo 최종 위치 확정
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
