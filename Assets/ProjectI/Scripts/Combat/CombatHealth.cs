using System; // 체력 변경·사망 이벤트 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public sealed class CombatHealth : MonoBehaviour, IDamageable // 몬스터·더미·파괴물용 공통 체력과 피해 대상 구현
    {
        [SerializeField] private string displayName = "Combat Target"; // 진단 화면 표시 이름
        [SerializeField] private CombatFaction faction = CombatFaction.Enemy; // 대상 진영
        [SerializeField] private float maxHealth = 100f; // 최대 체력
        [SerializeField] private float currentHealth = 100f; // 현재 체력

        public event Action<float, float> HealthChanged; // 현재·최대 체력 변경 이벤트
        public event Action<DamageInfo, float> Damaged; // 몬스터 AI가 실제 피해 공격자를 추적할 수 있는 피해 수신 이벤트
        public event Action Died; // 사망 이벤트

        public string DisplayName => displayName; // 진단용 표시 이름 공개
        public CombatFaction Faction => faction; // 공통 피해 대상 진영 공개
        public bool IsAlive => currentHealth > 0f; // 현재 생존 여부 공개
        public Transform DamageTransform => transform; // 대표 피격 Transform 공개
        public float CurrentHealth => currentHealth; // 현재 체력 공개
        public float MaxHealth => maxHealth; // 최대 체력 공개
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth); // 현재 체력 비율 공개

        public void Configure(string targetDisplayName, CombatFaction targetFaction, float targetMaxHealth) // Day14 자동 Setup용 더미 체력 구성
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 표시 이름 저장
            faction = targetFaction; // 대상 진영 저장
            maxHealth = Mathf.Max(1f, targetMaxHealth); // 최대 체력 최소값 보정
            currentHealth = maxHealth; // 테스트 대상 체력을 최대값으로 초기화
        }

        public float ApplyDamage(DamageInfo damageInfo) // 공통 Damage Pipeline 승인 피해 적용
        {
            if (!IsAlive) // 이미 사망 상태인지 확인
            {
                return 0f; // 추가 피해 적용 차단
            }

            float previousHealth = currentHealth; // 피해 전 체력 저장
            currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, damageInfo.BaseDamage)); // 요청 피해량만큼 체력 감소
            float appliedDamage = previousHealth - currentHealth; // 실제 감소한 피해량 계산

            if (appliedDamage > 0f) // 실제 체력 변화 여부 확인
            {
                HealthChanged?.Invoke(currentHealth, maxHealth); // 체력 변경 이벤트 발생
                Damaged?.Invoke(damageInfo, appliedDamage); // AI 대상 선정용 실제 피해 정보와 적용량 전달
            }

            if (previousHealth > 0f && currentHealth <= 0f) // 이번 피해로 사망했는지 확인
            {
                Died?.Invoke(); // 사망 이벤트 발생
            }

            return appliedDamage; // 실제 적용 피해량 반환
        }

        public void ResetHealth() // 테스트 대상 체력 최대값 복구
        {
            currentHealth = maxHealth; // 현재 체력을 최대값으로 복구
            HealthChanged?.Invoke(currentHealth, maxHealth); // 체력 복구 이벤트 발생
        }

        private void OnValidate() // 인스펙터 체력 값 검증
        {
            maxHealth = Mathf.Max(1f, maxHealth); // 최대 체력 최소값 보정
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth); // 현재 체력 범위 보정
        }
    }
}
