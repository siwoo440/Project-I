using ProjectI.Player; // 기존 체력·스태미나·이동 시스템 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    [RequireComponent(typeof(PlayerHealth))] // 기존 플레이어 체력 필수 지정
    [RequireComponent(typeof(PlayerStamina))] // 기존 플레이어 스태미나 필수 지정
    [RequireComponent(typeof(PlayerMovement))] // 기존 플레이어 이동 필수 지정
    public sealed class CombatController : MonoBehaviour // 플레이어 단발 근접 공격 상태와 쿨타임 관리
    {
        [SerializeField] private Transform attackOrigin; // 벽 가림 검사와 향후 투사체 기준이 될 공격 시점
        private PlayerHealth health; // 기존 플레이어 체력 참조
        private PlayerStamina stamina; // 기존 플레이어 스태미나 참조
        private PlayerMovement movement; // 기존 플레이어 이동 참조
        private MeleeWeaponItem activeWeapon; // 현재 공격 중인 근접 무기
        private AttackDefinition lastAttackDefinition; // 마지막으로 시작한 단일 공격 데이터
        private string lastWeaponName = string.Empty; // 마지막으로 공격한 무기 표시 이름
        private CombatState state = CombatState.Idle; // 현재 전투 상태
        private AttackPhase phase = AttackPhase.None; // 현재 공격 단계
        private float phaseElapsed; // 현재 공격 단계 경과 시간
        private float cooldownRemaining; // 다음 공격까지 남은 쿨타임
        private float cooldownDuration; // 현재 단발 공격의 전체 쿨타임
        private int attackSequence; // 공격 식별값 증가 시퀀스
        private CombatHitResult lastHitResult; // F1 진단용 마지막 피격 결과
        private bool hasLastHit; // 마지막 피격 결과 존재 여부
        private GameObject lastWallObject; // F1 진단용 마지막 벽 충돌 오브젝트
        private Vector3 lastWallPoint; // F1 진단용 마지막 벽 충돌 위치
        private string lastFailureReason = string.Empty; // 마지막 공격 시작 실패 사유

        public event System.Action StateChanged; // 전투 상태·공격 단계·쿨타임 변경 이벤트

        public CombatState State => state; // 현재 전투 상태 공개
        public AttackPhase Phase => phase; // 현재 공격 단계 공개
        public int AttackId => attackSequence; // 현재 공격 식별값 공개
        public MeleeWeaponItem ActiveWeapon => activeWeapon; // 현재 공격 무기 공개
        public AttackDefinition LastAttackDefinition => lastAttackDefinition; // 마지막 단일 공격 데이터 공개
        public string LastWeaponName => lastWeaponName; // 마지막 공격 무기 이름 공개
        public PlayerStamina Stamina => stamina; // F1 진단용 스태미나 참조 공개
        public PlayerMovement Movement => movement; // F1 진단용 이동 참조 공개
        public Transform AttackOrigin => attackOrigin != null ? attackOrigin : transform; // 공격 기준 Transform 공개
        public float PhaseProgress => CalculatePhaseProgress(); // 현재 공격 단계 진행률 공개
        public float CooldownRemaining => cooldownRemaining; // 다음 공격까지 남은 시간 공개
        public float CooldownDuration => cooldownDuration; // 현재 공격 쿨타임 전체 시간 공개
        public float CooldownProgress => cooldownDuration <= 0f ? 1f : Mathf.Clamp01(1f - (cooldownRemaining / cooldownDuration)); // 현재 쿨타임 진행률 공개
        public bool CanAttackNow => state == CombatState.Idle && cooldownRemaining <= 0f; // 현재 즉시 새 공격 가능 여부 공개
        public bool HasLastHit => hasLastHit; // 마지막 피격 결과 존재 여부 공개
        public CombatHitResult LastHitResult => lastHitResult; // 마지막 피격 결과 공개
        public GameObject LastWallObject => lastWallObject; // 마지막 벽 충돌 오브젝트 공개
        public Vector3 LastWallPoint => lastWallPoint; // 마지막 벽 충돌 위치 공개
        public string LastFailureReason => lastFailureReason; // 마지막 공격 실패 사유 공개

        private void Awake() // 공통 전투 제어기 초기화
        {
            ResolveReferences(); // 기존 플레이어 시스템 참조 확보
        }

        private void OnEnable() // 전투 제어기 활성화 처리
        {
            ResolveReferences(); // 활성화 직후 참조 재확인

            if (health != null) // 기존 체력 참조 존재 여부 확인
            {
                health.Died += HandlePlayerDied; // 플레이어 사망 이벤트 구독
            }
        }

        private void OnDisable() // 전투 제어기 비활성화 처리
        {
            if (health != null) // 기존 체력 참조 존재 여부 확인
            {
                health.Died -= HandlePlayerDied; // 플레이어 사망 이벤트 구독 해제
            }

            CancelAttack(); // 비활성화 시 이동 제한과 무기 궤적 복구
        }

        private void Update() // 공격 단계와 공격 시작 기준 쿨타임 갱신
        {
            TickCooldown(); // 공격 중에도 흐르는 최소 공격 간격 타이머 갱신

            if (state == CombatState.Cooldown) // 공격 동작은 끝났지만 쿨타임이 남았는지 확인
            {
                if (cooldownRemaining <= 0f) // 다음 공격 가능 시간이 되었는지 확인
                {
                    state = CombatState.Idle; // 전투 입력 대기 상태 복귀
                    StateChanged?.Invoke(); // 쿨타임 종료 상태 외부 전달
                }

                return; // 쿨타임 중 공격 단계 계산 생략
            }

            if (state != CombatState.Attacking) // 실제 공격 진행 상태 여부 확인
            {
                return; // 비공격 상태 프레임 계산 생략
            }

            if (activeWeapon == null || !activeWeapon.IsHeld || activeWeapon.AttackDefinition == null) // 공격 중 무기 연결 상태 확인
            {
                CancelAttack(); // 무기가 사라지거나 내려놓아진 경우 공격 취소
                return; // 공격 프레임 처리 종료
            }

            AttackDefinition definition = activeWeapon.AttackDefinition; // 현재 단발 공격 데이터 조회
            phaseElapsed += Time.deltaTime; // 현재 공격 단계 경과 시간 누적
            activeWeapon.UpdateAttackPose(phase, CalculatePhaseProgress()); // 현재 공격 단계에 맞는 무기별 한 번 휘두르기 시각 갱신

            if (phase == AttackPhase.Windup) // 공격 준비 단계 여부 확인
            {
                if (phaseElapsed >= definition.WindupDuration) // 준비 단계 완료 시간 도달 여부 확인
                {
                    EnterPhase(AttackPhase.Active); // 실제 피해 판정 활성 단계 진입
                    activeWeapon.BeginActiveTrace(attackSequence, transform, AttackOrigin); // 근접 무기 궤적 검사 시작
                }

                return; // 준비 단계 처리 종료
            }

            if (phase == AttackPhase.Active) // 실제 피해 판정 단계 여부 확인
            {
                activeWeapon.TickActiveTrace(this); // 현재 무기날 이동 구간 피해와 벽 충돌 검사

                if (phaseElapsed >= definition.ActiveDuration) // 판정 활성 단계 완료 시간 도달 여부 확인
                {
                    activeWeapon.EndActiveTrace(); // 근접 궤적 검사 종료
                    EnterPhase(AttackPhase.Recovery); // 공격 후 회복 단계 진입
                }

                return; // 활성 단계 처리 종료
            }

            if (phase == AttackPhase.Recovery && phaseElapsed >= definition.RecoveryDuration) // 회복 단계 완료 시간 도달 여부 확인
            {
                CompleteAttack(); // 단일 휘두르기 종료와 남은 쿨타임 상태 전환
            }
        }

        public void Configure(PlayerHealth targetHealth, PlayerStamina targetStamina, PlayerMovement targetMovement, Transform targetAttackOrigin) // Day14~15 자동 Setup용 기존 플레이어 시스템 연결
        {
            health = targetHealth; // 기존 플레이어 체력 연결
            stamina = targetStamina; // 기존 플레이어 스태미나 연결
            movement = targetMovement; // 기존 플레이어 이동 연결
            attackOrigin = targetAttackOrigin != null ? targetAttackOrigin : transform; // 공격 시점 연결
        }

        public bool CanStartAttack(MeleeWeaponItem weapon) // 지정 무기로 현재 단발 공격을 시작할 수 있는지 확인
        {
            ResolveReferences(); // 최신 플레이어 시스템 참조 확보

            if (weapon == null || weapon.AttackDefinition == null || !weapon.IsHeld) // 유효 무기와 실제 운반 상태 확인
            {
                return false; // 유효하지 않은 무기 공격 차단
            }

            if (health != null && health.IsDead) // 플레이어 사망 여부 확인
            {
                return false; // 사망 상태 공격 차단
            }

            if (state != CombatState.Idle || cooldownRemaining > 0f) // 공격 진행 또는 쿨타임 상태 확인
            {
                return false; // 연속 입력과 쿨타임 중 추가 공격 차단
            }

            return stamina != null && stamina.CanSpend(weapon.AttackDefinition.StaminaCost); // 공격 스태미나 보유 여부 반환
        }

        public bool TryStartAttack(MeleeWeaponItem weapon) // 지정 근접 무기로 한 번의 공격 시작 시도
        {
            ResolveReferences(); // 공격 시작 직전 플레이어 시스템 참조 확보
            lastFailureReason = string.Empty; // 이전 공격 실패 사유 초기화

            if (weapon == null || weapon.AttackDefinition == null || !weapon.IsHeld) // 무기 구성과 운반 상태 확인
            {
                lastFailureReason = "Weapon Not Ready"; // 무기 미준비 실패 사유 저장
                return false; // 공격 시작 실패 반환
            }

            if (health != null && health.IsDead) // 플레이어 사망 여부 확인
            {
                lastFailureReason = "Player Dead"; // 사망 상태 실패 사유 저장
                return false; // 공격 시작 실패 반환
            }

            if (state == CombatState.Cooldown || cooldownRemaining > 0f) // 현재 공격 쿨타임 여부 확인
            {
                lastFailureReason = $"Cooldown {cooldownRemaining:0.00}s"; // 남은 쿨타임 실패 사유 저장
                return false; // 쿨타임 동안 추가 휘두르기 차단
            }

            if (state != CombatState.Idle) // 현재 다른 전투 상태 진행 여부 확인
            {
                lastFailureReason = "Combat Busy"; // 공격 중복 실패 사유 저장
                return false; // 공격 진행 중 추가 입력 차단
            }

            AttackDefinition definition = weapon.AttackDefinition; // 현재 무기 단일 공격 데이터 조회

            if (stamina == null || !stamina.TrySpend(definition.StaminaCost)) // 기존 스태미나 시스템에서 공격 비용 소비 시도
            {
                lastFailureReason = "Not Enough Stamina"; // 스태미나 부족 실패 사유 저장
                return false; // 공격 시작 실패 반환
            }

            activeWeapon = weapon; // 현재 공격 무기 저장
            lastAttackDefinition = definition; // F1 진단용 마지막 공격 데이터 저장
            lastWeaponName = weapon.WorldItem == null ? weapon.name : weapon.WorldItem.DisplayName; // 마지막 공격 무기 표시 이름 저장
            attackSequence++; // 새 공격 식별값 증가
            cooldownDuration = Mathf.Max(0.01f, definition.CooldownDuration); // 현재 공격의 최소 공격 간격 저장
            cooldownRemaining = cooldownDuration; // 공격 시작 순간부터 쿨타임 타이머 시작
            state = CombatState.Attacking; // 공통 전투 상태를 공격 중으로 변경
            movement?.SetExternalMovementModifier(definition.MovementMultiplier, false); // 공격 중 이동 감속과 달리기 제한 적용
            weapon.BeginAttack(attackSequence, transform, AttackOrigin); // 무기 단일 공격 시각과 이전 궤적 상태 초기화
            EnterPhase(AttackPhase.Windup); // 공격 준비 단계 진입
            StateChanged?.Invoke(); // 공격 시작 상태 변경 이벤트 발생
            return true; // 공격 시작 성공 반환
        }

        public DamageInfo BuildDamageInfo(GameObject source, AttackDefinition definition, Vector3 hitPoint, Vector3 hitNormal, int attackId) // 현재 플레이어 공격의 공통 피해 요청 데이터 생성
        {
            float knockbackForce = definition == null ? 0f : definition.KnockbackForce; // 공격 데이터 넉백 힘 조회
            float staggerPower = definition == null ? 0f : definition.StaggerPower; // 공격 데이터 경직 힘 조회
            Vector3 direction = Vector3.ProjectOnPlane(hitPoint - transform.position, Vector3.up); // 공격자에서 피격 지점으로 향하는 수평 방향 계산
            Vector3 forceDirection = direction.sqrMagnitude <= 0.0001f ? transform.forward : direction.normalized; // 지나치게 가까운 피격은 정면 방향으로 보정
            Vector3 force = forceDirection * knockbackForce; // 무기별 넉백 거리 벡터 계산
            float damage = definition == null ? 0f : definition.BaseDamage; // 공격 데이터 기본 피해량 조회
            CombatDamageType damageType = definition == null ? CombatDamageType.Physical : definition.DamageType; // 공격 데이터 피해 종류 조회
            return new DamageInfo(source, gameObject, CombatFaction.Player, damageType, damage, hitPoint, hitNormal, staggerPower, force, attackId); // 경직·넉백을 포함한 공통 피해 요청 생성 반환
        }

        public void RecordHit(CombatHitResult result) // 근접 궤적의 마지막 피해 결과 기록
        {
            lastHitResult = result; // 마지막 피격 처리 결과 저장
            hasLastHit = true; // 마지막 피격 결과 존재 상태 활성화
        }

        public void RecordWallHit(GameObject wallObject, Vector3 hitPoint) // 근접 무기 벽 충돌 기록
        {
            lastWallObject = wallObject; // 마지막 벽 충돌 오브젝트 저장
            lastWallPoint = hitPoint; // 마지막 벽 충돌 위치 저장
        }

        private void TickCooldown() // 공격 시작 기준 최소 공격 간격 타이머 갱신
        {
            if (cooldownRemaining <= 0f) // 남은 쿨타임 존재 여부 확인
            {
                cooldownRemaining = 0f; // 소수점 오차 방지용 0 고정
                return; // 쿨타임 갱신 생략
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime); // 프레임 시간만큼 쿨타임 감소
        }

        private void EnterPhase(AttackPhase nextPhase) // 공격 진행 단계 전환
        {
            phase = nextPhase; // 새 공격 단계 저장
            phaseElapsed = 0f; // 새 단계 경과 시간 초기화
            StateChanged?.Invoke(); // F1 진단 등 외부 기능에 단계 변경 전달
        }

        private void CompleteAttack() // 정상 단일 공격 종료 처리
        {
            activeWeapon?.EndAttack(); // 무기 궤적과 시각 자세 복구
            activeWeapon = null; // 현재 공격 무기 참조 해제
            phase = AttackPhase.None; // 공격 단계 없음으로 변경
            phaseElapsed = 0f; // 공격 단계 시간 초기화
            movement?.ResetExternalMovementModifier(); // 공격 중 이동 감속과 달리기 제한 해제

            if (health != null && health.IsDead) // 공격 종료 시점 플레이어 사망 여부 확인
            {
                state = CombatState.Dead; // 사망 상태 유지
            }
            else // 생존 상태의 공격 종료 처리
            {
                state = cooldownRemaining > 0f ? CombatState.Cooldown : CombatState.Idle; // 남은 최소 공격 간격에 따라 쿨타임 또는 대기 상태 전환
            }

            StateChanged?.Invoke(); // 공격 종료 상태 변경 이벤트 발생
        }

        private void CancelAttack() // 비정상 종료 또는 무기 분리 시 공격 취소
        {
            activeWeapon?.EndAttack(); // 활성 무기 궤적과 자세 즉시 정리
            activeWeapon = null; // 현재 공격 무기 참조 해제
            phase = AttackPhase.None; // 공격 단계 없음으로 초기화
            phaseElapsed = 0f; // 단계 시간 초기화
            movement?.ResetExternalMovementModifier(); // 외부 이동 제한 기본값 복구

            if (health != null && health.IsDead) // 플레이어 사망 여부 확인
            {
                state = CombatState.Dead; // 사망 상태 유지
            }
            else // 생존 상태 취소 처리
            {
                state = cooldownRemaining > 0f ? CombatState.Cooldown : CombatState.Idle; // 이미 시작된 공격 쿨타임은 취소 후에도 유지
            }
        }

        private void HandlePlayerDied() // 기존 PlayerHealth 사망 이벤트 처리
        {
            CancelAttack(); // 진행 중 공격과 이동 제한 즉시 해제
            cooldownRemaining = 0f; // 사망 상태에서는 공격 쿨타임 제거
            state = CombatState.Dead; // 공통 전투 상태를 사망으로 변경
            StateChanged?.Invoke(); // 사망 상태 변경 이벤트 발생
        }

        private float CalculatePhaseProgress() // 현재 공격 단계 0~1 진행률 계산
        {
            AttackDefinition definition = activeWeapon == null ? null : activeWeapon.AttackDefinition; // 현재 공격 데이터 조회

            if (definition == null) // 공격 데이터 누락 여부 확인
            {
                return 0f; // 진행률 0 반환
            }

            float duration = phase == AttackPhase.Windup ? definition.WindupDuration : phase == AttackPhase.Active ? definition.ActiveDuration : phase == AttackPhase.Recovery ? definition.RecoveryDuration : 1f; // 현재 단계 전체 시간 선택
            return Mathf.Clamp01(phaseElapsed / Mathf.Max(0.01f, duration)); // 현재 단계 진행률 반환
        }

        private void ResolveReferences() // 기존 플레이어 전투 연동 참조 확보
        {
            if (health == null) // 체력 참조 누락 확인
            {
                health = GetComponent<PlayerHealth>(); // 기존 PlayerHealth 조회
            }

            if (stamina == null) // 스태미나 참조 누락 확인
            {
                stamina = GetComponent<PlayerStamina>(); // 기존 PlayerStamina 조회
            }

            if (movement == null) // 이동 참조 누락 확인
            {
                movement = GetComponent<PlayerMovement>(); // 기존 PlayerMovement 조회
            }
        }
    }
}
