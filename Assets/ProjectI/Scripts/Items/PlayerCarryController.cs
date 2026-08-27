using ProjectI.Player; // 플레이어 입력 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInputReader))] // 플레이어 입력 래퍼 필수 지정
    public sealed class PlayerCarryController : MonoBehaviour // 선택 슬롯 아이템의 화면 운반 표현 담당
    {
        [SerializeField] private Transform viewTransform; // 시선과 내려놓기 방향 기준
        [SerializeField] private Transform oneHandCarryPoint; // 한손 아이템 화면 오른쪽 아래 운반 지점
        [SerializeField] private Transform twoHandCarryPoint; // 양손 아이템 화면 중앙 아래 운반 지점
        [SerializeField] private float obstructionPadding = 0.08f; // 내려놓기 시 벽과 아이템 사이 여유 거리
        [SerializeField] private float dropDistance = 0.60f; // 플레이어 바로 앞에 떨어뜨릴 짧은 거리
        [SerializeField] private float throwVelocityChange = 7f; // 향후 투척용 순간 속도 변화량
        [SerializeField] private LayerMask obstructionMask = ~0; // 내려놓기 공간 검사 레이어
        private WorldItem heldItem; // 현재 화면에 들고 있는 선택 슬롯 아이템

        public bool HasItem => heldItem != null; // 현재 아이템 화면 운반 여부 공개
        public WorldItem HeldItem => heldItem; // 현재 화면 운반 아이템 공개

        private void Awake() // 운반 기능 초기화
        {
            if (viewTransform == null) // 시점 참조 누락 확인
            {
                Camera childCamera = GetComponentInChildren<Camera>(true); // 플레이어 자식 카메라 조회
                viewTransform = childCamera == null ? null : childCamera.transform; // 시점 트랜스폼 자동 지정
            }

            ResolveCarryPoints(); // 카메라 아래 한손·양손 운반 지점 자동 연결
        }

        private void LateUpdate() // PlayerLook 카메라 회전 적용 뒤 선택 아이템 정확히 재동기화
        {
            if (heldItem == null) // 현재 운반 아이템 존재 여부 확인
            {
                return; // 동기화 중단
            }

            ResolveCarryPoints(); // 현재 CarryPoint 참조 상태 확인
            Transform carryPoint = GetCarryPoint(heldItem.CarryType); // 현재 아이템에 맞는 한손·양손 지점 선택
            heldItem.SnapToCarryPoint(carryPoint); // 보간 없이 CarryPoint 로컬 위치와 회전에 즉시 재고정
        }

        public void Configure(Transform view, Transform oneHandPoint, Transform twoHandPoint, PlayerInputReader reader) // 기존 Day 5 에디터 설정 호환용 참조 지정
        {
            viewTransform = view; // 시점 참조 저장
            oneHandCarryPoint = oneHandPoint; // 한손 운반 지점 저장
            twoHandCarryPoint = twoHandPoint; // 양손 운반 지점 저장
        }

        public bool TryPickup(WorldItem item) // 기존 호출을 6일차 인벤토리 획득으로 전달
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>(); // 같은 플레이어의 인벤토리 조회
            return inventory != null && inventory.TryPickup(item); // 인벤토리가 있으면 빠른 슬롯 획득 실행
        }

        public bool EquipItem(WorldItem item) // 선택 슬롯 아이템을 화면에 표시
        {
            if (item == null || heldItem != null || viewTransform == null) // 아이템 누락·이미 운반 중·카메라 누락 확인
            {
                return false; // 장착 실패 반환
            }

            ResolveCarryPoints(); // 최신 CarryPoint 상태 확인
            Transform carryPoint = GetCarryPoint(item.CarryType); // 아이템 운반 방식에 맞는 지점 선택

            if (carryPoint == null) // 필요한 CarryPoint 누락 확인
            {
                return false; // 장착 실패 반환
            }

            heldItem = item; // 현재 화면 운반 아이템 저장
            heldItem.IgnoreCollisionsWith(transform); // 플레이어와 아이템 충돌 무시 유지
            heldItem.BeginCarry(carryPoint); // CarryPoint 자식으로 연결하여 화면 표시
            return true; // 장착 성공 반환
        }

        public void HolsterHeldItem(Transform storageRoot) // 현재 한손 아이템을 슬롯 보관 상태로 숨김
        {
            if (heldItem == null || storageRoot == null) // 운반 아이템 또는 보관 루트 누락 확인
            {
                return; // 보관 처리 중단
            }

            WorldItem itemToStore = heldItem; // 현재 아이템 임시 저장
            heldItem = null; // 화면 운반 상태 먼저 비우기
            itemToStore.Store(storageRoot); // 아이템을 인벤토리 숨김 보관 상태로 전환
        }

        public WorldItem DropHeldItem() // 현재 선택 슬롯 아이템을 플레이어 바로 앞 월드에 내려놓기
        {
            if (heldItem == null || viewTransform == null) // 운반 아이템 또는 시점 누락 확인
            {
                return null; // 내려놓기 실패 반환
            }

            Vector3 releasePosition = CalculateReleasePosition(dropDistance); // 플레이어 발앞 안전한 내려놓기 위치 계산
            Quaternion releaseRotation = Quaternion.Euler(0f, viewTransform.eulerAngles.y, 0f); // 바닥 배치를 위한 수평 회전 계산
            WorldItem itemToRelease = heldItem; // 해제 전 아이템 임시 저장
            heldItem = null; // 화면 운반 상태 비우기
            itemToRelease.Release(releasePosition, releaseRotation, Vector3.zero); // 인벤토리 종속 해제와 월드 물리 복구
            return itemToRelease; // 내려놓은 아이템 반환
        }

        public WorldItem ThrowHeldItem() // 향후 투척 가능 아이템용 기존 기능 유지
        {
            if (heldItem == null || viewTransform == null) // 운반 아이템 또는 시점 누락 확인
            {
                return null; // 투척 실패 반환
            }

            Vector3 releasePosition = CalculateReleasePosition(Mathf.Max(0.85f, dropDistance)); // 플레이어 앞 투척 시작 위치 계산
            Quaternion releaseRotation = heldItem.transform.rotation; // 현재 손에 든 회전 유지
            Vector3 velocityChange = viewTransform.forward * throwVelocityChange; // 시선 방향 투척 속도 변화 계산
            WorldItem itemToRelease = heldItem; // 해제 전 아이템 임시 저장
            heldItem = null; // 화면 운반 상태 비우기
            itemToRelease.Release(releasePosition, releaseRotation, velocityChange); // 월드 물리 복구와 투척 힘 적용
            return itemToRelease; // 투척한 아이템 반환
        }

        private void ResolveCarryPoints() // 카메라 자식의 한손·양손 운반 지점 자동 조회
        {
            if (viewTransform == null) // 시점 참조 누락 확인
            {
                return; // 운반 지점 검색 중단
            }

            if (oneHandCarryPoint == null) // 한손 지점 누락 확인
            {
                oneHandCarryPoint = viewTransform.Find("OneHandCarryPoint"); // 카메라 자식 한손 지점 조회
            }

            if (twoHandCarryPoint == null) // 양손 지점 누락 확인
            {
                twoHandCarryPoint = viewTransform.Find("TwoHandCarryPoint"); // 카메라 자식 양손 지점 조회
            }
        }

        private Transform GetCarryPoint(CarryType carryType) // 운반 방식에 맞는 화면상 손 위치 반환
        {
            return carryType == CarryType.TwoHand ? twoHandCarryPoint : oneHandCarryPoint; // 양손은 중앙 아래, 한손은 오른쪽 아래 지점 사용
        }

        private Vector3 CalculateReleasePosition(float distance) // 플레이어 바로 앞의 낮은 위치 계산
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized; // 카메라 상하 각도를 제외한 수평 전방 계산

            if (flatForward.sqrMagnitude <= 0.0001f) // 수평 전방 계산 실패 여부 확인
            {
                flatForward = transform.forward; // 플레이어 몸체 전방을 대체 방향으로 사용
            }

            Vector3 origin = transform.position + (Vector3.up * 0.35f); // 플레이어 발 위치보다 약간 위를 기준점으로 사용
            float radius = heldItem == null ? 0.18f : heldItem.CarryRadius; // 현재 아이템 크기에 맞는 검사 반지름 사용
            float safeDistance = Mathf.Max(0.35f, distance); // 플레이어 바로 앞 최소 배치 거리 보장
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, flatForward, safeDistance, obstructionMask, QueryTriggerInteraction.Ignore); // 발앞 짧은 구간 장애물 검사

            foreach (RaycastHit hit in hits) // 장애물 후보 순회
            {
                if (ShouldIgnoreHit(hit.collider)) // 플레이어·현재 아이템·다른 WorldItem 여부 확인
                {
                    continue; // 내려놓기 방해 대상에서 제외
                }

                safeDistance = Mathf.Min(safeDistance, Mathf.Max(0.20f, hit.distance - obstructionPadding)); // 실제 월드 장애물 앞까지 거리 제한
            }

            return origin + (flatForward * safeDistance); // 플레이어 발앞 가까운 최종 위치 반환
        }

        private bool ShouldIgnoreHit(Collider hitCollider) // 내려놓기 위치 검사에서 제외할 충돌체 판정
        {
            if (hitCollider == null) // 유효하지 않은 Collider 확인
            {
                return true; // 검사 대상에서 제외
            }

            Transform hitTransform = hitCollider.transform; // 충돌체 트랜스폼 조회

            if (hitTransform == transform || hitTransform.IsChildOf(transform)) // 플레이어 자신의 Collider 여부 확인
            {
                return true; // 플레이어는 검사에서 제외
            }

            if (heldItem != null && (hitTransform == heldItem.transform || hitTransform.IsChildOf(heldItem.transform))) // 현재 운반 아이템 여부 확인
            {
                return true; // 자기 아이템은 검사에서 제외
            }

            if (hitCollider.GetComponentInParent<WorldItem>() != null) // 다른 월드 아이템 Collider 여부 확인
            {
                return true; // 아이템끼리 겹칠 수 있도록 배치 장애물에서 제외
            }

            return false; // 바닥·벽 등 실제 월드 장애물로 사용
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            obstructionPadding = Mathf.Max(0.01f, obstructionPadding); // 벽 여유 거리 최소값 보정
            dropDistance = Mathf.Max(0.6f, dropDistance); // 내려놓기 거리 최소값 보정
            throwVelocityChange = Mathf.Max(0.1f, throwVelocityChange); // 향후 투척 힘 최소값 보정
        }
    }
}
