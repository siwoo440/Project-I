namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public sealed class HealthState // 체력 순수 상태 모델
    {
        public HealthState(float maxHealth) // 체력 상태 생성
        {
            MaxHealth = maxHealth > 0f ? maxHealth : 1f; // 최대 체력 최소값 보정
            CurrentHealth = MaxHealth; // 시작 체력을 최대값으로 설정
        }

        public float MaxHealth { get; } // 최대 체력 공개
        public float CurrentHealth { get; private set; } // 현재 체력 공개
        public bool IsDead => CurrentHealth <= 0f; // 사망 여부 공개
        public float Normalized => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth; // 체력 비율 공개

        public float ApplyDamage(float amount) // 피해 적용
        {
            if (amount <= 0f || IsDead) // 무효 피해 또는 사망 상태 확인
            {
                return 0f; // 실제 피해 없음 반환
            }

            float previousHealth = CurrentHealth; // 피해 전 체력 저장
            CurrentHealth = Clamp(CurrentHealth - amount, 0f, MaxHealth); // 체력 감소 적용
            return previousHealth - CurrentHealth; // 실제 적용 피해량 반환
        }

        public float Heal(float amount) // 체력 회복
        {
            if (amount <= 0f || IsDead) // 무효 회복 또는 사망 상태 확인
            {
                return 0f; // 실제 회복 없음 반환
            }

            float previousHealth = CurrentHealth; // 회복 전 체력 저장
            CurrentHealth = Clamp(CurrentHealth + amount, 0f, MaxHealth); // 체력 회복 적용
            return CurrentHealth - previousHealth; // 실제 회복량 반환
        }

        private static float Clamp(float value, float minimum, float maximum) // 실수 범위 제한
        {
            if (value < minimum) // 최소값 미만 확인
            {
                return minimum; // 최소값 반환
            }

            if (value > maximum) // 최대값 초과 확인
            {
                return maximum; // 최대값 반환
            }

            return value; // 범위 안의 값 반환
        }
    }
}
