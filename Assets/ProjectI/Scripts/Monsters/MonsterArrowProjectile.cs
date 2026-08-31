using ProjectI.Combat; // Damage Pipeline과 진영·피해 데이터 참조
using UnityEngine; // Rigidbody·Collision 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    [RequireComponent(typeof(Rigidbody))] // 포물선 화살 물리 이동 필수 지정
    [RequireComponent(typeof(Collider))] // 화살 충돌 판정 필수 지정
    public sealed class MonsterArrowProjectile : MonoBehaviour // 회수 불가능한 부패한 망자 궁수용 포물선 화살
    {
        private Rigidbody body; // 화살 물리 Rigidbody 참조
        private GameObject instigator; // 화살을 발사한 몬스터 루트
        private float damage; // 현재 화살 기본 피해량
        private float staggerPower; // 현재 화살 경직 힘
        private float knockbackForce; // 현재 화살 넉백 힘
        private int attackId; // Damage Pipeline 공격 식별값
        private bool launched; // 실제 비행 상태 여부
        private Vector3 lastVelocity; // 충돌 순간 진행 방향 보존용 마지막 속도

        public static int ActiveProjectileCount { get; private set; } // 현재 활성 적 화살 개수 진단값

        private void Awake() // 화살 물리 참조 초기화
        {
            body = GetComponent<Rigidbody>(); // 같은 오브젝트 Rigidbody 조회
        }

        private void OnEnable() // 활성 적 화살 진단 개수 등록
        {
            ActiveProjectileCount++; // 활성 투사체 개수 증가
        }

        private void OnDisable() // 비활성 적 화살 진단 개수 해제
        {
            ActiveProjectileCount = Mathf.Max(0, ActiveProjectileCount - 1); // 활성 투사체 개수 안전 감소
        }

        private void LateUpdate() // 비행 중 화살촉을 현재 속도 방향으로 정렬
        {
            if (!launched || body == null) // 실제 비행 상태 여부 확인
            {
                return; // 방향 정렬 생략
            }

            Vector3 velocity = body.linearVelocity; // 현재 Rigidbody 속도 조회

            if (velocity.sqrMagnitude > 0.01f) // 유효 이동 속도 여부 확인
            {
                lastVelocity = velocity; // 충돌 순간 사용할 진행 속도 저장
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up); // 화살 전방을 포물선 진행 방향에 일치
            }
        }

        public void Launch(GameObject sourceMonster, Vector3 initialVelocity, float baseDamage, float stagger, float knockback, int sequence) // 망자 궁수 발사 정보로 포물선 화살 초기화
        {
            if (body == null) // Rigidbody 참조 누락 확인
            {
                body = GetComponent<Rigidbody>(); // 런타임 Rigidbody 재검색
            }

            instigator = sourceMonster; // 공격 몬스터 루트 저장
            damage = Mathf.Max(0f, baseDamage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지
            attackId = sequence; // 공격 식별 번호 저장
            launched = true; // 비행 상태 활성화
            transform.SetParent(null, true); // 템플릿 부모에서 런타임 월드 루트로 분리
            body.isKinematic = false; // 실제 물리 이동 활성화
            body.useGravity = true; // 포물선 비행을 위한 중력 활성화
            body.detectCollisions = true; // 비행 중 충돌 검사 활성화
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 화살 터널링 감소
            body.linearVelocity = initialVelocity; // 계산된 포물선 초기 속도 적용
            lastVelocity = initialVelocity; // 초기 진행 방향 저장
            IgnoreInstigatorCollisions(); // 발사 몬스터와 자기 화살 충돌 방지
            Destroy(gameObject, 8f); // 빗나간 화살도 일정 시간 후 자동 제거
        }

        private void OnCollisionEnter(Collision collision) // 화살 첫 충돌 피해와 자동 제거 처리
        {
            if (!launched || collision == null || collision.collider == null) // 유효 첫 충돌 여부 확인
            {
                return; // 중복·무효 충돌 생략
            }

            launched = false; // 첫 충돌 이후 추가 피해 차단
            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default; // 첫 접촉점 조회
            Vector3 hitPoint = collision.contactCount > 0 ? contact.point : transform.position; // 실제 피격 위치 계산
            Vector3 hitNormal = collision.contactCount > 0 ? contact.normal : -transform.forward; // 피격 표면 방향 계산
            IDamageable target = DamagePipeline.FindDamageable(collision.collider); // 충돌 Collider 부모에서 공통 피해 대상 검색

            if (target != null) // 플레이어 등 공통 피해 대상 명중 여부 확인
            {
                Vector3 forceDirection = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : transform.forward; // 화살 진행 방향 기반 넉백 방향 계산
                DamageInfo damageInfo = new DamageInfo(gameObject, instigator, CombatFaction.Enemy, CombatDamageType.Piercing, damage, hitPoint, hitNormal, staggerPower, forceDirection * knockbackForce, attackId); // Enemy 진영 관통 피해 요청 생성
                DamagePipeline.TryApply(damageInfo, target, out _); // 기존 공통 Damage Pipeline으로 플레이어 피해 처리
            }

            if (body != null) // Rigidbody 존재 여부 확인
            {
                body.linearVelocity = Vector3.zero; // 충돌 후 이동 속도 제거
                body.angularVelocity = Vector3.zero; // 충돌 후 회전 속도 제거
                body.useGravity = false; // 박힌 뒤 중력 비활성화
                body.isKinematic = true; // 충돌 후 물리 이동 정지
            }

            transform.position = hitPoint + (hitNormal * 0.015f); // 표면 앞쪽에 화살 위치 고정
            Destroy(gameObject, 2.5f); // 적 화살은 회수되지 않고 잠시 뒤 자동 삭제
        }

        private void IgnoreInstigatorCollisions() // 발사 몬스터 전체 Collider와 화살 충돌 무시
        {
            if (instigator == null) // 공격 몬스터 누락 확인
            {
                return; // 충돌 무시 처리 생략
            }

            Collider ownCollider = GetComponent<Collider>(); // 화살 루트 Collider 조회
            Collider[] ownerColliders = instigator.GetComponentsInChildren<Collider>(true); // 발사 몬스터 전체 Collider 조회

            foreach (Collider ownerCollider in ownerColliders) // 발사 몬스터 Collider 순회
            {
                if (ownCollider != null && ownerCollider != null) // 양쪽 Collider 유효성 확인
                {
                    Physics.IgnoreCollision(ownCollider, ownerCollider, true); // 발사 몬스터와 자기 화살 충돌 무시
                }
            }
        }
    }
}
