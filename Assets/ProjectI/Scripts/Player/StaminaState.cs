namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public sealed class StaminaState // 스태미나 순수 상태 모델
    {
        private readonly float drainPerSecond; // 초당 스태미나 소비량
        private readonly float recoveryPerSecond; // 초당 스태미나 회복량
        private readonly float recoveryDelay; // 회복 시작 대기 시간
        private readonly float restartValue; // 탈진 해제 기준값
        private float recoveryDelayRemaining; // 남은 회복 대기 시간

        public StaminaState(float maxValue, float drainPerSecond, float recoveryPerSecond, float recoveryDelay, float restartValue) // 스태미나 상태 생성
        {
            MaxValue = maxValue > 0f ? maxValue : 1f; // 최대 스태미나 최소값 보정
            this.drainPerSecond = drainPerSecond > 0f ? drainPerSecond : 0f; // 소비량 음수 방지
            this.recoveryPerSecond = recoveryPerSecond > 0f ? recoveryPerSecond : 0f; // 회복량 음수 방지
            this.recoveryDelay = recoveryDelay > 0f ? recoveryDelay : 0f; // 회복 지연 음수 방지
            this.restartValue = Clamp(restartValue, 0f, MaxValue); // 탈진 해제 기준 범위 보정
            CurrentValue = MaxValue; // 시작 스태미나를 최대값으로 설정
        }

        public float MaxValue { get; } // 최대 스태미나 공개
        public float CurrentValue { get; private set; } // 현재 스태미나 공개
        public bool IsExhausted { get; private set; } // 탈진 여부 공개
        public float Normalized => MaxValue <= 0f ? 0f : CurrentValue / MaxValue; // 0~1 스태미나 비율 반환

        public bool Tick(bool sprintRequested, bool isMoving, float deltaTime) // 한 프레임 스태미나 상태 갱신
        {
            if (deltaTime <= 0f) // 유효하지 않은 시간 확인
            {
                return false; // 상태 변경 없이 달리기 중지
            }

            bool canSprint = sprintRequested && isMoving && !IsExhausted && CurrentValue > 0f; // 현재 달리기 가능 여부 계산

            if (canSprint) // 달리기 가능 여부 확인
            {
                CurrentValue = Clamp(CurrentValue - (drainPerSecond * deltaTime), 0f, MaxValue); // 스태미나 소비
                recoveryDelayRemaining = recoveryDelay; // 회복 대기 시간 초기화

                if (CurrentValue <= 0f) // 스태미나 완전 소진 확인
                {
                    IsExhausted = true; // 탈진 상태 활성화
                }

                return true; // 이번 프레임 달리기 허용
            }

            if (recoveryDelayRemaining > 0f) // 회복 대기 시간 확인
            {
                recoveryDelayRemaining = Clamp(recoveryDelayRemaining - deltaTime, 0f, recoveryDelay); // 회복 대기 시간 감소
            }
            else // 회복 가능 상태 처리
            {
                CurrentValue = Clamp(CurrentValue + (recoveryPerSecond * deltaTime), 0f, MaxValue); // 스태미나 회복
            }

            if (IsExhausted && CurrentValue >= restartValue) // 탈진 해제 기준 도달 확인
            {
                IsExhausted = false; // 탈진 상태 해제
            }

            return false; // 이번 프레임 달리기 중지
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

            return value; // 범위 안의 값 그대로 반환
        }
    }
}
