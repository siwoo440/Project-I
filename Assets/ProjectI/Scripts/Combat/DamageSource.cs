using UnityEngine; // 유니티 Collider와 벡터 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public sealed class DamageSource : MonoBehaviour // 함정·환경·몬스터 공격이 재사용할 범용 피해 발생기
    {
        [SerializeField] private CombatFaction sourceFaction = CombatFaction.Environment; // 피해 발생기 진영
        [SerializeField] private CombatDamageType damageType = CombatDamageType.Environment; // 기본 피해 종류
        [SerializeField] private float baseDamage = 10f; // 기본 피해량
        [SerializeField] private float force = 0f; // 기본 넉백 힘 크기
        [SerializeField] private GameObject instigator; // 실제 공격 행위자 선택 참조

        public CombatFaction SourceFaction => sourceFaction; // 피해 발생기 진영 공개
        public CombatDamageType DamageType => damageType; // 피해 종류 공개
        public float BaseDamage => baseDamage; // 기본 피해량 공개

        public void Configure(CombatFaction faction, CombatDamageType type, float damage, float forceAmount, GameObject sourceInstigator) // 향후 함정·환경 자동 구성용 설정
        {
            sourceFaction = faction; // 피해 발생기 진영 저장
            damageType = type; // 피해 종류 저장
            baseDamage = Mathf.Max(0f, damage); // 기본 피해량 음수 방지
            force = Mathf.Max(0f, forceAmount); // 넉백 힘 음수 방지
            instigator = sourceInstigator; // 실제 공격 행위자 저장
        }

        public bool TryDamage(Collider targetCollider, Vector3 hitPoint, Vector3 hitNormal, Vector3 forceDirection, int attackId, out CombatHitResult result) // 지정 Collider에 공통 피해 적용 시도
        {
            IDamageable target = DamagePipeline.FindDamageable(targetCollider); // Collider 계층의 공통 피해 대상 조회
            Vector3 appliedForce = forceDirection.sqrMagnitude <= 0f ? Vector3.zero : forceDirection.normalized * force; // 설정된 넉백 힘 벡터 계산
            DamageInfo damageInfo = new DamageInfo(gameObject, instigator, sourceFaction, damageType, baseDamage, hitPoint, hitNormal, appliedForce, attackId); // 공통 피해 요청 데이터 생성
            return DamagePipeline.TryApply(damageInfo, target, out result); // 공통 Damage Pipeline에 피해 적용 요청
        }

        private void OnValidate() // 인스펙터 피해 설정 검증
        {
            baseDamage = Mathf.Max(0f, baseDamage); // 기본 피해량 음수 방지
            force = Mathf.Max(0f, force); // 넉백 힘 음수 방지
        }
    }
}
