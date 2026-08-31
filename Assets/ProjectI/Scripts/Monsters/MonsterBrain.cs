using System; // AI 상태 변경 이벤트 기능 참조
using ProjectI.Combat; // 공통 체력·경직·피해 데이터 참조
using UnityEngine; // MonoBehaviour와 거리 계산 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class MonsterBrain : MonoBehaviour // 감지·기억·이동·근접·원거리 공격을 연결하는 공통 몬스터 상태 머신
    {
        [SerializeField] private MonsterData data; // 몬스터 행동 수치 데이터
        [SerializeField] private CombatHealth health; // 몬스터 공통 체력
        [SerializeField] private CombatReaction reaction; // 몬스터 공통 경직·넉백 반응
        [SerializeField] private MonsterSensor sensor; // 시각·청각 감지
        [SerializeField] private MonsterTargetSelector targetSelector; // 현재 대상·마지막 위치 기억
        [SerializeField] private MonsterMotor motor; // 실제 충돌 이동 계층
        [SerializeField] private CorruptedUndeadArcherAttack rangedAttack; // 궁수형 원거리 공격 구현
        [SerializeField] private MonsterMeleeAttack meleeAttack; // 부패한 망자·미믹 근접 공격 구현
        private MonsterState state = MonsterState.Idle; // 현재 AI 상태
        private float stateChangedTime; // 마지막 상태 전환 시각
        private Transform damageAggroTarget; // 피해를 준 공격자 우선 추적 대상
        private float lastProcessedNoiseTime = -999f; // 같은 소음을 매 프레임 다시 기억하지 않도록 마지막 처리 시각 저장
        private float suspiciousUntil; // 새 소리를 들은 직후 잠깐 멈춰 경계하는 시간

        public event Action<MonsterState> StateChanged; // AI 상태 전환 이벤트

        public MonsterData Data => data; // F1·Validator용 몬스터 데이터 공개
        public MonsterState State => state; // 현재 AI 상태 공개
        public CombatHealth Health => health; // 현재 체력 참조 공개
        public CombatReaction Reaction => reaction; // 현재 경직 반응 참조 공개
        public MonsterSensor Sensor => sensor; // 감각 참조 공개
        public MonsterTargetSelector TargetSelector => targetSelector; // 대상 선택기 참조 공개
        public MonsterMotor Motor => motor; // 이동 계층 참조 공개
        public CorruptedUndeadArcherAttack RangedAttack => rangedAttack; // 궁수 공격 기능 공개
        public MonsterMeleeAttack MeleeAttack => meleeAttack; // 근접 공격 기능 공개
        public Transform CurrentTarget => targetSelector == null ? null : targetSelector.CurrentTarget; // 현재 추적 대상 공개
        public float StateAge => Time.time - stateChangedTime; // 현재 상태 유지 시간 공개
        public float DistanceToTarget => CurrentTarget == null ? float.PositiveInfinity : Vector3.Distance(transform.position, CurrentTarget.position); // 현재 대상까지 거리 공개

        private void Awake() // 공통 AI 필수 참조 초기화
        {
            ResolveReferences(); // 같은 몬스터 루트 컴포넌트 자동 확보
        }

        private void OnEnable() // AI 활성화 시 체력 이벤트 연결
        {
            ResolveReferences(); // 활성화 직후 참조 재확인
            SubscribeHealth(); // 피해·사망 이벤트 구독
            stateChangedTime = Time.time; // 시작 상태 시각 기록
        }

        private void OnDisable() // AI 비활성화 시 이벤트와 이동 정리
        {
            UnsubscribeHealth(); // 체력 이벤트 구독 해제
            rangedAttack?.CancelAttack(); // 비활성화 중 남은 원거리 조준 취소
            meleeAttack?.CancelAttack(); // 비활성화 중 남은 근접 공격 취소
            motor?.Stop(); // 비활성화 중 이동 정지
        }

        private void Update() // 프레임별 공통 상태 머신 판단
        {
            if (data == null || health == null || sensor == null || targetSelector == null || motor == null) // 필수 AI 참조 존재 여부 확인
            {
                return; // AI 판단 중단
            }

            if (!health.IsAlive) // 몬스터 사망 상태 확인
            {
                EnterDeadState(); // 사망 상태 유지
                return; // 추가 AI 처리 차단
            }

            if (reaction != null && (reaction.IsStaggered || reaction.KnockbackDistanceRemaining > 0.01f)) // 공통 경직·넉백 반응 중인지 확인
            {
                SetState(MonsterState.Staggered); // 경직 상태 표시
                motor.Stop(); // AI 이동 중지
                rangedAttack?.CancelAttack(); // 피격 중 원거리 공격 취소
                meleeAttack?.CancelAttack(); // 피격 중 근접 공격 취소
                return; // 경직·넉백 종료 전 AI 판단 중단
            }

            Transform visible = sensor.VisibleTarget; // 현재 직접 시야 플레이어 조회

            if (visible != null) // 플레이어 직접 시야 확보 여부 확인
            {
                targetSelector.SetTarget(visible); // 현재 공격 대상 지정
                targetSelector.Remember(visible.position, data.MemoryDuration); // 직접 본 플레이어 위치 기억 갱신
                damageAggroTarget = visible; // 피해 우선 대상도 현재 플레이어로 동기화
            }
            else if (damageAggroTarget != null && targetSelector.CurrentTarget == null) // 직접 시야는 없지만 최근 공격자 우선 대상 존재 여부 확인
            {
                targetSelector.SetTarget(damageAggroTarget); // 최근 공격자를 현재 대상 참조로 유지
            }

            if (IsAnyAttackBusy()) // 현재 근접 또는 원거리 공격 동작 중인지 확인
            {
                SetState(MonsterState.Attack); // 공격 상태 유지
                motor.Stop(); // 공격 중 위치 고정

                if (targetSelector.CurrentTarget != null) // 현재 대상 존재 여부 확인
                {
                    motor.FaceTarget(targetSelector.CurrentTarget.position); // 공격 중 플레이어 방향 바라보기
                }

                return; // 공격 완료는 개별 공격 컴포넌트가 처리
            }

            if (visible != null) // 직접 시야가 유지되는 전투 판단
            {
                HandleVisibleTarget(visible); // 몬스터 공격 유형에 맞는 추적·공격·후퇴 처리
                return; // 직접 시야 전투 판단 완료
            }

            if (sensor.TrySpecialSense(this, out Transform specialTarget, out Vector3 specialPosition)) // 향후 특수 감각 구현체 감지 여부 확인
            {
                if (specialTarget != null) // 특수 감각이 직접 대상을 반환했는지 확인
                {
                    targetSelector.SetTarget(specialTarget); // 특수 대상 현재 대상으로 지정
                }

                targetSelector.Remember(specialPosition, data.InvestigateDuration); // 특수 감지 위치 조사 기억 저장
                SetState(MonsterState.Suspicious); // 특수 감지 경계 상태 표시
                motor.MoveTo(specialPosition, data.MoveSpeed); // 특수 감지 위치로 이동
                return; // 특수 감지 처리 완료
            }

            if (sensor.HasRecentNoise && sensor.LastHeardTime > lastProcessedNoiseTime) // 아직 처리하지 않은 새로운 청각 단서인지 확인
            {
                lastProcessedNoiseTime = sensor.LastHeardTime; // 이번 소음을 처리한 시각으로 기록
                targetSelector.Remember(sensor.LastHeardPosition, data.InvestigateDuration); // 마지막 소음 위치를 한 번만 기억
                suspiciousUntil = Time.time + 0.35f; // 소리를 듣자마자 뛰지 않고 잠깐 경계하는 시간 설정
                SetState(MonsterState.Suspicious); // 소음 감지 경계 상태 표시
                motor.Stop(); // 짧은 경계 순간 이동 정지
            }

            if (Time.time < suspiciousUntil) // 새 소리를 들은 직후 경계 시간이 남았는지 확인
            {
                SetState(MonsterState.Suspicious); // 경계 상태 유지
                motor.Stop(); // 경계 중 이동 정지
                return; // 경계 시간이 끝난 뒤 조사 이동 시작
            }

            if (targetSelector.HasMemory) // 마지막 시야·소리 위치 기억이 유효한지 확인
            {
                HandleInvestigation(); // 마지막 위치 조사 이동 처리
                return; // 조사 상태 처리 완료
            }

            targetSelector.ClearTarget(); // 기억이 끝난 직접 대상 참조 해제
            damageAggroTarget = null; // 피해 우선 대상 기억 종료
            SetState(MonsterState.Idle); // 완전 대기 상태 전환
            motor.Stop(); // 대기 중 이동 정지
        }

        public void Configure(MonsterData targetData, CombatHealth targetHealth, CombatReaction targetReaction, MonsterSensor targetSensor, MonsterTargetSelector selector, MonsterMotor targetMotor, CorruptedUndeadArcherAttack targetRangedAttack, MonsterMeleeAttack targetMeleeAttack) // Day17 자동 Setup용 공통 AI 참조 구성
        {
            data = targetData; // 몬스터 행동 데이터 저장
            health = targetHealth; // 공통 체력 저장
            reaction = targetReaction; // 공통 경직 반응 저장
            sensor = targetSensor; // 시각·청각 감각 저장
            targetSelector = selector; // 대상 기억 기능 저장
            motor = targetMotor; // 실제 이동 계층 저장
            rangedAttack = targetRangedAttack; // 원거리 공격 저장
            meleeAttack = targetMeleeAttack; // 근접 공격 저장
        }

        public void Configure(MonsterData targetData, CombatHealth targetHealth, CombatReaction targetReaction, MonsterSensor targetSensor, MonsterTargetSelector selector, MonsterMotor targetMotor, CorruptedUndeadArcherAttack targetRangedAttack) // 기존 궁수 Setup 호출 호환 오버로드
        {
            Configure(targetData, targetHealth, targetReaction, targetSensor, selector, targetMotor, targetRangedAttack, null); // 기존 궁수 호출을 확장된 구성 함수로 연결
        }

        public void ForceTarget(Transform target) // 미믹 변신 등 특수 행동에서 플레이어를 즉시 공통 AI 대상으로 주입
        {
            if (target == null || data == null || targetSelector == null) // 대상 또는 필수 데이터 누락 여부 확인
            {
                return; // 강제 대상 지정 중단
            }

            damageAggroTarget = target; // 피해 우선 대상과 동일한 강제 대상 저장
            targetSelector.SetTarget(target); // 현재 직접 추적 대상으로 지정
            targetSelector.Remember(target.position, data.MemoryDuration); // 대상 현재 위치를 마지막 확인 위치로 기억
        }

        private void HandleVisibleTarget(Transform target) // 직접 보이는 플레이어와 공격 유형별 전투 판단
        {
            float distance = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up).magnitude; // 플레이어까지 수평 거리 계산

            if (meleeAttack != null && rangedAttack == null) // 근접형 부패한 망자·미믹 공격 구조 여부 확인
            {
                if (distance <= data.AttackRange) // 근접 공격 거리 진입 여부 확인
                {
                    SetState(MonsterState.Attack); // 근접 공격 상태 전환
                    motor.Stop(); // 공격 시 위치 정지
                    motor.FaceTarget(target.position); // 플레이어 방향 정렬

                    if (meleeAttack.CanStartAttack) // 공격 쿨타임 종료 여부 확인
                    {
                        meleeAttack.TryStartAttack(target); // 공통 단발 근접 공격 시작
                    }

                    return; // 근접 공격 처리 완료
                }

                SetState(MonsterState.Chase); // 근접형 공격 거리 밖 추적 상태 전환
                motor.MoveTo(target.position, data.ChaseSpeed); // 플레이어 현재 위치 추적
                return; // 근접형 판단 완료
            }

            if (distance < data.PreferredMinRange) // 원거리 궁수 최소 선호 거리보다 가까운지 확인
            {
                SetState(MonsterState.Retreat); // 거리 확보 후퇴 상태 전환
                motor.RetreatFrom(target.position, data.RetreatSpeed); // 플레이어 반대 방향으로 이동
                return; // 후퇴 판단 완료
            }

            if (distance > data.PreferredMaxRange) // 플레이어가 원거리 선호 최대 거리보다 멀리 있는지 확인
            {
                SetState(MonsterState.Chase); // 적정 사거리까지 추적 상태 전환
                motor.MoveTo(target.position, data.ChaseSpeed); // 플레이어 현재 위치로 접근 이동
                return; // 접근 판단 완료
            }

            if (distance <= data.AttackRange) // 선호 거리 안에서 활 공격 가능한지 확인
            {
                SetState(MonsterState.Attack); // 공격 상태 전환
                motor.Stop(); // 활 조준을 위해 이동 정지
                motor.FaceTarget(target.position); // 플레이어 방향 바라보기

                if (rangedAttack != null && rangedAttack.CanStartAttack) // 현재 공격 쿨타임이 끝났는지 확인
                {
                    rangedAttack.TryStartAttack(target); // 시야 검사를 포함한 활 조준 시작
                }

                return; // 공격 거리 처리 완료
            }

            SetState(MonsterState.Chase); // 예외적으로 공격 사거리 밖 직접 시야 대상 추적 상태 전환
            motor.MoveTo(target.position, data.ChaseSpeed); // 플레이어 현재 위치로 추적 이동
        }

        private void HandleInvestigation() // 마지막 확인 위치나 소리 위치 조사 처리
        {
            Vector3 position = targetSelector.LastKnownPosition; // 현재 기억 중인 조사 위치 조회
            float distance = Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude; // 조사 위치까지 수평 거리 계산
            SetState(MonsterState.Investigate); // 조사 상태 표시

            if (distance > 0.65f) // 조사 위치까지 이동이 필요한지 확인
            {
                motor.MoveTo(position, data.MoveSpeed); // 마지막 확인 위치로 이동
            }
            else // 조사 위치에 도착한 경우 처리
            {
                motor.Stop(); // 위치 도착 후 정지
                motor.FaceTarget(position + transform.forward); // 현재 방향 유지
            }
        }

        private void HandleDamaged(DamageInfo damageInfo, float appliedDamage) // 플레이어 공격을 받은 순간 공격자를 우선 대상으로 등록
        {
            if (appliedDamage <= 0f || data == null) // 실제 피해 적용 여부 확인
            {
                return; // 피해 반응 대상 선정 생략
            }

            Transform attacker = ResolvePlayerAttacker(damageInfo); // DamageInfo에서 플레이어 공격자 Transform 검색

            if (attacker == null) // 플레이어 공격자를 찾지 못했는지 확인
            {
                return; // 대상 선정 생략
            }

            damageAggroTarget = attacker; // 피해를 준 플레이어 우선 대상 저장
            targetSelector?.SetTarget(attacker); // 현재 추적 대상으로 즉시 등록
            targetSelector?.Remember(attacker.position, data.MemoryDuration); // 공격자의 마지막 위치 기억 저장
        }

        private void HandleDied() // 몬스터 체력 0 사망 이벤트 처리
        {
            EnterDeadState(); // AI 사망 상태 전환
        }

        private void EnterDeadState() // 사망 상태에서 이동·공격 완전 종료
        {
            SetState(MonsterState.Dead); // 사망 상태 저장
            motor?.Stop(); // 사망 즉시 이동 중지
            rangedAttack?.CancelAttack(); // 진행 중 원거리 공격 취소
            meleeAttack?.CancelAttack(); // 진행 중 근접 공격 취소
        }

        private bool IsAnyAttackBusy() // 현재 연결된 공격 컴포넌트 중 하나라도 진행 중인지 확인
        {
            bool rangedBusy = rangedAttack != null && rangedAttack.IsBusy; // 원거리 공격 진행 여부 계산
            bool meleeBusy = meleeAttack != null && meleeAttack.IsBusy; // 근접 공격 진행 여부 계산
            return rangedBusy || meleeBusy; // 하나 이상의 공격 진행 상태 반환
        }

        private void SetState(MonsterState nextState) // 현재 AI 상태 변경과 이벤트 처리
        {
            if (state == nextState) // 동일 상태 반복 요청인지 확인
            {
                return; // 중복 상태 변경 생략
            }

            state = nextState; // 새 AI 상태 저장
            stateChangedTime = Time.time; // 상태 전환 시각 기록
            StateChanged?.Invoke(state); // 외부 진단·시각 기능에 상태 변경 전달
        }

        private void ResolveReferences() // 같은 몬스터 루트의 공통 컴포넌트 자동 확보
        {
            health ??= GetComponent<CombatHealth>(); // 같은 몬스터 공통 체력 참조 확보
            reaction ??= GetComponent<CombatReaction>(); // 같은 몬스터 경직·넉백 반응 참조 확보
            sensor ??= GetComponent<MonsterSensor>(); // 같은 몬스터 시각·청각 감각 참조 확보
            targetSelector ??= GetComponent<MonsterTargetSelector>(); // 같은 몬스터 대상·기억 기능 참조 확보
            motor ??= GetComponent<MonsterMotor>(); // 같은 몬스터 이동 기능 참조 확보
            rangedAttack ??= GetComponent<CorruptedUndeadArcherAttack>(); // 같은 몬스터 궁수 공격 기능 참조 확보
            meleeAttack ??= GetComponent<MonsterMeleeAttack>(); // 같은 몬스터 근접 공격 기능 참조 확보
        }

        private void SubscribeHealth() // 체력 피해·사망 이벤트 구독
        {
            if (health == null) // 체력 참조 누락 확인
            {
                return; // 이벤트 연결 생략
            }

            health.Damaged -= HandleDamaged; // 중복 피해 이벤트 연결 방지
            health.Died -= HandleDied; // 중복 사망 이벤트 연결 방지
            health.Damaged += HandleDamaged; // 플레이어 공격자 우선 대상 등록 이벤트 연결
            health.Died += HandleDied; // 사망 시 AI 종료 이벤트 연결
        }

        private void UnsubscribeHealth() // 체력 이벤트 구독 해제
        {
            if (health == null) // 체력 참조 누락 확인
            {
                return; // 이벤트 해제 생략
            }

            health.Damaged -= HandleDamaged; // 피해 이벤트 구독 해제
            health.Died -= HandleDied; // 사망 이벤트 구독 해제
        }

        private static Transform ResolvePlayerAttacker(DamageInfo damageInfo) // 피해 정보에서 플레이어 루트 Transform 추출
        {
            GameObject instigator = damageInfo.Instigator; // 우선 공격 지시자 조회
            PlayerDamageReceiver receiver = instigator == null ? null : instigator.GetComponentInParent<PlayerDamageReceiver>(); // 공격 지시자 계층의 플레이어 피해 수신기 조회

            if (receiver != null) // 플레이어 공격 지시자 발견 여부 확인
            {
                return receiver.transform; // 플레이어 루트 반환
            }

            GameObject source = damageInfo.Source; // 실제 무기·투사체 피해 원인 조회
            receiver = source == null ? null : source.GetComponentInParent<PlayerDamageReceiver>(); // 피해 원인 계층의 플레이어 루트 조회
            return receiver == null ? null : receiver.transform; // 발견된 플레이어 루트 또는 없음 반환
        }
    }
}
