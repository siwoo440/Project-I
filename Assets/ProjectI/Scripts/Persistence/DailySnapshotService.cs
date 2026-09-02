using System; // 문자열·숫자 변환 기능 사용
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
    public sealed class DailySnapshotService : MonoBehaviour // 일차 전체 상태 저장·검증·롤백 관리자
    {
        private static DailySnapshotService instance; // 현재 Persistent 저장 서비스 단일 참조
        [SerializeField] private int currentDay = 1; // 현재 플레이 중인 일차 번호
        [SerializeField] private bool initializeOnStart = true; // Persistent 씬 시작 시 Current 또는 이전 일차 자동 복구 여부
        private DailySnapshotStore store; // 로컬 SHA-256 저장소
        private EconomySnapshotData runtimeEconomy = new EconomySnapshotData(); // Office가 언로드되어도 유지할 현재 경제 상태
        private IRemoteDailySnapshotStore remoteStore; // 향후 서버 백업 구현 연결 지점
        private bool initialized; // 최초 저장·복구 초기화 완료 여부
        private bool restoreInProgress; // 중복 복구 방지 상태

        public static DailySnapshotService Instance => instance; // 전역 저장 서비스 공개
        public int CurrentDay => currentDay; // UI·다음 날 시스템용 현재 일차 공개
        public bool IsInitialized => initialized; // 저장 시스템 준비 완료 여부 공개
        public bool IsRestoreInProgress => restoreInProgress; // 복구 진행 여부 공개

        private void Awake() // Persistent 저장 서비스 초기화
        {
            if (instance != null && instance != this) // 중복 서비스 존재 여부 확인
            {
                Destroy(this); // 뒤늦게 생성된 중복 컴포넌트 제거
                return; // 중복 초기화 중단
            }

            instance = this; // 현재 서비스를 단일 인스턴스로 등록
            currentDay = Mathf.Max(1, currentDay); // 일차 최소값 보정
            store = new DailySnapshotStore(); // 로컬 저장소 생성
        }

        private IEnumerator Start() // Additive 초기 Office 준비 후 저장 데이터 적용
        {
            if (!initializeOnStart) // 자동 초기화 비활성 여부 확인
            {
                initialized = true; // 외부에서 직접 사용할 수 있도록 준비 상태만 설정
                yield break; // 자동 복구 수행 안 함
            }

            yield return WaitForMapLoaderReady(); // Step2 초기 환경 맵 로드 완료 대기
            yield return InitializeFromDisk(); // Current 검증 또는 이전 정상 일차 복구
        }

        private void OnDestroy() // 서비스 제거 시 단일 참조 정리
        {
            if (instance == this) // 현재 등록 인스턴스인지 확인
            {
                instance = null; // 전역 참조 해제
            }
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

        public void CompleteCurrentDay() // 다음 날 처리 지점에서 호출할 일차 완료 저장 API
        {
            if (!initialized || restoreInProgress) // 저장 서비스 준비와 복구 충돌 여부 확인
            {
                Debug.LogWarning("[Project I] 일차 Snapshot 저장 불가 / 저장 시스템 초기화 또는 복구 진행 중", this); // 호출 시점 안내
                return; // 중복 저장 차단
            }

            StartCoroutine(CompleteCurrentDayRoutine()); // 일차 완료 Snapshot 생성 절차 시작
        }

        public void RestoreMostRecentDailySnapshot() // 플레이 중 논리 손상 시 사용자가 최근 정상 일차로 수동 롤백
        {
            if (restoreInProgress) // 이미 복구 중인지 확인
            {
                return; // 중복 롤백 차단
            }

            if (store == null) // 저장소 초기화 여부 확인
            {
                store = new DailySnapshotStore(); // 늦은 호출에도 저장소 생성
            }

            if (!store.TryReadLatestValidDailySnapshot(out DailySnapshotData snapshot, out string path)) // 최신 정상 완료 일차 조회
            {
                Debug.LogError("[Project I] 복구 가능한 정상 이전 일차 Snapshot이 없습니다.", this); // 백업 없음 로그 출력
                return; // 복구 중단
            }

            snapshot.currentDay = snapshot.completedDay + 1; // 완료 일차 다음 날을 다시 시작하도록 현재 일차 계산
            Debug.LogWarning($"[Project I] 이전 일차 롤백 시작 / Source={path} / RestartDay={snapshot.currentDay}", this); // 수동 복구 출처 안내
            StartCoroutine(RestoreSnapshotRoutine(snapshot, true)); // 전체 게임 상태 복구 후 Current 재생성
        }

        public bool SaveCurrentDayStart() // 현재 상태를 Current 일차 시작점으로 명시 저장
        {
            if (!TryCaptureSnapshot(currentDay, 0, out DailySnapshotData snapshot)) // 전체 게임 상태 캡처 가능 여부 확인
            {
                return false; // 불완전한 Current 저장 방지
            }

            return store.WriteCurrent(snapshot); // SHA-256 Current 저장 결과 반환
        }

        private IEnumerator InitializeFromDisk() // 게임 시작 시 Current 검증과 자동 백업 폴백 처리
        {
            if (store == null) // 저장소 누락 확인
            {
                store = new DailySnapshotStore(); // 기본 로컬 저장소 생성
            }

            bool currentExists = store.CurrentFileExists(); // Current 파일 실제 존재 여부 확인

            if (store.TryReadCurrent(out DailySnapshotData current, out string currentReason)) // 정상 Current 데이터 확인
            {
                currentDay = Mathf.Max(1, current.currentDay); // 저장된 현재 일차 복구
                yield return RestoreSnapshotRoutine(current, false); // 정상 Current 전체 상태 적용
                initialized = true; // 초기 복구 완료 표시
                yield break; // 폴백 불필요
            }

            if (store.TryReadLatestValidDailySnapshot(out DailySnapshotData fallback, out string fallbackPath)) // Current 누락·손상 시 가장 최근 완료 일차 검색
            {
                fallback.currentDay = fallback.completedDay + 1; // 이전 완료 일차 다음 날부터 재시작
                Debug.LogWarning($"[Project I] Current 데이터 {(currentExists ? "손상" : "누락")} / 이유={currentReason} / 이전 일차 자동 복구={fallbackPath}", this); // 자동 폴백 이유 출력
                yield return RestoreSnapshotRoutine(fallback, true); // 정상 이전 일차 전체 상태 복구 후 새 Current 생성
                initialized = true; // 초기 복구 완료 표시
                yield break; // 새 게임 초기화 불필요
            }

            currentDay = Mathf.Max(1, currentDay); // 최초 새 게임 일차 보정
            CaptureRuntimeOfficeState(); // 현재 Office 초기 경제 상태 메모리 보관

            if (!SaveCurrentDayStart()) // 최초 Day 1 시작 상태 Current 저장 시도
            {
                Debug.LogWarning("[Project I] 최초 Current Snapshot 생성 실패 / ItemDefinition 누락 여부를 확인하세요.", this); // 초기 백업 실패 안내
            }

            initialized = true; // 디스크 백업이 없어도 런타임 저장 서비스 준비 완료
        }

        private IEnumerator CompleteCurrentDayRoutine() // 완료 일차 불변 Snapshot과 다음 날 Current 생성
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 여행 로더 조회

            if (loader != null && loader.IsTransitioning) // 맵 교체 중인지 확인
            {
                Debug.LogWarning("[Project I] 맵 이동 중에는 일차 Snapshot을 만들 수 없습니다.", this); // 불안정한 전환 상태 저장 방지
                yield break; // 저장 중단
            }

            CaptureRuntimeOfficeState(); // 로드된 Office가 있다면 최신 경제 상태 갱신

            if (!TryCaptureSnapshot(currentDay, currentDay, out DailySnapshotData completedSnapshot)) // 완료 일차 전체 상태 캡처
            {
                Debug.LogError($"[Project I] Day {currentDay} Snapshot 생성 실패 / 복구 정의가 없는 아이템이 존재합니다.", this); // 불완전 백업 생성 차단 안내
                yield break; // 기존 정상 백업 보호
            }

            if (!store.WriteImmutableDailySnapshot(completedSnapshot)) // 같은 일차를 절대 덮어쓰지 않는 불변 저장 시도
            {
                Debug.LogWarning($"[Project I] Day {currentDay} Snapshot이 이미 존재하거나 저장에 실패했습니다. 일차를 진행하지 않습니다.", this); // 중복 다음 날 진입 방지
                yield break; // 현재 일차 유지
            }

            string envelopeText = store.ReadEnvelopeTextForRemote(currentDay); // 서버 백업에 사용할 동일 검증 원문 조회

            if (remoteStore != null && !string.IsNullOrWhiteSpace(envelopeText)) // 실제 서버 구현이 연결됐는지 확인
            {
                remoteStore.Upload($"Day_{currentDay:000}", envelopeText); // 동일 일차 Snapshot 서버 업로드 요청
            }

            int completedDay = currentDay; // 완료 로그용 일차 저장
            currentDay++; // 다음 플레이 일차로 진행

            if (TryCaptureSnapshot(currentDay, completedDay, out DailySnapshotData nextDayStart)) // 동일 게임 상태를 다음 날 시작점으로 캡처
            {
                store.WriteCurrent(nextDayStart); // Current를 다음 날 시작 데이터로 교체
            }

            Debug.Log($"[Project I] 일차 Snapshot 저장 완료 / CompletedDay={completedDay} / NextDay={currentDay}", this); // 완료 결과 로그 출력
            yield return null; // Coroutine 정상 종료 프레임 반환
        }

        private bool TryCaptureSnapshot(int targetCurrentDay, int completedDay, out DailySnapshotData snapshot) // 전체 게임 상태를 하나의 데이터로 캡처
        {
            snapshot = new DailySnapshotData // 새 스냅샷 루트 생성
            {
                currentDay = Mathf.Max(1, targetCurrentDay), // 시작 일차 기록
                completedDay = Mathf.Max(0, completedDay), // 완료 일차 기록
                activeDestination = GetCurrentDestinationName(), // 현재 Office/TestDungeon 기록
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

        private IEnumerator RestoreSnapshotRoutine(DailySnapshotData snapshot, bool writeCurrentAfterRestore) // 전체 게임 상태를 동일 일차 시작점으로 복원
        {
            if (snapshot == null || restoreInProgress) // 복구 데이터와 중복 실행 확인
            {
                yield break; // 복구 중단
            }

            restoreInProgress = true; // 다른 저장·복구 요청 잠금
            PersistentMapLoader loader = PersistentMapLoader.Instance; // Persistent 여행 로더 조회
            TravelDestination destination = ParseDestination(snapshot.activeDestination); // 저장된 환경 목적지 해석

            if (loader != null) // Additive 환경 로더 존재 여부 확인
            {
                while (loader.IsTransitioning) // 현재 종 이동이 끝날 때까지 대기
                {
                    yield return null; // 다음 프레임 재확인
                }

                if (loader.CurrentDestination != destination) // 저장된 환경과 현재 환경이 다른지 확인
                {
                    yield return loader.LoadDestinationForRecovery(destination); // 연출 없이 복구 대상 환경으로 교체
                }
            }

            runtimeEconomy = CloneEconomy(snapshot.economy); // 저장된 경제 상태를 Persistent 메모리에 먼저 복원
            Day23SnapshotBridge.ClearInventoryForRestore(); // 빠른 슬롯의 기존 참조 제거
            Day23SnapshotBridge.ClearOfficeStorageForRestore(); // 단상 기존 참조 제거
            WorldItem[] oldItems = UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 실제 WorldItem 전체 조회

            foreach (WorldItem oldItem in oldItems) // 기존 런타임 아이템 순회
            {
                if (oldItem != null) // 유효 아이템 확인
                {
                    Destroy(oldItem.gameObject); // 현재 일차 상태를 제거해 Snapshot과 중복 방지
                }
            }

            yield return null; // Destroy 예약 반영 후 새 아이템 생성
            ItemRegistry.Reload(); // Editor 생성된 최신 ItemDefinition 캐시 갱신
            Scene persistentScene = gameObject.scene; // 저장 서비스가 속한 00_WagonPersistent 씬 조회
            Scene environmentScene = SceneManager.GetSceneByName(destination == TravelDestination.TestDungeon ? "02_TestDungeon" : "01_Office"); // 복구 대상 환경 씬 조회
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

                        item.transform.SetPositionAndRotation(wagonRoot.TransformPoint(data.position), wagonRoot.rotation * data.rotation); // Wagon 로컬 위치·회전 복원
                        break; // Cargo 복구 종료
                    default: // 일반 환경 WorldItem 복구
                        Scene worldScene = SceneManager.GetSceneByName(data.sceneName); // 저장 당시 씬 이름 우선 조회

                        if (!worldScene.IsValid() || !worldScene.isLoaded) // 원래 환경 씬이 현재 없는지 확인
                        {
                            worldScene = environmentScene; // 현재 복구 대상 환경 씬 사용
                        }

                        MoveRootToScene(itemObject, worldScene); // 실제 환경 씬 소속 복구
                        item.transform.SetPositionAndRotation(data.position, data.rotation); // 저장 당시 월드 위치·회전 복구
                        break; // World 복구 종료
                }
            }

            yield return null; // Trigger와 인벤토리 상태가 생성 아이템을 인식할 프레임 제공
            Day23SnapshotBridge.FinalizeInventorySelection(snapshot.selectedQuickSlot); // 저장 당시 선택 슬롯 화면 복구
            RestoreRuntimeOfficeState(); // 공동 자금·채무 상태 복구
            currentDay = Mathf.Max(1, snapshot.currentDay); // 저장된 시작 일차 적용

            if (failedItems == 0 && writeCurrentAfterRestore) // 전체 아이템 복구 성공 시에만 Current를 새 정상본으로 갱신
            {
                store.WriteCurrent(snapshot); // 복구된 일차 시작점 SHA-256 저장
            }

            if (failedItems > 0) // 일부 아이템 복구 실패 여부 확인
            {
                Debug.LogError($"[Project I] Snapshot 복구 부분 실패 / 실패 아이템={failedItems} / Current는 덮어쓰지 않았습니다.", this); // 정상 백업 보존 안내
            }
            else // 전체 복구 성공
            {
                Debug.Log($"[Project I] Snapshot 전체 복구 완료 / Day={currentDay} / Items={snapshot.items.Count}", this); // 정상 복구 결과 출력
            }

            restoreInProgress = false; // 저장·복구 잠금 해제
        }

        private IEnumerator WaitForMapLoaderReady() // Step2 PersistentMapLoader 초기 Additive 맵 준비 대기
        {
            float timeout = 15f; // 초기 씬 로드 무한 대기 방지 시간
            float elapsed = 0f; // 경과 시간 초기화

            while (elapsed < timeout) // 제한 시간 동안 준비 상태 검사
            {
                PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 맵 로더 조회

                if (loader != null && !loader.IsTransitioning) // 로더 존재 여부 확인
                {
                    Scene office = SceneManager.GetSceneByName("01_Office"); // 초기 Office 씬 조회
                    Scene dungeon = SceneManager.GetSceneByName("02_TestDungeon"); // 직접 복구용 Dungeon 씬 조회

                    if ((office.IsValid() && office.isLoaded) || (dungeon.IsValid() && dungeon.isLoaded)) // 환경 맵 하나가 준비됐는지 확인
                    {
                        yield break; // 저장·복구 시작 가능
                    }
                }

                elapsed += Time.unscaledDeltaTime; // 대기 시간 누적
                yield return null; // 다음 프레임 재확인
            }

            Debug.LogWarning("[Project I] Snapshot 초기화 대기 시간 초과 / 현재 씬 상태로 계속 진행합니다.", this); // 초기 맵 누락 진단
        }

        private string GetCurrentDestinationName() // 현재 Additive 환경 목적지를 저장 문자열로 변환
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 현재 맵 로더 조회
            return loader != null && loader.CurrentDestination == TravelDestination.TestDungeon ? "TestDungeon" : "Office"; // 안정 문자열 반환
        }

        private static TravelDestination ParseDestination(string value) // 저장 문자열을 여행 목적지 enum으로 복원
        {
            return string.Equals(value, "TestDungeon", StringComparison.OrdinalIgnoreCase) ? TravelDestination.TestDungeon : TravelDestination.Office; // 알 수 없는 값은 안전한 Office로 폴백
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
