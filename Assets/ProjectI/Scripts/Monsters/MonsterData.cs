using UnityEngine; // ScriptableObject와 수치 검증 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    [CreateAssetMenu(menuName = "Project I/Monsters/Monster Data", fileName = "MonsterData")] // 몬스터 데이터 에셋 생성 메뉴 등록
    public sealed class MonsterData : ScriptableObject // 몬스터별 체력·감지·이동·공격 수치를 분리하는 공통 데이터
    {
        [SerializeField] private string displayName = "Monster"; // 진단 화면과 런타임 표시 이름
        [SerializeField] private MonsterArchetype archetype = MonsterArchetype.CorruptedUndead; // 몬스터 행동 유형
        [SerializeField] private float maxHealth = 100f; // 몬스터 최대 체력
        [SerializeField] private float moveSpeed = 2.2f; // 조사·일반 이동 속도
        [SerializeField] private float chaseSpeed = 3.2f; // 플레이어 추적 이동 속도
        [SerializeField] private float retreatSpeed = 3.0f; // 원거리 몬스터 거리 확보 이동 속도
        [SerializeField] private float turnSpeed = 420f; // 목표 방향 회전 속도
        [SerializeField] private float visionRange = 24f; // 시각 감지 최대 거리
        [SerializeField, Range(10f, 180f)] private float visionAngle = 110f; // 정면 기준 시야각
        [SerializeField] private float visionInterval = 0.14f; // 시각 Raycast 검사 간격
        [SerializeField] private float hearingRange = 24f; // 청각 감지 최대 거리
        [SerializeField] private float memoryDuration = 4.5f; // 플레이어를 놓친 뒤 마지막 위치 기억 시간
        [SerializeField] private float investigateDuration = 4.0f; // 소리 위치 조사 유지 시간
        [SerializeField] private float preferredMinRange = 8f; // 원거리 몬스터가 유지하려는 최소 전투 거리
        [SerializeField] private float preferredMaxRange = 13f; // 원거리 몬스터가 유지하려는 최대 선호 거리
        [SerializeField] private float attackRange = 17f; // 현재 몬스터 공격 시작 최대 거리
        [SerializeField] private float aimTime = 0.80f; // 원거리 조준 또는 근접 Windup 시간
        [SerializeField] private float attackCooldown = 2.05f; // 공격 후 다음 공격까지 대기 시간
        [SerializeField] private float projectileSpeed = 24f; // 투사체형 몬스터 발사 속도
        [SerializeField] private float attackDamage = 14f; // 한 번 공격 기본 피해량
        [SerializeField] private float staggerPower = 6f; // 공격 피격 경직 힘
        [SerializeField] private float knockbackForce = 0.25f; // 공격 피격 넉백 힘
        [SerializeField] private float staggerThreshold = 42f; // 몬스터 자신이 경직되기 위한 누적 수치
        [SerializeField, Range(0f, 0.95f)] private float knockbackResistance = 0.25f; // 몬스터 자신의 넉백 저항

        public string DisplayName => displayName; // 진단용 표시 이름 공개
        public MonsterArchetype Archetype => archetype; // 몬스터 행동 유형 공개
        public float MaxHealth => maxHealth; // 최대 체력 공개
        public float MoveSpeed => moveSpeed; // 일반 이동 속도 공개
        public float ChaseSpeed => chaseSpeed; // 추적 속도 공개
        public float RetreatSpeed => retreatSpeed; // 후퇴 속도 공개
        public float TurnSpeed => turnSpeed; // 회전 속도 공개
        public float VisionRange => visionRange; // 시야 거리 공개
        public float VisionAngle => visionAngle; // 시야각 공개
        public float VisionInterval => visionInterval; // 시야 검사 간격 공개
        public float HearingRange => hearingRange; // 청각 거리 공개
        public float MemoryDuration => memoryDuration; // 마지막 위치 기억 시간 공개
        public float InvestigateDuration => investigateDuration; // 조사 유지 시간 공개
        public float PreferredMinRange => preferredMinRange; // 선호 최소 거리 공개
        public float PreferredMaxRange => preferredMaxRange; // 선호 최대 거리 공개
        public float AttackRange => attackRange; // 공격 거리 공개
        public float AimTime => aimTime; // 원거리 조준 또는 근접 준비 시간 공개
        public float AttackCooldown => attackCooldown; // 공격 쿨타임 공개
        public float ProjectileSpeed => projectileSpeed; // 투사체 속도 공개
        public float AttackDamage => attackDamage; // 공격 피해량 공개
        public float StaggerPower => staggerPower; // 공격 경직 힘 공개
        public float KnockbackForce => knockbackForce; // 공격 넉백 힘 공개
        public float StaggerThreshold => staggerThreshold; // 몬스터 경직 한계 공개
        public float KnockbackResistance => knockbackResistance; // 몬스터 넉백 저항 공개

        public void ConfigureArcher(string targetName, float health, float walk, float chase, float retreat, float vision, float angle, float hearing, float minRange, float maxRange, float range, float aim, float cooldown, float arrowSpeed, float damage, float stagger, float knockback, float threshold, float resistance) // Day17 자동 Setup용 부패한 망자 궁수 데이터 구성
        {
            ConfigureCommon(MonsterArchetype.CorruptedUndeadArcher, targetName, health, walk, chase, retreat, vision, angle, hearing, minRange, maxRange, range, aim, cooldown, arrowSpeed, damage, stagger, knockback, threshold, resistance); // 궁수 원거리 데이터를 공통 저장 함수에 전달
        }

        public void ConfigureMelee(MonsterArchetype targetArchetype, string targetName, float health, float walk, float chase, float vision, float angle, float hearing, float range, float windup, float cooldown, float damage, float stagger, float knockback, float threshold, float resistance) // 근접 부패한 망자·석상·미믹 공통 데이터 구성
        {
            ConfigureCommon(targetArchetype, targetName, health, walk, chase, chase, vision, angle, hearing, range * 0.55f, range, range, windup, cooldown, 5f, damage, stagger, knockback, threshold, resistance); // 근접형 수치를 공통 필드 형식으로 변환해 저장
        }

        private void ConfigureCommon(MonsterArchetype targetArchetype, string targetName, float health, float walk, float chase, float retreat, float vision, float angle, float hearing, float minRange, float maxRange, float range, float aim, float cooldown, float projectile, float damage, float stagger, float knockback, float threshold, float resistance) // 모든 몬스터 종류가 공유하는 수치 저장
        {
            archetype = targetArchetype; // 몬스터 행동 유형 저장
            displayName = string.IsNullOrWhiteSpace(targetName) ? "Monster" : targetName; // 표시 이름 저장
            maxHealth = targetArchetype == MonsterArchetype.SmilingStatue ? 0f : Mathf.Max(1f, health); // 웃는 석상은 체력 자체가 없고 다른 몬스터만 최소 체력 보정
            moveSpeed = Mathf.Max(0.1f, walk); // 일반 이동 속도 최소값 보정
            chaseSpeed = Mathf.Max(moveSpeed, chase); // 추적 속도를 일반 이동 이상으로 보정
            retreatSpeed = Mathf.Max(0.1f, retreat); // 후퇴 속도 최소값 보정
            turnSpeed = targetArchetype == MonsterArchetype.SmilingStatue ? 720f : 420f; // 석상은 시야 밖에서 빠르게 방향을 맞추도록 회전 속도 강화
            visionRange = Mathf.Max(1f, vision); // 시야 거리 최소값 보정
            visionAngle = Mathf.Clamp(angle, 10f, 180f); // 시야각 범위 보정
            visionInterval = targetArchetype == MonsterArchetype.SmilingStatue ? 0.08f : 0.14f; // 석상 관찰 반응은 더 빠른 검사 간격 사용
            hearingRange = Mathf.Max(0f, hearing); // 청각 거리 음수 방지
            memoryDuration = targetArchetype == MonsterArchetype.ChestMimic ? 7f : 4.5f; // 미믹은 변신 후 공격 대상을 더 오래 기억
            investigateDuration = 4.0f; // 소리 위치 조사 시간 지정
            preferredMinRange = Mathf.Max(0.3f, minRange); // 선호 최소 거리 보정
            preferredMaxRange = Mathf.Max(preferredMinRange + 0.1f, maxRange); // 선호 최대 거리 역전 방지
            attackRange = Mathf.Max(0.5f, range); // 공격 사거리 최소값 보정
            aimTime = Mathf.Max(0.08f, aim); // 조준·근접 준비 시간 최소값 보정
            attackCooldown = Mathf.Max(0.2f, cooldown); // 공격 쿨타임 최소값 보정
            projectileSpeed = Mathf.Max(5f, projectile); // 투사체 속도 최소값 보정
            attackDamage = Mathf.Max(0f, damage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지
            staggerThreshold = Mathf.Max(1f, threshold); // 몬스터 경직 한계 최소값 보정
            knockbackResistance = Mathf.Clamp(resistance, 0f, 0.95f); // 넉백 저항 범위 보정
        }

        private void OnValidate() // 인스펙터 데이터 안전 범위 검증
        {
            maxHealth = archetype == MonsterArchetype.SmilingStatue ? 0f : Mathf.Max(1f, maxHealth); // 웃는 석상은 무체력 상태를 유지하고 다른 몬스터만 최소 체력 보정
            moveSpeed = Mathf.Max(0.1f, moveSpeed); // 일반 이동 속도 최소값 보정
            chaseSpeed = Mathf.Max(moveSpeed, chaseSpeed); // 추적 속도 최소값 보정
            retreatSpeed = Mathf.Max(0.1f, retreatSpeed); // 후퇴 속도 최소값 보정
            turnSpeed = Mathf.Max(30f, turnSpeed); // 회전 속도 최소값 보정
            visionRange = Mathf.Max(1f, visionRange); // 시야 거리 최소값 보정
            visionAngle = Mathf.Clamp(visionAngle, 10f, 180f); // 시야각 범위 보정
            visionInterval = Mathf.Clamp(visionInterval, 0.05f, 1f); // 시야 검사 간격 범위 보정
            hearingRange = Mathf.Max(0f, hearingRange); // 청각 거리 음수 방지
            memoryDuration = Mathf.Max(0.1f, memoryDuration); // 기억 시간 최소값 보정
            investigateDuration = Mathf.Max(0.1f, investigateDuration); // 조사 시간 최소값 보정
            preferredMinRange = Mathf.Max(0.3f, preferredMinRange); // 선호 최소 거리 보정
            preferredMaxRange = Mathf.Max(preferredMinRange + 0.1f, preferredMaxRange); // 선호 최대 거리 보정
            attackRange = Mathf.Max(0.5f, attackRange); // 공격 사거리 최소값 보정
            aimTime = Mathf.Max(0.08f, aimTime); // 조준·근접 준비 시간 최소값 보정
            attackCooldown = Mathf.Max(0.2f, attackCooldown); // 공격 쿨타임 최소값 보정
            projectileSpeed = Mathf.Max(5f, projectileSpeed); // 투사체 속도 최소값 보정
            attackDamage = Mathf.Max(0f, attackDamage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, staggerPower); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockbackForce); // 넉백 힘 음수 방지
            staggerThreshold = Mathf.Max(1f, staggerThreshold); // 경직 한계 최소값 보정
            knockbackResistance = Mathf.Clamp(knockbackResistance, 0f, 0.95f); // 넉백 저항 범위 보정
        }
    }
}
