using ProjectI.Items; // 기존 빠른 슬롯·월드 아이템 기능 참조
using ProjectI.Wagon; // 마차 공통 적재·회수 영역 기능 참조
using UnityEngine; // 유니티 물리·카메라·컴포넌트 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerHealth))] // 기존 플레이어 사망 이벤트 연결 보장
    public sealed class PlayerDeathController : MonoBehaviour // 기존 Player 오브젝트를 물리 시체 상태로 전환
    {
        [SerializeField] private PlayerHealth health; // 기존 플레이어 체력 참조
        [SerializeField] private PlayerInventory inventory; // 사망 시 소지품 월드 드롭용 기존 인벤토리 참조
        [SerializeField] private CharacterController characterController; // 생존 이동 충돌체 참조
        [SerializeField] private Camera viewCamera; // 사망 후 몸을 보여줄 기존 1인칭 카메라 참조
        [SerializeField] private GameObject ragdollRoot; // 평소 비활성 상태로 Player 내부에 준비된 래그돌 루트
        [SerializeField] private Transform ragdollCenter; // 회수 판정과 사망 카메라 기준 골반 Transform
        [SerializeField] private MonoBehaviour[] liveBehavioursToDisable; // 사망 순간 정지할 이동·시점·전투·상호작용 기능
        [SerializeField] private float minimumPhysicsTime = 1.8f; // 털썩 쓰러지는 물리를 최소 유지할 시간
        [SerializeField] private float stillRequiredTime = 0.85f; // 충분히 정지했다고 판단할 연속 시간
        [SerializeField] private float maximumPhysicsTime = 6f; // 장시간 흔들림을 방지할 최대 래그돌 계산 시간
        [SerializeField] private float linearSleepThreshold = 0.12f; // 정지 판정 직선 속도 임계값
        [SerializeField] private float angularSleepThreshold = 0.85f; // 정지 판정 회전 속도 임계값
        [SerializeField] private Vector3 deathCameraOffset = new Vector3(0f, 1.35f, -2.6f); // 죽은 몸을 확인하는 카메라 상대 위치
        private Rigidbody[] ragdollBodies; // 래그돌 전체 물리 몸체 목록
        private bool isDead; // 현재 사망 상태
        private bool isRagdollFrozen; // 안정화 후 물리 계산 정지 상태
        private bool isRecovered; // 마차 공통 CargoArea 회수 상태
        private WagonCargoArea recoveredArea; // 현재 죽은 플레이어를 확보한 마차 공통 영역
        private float physicsElapsed; // 사망 후 래그돌 물리 경과 시간
        private float stillElapsed; // 저속 상태 연속 유지 시간
        private Vector3 deathPosition; // 실제 사망 위치 기록

        public bool IsDead => isDead; // 외부 원정 시스템용 사망 여부 공개
        public bool IsRagdollFrozen => isRagdollFrozen; // 진단용 물리 절전 여부 공개
        public bool IsRecovered => isRecovered; // 마차 시체 회수 여부 공개
        public WagonCargoArea RecoveredArea => recoveredArea; // 현재 죽은 플레이어 확보 마차 공개
        public Vector3 DeathPosition => deathPosition; // 사망 지점 공개
        public Transform RagdollCenter => ragdollCenter; // 마차 회수 판정 기준 Transform 공개

        private void Awake() // 사망 시스템 런타임 참조 초기화
        {
            ResolveReferences(); // 기존 Player 구성 요소 자동 연결
            CacheRagdollBodies(); // 비활성 래그돌 Rigidbody 목록 확보
            PrepareAliveState(); // 시작 시 래그돌을 숨긴 생존 상태 유지
        }

        private void OnEnable() // 체력 사망 이벤트 구독
        {
            ResolveReferences(); // 씬 직렬화 누락 참조 보정

            if (health != null) // 기존 PlayerHealth 존재 확인
            {
                health.Died -= HandleDeath; // 중복 이벤트 구독 방지
                health.Died += HandleDeath; // HP 0 사망 이벤트 연결
            }
        }

        private void Start() // 이미 사망 상태로 시작하는 특수 경우 보정
        {
            if (health != null && health.IsDead) // 시작 시 체력이 이미 0인지 확인
            {
                HandleDeath(); // 동일 사망 전환 실행
            }
        }

        private void OnDisable() // 체력 이벤트 구독 해제
        {
            if (health != null) // 체력 참조 존재 확인
            {
                health.Died -= HandleDeath; // 비활성화 시 사망 이벤트 해제
            }
        }

        private void FixedUpdate() // 래그돌 안정화와 물리 절전 판정
        {
            if (!isDead || isRagdollFrozen || ragdollBodies == null || ragdollBodies.Length == 0) // 물리 판정 필요 여부 확인
            {
                return; // 생존·절전·래그돌 누락 상태에서는 중단
            }

            physicsElapsed += Time.fixedDeltaTime; // 사망 후 물리 경과 시간 누적

            if (physicsElapsed < minimumPhysicsTime) // 최소 털썩 동작 시간이 지나기 전인지 확인
            {
                return; // 너무 일찍 시체 물리를 고정하지 않음
            }

            bool allStill = true; // 모든 신체 부위 정지 여부 초기화

            foreach (Rigidbody body in ragdollBodies) // 래그돌 Rigidbody 전체 순회
            {
                if (body == null || body.isKinematic) // 유효 동적 Rigidbody 여부 확인
                {
                    continue; // 검사 불필요한 몸체 건너뜀
                }

                if (body.linearVelocity.sqrMagnitude > linearSleepThreshold * linearSleepThreshold || body.angularVelocity.sqrMagnitude > angularSleepThreshold * angularSleepThreshold) // 직선·회전 속도 임계값 초과 여부 확인
                {
                    allStill = false; // 아직 움직이는 신체 부위가 있음을 기록
                    break; // 추가 검사 불필요
                }
            }

            stillElapsed = allStill ? stillElapsed + Time.fixedDeltaTime : 0f; // 연속 정지 시간 누적 또는 초기화

            if (stillElapsed >= stillRequiredTime || physicsElapsed >= maximumPhysicsTime) // 충분히 멈췄거나 최대 물리 시간이 지났는지 확인
            {
                FreezeRagdoll(); // 시체 Rigidbody를 Kinematic으로 전환하여 불필요한 물리 계산 제거
            }
        }

        private void LateUpdate() // 사망 후 기존 카메라를 쓰러진 몸 관찰 위치로 부드럽게 이동
        {
            if (!isDead || viewCamera == null || ragdollCenter == null) // 사망 카메라 적용 조건 확인
            {
                return; // 생존 또는 참조 누락 상태에서는 기존 1인칭 카메라 유지
            }

            Vector3 worldOffset = transform.rotation * deathCameraOffset; // 플레이어 사망 방향을 기준으로 카메라 오프셋 계산
            Vector3 desiredPosition = ragdollCenter.position + worldOffset; // 쓰러진 몸 뒤쪽·위쪽 목표 카메라 위치 계산
            Vector3 lookTarget = ragdollCenter.position + Vector3.up * 0.30f; // 몸 중심보다 약간 위를 카메라 시선 대상으로 지정
            Quaternion desiredRotation = Quaternion.LookRotation((lookTarget - desiredPosition).normalized, Vector3.up); // 몸을 바라보는 목표 회전 계산
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desiredPosition, 1f - Mathf.Exp(-8f * Time.deltaTime)); // 카메라 위치를 급격한 순간이동 없이 이동
            viewCamera.transform.rotation = Quaternion.Slerp(viewCamera.transform.rotation, desiredRotation, 1f - Mathf.Exp(-10f * Time.deltaTime)); // 사망 몸 방향으로 카메라 회전 보간
        }

        public void Configure(PlayerHealth targetHealth, PlayerInventory targetInventory, CharacterController targetController, Camera targetCamera, GameObject targetRagdollRoot, Transform targetRagdollCenter, MonoBehaviour[] targetLiveBehaviours) // Day22 에디터 자동 설정용 참조 지정
        {
            health = targetHealth; // 기존 체력 참조 저장
            inventory = targetInventory; // 기존 인벤토리 참조 저장
            characterController = targetController; // 기존 CharacterController 참조 저장
            viewCamera = targetCamera; // 기존 카메라 참조 저장
            ragdollRoot = targetRagdollRoot; // Player 내부 래그돌 루트 저장
            ragdollCenter = targetRagdollCenter; // 골반 기준 Transform 저장
            liveBehavioursToDisable = targetLiveBehaviours; // 사망 시 정지할 기존 기능 목록 저장
            CacheRagdollBodies(); // 새 래그돌 구조 기준 Rigidbody 목록 갱신
            PrepareAliveState(); // Edit Mode 구성 직후 래그돌 비활성 상태 보장
        }

        public void WakeRagdoll() // 향후 동료가 시체를 다시 이동할 때 물리 활성화
        {
            if (!isDead) // 생존 플레이어 여부 확인
            {
                return; // 생존 상태에는 래그돌 활성화 금지
            }

            CacheRagdollBodies(); // 최신 Rigidbody 목록 확보

            foreach (Rigidbody body in ragdollBodies) // 전체 신체 Rigidbody 순회
            {
                if (body == null) // 제거된 Rigidbody 확인
                {
                    continue; // 다음 몸체 검사
                }

                body.isKinematic = false; // 외부 힘과 중력 반응 복구
                body.detectCollisions = true; // 시체 충돌 계산 복구
                body.WakeUp(); // Unity Rigidbody 절전 상태 해제
            }

            isRagdollFrozen = false; // 물리 활성 상태 기록
            physicsElapsed = 0f; // 새 이동 이후 안정화 타이머 초기화
            stillElapsed = 0f; // 정지 연속 시간 초기화
        }

        public void FreezeRagdoll() // 안정화된 시체의 지속 물리 계산 제거
        {
            CacheRagdollBodies(); // 최신 Rigidbody 목록 확보

            foreach (Rigidbody body in ragdollBodies) // 전체 신체 Rigidbody 순회
            {
                if (body == null) // 제거된 Rigidbody 확인
                {
                    continue; // 다음 몸체 검사
                }

                if (!body.isKinematic) // 동적 Rigidbody인지 확인
                {
                    body.linearVelocity = Vector3.zero; // 남은 직선 속도 제거
                    body.angularVelocity = Vector3.zero; // 남은 회전 속도 제거
                }

                body.isKinematic = true; // 시체 위치를 유지하면서 물리 시뮬레이션 비용 제거
                body.detectCollisions = true; // 다른 플레이어 Ray·영역 감지는 유지
                body.Sleep(); // Rigidbody 절전 상태 명시
            }

            isRagdollFrozen = true; // 물리 고정 상태 기록
        }

        public void SetRecovered(WagonCargoArea area, bool recovered) // 마차 공통 CargoArea에서 죽은 플레이어 회수 상태 갱신
        {
            isRecovered = recovered && area != null; // 유효한 마차 영역이 있을 때만 회수 상태 활성화
            recoveredArea = isRecovered ? area : null; // 회수 해제 시 마차 참조 제거
        }

        private void HandleDeath() // PlayerHealth.Died를 실제 Alive→Dead 전환으로 처리
        {
            if (isDead) // 중복 사망 이벤트 여부 확인
            {
                return; // 한 번만 사망 전환 실행
            }

            isDead = true; // 현재 Player를 사망 상태로 기록
            isRecovered = false; // 새 사망 시 기존 회수 상태 초기화
            recoveredArea = null; // 기존 회수 마차 참조 초기화
            deathPosition = transform.position; // 사망 순간 Player 위치 저장
            DropInventoryItems(); // 기존 빠른 슬롯 아이템을 시체 주변 월드 오브젝트로 복귀
            DisableLiveControls(); // 이동·공격·상호작용 등 살아있는 플레이어 기능 정지
            ActivateRagdoll(); // Player 내부 래그돌 물리 활성화
        }

        private void DropInventoryItems() // 사망자의 빠른 슬롯 전체를 월드에 떨어뜨림
        {
            if (inventory == null) // 기존 PlayerInventory 존재 여부 확인
            {
                return; // 소지품 처리 불필요
            }

            int scatterIndex = 0; // 아이템 분산 배치 순번 초기화

            if (inventory.SelectedItem != null) // 현재 손에 든 선택 아이템 존재 여부 확인
            {
                WorldItem droppedSelected = inventory.SelectedItem; // bool 반환 DropSelectedItem 호출 전에 실제 선택 아이템 참조 보존
                bool selectedDropped = inventory.DropSelectedItem(); // 양손 슬롯 잠금을 먼저 해제하면서 현재 아이템 드롭

                if (selectedDropped) // 실제 월드 드롭 성공 여부 확인
                {
                    ScatterDroppedItem(droppedSelected, scatterIndex++); // 시체 주변으로 선택 아이템 분산
                }
            }

            for (int index = 0; index < inventory.SlotCount; index++) // 남은 빠른 슬롯 전체 순회
            {
                WorldItem itemToDrop = inventory.GetItem(index); // bool 반환 DropSelectedItem 호출 전에 현재 슬롯 실제 아이템 참조 보존

                if (itemToDrop == null) // 현재 슬롯 아이템 존재 여부 확인
                {
                    continue; // 빈 슬롯 건너뜀
                }

                if (!inventory.SelectSlot(index)) // 현재 슬롯을 선택하여 기존 Drop 경로 사용 가능한지 확인
                {
                    continue; // 선택 실패 슬롯은 다음 검사로 이동
                }

                bool itemDropped = inventory.DropSelectedItem(); // 기존 PlayerCarryController를 통해 월드 물리 상태로 복귀

                if (itemDropped) // 실제 월드 드롭 성공 여부 확인
                {
                    ScatterDroppedItem(itemToDrop, scatterIndex++); // 시체 주변에 겹치지 않도록 분산
                }
            }
        }

        private void ScatterDroppedItem(WorldItem item, int index) // 사망 위치 주변으로 월드 아이템을 짧게 흩뿌림
        {
            if (item == null) // 드롭 실패 아이템 확인
            {
                return; // 분산 처리 중단
            }

            float angle = index * 137.5f; // 여러 아이템이 같은 위치에 겹치지 않는 황금각 분산 계산
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward; // 현재 순번의 수평 분산 방향 계산
            float distance = 0.42f + (index % 3) * 0.16f; // 슬롯 순번별 작은 반경 차이 적용
            Vector3 targetPosition = deathPosition + Vector3.up * 0.28f + direction * distance; // 시체 주변 실제 아이템 배치 위치 계산

            if (item.Body != null) // WorldItem Rigidbody 존재 확인
            {
                item.Body.position = targetPosition; // Rigidbody 기준으로 사망 위치 주변 이동
                item.Body.rotation = Quaternion.Euler(0f, angle, 0f); // 분산 방향에 맞는 수평 회전 적용
                item.Body.AddForce(direction * 0.55f + Vector3.up * 0.20f, ForceMode.VelocityChange); // 약한 힘으로 자연스럽게 바닥에 흩어지도록 처리
            }
            else // 예외적으로 Rigidbody 캐시가 없는 경우
            {
                item.transform.position = targetPosition; // Transform 기준 위치 보정
            }
        }

        private void DisableLiveControls() // 생존 상태에서만 필요한 기능 정지
        {
            if (liveBehavioursToDisable != null) // 에디터가 연결한 기존 기능 목록 존재 확인
            {
                foreach (MonoBehaviour behaviour in liveBehavioursToDisable) // 비활성화 대상 순회
                {
                    if (behaviour == null || behaviour == this) // 유효하지 않거나 자기 자신인지 확인
                    {
                        continue; // 다음 기능 검사
                    }

                    behaviour.enabled = false; // 이동·시점·전투·상호작용 기능 정지
                }
            }

            if (characterController != null) // 기존 CharacterController 존재 확인
            {
                characterController.enabled = false; // 루트 캡슐 이동 충돌을 제거하여 래그돌과 충돌하지 않게 처리
            }
        }

        private void ActivateRagdoll() // 기존 Player 내부의 비활성 래그돌을 실제 시체로 전환
        {
            if (ragdollRoot == null) // 래그돌 구조 존재 여부 확인
            {
                Debug.LogError("[Project I] PlayerDeathController에 DeathRagdoll이 연결되지 않았습니다.", this); // 구성 누락 오류 출력
                return; // 래그돌 전환 중단
            }

            ragdollRoot.transform.localPosition = Vector3.zero; // 플레이어 루트 기준 초기 래그돌 위치 복구
            ragdollRoot.transform.localRotation = Quaternion.identity; // 사망 순간 Player 방향을 그대로 사용하는 회전 복구
            ragdollRoot.SetActive(true); // 평소 숨겨둔 몸체 렌더러·Collider 활성화
            CacheRagdollBodies(); // 활성화된 Rigidbody 목록 재확인

            foreach (Rigidbody body in ragdollBodies) // 모든 신체 Rigidbody 순회
            {
                if (body == null) // 유효 몸체 여부 확인
                {
                    continue; // 다음 몸체 검사
                }

                body.detectCollisions = true; // 바닥·벽과 래그돌 충돌 활성화
                body.isKinematic = false; // 중력과 관절 물리 반응 활성화
                body.linearVelocity = Vector3.zero; // 시작 직선 속도 초기화
                body.angularVelocity = Vector3.zero; // 시작 회전 속도 초기화
                body.WakeUp(); // 즉시 물리 계산 시작
            }

            if (ragdollCenter != null) // 골반 Rigidbody 존재 가능 여부 확인
            {
                Rigidbody centerBody = ragdollCenter.GetComponent<Rigidbody>(); // 골반 Rigidbody 조회

                if (centerBody != null && !centerBody.isKinematic) // 동적 골반 Rigidbody 확인
                {
                    Vector3 slumpImpulse = (-transform.forward * 0.48f) + (transform.right * 0.16f); // 완벽한 직립 정지를 피하는 약한 뒤·옆 방향 힘 생성
                    centerBody.AddForce(slumpImpulse, ForceMode.VelocityChange); // 몸에 힘이 풀리듯 자연스럽게 무너지도록 작은 초기 힘 적용
                }
            }

            physicsElapsed = 0f; // 래그돌 물리 시간 초기화
            stillElapsed = 0f; // 정지 판정 시간 초기화
            isRagdollFrozen = false; // 현재 동적 물리 상태 기록
        }

        private void PrepareAliveState() // 플레이 시작 시 사망 전용 몸체를 숨김
        {
            if (ragdollRoot != null && !isDead) // 생존 상태의 래그돌 루트 존재 확인
            {
                ragdollRoot.SetActive(false); // 1인칭 생존 중 Primitive 몸체 숨김
            }

            isRagdollFrozen = false; // 초기 물리 절전 상태 해제
            isRecovered = false; // 초기 시체 회수 상태 해제
            recoveredArea = null; // 초기 회수 마차 참조 제거
        }

        private void ResolveReferences() // 기존 Player 컴포넌트 자동 조회
        {
            if (health == null) // 체력 참조 누락 확인
            {
                health = GetComponent<PlayerHealth>(); // 기존 PlayerHealth 조회
            }

            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                inventory = GetComponent<PlayerInventory>(); // 기존 PlayerInventory 조회
            }

            if (characterController == null) // CharacterController 참조 누락 확인
            {
                characterController = GetComponent<CharacterController>(); // 기존 이동 충돌체 조회
            }

            if (viewCamera == null) // 사망 카메라 참조 누락 확인
            {
                viewCamera = GetComponentInChildren<Camera>(true); // 기존 View 카메라 자동 조회
            }

            if (ragdollRoot == null) // 래그돌 루트 참조 누락 확인
            {
                Transform existingRoot = transform.Find("DeathRagdoll"); // Player 자식 래그돌 루트 검색
                ragdollRoot = existingRoot == null ? null : existingRoot.gameObject; // 검색 결과 GameObject 저장
            }

            if (ragdollCenter == null && ragdollRoot != null) // 골반 기준 Transform 누락 확인
            {
                ragdollCenter = ragdollRoot.transform.Find("Pelvis"); // DeathRagdoll의 골반 Transform 자동 연결
            }
        }

        private void CacheRagdollBodies() // 비활성 자식을 포함한 래그돌 Rigidbody 목록 갱신
        {
            ragdollBodies = ragdollRoot == null ? System.Array.Empty<Rigidbody>() : ragdollRoot.GetComponentsInChildren<Rigidbody>(true); // 현재 래그돌 전체 Rigidbody 캐시
        }

        private void OnValidate() // 사망 물리 설정값 안전 범위 보정
        {
            minimumPhysicsTime = Mathf.Max(0.1f, minimumPhysicsTime); // 최소 물리 시간 양수 보장
            stillRequiredTime = Mathf.Max(0.1f, stillRequiredTime); // 정지 판정 시간 양수 보장
            maximumPhysicsTime = Mathf.Max(minimumPhysicsTime, maximumPhysicsTime); // 최대 물리 시간이 최소 시간보다 짧지 않게 보정
            linearSleepThreshold = Mathf.Max(0.01f, linearSleepThreshold); // 직선 속도 임계값 최소 보정
            angularSleepThreshold = Mathf.Max(0.05f, angularSleepThreshold); // 회전 속도 임계값 최소 보정
        }
    }
}
