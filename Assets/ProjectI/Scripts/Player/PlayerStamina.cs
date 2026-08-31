using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public sealed class PlayerStamina : MonoBehaviour // 플레이어 스태미나 컴포넌트
    {
        [SerializeField] private float maxStamina = 100f; // 최대 스태미나
        [SerializeField] private float sprintDrainPerSecond = 18f; // 달리기 초당 소비량
        [SerializeField] private float recoveryPerSecond = 25f; // 초당 회복량
        [SerializeField] private float recoveryDelay = 0.75f; // 달리기 종료 후 회복 지연
        [SerializeField] private float restartValue = 15f; // 탈진 후 달리기 재개 기준
        private StaminaState state; // 실제 스태미나 상태 모델

        public float CurrentStamina => state == null ? maxStamina : state.CurrentValue; // 현재 스태미나 반환
        public float MaxStamina => state == null ? maxStamina : state.MaxValue; // 최대 스태미나 반환
        public float Normalized => state == null ? 1f : state.Normalized; // 스태미나 비율 반환
        public bool IsExhausted => state != null && state.IsExhausted; // 탈진 여부 반환

        private void Awake() // 런타임 초기화
        {
            CreateState(); // 스태미나 상태 생성
        }

        public bool UpdateSprint(bool sprintRequested, bool isMoving, float deltaTime) // 달리기 요청과 스태미나 갱신
        {
            EnsureState(); // 스태미나 상태 존재 보장
            return state.Tick(sprintRequested, isMoving, deltaTime); // 현재 달리기 가능 여부 반환
        }

        public bool CanSpend(float amount) // 공격 등 즉시 소비 가능 여부 확인
        {
            EnsureState(); // 스태미나 상태 존재 보장
            return state.CanSpend(amount); // 순수 상태 모델의 소비 가능 여부 반환
        }

        public bool TrySpend(float amount) // 공격 등 즉시 스태미나 소비 시도
        {
            EnsureState(); // 스태미나 상태 존재 보장
            return state.TrySpend(amount); // 순수 상태 모델에 즉시 소비 요청 전달
        }

        private void EnsureState() // 스태미나 상태 지연 생성 보장
        {
            if (state == null) // 상태 미생성 여부 확인
            {
                CreateState(); // 누락 상태 생성
            }
        }

        private void CreateState() // 스태미나 상태 생성
        {
            state = new StaminaState(maxStamina, sprintDrainPerSecond, recoveryPerSecond, recoveryDelay, restartValue); // 설정값으로 상태 생성
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            maxStamina = Mathf.Max(1f, maxStamina); // 최대 스태미나 최소값 보정
            sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond); // 소비량 음수 방지
            recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond); // 회복량 음수 방지
            recoveryDelay = Mathf.Max(0f, recoveryDelay); // 회복 지연 음수 방지
            restartValue = Mathf.Clamp(restartValue, 0f, maxStamina); // 재개 기준 범위 보정
        }
    }
}
