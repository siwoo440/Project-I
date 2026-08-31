using ProjectI.Interaction; // F 회수 상호작용 기능 참조
using ProjectI.Items; // 플레이어 빠른 슬롯에서 석궁 검색 기능 참조
using ProjectI.Player; // PlayerInteractor 자료형 참조
using UnityEngine; // Rigidbody·Collision 기능 참조

namespace ProjectI.Combat.Ranged // 원거리 전투 기능 네임스페이스
{
    [RequireComponent(typeof(Rigidbody))] // 실제 포물선 비행용 Rigidbody 필수 지정
    [RequireComponent(typeof(Collider))] // 충돌·회수 Raycast용 Collider 필수 지정
    public sealed class CrossbowBoltProjectile : MonoBehaviour, IInteractable // 중력 포물선 비행 후 표면에 박혀 F 회수 가능한 석궁 볼트
    {
        private Rigidbody body; // 볼트 물리 Rigidbody
        private GameObject sourceObject; // Damage Pipeline 실제 피해 발생 석궁
        private GameObject instigatorObject; // 볼트를 발사한 플레이어
        private float baseDamage; // 현재 볼트 기본 피해량
        private float staggerPower; // 현재 볼트 경직 힘
        private float knockbackForce; // 현재 볼트 넉백 힘
        private int attackId; // 현재 볼트 공격 식별값
        private bool launched; // 발사 후 비행 상태 여부
        private bool stuck; // 표면에 박혀 회수 가능한 상태 여부
        private Vector3 lastVelocity; // 충돌 순간 방향 보존용 마지막 비행 속도

        public static int ActiveProjectileCount { get; private set; } // 현재 활성 발사 볼트 개수 진단값

        public string Prompt => "석궁 볼트 회수"; // F 상호작용 표시 문구
        public InteractionType InteractionType => InteractionType.Press; // 볼트 회수는 즉시 누르기 방식
        public float HoldDuration => 0f; // 볼트 회수 Hold 시간 없음
        public bool IsStuck => stuck; // F1·Validator용 표면 박힘 상태 공개

        private void Awake() // 볼트 물리 참조 초기화
        {
            body = GetComponent<Rigidbody>(); // 같은 오브젝트 Rigidbody 조회
        }

        private void OnEnable() // 활성 발사 볼트 개수 등록
        {
            ActiveProjectileCount++; // 활성 투사체 진단 개수 증가
        }

        private void OnDisable() // 활성 발사 볼트 개수 해제
        {
            ActiveProjectileCount = Mathf.Max(0, ActiveProjectileCount - 1); // 활성 투사체 진단 개수 안전 감소
        }

        private void LateUpdate() // 포물선 비행 중 화살촉 방향 정렬
        {
            if (!launched || stuck || body == null) // 실제 비행 상태 여부 확인
            {
                return; // 비행 방향 정렬 생략
            }

            Vector3 velocity = body.linearVelocity; // 현재 Rigidbody 이동 속도 조회

            if (velocity.sqrMagnitude > 0.01f) // 유효 비행 속도 여부 확인
            {
                lastVelocity = velocity; // 충돌 직전 방향용 속도 저장
                transform.rotation = Quaternion.LookRotation(velocity.normalized); // 볼트 전방을 포물선 이동 방향과 일치
            }
        }

        public void Launch(GameObject source, GameObject instigator, Vector3 direction, float speed, float damage, float stagger, float knockback, int sequence) // 석궁에서 볼트 포물선 발사 초기화
        {
            if (body == null) // Rigidbody 참조 누락 확인
            {
                body = GetComponent<Rigidbody>(); // 런타임 Rigidbody 재검색
            }

            sourceObject = source; // 실제 피해 발생 석궁 저장
            instigatorObject = instigator; // 공격 플레이어 저장
            baseDamage = Mathf.Max(0f, damage); // 피해량 음수 방지 저장
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지 저장
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지 저장
            attackId = sequence; // 공격 식별 번호 저장
            launched = true; // 실제 비행 상태 활성화
            stuck = false; // 표면 박힘 상태 초기화
            transform.SetParent(null, true); // 템플릿 부모에서 월드 루트로 분리
            body.isKinematic = false; // 실제 물리 이동 활성화
            body.useGravity = true; // 포물선을 위한 Unity 중력 활성화
            body.detectCollisions = true; // 비행 중 충돌 검사 활성화
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 볼트 터널링 감소
            body.linearVelocity = direction.normalized * Mathf.Max(1f, speed); // 초기 발사 속도 부여
            lastVelocity = body.linearVelocity; // 초기 비행 방향 저장
            IgnoreInstigatorCollisions(); // 발사 직후 플레이어와 자기 충돌 방지
        }

        public bool CanInteract(PlayerInteractor interactor) // 표면에 박힌 볼트 회수 가능 여부 반환
        {
            return stuck && interactor != null && FindOwnedCrossbow(interactor) != null; // 박힘 상태이며 플레이어가 석궁을 소유할 때만 회수 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 석궁 볼트 회수
        {
            CrossbowWeaponItem crossbow = FindOwnedCrossbow(interactor); // 플레이어 빠른 슬롯에서 석궁 검색

            if (!stuck || crossbow == null) // 회수 가능한 볼트·석궁 여부 확인
            {
                return; // 회수 처리 중단
            }

            crossbow.AddReserveBolts(1); // 회수한 볼트 한 발을 석궁 예비 탄약에 추가
            Destroy(gameObject); // 월드에 박힌 볼트 제거
        }

        private void OnCollisionEnter(Collision collision) // 볼트 첫 충돌 처리
        {
            if (!launched || stuck || collision == null || collision.collider == null) // 유효 비행 첫 충돌 여부 확인
            {
                return; // 중복·무효 충돌 처리 생략
            }

            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default; // 첫 충돌 접촉점 조회
            Vector3 hitPoint = collision.contactCount > 0 ? contact.point : transform.position; // Damage Pipeline 피격 위치 결정
            Vector3 hitNormal = collision.contactCount > 0 ? contact.normal : -transform.forward; // 피격 표면 방향 결정
            IDamageable target = DamagePipeline.FindDamageable(collision.collider); // 충돌 Collider 부모에서 공통 피해 대상 검색

            if (target != null) // 공통 피해 대상 충돌 여부 확인
            {
                Vector3 forceDirection = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : transform.forward; // 볼트 진행 방향 기반 넉백 방향 계산
                DamageInfo damageInfo = new DamageInfo(gameObject, instigatorObject, CombatFaction.Player, CombatDamageType.Piercing, baseDamage, hitPoint, hitNormal, staggerPower, forceDirection * knockbackForce, attackId); // 석궁 관통 피해 요청 생성
                DamagePipeline.TryApply(damageInfo, target, out _); // 기존 공통 Damage Pipeline으로 실제 피해·경직 적용
            }

            StickToSurface(collision.transform, hitPoint, hitNormal); // 적·벽·바닥 모두 첫 충돌 위치에 볼트 박힘 처리
        }

        private void StickToSurface(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal) // 충돌 후 볼트를 표면에 고정
        {
            Vector3 forward = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : transform.forward; // 충돌 직전 진행 방향 유지
            launched = false; // 비행 상태 종료
            stuck = true; // F 회수 가능한 박힘 상태 활성화
            body.linearVelocity = Vector3.zero; // 남은 직선 속도 제거
            body.angularVelocity = Vector3.zero; // 남은 회전 속도 제거
            body.useGravity = false; // 박힌 뒤 중력 비활성화
            body.isKinematic = true; // 박힌 뒤 물리 힘 비활성화
            transform.SetPositionAndRotation(hitPoint + (hitNormal * 0.015f), Quaternion.LookRotation(forward)); // 표면 바로 앞에 볼트 위치·방향 고정

            if (hitTransform != null && hitTransform.GetComponentInParent<CrossbowBoltProjectile>() == null) // 다른 볼트가 아닌 실제 표면 여부 확인
            {
                transform.SetParent(hitTransform, true); // 움직이는 피해 대상에도 볼트가 따라가도록 부모 연결
            }
        }

        private void IgnoreInstigatorCollisions() // 발사 플레이어와 볼트 Collider 충돌 무시
        {
            if (instigatorObject == null) // 공격 플레이어 누락 확인
            {
                return; // 충돌 무시 처리 중단
            }

            Collider ownCollider = GetComponent<Collider>(); // 볼트 루트 Collider 조회
            Collider[] playerColliders = instigatorObject.GetComponentsInChildren<Collider>(true); // 플레이어 전체 Collider 조회

            foreach (Collider playerCollider in playerColliders) // 플레이어 Collider 순회
            {
                if (ownCollider != null && playerCollider != null) // 양쪽 Collider 유효성 확인
                {
                    Physics.IgnoreCollision(ownCollider, playerCollider, true); // 볼트와 발사 플레이어 충돌 영구 무시
                }
            }
        }

        private CrossbowWeaponItem FindOwnedCrossbow(PlayerInteractor interactor) // 회수 플레이어 빠른 슬롯에서 석궁 검색
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 같은 플레이어 인벤토리 조회

            if (inventory == null) // 인벤토리 누락 확인
            {
                return null; // 석궁 검색 실패 반환
            }

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 빠른 슬롯 6칸 순회
            {
                WorldItem item = inventory.GetItem(index); // 현재 슬롯 아이템 조회
                CrossbowWeaponItem crossbow = item == null ? null : item.GetComponent<CrossbowWeaponItem>(); // 슬롯 아이템의 석궁 기능 조회

                if (crossbow != null) // 소유 석궁 발견 여부 확인
                {
                    return crossbow; // 첫 소유 석궁 반환
                }
            }

            return null; // 플레이어가 석궁을 보유하지 않은 상태 반환
        }
    }
}
