using System; // 체력 이벤트 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public sealed class PlayerHealth : MonoBehaviour // 플레이어 체력 컴포넌트
    {
        [SerializeField] private float maxHealth = 100f; // 최대 체력
        private HealthState state; // 실제 체력 상태 모델

        public event Action<float, float> HealthChanged; // 현재 체력과 최대 체력 변경 이벤트
        public event Action Died; // 사망 이벤트

        public float CurrentHealth => state == null ? maxHealth : state.CurrentHealth; // 현재 체력 반환
        public float MaxHealth => state == null ? maxHealth : state.MaxHealth; // 최대 체력 반환
        public float Normalized => state == null ? 1f : state.Normalized; // 체력 비율 반환
        public bool IsDead => state != null && state.IsDead; // 사망 여부 반환

        private void Awake() // 체력 컴포넌트 초기화
        {
            state = new HealthState(maxHealth); // 설정값으로 체력 상태 생성
        }

        public float TakeDamage(float amount) // 공통 피해 적용
        {
            if (state == null) // 상태 미생성 확인
            {
                state = new HealthState(maxHealth); // 체력 상태 지연 생성
            }

            bool wasDead = state.IsDead; // 피해 전 사망 여부 저장
            float appliedDamage = state.ApplyDamage(amount); // 실제 피해 적용

            if (appliedDamage > 0f) // 체력 변화 여부 확인
            {
                HealthChanged?.Invoke(state.CurrentHealth, state.MaxHealth); // 체력 변경 이벤트 발생
            }

            if (!wasDead && state.IsDead) // 이번 피해로 사망했는지 확인
            {
                Debug.Log("[Project I] Player 사망 상태에 진입했습니다.", this); // 개발용 사망 로그 출력
                Died?.Invoke(); // 사망 이벤트 발생
            }

            return appliedDamage; // 실제 적용 피해량 반환
        }

        public float Heal(float amount) // 공통 체력 회복
        {
            if (state == null) // 상태 미생성 확인
            {
                state = new HealthState(maxHealth); // 체력 상태 지연 생성
            }

            float healedAmount = state.Heal(amount); // 실제 체력 회복 적용

            if (healedAmount > 0f) // 체력 변화 여부 확인
            {
                HealthChanged?.Invoke(state.CurrentHealth, state.MaxHealth); // 체력 변경 이벤트 발생
            }

            return healedAmount; // 실제 회복량 반환
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            maxHealth = Mathf.Max(1f, maxHealth); // 최대 체력 최소값 보정
        }
    }
}
