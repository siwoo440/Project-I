using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(Rigidbody))] // 마차 이동에 Rigidbody 필수 지정
    public class VillageWagonTravelController : MonoBehaviour // 출발 레버 이후 마차 이동 담당
    {
        [Header("이동 설정")] // Inspector 마차 이동 설정 구분
        [SerializeField][Min(0.1f)] float moveSpeed = 4f; // 마차 초당 이동 거리
        [SerializeField][Min(1f)] float travelDistance = 25f; // 던전 선택 창이 열리는 이동 거리
        [SerializeField] Vector3 localTravelDirection = Vector3.back; // 마차 모델 기준 앞쪽 이동 방향
        [SerializeField] Transform passengerAnchor; // 이동 중 플레이어를 붙잡아 둘 부모 지점

        [Header("마을 UI")] // Inspector 마을 UI 설정 구분
        [SerializeField] VillageSettlementUI settlementUI; // 정산 창 상태 확인용 UI
        [SerializeField] VillageDungeonSelectionUI dungeonSelectionUI; // 도착 후 활성화할 던전 선택 UI

        Rigidbody wagonBody; // 마차 이동에 사용할 Rigidbody
        CampaignManager campaignManager; // 다음 탐험 출발 가능 여부 확인용 매니저

        PlayerController passengerController; // 이동 중 마차에 탑승한 플레이어
        PlayerInteractor passengerInteractor; // 탑승 플레이어 상호작용 컴포넌트
        PlayerCombat passengerCombat; // 탑승 플레이어 전투 컴포넌트
        CharacterController passengerCharacterController; // 탑승 플레이어 충돌 컨트롤러
        Transform originalPassengerParent; // 탑승 전 플레이어 부모 Transform

        bool playerControllerWasEnabled; // 이동 전 플레이어 이동 활성 상태
        bool interactorWasEnabled; // 이동 전 상호작용 활성 상태
        bool combatWasEnabled; // 이동 전 전투 활성 상태
        bool characterControllerWasEnabled; // 이동 전 CharacterController 활성 상태

        Vector3 targetPosition; // 마차가 도착할 최종 위치
        bool isMoving; // 현재 마차 이동 여부
        bool hasArrived; // 마차의 목적지 도착 여부

        public bool IsMoving => isMoving; // 외부에 현재 이동 여부 반환
        public bool HasArrived => hasArrived; // 외부에 도착 완료 여부 반환

        void Reset() // 컴포넌트 추가 시 기본 참조 자동 검색
        {
            Rigidbody foundBody = GetComponent<Rigidbody>(); // 같은 오브젝트의 Rigidbody 검색

            if (foundBody != null) // Rigidbody 존재 여부 확인
            {
                foundBody.isKinematic = true; // 물리 힘에 밀리지 않는 마차로 설정
                foundBody.useGravity = false; // 마차 중력 비활성화
            }

            settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 정산 UI 자동 검색
            dungeonSelectionUI = FindFirstObjectByType<VillageDungeonSelectionUI>(); // 던전 선택 UI 자동 검색
        }

        void Awake() // 마차 물리와 UI 초기화
        {
            wagonBody = GetComponent<Rigidbody>(); // 마차 Rigidbody 가져오기
            wagonBody.isKinematic = true; // 스크립트로 이동하는 Kinematic 상태 설정
            wagonBody.useGravity = false; // 중력에 의한 낙하 방지
            wagonBody.interpolation = RigidbodyInterpolation.Interpolate; // 마차 이동 화면 보간 적용
            wagonBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 이동 중 충돌 누락 감소

            campaignManager = CampaignManager.Instance; // 영구 캠페인 매니저 가져오기

            if (dungeonSelectionUI != null) // 던전 선택 UI 연결 여부 확인
            {
                dungeonSelectionUI.gameObject.SetActive(true); // UI GameObject 활성 상태 보장
                dungeonSelectionUI.enabled = true; // OnGUI 실행을 위해 컴포넌트 활성 상태 보장
                dungeonSelectionUI.LockSelection(); // 마차 도착 전 화면과 입력만 잠금
            }
            else // 던전 선택 UI 참조가 없는 경우
            {
                Debug.LogError("[VillageWagon] Dungeon Selection UI가 연결되지 않았습니다."); // Inspector 참조 누락 출력
            }
        }

        void OnValidate() // Inspector 값 변경 시 안전한 범위로 보정
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed); // 이동속도 최소값 보장
            travelDistance = Mathf.Max(1f, travelDistance); // 이동거리 최소값 보장

            if (localTravelDirection.sqrMagnitude < 0.001f) // 이동 방향이 0인지 확인
            {
                localTravelDirection = Vector3.back; // 기본 마차 앞쪽 방향으로 복구
            }
        }

        void FixedUpdate() // 일정한 물리 주기로 마차 이동
        {
            if (!isMoving) // 현재 이동 상태 확인
            {
                return; // 이동 처리 중단
            }

            Vector3 nextPosition = Vector3.MoveTowards( // 다음 마차 위치 계산
                wagonBody.position, // 현재 Rigidbody 위치 전달
                targetPosition, // 최종 목적지 전달
                moveSpeed * Time.fixedDeltaTime); // 한 물리 프레임의 이동 거리 전달

            wagonBody.MovePosition(nextPosition); // Rigidbody를 다음 위치로 이동

            if (Vector3.SqrMagnitude(targetPosition - nextPosition) <= 0.0001f) // 목적지 도달 여부 확인
            {
                wagonBody.position = targetPosition; // 마차를 정확한 최종 위치에 고정
                CompleteTravel(); // 이동 완료와 던전 선택 창 활성화 처리
            }
        }

        public string GetDeparturePrompt() // 현재 마차 상태에 맞는 상호작용 문구 반환
        {
            if (hasArrived) // 이미 목적지에 도착했는지 확인
            {
                return "던전 선택 중"; // 도착 완료 문구 반환
            }

            if (isMoving) // 마차가 이동 중인지 확인
            {
                return "마차 이동 중"; // 이동 중 문구 반환
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창이 열려 있는지 확인
            {
                return "먼저 마을 정산을 완료하세요"; // 정산 우선 안내 반환
            }

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                return "CampaignManager 연결 필요"; // 매니저 누락 안내 반환
            }

            if (!campaignManager.CanStartNextRun) // 다음 탐험 출발 가능 여부 확인
            {
                return campaignManager.GetDeadlineMessage(); // 캠페인 종료 또는 기한 안내 반환
            }

            return "[E] 마차 출발 레버 당기기"; // 정상 출발 상호작용 문구 반환
        }

        public bool TryStartTravel(PlayerInteractor interactor) // 플레이어 상호작용으로 마차 출발 시도
        {
            if (isMoving || hasArrived) // 이동 중이거나 이미 도착했는지 확인
            {
                return false; // 중복 출발 방지
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창 상태 확인
            {
                Debug.LogWarning("[VillageWagon] 정산을 완료한 뒤 출발할 수 있습니다."); // 정산 미완료 경고 출력
                return false; // 출발 중단
            }

            if (campaignManager == null) // 캠페인 매니저 연결 여부 확인
            {
                Debug.LogError("[VillageWagon] CampaignManager가 없습니다."); // 매니저 누락 오류 출력
                return false; // 출발 중단
            }

            if (!campaignManager.CanStartNextRun) // 다음 탐험 가능 여부 확인
            {
                Debug.LogWarning($"[VillageWagon] 출발 불가 — {campaignManager.GetDeadlineMessage()}"); // 출발 불가 원인 출력
                return false; // 출발 중단
            }

            if (dungeonSelectionUI == null) // 도착 후 사용할 선택 UI 연결 여부 확인
            {
                Debug.LogError("[VillageWagon] VillageDungeonSelectionUI가 연결되지 않았습니다."); // UI 누락 오류 출력
                return false; // 플레이어가 이동 후 갇히지 않도록 출발 중단
            }

            if (interactor == null) // 상호작용한 플레이어 존재 여부 확인
            {
                Debug.LogError("[VillageWagon] 출발을 요청한 PlayerInteractor가 없습니다."); // 플레이어 누락 오류 출력
                return false; // 출발 중단
            }

            passengerInteractor = interactor; // 상호작용한 플레이어 저장
            passengerController = interactor.GetComponent<PlayerController>(); // 플레이어 이동 컴포넌트 가져오기
            passengerCombat = interactor.GetComponent<PlayerCombat>(); // 플레이어 전투 컴포넌트 가져오기
            passengerCharacterController = interactor.GetComponent<CharacterController>(); // 플레이어 CharacterController 가져오기

            if (passengerController == null) // 플레이어 이동 컴포넌트 존재 여부 확인
            {
                Debug.LogError("[VillageWagon] 레버를 조작한 오브젝트에 PlayerController가 없습니다."); // 잘못된 상호작용 대상 출력
                return false; // 출발 중단
            }

            originalPassengerParent = passengerController.transform.parent; // 탑승 전 부모 Transform 저장
            playerControllerWasEnabled = passengerController.enabled; // 이동 활성 상태 저장
            interactorWasEnabled = passengerInteractor.enabled; // 상호작용 활성 상태 저장
            combatWasEnabled = passengerCombat != null && passengerCombat.enabled; // 전투 활성 상태 저장
            characterControllerWasEnabled = passengerCharacterController != null && passengerCharacterController.enabled; // 충돌 활성 상태 저장

            passengerController.enabled = false; // 이동 중 플레이어 직접 이동 차단
            passengerInteractor.enabled = false; // 이동 중 추가 상호작용 차단

            if (passengerCombat != null) // 전투 컴포넌트 존재 여부 확인
            {
                passengerCombat.enabled = false; // 이동 중 공격 차단
            }

            if (passengerCharacterController != null) // CharacterController 존재 여부 확인
            {
                passengerCharacterController.enabled = false; // 부모 이동 중 CharacterController 충돌 계산 중단
            }

            Transform rideParent = passengerAnchor != null ? passengerAnchor : transform; // 탑승 중 사용할 부모 지점 결정
            passengerController.transform.SetParent(rideParent, true); // 현재 위치를 유지하며 플레이어를 마차에 연결

            Vector3 worldDirection = transform.TransformDirection(localTravelDirection.normalized); // 로컬 진행 방향을 월드 방향으로 변환
            targetPosition = wagonBody.position + worldDirection * travelDistance; // 최종 마차 도착 위치 계산
            isMoving = true; // 마차 이동 상태 활성화

            Debug.Log($"[VillageWagon] 마차 출발 — 이동거리 {travelDistance:F1}m"); // 출발 결과 출력
            return true; // 정상 출발 성공 반환
        }

        void CompleteTravel() // 목적지 도착 후 플레이어 분리와 던전 선택 UI 활성화
        {
            isMoving = false; // 마차 이동 상태 종료
            hasArrived = true; // 목적지 도착 상태 저장

            if (passengerController != null) // 탑승 플레이어 존재 여부 확인
            {
                passengerController.transform.SetParent(originalPassengerParent, true); // 플레이어를 원래 부모로 복구
            }

            if (passengerCharacterController != null) // CharacterController 존재 여부 확인
            {
                passengerCharacterController.enabled = characterControllerWasEnabled; // 플레이어 충돌 컴포넌트 복구
            }

            Cursor.lockState = CursorLockMode.None; // 던전 선택 버튼 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시

            if (dungeonSelectionUI != null) // 던전 선택 UI 연결 여부 확인
            {
                dungeonSelectionUI.gameObject.SetActive(true); // 비활성화된 UI GameObject 활성화
                dungeonSelectionUI.enabled = true; // OnGUI 실행을 위해 컴포넌트 활성화
                dungeonSelectionUI.UnlockSelection(); // 선택 화면과 선택 입력 허용

                Debug.Log("[VillageWagon] Dungeon Selection UI 활성화 완료"); // UI 활성화 결과 출력
            }
            else // 던전 선택 UI 참조가 없는 경우
            {
                Debug.LogError("[VillageWagon] 도착했지만 Dungeon Selection UI 참조가 없습니다."); // 참조 누락 오류 출력
                RestorePassengerControls(); // 플레이어 조작 불가 상태 방지
            }

            Debug.Log("[VillageWagon] 목적지 도착 — 던전 선택 창을 엽니다."); // 이동 완료 출력
        }

        void RestorePassengerControls() // 이동 실패 시 플레이어 조작 상태 복구
        {
            if (passengerController != null) // 플레이어 이동 컴포넌트 존재 여부 확인
            {
                passengerController.enabled = playerControllerWasEnabled; // 기존 이동 활성 상태 복구
            }

            if (passengerInteractor != null) // 플레이어 상호작용 컴포넌트 존재 여부 확인
            {
                passengerInteractor.enabled = interactorWasEnabled; // 기존 상호작용 활성 상태 복구
            }

            if (passengerCombat != null) // 플레이어 전투 컴포넌트 존재 여부 확인
            {
                passengerCombat.enabled = combatWasEnabled; // 기존 전투 활성 상태 복구
            }
        }
    }
}