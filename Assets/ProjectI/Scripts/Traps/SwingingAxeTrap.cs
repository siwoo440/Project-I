using UnityEngine; // Quaternion 보간·시간 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class SwingingAxeTrap : TrapControllerBase // 통로를 빠르게 가로지르는 회전 도끼 함정
    {
        [SerializeField] private Transform pivot; // 도끼 자루·날 전체 회전 기준점
        [SerializeField] private Vector3 startEuler = new Vector3(0f, 0f, -72f); // 대기 중 한쪽 벽 방향 각도
        [SerializeField] private Vector3 endEuler = new Vector3(0f, 0f, 72f); // 공격 시 반대편까지 가르는 각도
        [SerializeField] private float warningDuration = 0.28f; // Swing 직전 지연 시간
        [SerializeField] private float swingDuration = 0.34f; // 통로를 가르는 실제 공격 시간
        [SerializeField] private float holdDuration = 0.08f; // 반대편 도달 후 짧은 유지 시간
        [SerializeField] private float resetDuration = 0.58f; // 원위치 복귀 시간
        [SerializeField] private float cooldownDuration = 1.20f; // 재사용 대기 시간
        [SerializeField] private float damage = 55f; // 도끼 기본 피해량
        [SerializeField] private float staggerPower = 40f; // 도끼 경직 힘
        [SerializeField] private float knockbackForce = 2f; // 도끼 강한 횡방향 넉백
        private float stateTime; // 현재 상태 경과 시간
        private int currentAttackId; // 현재 Swing 공격 ID

        public float Damage => damage; // Validator·F1용 피해량 공개

        private void Awake() // 시작 시 도끼 원위치 설정
        {
            SetPivotEuler(startEuler); // 대기 각도로 도끼 정렬
        }

        private void Update() // 도끼 경고·Swing·유지·복귀·쿨다운 상태 처리
        {
            stateTime += Time.deltaTime; // 현재 상태 경과 시간 누적

            if (state == TrapState.Warning && stateTime >= warningDuration) // 경고 시간 종료 여부 확인
            {
                state = TrapState.Triggered; // 실제 Swing 상태 진입
                stateTime = 0f; // 상태 시간 초기화
                damageSource?.BeginDamageWindow(DisplayName, damage, staggerPower, knockbackForce, transform.right, currentAttackId); // Swing 동안 공통 함정 피해 창 활성화
            }
            else if (state == TrapState.Triggered) // 실제 도끼 Swing 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, swingDuration)); // 공격 진행률 계산
                float eased = Mathf.SmoothStep(0f, 1f, progress); // 도끼 회전 가감속 보간
                SetPivotEuler(Vector3.Lerp(startEuler, endEuler, eased)); // 한쪽 벽에서 반대편까지 회전

                if (progress >= 1f) // 공격 회전 완료 여부 확인
                {
                    state = TrapState.Active; // 반대편 짧은 유지 상태 진입
                    stateTime = 0f; // 상태 시간 초기화
                }
            }
            else if (state == TrapState.Active && stateTime >= holdDuration) // 공격 후 유지 종료 여부 확인
            {
                damageSource?.EndDamageWindow(); // Swing 피해 창 종료
                state = TrapState.Resetting; // 도끼 원위치 복귀 시작
                stateTime = 0f; // 상태 시간 초기화
            }
            else if (state == TrapState.Resetting) // 도끼 원위치 복귀 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, resetDuration)); // 복귀 진행률 계산
                SetPivotEuler(Vector3.Lerp(endEuler, startEuler, progress)); // 반대편에서 대기 위치로 천천히 복귀

                if (progress >= 1f) // 복귀 완료 여부 확인
                {
                    state = TrapState.Cooldown; // 재작동 대기 상태 진입
                    stateTime = 0f; // 상태 시간 초기화
                }
            }
            else if (state == TrapState.Cooldown && stateTime >= cooldownDuration) // 도끼 재사용 대기 완료 여부 확인
            {
                state = TrapState.Ready; // 다음 Trigger 허용
                stateTime = 0f; // 상태 시간 초기화
            }
        }

        public override bool TriggerTrap(GameObject triggerSource = null) // 숨은 Trigger·압력판에서 도끼 공격 시작
        {
            if (state != TrapState.Ready) // 이미 작동 중인지 확인
            {
                return false; // 중복 공격 요청 거부
            }

            currentAttackId = BeginActivation(triggerSource); // 새 Swing 공격 ID 생성
            state = TrapState.Warning; // 경고 단계 시작
            stateTime = 0f; // 상태 시간 초기화
            return true; // 작동 성공 반환
        }

        public void Configure(string targetName, TrapDamageSource source, Transform targetPivot, float targetDamage, float targetStagger, float targetKnockback) // Editor Setup용 도끼 함정 구성
        {
            ConfigureBase(targetName, source); // 공통 이름·피해 소스 저장
            pivot = targetPivot; // 회전 기준 Transform 저장
            damage = Mathf.Max(0f, targetDamage); // 피해량 저장
            staggerPower = Mathf.Max(0f, targetStagger); // 경직 값 저장
            knockbackForce = Mathf.Max(0f, targetKnockback); // 넉백 값 저장
            state = TrapState.Ready; // 최초 Trigger 가능 상태 설정
            SetPivotEuler(startEuler); // Edit Mode 대기 각도 정렬
        }

        private void SetPivotEuler(Vector3 euler) // 도끼 Pivot 회전 안전 적용
        {
            if (pivot != null) // Pivot 존재 여부 확인
            {
                pivot.localRotation = Quaternion.Euler(euler); // 현재 로컬 회전 적용
            }
        }
    }
}
