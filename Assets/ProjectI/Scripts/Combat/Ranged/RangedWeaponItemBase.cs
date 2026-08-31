using ProjectI.Items; // 기존 빠른 슬롯과 사용 아이템 기능 참조
using ProjectI.Player; // 플레이어 입력·이동 기능 참조
using UnityEngine; // 유니티 카메라·Transform 기능 참조

namespace ProjectI.Combat.Ranged // 원거리 전투 기능 네임스페이스
{
    [RequireComponent(typeof(WorldItem))] // 기존 월드 획득·빠른 슬롯 기능 필수 지정
    public abstract class RangedWeaponItemBase : MonoBehaviour, IUsableItem // 석궁·리볼버 공통 조준·재장전 표현 기반
    {
        [SerializeField] private Transform visualPivot; // 조준과 장전 모션을 적용할 무기 시각 루트
        [SerializeField] private Vector3 aimPositionOffset; // 기본 자세에서 조준 시 추가 이동량
        [SerializeField] private Vector3 aimEulerOffset; // 기본 자세에서 조준 시 추가 회전량
        [SerializeField] private float aimFieldOfView = 50f; // 조준 시 카메라 시야각
        [SerializeField] private float aimTransitionSpeed = 10f; // 조준 자세 전환 속도
        [SerializeField] private float zoomSpeed = 90f; // 카메라 시야각 전환 속도
        [SerializeField] private float aimMovementMultiplier = 0.65f; // 조준 중 이동 속도 배율
        [SerializeField] private float reloadMovementMultiplier = 0.55f; // 장전 중 이동 속도 배율
        private WorldItem worldItem; // 현재 원거리 무기의 월드 아이템 기능
        private PlayerInventory inventory; // 현재 소유 플레이어 인벤토리
        private PlayerInputReader inputReader; // 현재 소유 플레이어 입력 래퍼
        private PlayerMovement movement; // 현재 소유 플레이어 이동 기능
        private Camera aimCamera; // 현재 1인칭 조준 카메라
        private float defaultFieldOfView = 70f; // 조준 해제 시 복구할 기본 시야각
        private bool defaultFovCaptured; // 기본 시야각 저장 여부
        private bool wasHeld; // 이전 프레임 무기 장착 여부
        private bool movementRestricted; // 원거리 무기가 현재 이동 제한을 소유하는지 여부
        private bool isReloading; // 현재 재장전 모션 진행 여부
        private float reloadDuration; // 현재 재장전 총 시간
        private float reloadRemaining; // 현재 재장전 남은 시간

        public event System.Action StateChanged; // 조준·장전·탄약 상태 변경 외부 전달

        public bool IsAiming { get; private set; } // 현재 우클릭 조준 상태 공개
        public bool IsReloading => isReloading; // 현재 재장전 상태 공개
        public float ReloadDuration => reloadDuration; // 현재 재장전 총 시간 공개
        public float ReloadRemaining => reloadRemaining; // 현재 재장전 남은 시간 공개
        public float ReloadProgress => !isReloading || reloadDuration <= 0f ? (isReloading ? 0f : 1f) : Mathf.Clamp01(1f - (reloadRemaining / reloadDuration)); // 재장전 진행률 공개
        public float AimFieldOfView => aimFieldOfView; // F1 진단용 조준 FOV 공개
        public Transform VisualPivot => visualPivot; // Validator용 시각 루트 공개
        protected WorldItem WorldItem => worldItem; // 파생 무기용 WorldItem 참조 공개
        protected PlayerInventory Inventory => inventory; // 파생 무기용 인벤토리 참조 공개
        protected PlayerInputReader InputReader => inputReader; // 파생 무기용 입력 참조 공개
        protected PlayerMovement Movement => movement; // 파생 무기용 이동 참조 공개
        protected Camera AimCamera => aimCamera; // 파생 무기용 조준 카메라 공개
        protected GameObject InstigatorObject => inventory == null ? null : inventory.gameObject; // Damage Pipeline 공격 주체 공개

        protected virtual void Awake() // 원거리 무기 공통 초기화
        {
            worldItem = GetComponent<WorldItem>(); // 같은 오브젝트의 WorldItem 참조 획득
        }

        protected virtual void Update() // 조준·장전·표현 공통 프레임 처리
        {
            bool held = worldItem != null && worldItem.IsHeld; // 현재 빠른 슬롯에서 실제로 들고 있는지 확인

            if (held) // 장착된 원거리 무기 여부 확인
            {
                ResolvePlayerReferences(); // 플레이어 입력·이동·카메라 참조 확보
            }

            if (!held) // 현재 장착되지 않은 경우 확인
            {
                if (wasHeld) // 직전 프레임까지 장착되어 있었는지 확인
                {
                    RestorePresentation(); // 조준 FOV와 이동 제한 즉시 복구
                }

                wasHeld = false; // 비장착 상태 저장
                return; // 비장착 무기 프레임 처리 생략
            }

            wasHeld = true; // 현재 장착 상태 저장
            bool targetAim = !isReloading && inputReader != null && inputReader.AimHeld; // 장전 중이 아닐 때만 우클릭 조준 허용

            if (targetAim != IsAiming) // 조준 상태 변경 여부 확인
            {
                IsAiming = targetAim; // 새 조준 상태 저장
                StateChanged?.Invoke(); // F1 등 외부에 조준 상태 변경 전달
            }

            if (inputReader != null && inputReader.ReloadPressed && !isReloading) // R 재장전 입력 여부 확인
            {
                TryStartReload(); // 무기별 재장전 시작 시도
            }

            TickReload(); // 재장전 시간과 완료 처리
            ApplyAimAndReloadPose(); // 조준·장전 시각 자세 적용
            ApplyCameraZoom(); // 우클릭 조준 시 FOV 확대 적용
            ApplyMovementRestriction(); // 조준·장전 중 이동 속도와 달리기 제한 적용
            TickWeaponSpecific(); // 무기별 탄퍼짐·시각 부품 등 추가 갱신
        }

        protected virtual void OnDisable() // 원거리 무기 비활성화 처리
        {
            RestorePresentation(); // 카메라와 이동 제한 기본값 복구
        }

        public bool CanUse(PlayerInventory ownerInventory) // 좌클릭 사용 가능 여부 반환
        {
            ResolvePlayerReferences(ownerInventory); // 사용을 요청한 플레이어 참조 확보
            return worldItem != null && worldItem.IsHeld && !isReloading && CanFire(); // 실제 장착·비장전·무기별 발사 가능 조건 반환
        }

        public void Use(PlayerInventory ownerInventory) // 기존 좌클릭 Use 입력으로 원거리 발사 실행
        {
            ResolvePlayerReferences(ownerInventory); // 사용 플레이어 참조 확보

            if (!CanUse(ownerInventory)) // 현재 발사 가능 여부 재확인
            {
                return; // 발사 불가 상태에서는 처리 중단
            }

            Fire(); // 무기별 실제 발사 처리
            StateChanged?.Invoke(); // 탄약·발사 상태 변경 외부 전달
        }

        public void ConfigureCommon(Transform targetVisualPivot, Vector3 targetAimPositionOffset, Vector3 targetAimEulerOffset, float targetAimFov, float transitionSpeed, float targetZoomSpeed, float targetAimMovementMultiplier, float targetReloadMovementMultiplier) // Day16 자동 Setup용 공통 조준 설정
        {
            visualPivot = targetVisualPivot; // 시각 루트 저장
            aimPositionOffset = targetAimPositionOffset; // 조준 이동량 저장
            aimEulerOffset = targetAimEulerOffset; // 조준 회전량 저장
            aimFieldOfView = Mathf.Clamp(targetAimFov, 20f, 80f); // 조준 FOV 안전 범위 저장
            aimTransitionSpeed = Mathf.Max(1f, transitionSpeed); // 자세 전환 속도 최소값 보정
            zoomSpeed = Mathf.Max(10f, targetZoomSpeed); // 줌 속도 최소값 보정
            aimMovementMultiplier = Mathf.Clamp(targetAimMovementMultiplier, 0.1f, 1f); // 조준 이동 속도 범위 보정
            reloadMovementMultiplier = Mathf.Clamp(targetReloadMovementMultiplier, 0.1f, 1f); // 장전 이동 속도 범위 보정
        }

        protected bool BeginReload(float duration) // 파생 무기의 재장전 타이머 시작
        {
            if (isReloading) // 이미 장전 중인지 확인
            {
                return false; // 중복 장전 시작 차단
            }

            isReloading = true; // 재장전 상태 활성화
            IsAiming = false; // 장전 시작 시 조준 즉시 해제
            reloadDuration = Mathf.Max(0.1f, duration); // 장전 총 시간 최소값 보정
            reloadRemaining = reloadDuration; // 장전 남은 시간을 전체 시간으로 초기화
            OnReloadStarted(); // 파생 무기 장전 시작 시각 처리 호출
            StateChanged?.Invoke(); // 장전 시작 상태 외부 전달
            return true; // 재장전 시작 성공 반환
        }

        protected virtual void TickWeaponSpecific() // 파생 무기 프레임 처리 확장 지점
        {
        }

        protected virtual void OnReloadStarted() // 파생 무기 장전 시작 처리 확장 지점
        {
        }

        protected virtual Vector3 EvaluateReloadPosition(float progress) // 파생 무기 장전 중 추가 위치 오프셋 계산
        {
            return Vector3.zero; // 기본 추가 위치 없음 반환
        }

        protected virtual Vector3 EvaluateReloadEuler(float progress) // 파생 무기 장전 중 추가 회전 계산
        {
            return Vector3.zero; // 기본 추가 회전 없음 반환
        }

        protected abstract bool CanFire(); // 무기별 현재 발사 가능 여부 반환
        protected abstract void Fire(); // 무기별 실제 발사 처리
        protected abstract bool TryStartReload(); // 무기별 R 재장전 시작 조건 처리
        protected abstract void CompleteReload(); // 무기별 재장전 완료 탄약 처리

        protected Vector3 ApplySpread(Vector3 forward, float spreadDegrees) // 시선 전방에 각도 기반 무작위 탄퍼짐 적용
        {
            if (spreadDegrees <= 0.001f || aimCamera == null) // 탄퍼짐이 없거나 카메라가 없는지 확인
            {
                return forward.normalized; // 원본 전방 방향 반환
            }

            Vector2 random = Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad); // 원형 랜덤 탄퍼짐 크기 계산
            Vector3 direction = forward + (aimCamera.transform.right * random.x) + (aimCamera.transform.up * random.y); // 카메라 좌우·상하 축으로 퍼짐 방향 합성
            return direction.normalized; // 최종 발사 방향 정규화 반환
        }

        private void TickReload() // 공통 재장전 시간 처리
        {
            if (!isReloading) // 현재 재장전 상태 여부 확인
            {
                return; // 재장전 프레임 처리 생략
            }

            reloadRemaining = Mathf.Max(0f, reloadRemaining - Time.deltaTime); // 남은 장전 시간 감소

            if (reloadRemaining > 0f) // 아직 장전 시간이 남았는지 확인
            {
                return; // 완료 처리 대기
            }

            isReloading = false; // 재장전 상태 종료
            CompleteReload(); // 무기별 탄약 실제 충전 처리
            StateChanged?.Invoke(); // 재장전 완료 상태 외부 전달
        }

        private void ApplyAimAndReloadPose() // 시각 루트 조준·장전 Transform 적용
        {
            if (visualPivot == null) // 시각 루트 누락 확인
            {
                return; // 자세 적용 중단
            }

            Vector3 targetPosition = IsAiming ? aimPositionOffset : Vector3.zero; // 조준 여부에 따른 기본 목표 위치 계산
            Vector3 targetEuler = IsAiming ? aimEulerOffset : Vector3.zero; // 조준 여부에 따른 기본 목표 회전 계산

            if (isReloading) // 재장전 모션 진행 여부 확인
            {
                targetPosition += EvaluateReloadPosition(ReloadProgress); // 무기별 장전 추가 이동 적용
                targetEuler += EvaluateReloadEuler(ReloadProgress); // 무기별 장전 추가 회전 적용
            }

            float blend = 1f - Mathf.Exp(-aimTransitionSpeed * Time.deltaTime); // 프레임률 독립 지수 보간량 계산
            visualPivot.localPosition = Vector3.Lerp(visualPivot.localPosition, targetPosition, blend); // 시각 루트 위치 부드럽게 이동
            visualPivot.localRotation = Quaternion.Slerp(visualPivot.localRotation, Quaternion.Euler(targetEuler), blend); // 시각 루트 회전 부드럽게 이동
        }

        private void ApplyCameraZoom() // 조준 시 카메라 FOV 변경
        {
            if (aimCamera == null) // 조준 카메라 누락 확인
            {
                return; // FOV 처리 중단
            }

            CaptureDefaultFov(); // 최초 기본 FOV 저장 보장
            float targetFov = IsAiming ? aimFieldOfView : defaultFieldOfView; // 조준 여부에 따른 목표 FOV 선택
            aimCamera.fieldOfView = Mathf.MoveTowards(aimCamera.fieldOfView, targetFov, zoomSpeed * Time.deltaTime); // 시야각 부드럽게 확대·복구
        }

        private void ApplyMovementRestriction() // 조준·장전 중 플레이어 이동 제한 적용
        {
            if (movement == null) // 플레이어 이동 기능 누락 확인
            {
                return; // 이동 제한 처리 중단
            }

            if (isReloading) // 재장전 중인지 확인
            {
                movement.SetExternalMovementModifier(reloadMovementMultiplier, false); // 장전 중 느린 이동과 달리기 차단 적용
                movementRestricted = true; // 이동 제한 소유 상태 기록
                return; // 조준 이동 제한보다 장전 제한 우선 적용
            }

            if (IsAiming) // 조준 상태 여부 확인
            {
                movement.SetExternalMovementModifier(aimMovementMultiplier, false); // 조준 중 이동 감속과 달리기 차단 적용
                movementRestricted = true; // 이동 제한 소유 상태 기록
                return; // 조준 제한 적용 완료
            }

            if (movementRestricted) // 이전 프레임 원거리 이동 제한을 사용했는지 확인
            {
                movement.ResetExternalMovementModifier(); // 평상시 이동 속도와 달리기 허용 복구
                movementRestricted = false; // 이동 제한 소유 상태 해제
            }
        }

        private void ResolvePlayerReferences(PlayerInventory ownerInventory = null) // 현재 소유 플레이어 시스템 참조 확보
        {
            if (ownerInventory != null) // 사용 호출에서 명시 인벤토리가 전달됐는지 확인
            {
                inventory = ownerInventory; // 현재 소유 인벤토리 저장
            }

            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 싱글 플레이어 인벤토리 자동 검색
            }

            if (inventory == null) // 플레이어 인벤토리 검색 실패 여부 확인
            {
                return; // 추가 참조 검색 중단
            }

            inputReader = inventory.GetComponent<PlayerInputReader>(); // 같은 플레이어 입력 래퍼 조회
            movement = inventory.GetComponent<PlayerMovement>(); // 같은 플레이어 이동 기능 조회

            if (aimCamera == null) // 조준 카메라 누락 확인
            {
                aimCamera = inventory.GetComponentInChildren<Camera>(true); // 플레이어 자식 1인칭 카메라 조회
                CaptureDefaultFov(); // 카메라 확보 직후 기본 FOV 저장
            }
        }

        private void CaptureDefaultFov() // 원래 카메라 FOV 한 번 저장
        {
            if (aimCamera == null || defaultFovCaptured) // 카메라 누락 또는 이미 저장 여부 확인
            {
                return; // 기본 FOV 재저장 방지
            }

            defaultFieldOfView = aimCamera.fieldOfView; // 현재 카메라 FOV를 원래 값으로 저장
            defaultFovCaptured = true; // 기본 FOV 저장 완료 기록
        }

        private void RestorePresentation() // 무기 해제·비활성화 시 조준 표현 기본값 복구
        {
            IsAiming = false; // 조준 상태 해제

            if (visualPivot != null) // 시각 루트 존재 여부 확인
            {
                visualPivot.localPosition = Vector3.zero; // 시각 루트 기본 위치 즉시 복구
                visualPivot.localRotation = Quaternion.identity; // 시각 루트 기본 회전 즉시 복구
            }

            if (aimCamera != null && defaultFovCaptured) // 복구할 카메라와 원래 FOV 존재 여부 확인
            {
                aimCamera.fieldOfView = defaultFieldOfView; // 카메라 원래 FOV 즉시 복구
            }

            if (movement != null && movementRestricted) // 원거리 무기가 적용한 이동 제한 존재 여부 확인
            {
                movement.ResetExternalMovementModifier(); // 이동 속도와 달리기 허용 복구
            }

            movementRestricted = false; // 이동 제한 소유 상태 초기화
        }

        private void OnValidate() // 인스펙터 공통 값 안전 범위 보정
        {
            aimFieldOfView = Mathf.Clamp(aimFieldOfView, 20f, 80f); // 조준 FOV 범위 보정
            aimTransitionSpeed = Mathf.Max(1f, aimTransitionSpeed); // 자세 전환 속도 최소값 보정
            zoomSpeed = Mathf.Max(10f, zoomSpeed); // FOV 전환 속도 최소값 보정
            aimMovementMultiplier = Mathf.Clamp(aimMovementMultiplier, 0.1f, 1f); // 조준 이동 배율 범위 보정
            reloadMovementMultiplier = Mathf.Clamp(reloadMovementMultiplier, 0.1f, 1f); // 장전 이동 배율 범위 보정
        }
    }
}
