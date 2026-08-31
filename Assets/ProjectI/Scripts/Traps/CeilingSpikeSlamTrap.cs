using UnityEngine; // Transform 보간·시간·진동 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class CeilingSpikeSlamTrap : TrapControllerBase // 천장에서 아래로 일정 주기로 찍어내리는 자동 가시판 함정
    {
        [SerializeField] private Transform movingPlate; // 천장과 바닥 사이를 이동하는 가시판
        [SerializeField] private Vector3 topLocalPosition; // 천장 대기 위치
        [SerializeField] private Vector3 bottomLocalPosition; // 내려찍기 최저 위치
        [SerializeField] private float waitDuration = 2.5f; // 자동 반복 사이 대기 시간
        [SerializeField] private float warningDuration = 0.55f; // 내려찍기 전 진동 경고 시간
        [SerializeField] private float slamDuration = 0.18f; // 천장부터 바닥까지 빠른 낙하 시간
        [SerializeField] private float activeDuration = 0.35f; // 바닥 근처 유지 시간
        [SerializeField] private float returnDuration = 0.75f; // 천장으로 복귀 시간
        [SerializeField] private float startOffset; // 여러 자동 함정 주기 분산용 최초 지연
        [SerializeField] private float damage = 70f; // 내려찍기 강한 피해량
        [SerializeField] private float staggerPower = 55f; // 강한 경직 힘
        [SerializeField] private float knockbackForce = 1.0f; // 내려찍기 넉백 힘
        private float stateTime; // 현재 상태 경과 시간
        private int currentAttackId; // 현재 내려찍기 공격 ID
        private Vector3 warningBasePosition; // 경고 진동 기준 위치

        public override bool CanTrigger => state == TrapState.Waiting; // 자동 대기 중 외부 강제 작동도 허용
        public float Damage => damage; // Validator·F1용 피해량 공개
        public float CycleWaitDuration => waitDuration; // F1용 주기 대기 시간 공개

        private void Awake() // 런타임 자동 주기 초기화
        {
            SetPlatePosition(topLocalPosition); // 시작 시 천장 위치로 정렬
            warningBasePosition = topLocalPosition; // 경고 진동 기준 저장
            state = TrapState.Waiting; // 자동 대기 상태 시작
            stateTime = -Mathf.Max(0f, startOffset); // 개별 시작 오프셋만큼 첫 주기 지연
        }

        private void Update() // 자동 경고·낙하·유지·복귀 주기 진행
        {
            stateTime += Time.deltaTime; // 현재 상태 경과 시간 누적

            if (state == TrapState.Waiting && stateTime >= waitDuration) // 다음 자동 주기 시작 시각 확인
            {
                currentAttackId = BeginActivation(null); // 자동 작동 공격 ID 생성
                state = TrapState.Warning; // 내려찍기 전 경고 단계 진입
                stateTime = 0f; // 상태 시간 초기화
                warningBasePosition = topLocalPosition; // 경고 진동 기준 재설정
            }
            else if (state == TrapState.Warning) // 천장 가시판 경고 중 여부 확인
            {
                float shake = Mathf.Sin(stateTime * 70f) * 0.035f; // 작은 좌우 진동값 계산
                SetPlatePosition(warningBasePosition + new Vector3(shake, 0f, 0f)); // 체인·금속 흔들림을 위치 진동으로 표현

                if (stateTime >= warningDuration) // 경고 시간 종료 여부 확인
                {
                    state = TrapState.Triggered; // 빠른 내려찍기 단계 진입
                    stateTime = 0f; // 상태 시간 초기화
                    damageSource?.BeginDamageWindow(DisplayName, damage, staggerPower, knockbackForce, Vector3.down, currentAttackId); // 낙하 시작과 함께 이동 피해 창 활성화
                }
            }
            else if (state == TrapState.Triggered) // 가시판 낙하 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, slamDuration)); // 낙하 진행률 계산
                float eased = progress * progress; // 초반보다 후반이 빨라지는 내려찍기 가속 표현
                SetPlatePosition(Vector3.Lerp(topLocalPosition, bottomLocalPosition, eased)); // 천장에서 바닥까지 가시판 이동

                if (progress >= 1f) // 바닥 위치 도달 여부 확인
                {
                    state = TrapState.Active; // 바닥 근처 유지 상태 진입
                    stateTime = 0f; // 상태 시간 초기화
                }
            }
            else if (state == TrapState.Active && stateTime >= activeDuration) // 바닥 유지 시간 종료 여부 확인
            {
                damageSource?.EndDamageWindow(); // 한 내려찍기 피해 창 종료
                state = TrapState.Resetting; // 천장 복귀 상태 진입
                stateTime = 0f; // 상태 시간 초기화
            }
            else if (state == TrapState.Resetting) // 천장 복귀 중 여부 확인
            {
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, returnDuration)); // 상승 진행률 계산
                SetPlatePosition(Vector3.Lerp(bottomLocalPosition, topLocalPosition, progress)); // 가시판 천장 위치로 복귀

                if (progress >= 1f) // 천장 복귀 완료 여부 확인
                {
                    state = TrapState.Waiting; // 다음 자동 주기 대기 상태 진입
                    stateTime = 0f; // 주기 대기 시간 초기화
                }
            }
        }

        public override bool TriggerTrap(GameObject triggerSource = null) // 외부 Trigger가 자동 대기를 앞당기는 선택 기능
        {
            if (state != TrapState.Waiting) // 현재 자동 대기 상태인지 확인
            {
                return false; // 이미 작동 중이면 강제 작동 거부
            }

            currentAttackId = BeginActivation(triggerSource); // 외부 작동 공격 ID 생성
            state = TrapState.Warning; // 즉시 경고 단계 진입
            stateTime = 0f; // 상태 시간 초기화
            return true; // 강제 작동 성공 반환
        }

        public void Configure(string targetName, TrapDamageSource source, Transform targetPlate, Vector3 topPosition, Vector3 bottomPosition, float targetDamage, float targetStagger, float targetKnockback, float targetStartOffset) // Editor Setup용 천장 가시 구성
        {
            ConfigureBase(targetName, source); // 공통 함정 이름·피해 소스 구성
            movingPlate = targetPlate; // 이동 가시판 저장
            topLocalPosition = topPosition; // 천장 위치 저장
            bottomLocalPosition = bottomPosition; // 바닥 위치 저장
            damage = Mathf.Max(0f, targetDamage); // 피해량 저장
            staggerPower = Mathf.Max(0f, targetStagger); // 경직 값 저장
            knockbackForce = Mathf.Max(0f, targetKnockback); // 넉백 값 저장
            startOffset = Mathf.Max(0f, targetStartOffset); // 첫 주기 오프셋 저장
            state = TrapState.Waiting; // 자동 대기 상태 설정
            SetPlatePosition(topLocalPosition); // Edit Mode 기본 위치 적용
        }

        private void SetPlatePosition(Vector3 position) // 이동 가시판 위치 안전 적용
        {
            if (movingPlate != null) // 이동 Transform 존재 여부 확인
            {
                movingPlate.localPosition = position; // 현재 로컬 위치 적용
            }
        }
    }
}
