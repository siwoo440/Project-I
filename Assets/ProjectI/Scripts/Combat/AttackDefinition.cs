using UnityEngine; // ScriptableObject와 수치 보정 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    [CreateAssetMenu(menuName = "Project I/Combat/Attack Definition", fileName = "AttackDefinition")] // 공격 데이터 에셋 생성 메뉴 등록
    public sealed class AttackDefinition : ScriptableObject // 검·도끼·몬스터 공격이 공유하는 단일 공격 데이터
    {
        [SerializeField] private string displayName = "Attack"; // 진단 화면 공격 이름
        [SerializeField] private float baseDamage = 25f; // 기본 피해량
        [SerializeField] private CombatDamageType damageType = CombatDamageType.Physical; // 기본 피해 종류
        [SerializeField] private float staminaCost = 12f; // 공격 시작 즉시 소비 스태미나
        [SerializeField] private float windupDuration = 0.15f; // 공격 준비 시간
        [SerializeField] private float activeDuration = 0.18f; // 실제 궤적 판정 시간
        [SerializeField] private float recoveryDuration = 0.30f; // 공격 후 회복 시간
        [SerializeField] private float cooldownDuration = 0.65f; // 공격 시작 기준 다음 공격까지 최소 대기 시간
        [SerializeField] private float movementMultiplier = 0.65f; // 공격 중 이동 속도 배율
        [SerializeField] private float traceRadius = 0.12f; // 근접 무기 궤적 반경
        [SerializeField] private float staggerPower = 10f; // 피격 대상 경직 누적 힘
        [SerializeField] private float knockbackForce = 1.5f; // 피격 대상 넉백 거리 계수
        [SerializeField] private Vector3 windupEuler = new Vector3(-25f, -35f, 20f); // 공격 준비 자세 로컬 회전
        [SerializeField] private Vector3 strikeEuler = new Vector3(20f, 70f, -35f); // 실제 휘두르기 종료 자세 로컬 회전

        public string DisplayName => displayName; // 공격 표시 이름 공개
        public float BaseDamage => baseDamage; // 기본 피해량 공개
        public CombatDamageType DamageType => damageType; // 피해 종류 공개
        public float StaminaCost => staminaCost; // 공격 스태미나 비용 공개
        public float WindupDuration => windupDuration; // 준비 시간 공개
        public float ActiveDuration => activeDuration; // 활성 시간 공개
        public float RecoveryDuration => recoveryDuration; // 회복 시간 공개
        public float CooldownDuration => cooldownDuration; // 다음 공격까지 최소 쿨타임 공개
        public float MovementMultiplier => movementMultiplier; // 공격 중 이동 배율 공개
        public float TraceRadius => traceRadius; // 근접 궤적 반경 공개
        public float StaggerPower => staggerPower; // 경직 힘 공개
        public float KnockbackForce => knockbackForce; // 넉백 힘 공개
        public Vector3 WindupEuler => windupEuler; // 준비 자세 로컬 회전 공개
        public Vector3 StrikeEuler => strikeEuler; // 타격 자세 로컬 회전 공개
        public float TotalAttackDuration => windupDuration + activeDuration + recoveryDuration; // 실제 한 번 휘두르기 동작 시간 공개

        public void Configure(string targetDisplayName, float damage, CombatDamageType type, float stamina, float windup, float active, float recovery, float movement, float radius, float stagger, float knockback) // 기존 Day14 자동 구성 호환용 설정
        {
            ConfigureDetailed(targetDisplayName, damage, type, stamina, windup, active, recovery, windup + active + recovery, movement, radius, stagger, knockback, new Vector3(-25f, -35f, 20f), new Vector3(20f, 70f, -35f)); // 기존 호출을 단발 공격 기본값으로 연결
        }

        public void ConfigureDetailed(string targetDisplayName, float damage, CombatDamageType type, float stamina, float windup, float active, float recovery, float cooldown, float movement, float radius, float stagger, float knockback, Vector3 targetWindupEuler, Vector3 targetStrikeEuler) // Day15 검·도끼 단발 공격 세부 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? name : targetDisplayName; // 공격 표시 이름 저장
            baseDamage = Mathf.Max(0f, damage); // 기본 피해량 음수 방지
            damageType = type; // 피해 종류 저장
            staminaCost = Mathf.Max(0f, stamina); // 공격 스태미나 비용 음수 방지
            windupDuration = Mathf.Max(0.01f, windup); // 준비 시간 최소값 보정
            activeDuration = Mathf.Max(0.01f, active); // 활성 시간 최소값 보정
            recoveryDuration = Mathf.Max(0.01f, recovery); // 회복 시간 최소값 보정
            cooldownDuration = Mathf.Max(0.01f, cooldown); // 쿨타임 최소값 보정
            movementMultiplier = Mathf.Clamp(movement, 0f, 1f); // 공격 중 이동 배율 범위 보정
            traceRadius = Mathf.Clamp(radius, 0.01f, 0.5f); // 궤적 반경 범위 보정
            staggerPower = Mathf.Max(0f, stagger); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockback); // 넉백 힘 음수 방지
            windupEuler = targetWindupEuler; // 무기별 준비 자세 저장
            strikeEuler = targetStrikeEuler; // 무기별 타격 자세 저장
        }

        private void OnValidate() // 공격 데이터 인스펙터 값 검증
        {
            baseDamage = Mathf.Max(0f, baseDamage); // 기본 피해량 음수 방지
            staminaCost = Mathf.Max(0f, staminaCost); // 스태미나 비용 음수 방지
            windupDuration = Mathf.Max(0.01f, windupDuration); // 준비 시간 최소값 보정
            activeDuration = Mathf.Max(0.01f, activeDuration); // 활성 시간 최소값 보정
            recoveryDuration = Mathf.Max(0.01f, recoveryDuration); // 회복 시간 최소값 보정
            cooldownDuration = Mathf.Max(0.01f, cooldownDuration); // 쿨타임 최소값 보정
            movementMultiplier = Mathf.Clamp(movementMultiplier, 0f, 1f); // 이동 배율 범위 보정
            traceRadius = Mathf.Clamp(traceRadius, 0.01f, 0.5f); // 궤적 반경 범위 보정
            staggerPower = Mathf.Max(0f, staggerPower); // 경직 힘 음수 방지
            knockbackForce = Mathf.Max(0f, knockbackForce); // 넉백 힘 음수 방지
        }
    }
}
