using UnityEngine; // Transform 보간·시간 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class FloorSpikeTrap : TrapControllerBase // 바닥에서 위로 솟아오르는 Trigger형 가시 함정
    {
        [SerializeField] private Transform movingSpikes; // 위아래로 이동하는 가시 묶음
        [SerializeField] private Vector3 hiddenLocalPosition; // 바닥 아래 숨김 위치
        [SerializeField] private Vector3 raisedLocalPosition; // 공격 시 완전 상승 위치
        [SerializeField] private float warningDuration = 0.18f; // 작동 전 짧은 경고 시간
        [SerializeField] private float riseDuration = 0.12f; // 가시 상승 시간
        [SerializeField] private float activeDuration = 0.32f; // 상승 후 피해 유지 시간
        [SerializeField] private float resetDuration = 0.36f; // 바닥 아래로 복귀 시간
        [SerializeField] private float cooldownDuration = 1.60f; // 재사용 대기 시간
        [SerializeField] private float damage = 35f; // 기본 가시 피해량
        [SerializeField] private float staggerPower = 25f; // 기본 가시 경직 힘
        [SerializeField] private float knockbackForce = 0.3f; // 기본 가시 넉백 힘
        private float stateTime; // 현재 상태 경과 시간
        private int currentAttackId; // 현재 작동 Damage Pipeline ID

        public float Damage => damage; // Validator·F1용 피해량 공개
        public float CooldownDuration => cooldownDuration; // F1용 재사용 시간 공개

        private void Awake() // 런타임 시작 시 가시 위치 초기화
        {
            if (movingSpikes != null) // 가시 묶음 존재 여부 확인
            {
                movingSpikes.localPosition = hiddenLocalPosition; // 시작 시 가시를 바닥 아래 숨김
            }
        }

        private void Update() // 가시 경고·상승·활성·복귀·쿨다운 상태 진행
        {
            stateTime += Time.deltaTime; // 현재 상태 경과 시간 누적

            if (state == TrapState.Warning && stateTime >= warningDuration) // 경고 시간 종료 여부 확인
            {
                state = TrapState.Triggered; // 가시 상승 단계 진입
                stateTime = 0f; // 새 상태 시간 초기화
                damageSource?.BeginDamageWindow(DisplayName, damage, staggerPower, knockbackForce, Vector3.up, currentAttackId); // 상승 순간부터 피해 창 활성화
            }
            else if (state == TrapState.Triggered) // 가시 상승 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, riseDuration)); // 상승 진행률 계산
                SetSpikePosition(Vector3.Lerp(hiddenLocalPosition, raisedLocalPosition, progress)); // 가시 묶음 상승 보간

                if (progress >= 1f) // 상승 완료 여부 확인
                {
                    state = TrapState.Active; // 완전 상승 피해 유지 상태 진입
                    stateTime = 0f; // 상태 시간 초기화
                }
            }
            else if (state == TrapState.Active && stateTime >= activeDuration) // 피해 유지 시간 종료 여부 확인
            {
                damageSource?.EndDamageWindow(); // 추가 피해 판정 종료
                state = TrapState.Resetting; // 가시 하강 단계 진입
                stateTime = 0f; // 상태 시간 초기화
            }
            else if (state == TrapState.Resetting) // 가시 복귀 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, resetDuration)); // 하강 진행률 계산
                SetSpikePosition(Vector3.Lerp(raisedLocalPosition, hiddenLocalPosition, progress)); // 가시 묶음 바닥 아래 복귀 보간

                if (progress >= 1f) // 복귀 완료 여부 확인
                {
                    state = TrapState.Cooldown; // 재사용 대기 상태 진입
                    stateTime = 0f; // 상태 시간 초기화
                }
            }
            else if (state == TrapState.Cooldown && stateTime >= cooldownDuration) // 재사용 대기 완료 여부 확인
            {
                state = TrapState.Ready; // 다음 압력판·Trigger 입력 허용
                stateTime = 0f; // 상태 시간 초기화
            }
        }

        public override bool TriggerTrap(GameObject triggerSource = null) // 압력판·숨은 Trigger가 바닥 가시를 작동시킴
        {
            if (!CanTrigger || state != TrapState.Ready) // 이미 작동 중인지 확인
            {
                return false; // 중복 작동 요청 거부
            }

            currentAttackId = BeginActivation(triggerSource); // 새 작동 공격 ID 생성
            state = TrapState.Warning; // 경고 상태 시작
            stateTime = 0f; // 경고 시간 초기화
            return true; // 작동 요청 성공 반환
        }

        public void Configure(string targetName, TrapDamageSource source, Transform targetMovingSpikes, Vector3 hiddenPosition, Vector3 raisedPosition, float targetDamage, float targetStagger, float targetKnockback) // Editor Setup용 가시 함정 구성
        {
            ConfigureBase(targetName, source); // 공통 함정 이름·피해 소스 구성
            movingSpikes = targetMovingSpikes; // 이동 가시 묶음 저장
            hiddenLocalPosition = hiddenPosition; // 숨김 위치 저장
            raisedLocalPosition = raisedPosition; // 상승 위치 저장
            damage = Mathf.Max(0f, targetDamage); // 피해량 저장
            staggerPower = Mathf.Max(0f, targetStagger); // 경직 값 저장
            knockbackForce = Mathf.Max(0f, targetKnockback); // 넉백 값 저장
            state = TrapState.Ready; // 최초 작동 가능 상태 설정
            SetSpikePosition(hiddenLocalPosition); // Edit Mode에서도 시작 위치 정렬
        }

        private void SetSpikePosition(Vector3 position) // 가시 묶음 위치 안전 설정
        {
            if (movingSpikes != null) // 이동 Transform 존재 여부 확인
            {
                movingSpikes.localPosition = position; // 현재 로컬 위치 적용
            }
        }
    }
}
