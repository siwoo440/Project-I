using System.Collections.Generic; // 활성 월드 아이템 목록 기능 참조
using ProjectI.Interaction; // 상호작용 공통 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    [RequireComponent(typeof(Rigidbody))] // 월드 물리용 Rigidbody 필수 지정
    public sealed class WorldItem : MonoBehaviour, IInteractable // 줍고 내려놓고 던질 수 있는 월드 아이템
    {
        private static readonly HashSet<WorldItem> ActiveItems = new HashSet<WorldItem>(); // 현재 활성화된 월드 아이템 목록
        [SerializeField] private string displayName = "테스트 아이템"; // 화면 표시 아이템 이름
        [SerializeField] private float carryRadius = 0.20f; // 내려놓기·투척 공간 검사 반지름
        [SerializeField] private CarryType carryType = CarryType.OneHand; // 한손 또는 양손 운반 방식
        [SerializeField] private Vector3 carryPositionOffset = Vector3.zero; // CarryPoint 기준 추가 로컬 위치 보정
        [SerializeField] private Vector3 carryEulerOffset = Vector3.zero; // CarryPoint 기준 추가 로컬 회전 보정
        private Rigidbody body; // 월드 물리 Rigidbody
        private Collider[] itemColliders; // 아이템 소속 Collider 목록
        private RigidbodyInterpolation interpolationBeforeCarry; // 운반 전 Rigidbody 보간 방식
        private bool isHeld; // 현재 운반 상태

        public string Prompt => $"{displayName} 줍기"; // 상호작용 UI 문구 반환
        public InteractionType InteractionType => InteractionType.Press; // 아이템 획득은 즉시 누르기 방식
        public float HoldDuration => 0f; // Hold 시간 불필요
        public bool IsHeld => isHeld; // 현재 운반 여부 공개
        public float CarryRadius => carryRadius; // 공간 검사 반지름 공개
        public CarryType CarryType => carryType; // 현재 한손 또는 양손 운반 방식 공개
        public Rigidbody Body => body; // 투척 처리용 Rigidbody 공개

        private void Awake() // 월드 아이템 초기화
        {
            body = GetComponent<Rigidbody>(); // Rigidbody 참조 획득
            itemColliders = GetComponentsInChildren<Collider>(true); // 아이템 Collider 목록 획득
        }

        private void OnEnable() // 월드 아이템 활성화 처리
        {
            EnsureColliderCache(); // Collider 목록 확보

            foreach (WorldItem otherItem in ActiveItems) // 기존 활성 아이템 순회
            {
                IgnoreItemCollision(otherItem); // 새 아이템과 기존 아이템 사이 충돌 무시
            }

            ActiveItems.Add(this); // 현재 아이템을 활성 목록에 등록
        }

        private void OnDisable() // 월드 아이템 비활성화 처리
        {
            ActiveItems.Remove(this); // 활성 아이템 목록에서 제거
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 아이템 획득 가능 여부 반환
        {
            return !isHeld && interactor != null && interactor.CarryController != null && !interactor.CarryController.HasItem; // 들고 있지 않고 플레이어 운반 상태가 비었을 때 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 아이템 획득 시도
        {
            interactor.CarryController.TryPickup(this); // 플레이어 운반 기능에 획득 요청
        }

        public void Configure(string itemName, float radius) // 기존 에디터 설정 호환용 기본 구성
        {
            Configure(itemName, radius, CarryType.OneHand); // 기본 한손 아이템으로 구성
        }

        public void Configure(string itemName, float radius, CarryType type) // 에디터 자동 설정용 아이템 값 지정
        {
            displayName = itemName; // 표시 이름 저장
            carryRadius = Mathf.Clamp(radius, 0.08f, 0.45f); // 작은 도구 크기에 맞게 공간 검사 반지름 보정
            carryType = type; // 한손 또는 양손 운반 방식 저장
            carryPositionOffset = Vector3.zero; // 테스트 기본 포즈 위치 보정 초기화
            carryEulerOffset = Vector3.zero; // 테스트 기본 포즈 회전 보정 초기화
        }

        public void ConfigureCarryPose(CarryType type, Vector3 positionOffset, Vector3 eulerOffset) // 아이템별 운반 포즈 세부 보정
        {
            carryType = type; // 운반 방식 저장
            carryPositionOffset = positionOffset; // CarryPoint 기준 위치 보정 저장
            carryEulerOffset = eulerOffset; // CarryPoint 기준 회전 보정 저장
        }

        public void IgnoreCollisionsWith(Transform targetRoot) // 플레이어와 아이템 사이 충돌을 영구적으로 무시
        {
            if (targetRoot == null) // 대상 루트 누락 확인
            {
                return; // 충돌 무시 처리 중단
            }

            EnsureColliderCache(); // 아이템 Collider 목록 확보
            Collider[] targetColliders = targetRoot.GetComponentsInChildren<Collider>(true); // 플레이어 소속 Collider 목록 조회

            foreach (Collider itemCollider in itemColliders) // 아이템 Collider 순회
            {
                if (itemCollider == null) // 유효하지 않은 Collider 확인
                {
                    continue; // 다음 아이템 Collider 검사
                }

                foreach (Collider targetCollider in targetColliders) // 플레이어 Collider 순회
                {
                    if (targetCollider == null || targetCollider == itemCollider) // 유효하지 않거나 자기 자신인지 확인
                    {
                        continue; // 충돌 무시 설정 건너뜀
                    }

                    Physics.IgnoreCollision(itemCollider, targetCollider, true); // 아이템과 플레이어 사이 충돌 영구 무시
                }
            }
        }

        public void BeginCarry(Transform carryPoint) // CarryPoint에 아이템을 직접 종속하여 운반 시작
        {
            if (carryPoint == null) // 운반 지점 누락 확인
            {
                return; // 운반 시작 중단
            }

            if (body == null) // Rigidbody 참조 누락 확인
            {
                body = GetComponent<Rigidbody>(); // Rigidbody 참조 재획득
            }

            EnsureColliderCache(); // Collider 목록 확보
            interpolationBeforeCarry = body.interpolation; // 월드 상태의 Rigidbody 보간 방식을 저장
            isHeld = true; // 운반 상태 활성화
            body.linearVelocity = Vector3.zero; // 기존 직선 속도 제거
            body.angularVelocity = Vector3.zero; // 기존 회전 속도 제거
            body.interpolation = RigidbodyInterpolation.None; // 물리 보간이 카메라 종속 Transform을 덮어쓰지 않도록 비활성화
            body.useGravity = false; // 운반 중 중력 비활성화
            body.isKinematic = true; // 운반 중 물리 힘 비활성화
            body.detectCollisions = false; // 운반 중 물리 충돌 계산 비활성화

            foreach (Collider itemCollider in itemColliders) // 아이템 Collider 순회
            {
                if (itemCollider != null) // 유효 Collider 확인
                {
                    itemCollider.enabled = false; // 들고 있는 동안 플레이어와 완전히 겹칠 수 있도록 비활성화
                }
            }

            SnapToCarryPoint(carryPoint); // 줍는 순간 CarryPoint 위치와 회전에 정확히 맞춤
        }

        public void SnapToCarryPoint(Transform carryPoint) // 카메라 회전 후 CarryPoint에 정확히 재동기화
        {
            if (!isHeld || carryPoint == null) // 운반 상태 또는 지점 유효성 확인
            {
                return; // 동기화 중단
            }

            if (transform.parent != carryPoint) // 다른 부모로 변경되었는지 확인
            {
                transform.SetParent(carryPoint, false); // 다시 CarryPoint 자식으로 연결
            }

            transform.localPosition = carryPositionOffset; // CarryPoint 기준 로컬 위치를 정확히 고정
            transform.localRotation = Quaternion.Euler(carryEulerOffset); // CarryPoint 기준 로컬 회전을 정확히 고정
        }

        public void Release(Vector3 position, Quaternion rotation, Vector3 throwVelocityChange) // 내려놓기 또는 투척 상태로 복귀
        {
            transform.SetParent(null, true); // CarryPoint 종속을 해제하면서 현재 월드 위치 유지
            transform.SetPositionAndRotation(position, rotation); // 안전한 월드 복귀 위치와 회전 적용
            isHeld = false; // 운반 상태 해제
            body.detectCollisions = true; // 월드 충돌 계산 복구
            body.isKinematic = false; // 물리 힘 반응 복구
            body.useGravity = true; // 중력 복구
            body.interpolation = interpolationBeforeCarry; // 운반 전 Rigidbody 보간 방식 복구
            body.linearVelocity = Vector3.zero; // 복귀 순간 기존 직선 속도 제거
            body.angularVelocity = Vector3.zero; // 복귀 순간 기존 회전 속도 제거

            foreach (Collider itemCollider in itemColliders) // 아이템 Collider 순회
            {
                if (itemCollider != null) // 유효 Collider 확인
                {
                    itemCollider.enabled = true; // 월드 Collider 복구
                }
            }

            if (throwVelocityChange.sqrMagnitude > 0.0001f) // 투척 속도 변화 존재 여부 확인
            {
                body.AddForce(throwVelocityChange, ForceMode.VelocityChange); // 질량과 무관한 즉시 투척 속도 적용
            }
        }

        private void IgnoreItemCollision(WorldItem otherItem) // 두 WorldItem 사이 모든 Collider 충돌 무시
        {
            if (otherItem == null || otherItem == this) // 상대 아이템 유효성 확인
            {
                return; // 충돌 무시 처리 중단
            }

            EnsureColliderCache(); // 현재 아이템 Collider 목록 확보
            otherItem.EnsureColliderCache(); // 상대 아이템 Collider 목록 확보

            foreach (Collider itemCollider in itemColliders) // 현재 아이템 Collider 순회
            {
                if (itemCollider == null) // 유효하지 않은 Collider 확인
                {
                    continue; // 다음 Collider 검사
                }

                foreach (Collider otherCollider in otherItem.itemColliders) // 상대 아이템 Collider 순회
                {
                    if (otherCollider == null) // 유효하지 않은 상대 Collider 확인
                    {
                        continue; // 다음 상대 Collider 검사
                    }

                    Physics.IgnoreCollision(itemCollider, otherCollider, true); // 아이템끼리 서로 충돌하지 않도록 영구 무시
                }
            }
        }

        private void EnsureColliderCache() // 아이템 Collider 목록 확보
        {
            if (itemColliders == null || itemColliders.Length == 0) // Collider 목록 미생성 확인
            {
                itemColliders = GetComponentsInChildren<Collider>(true); // 현재 아이템 Collider 목록 재조회
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            carryRadius = Mathf.Clamp(carryRadius, 0.08f, 0.45f); // 작은 도구 크기에 맞는 반지름 범위 보정
        }
    }
}
