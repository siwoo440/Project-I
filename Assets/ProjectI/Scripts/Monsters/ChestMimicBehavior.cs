using ProjectI.Combat; // 공통 체력·플레이어 피해 수신기 참조
using UnityEngine; // Transform·시간·오브젝트 검색 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class ChestMimicBehavior : MonoBehaviour // 상자로 위장하다 플레이어 접근·피격 시 변신하고 공통 AI를 활성화하는 미믹 행동
    {
        [SerializeField] private MonsterData data; // 미믹 이동·감지·공격 수치 데이터
        [SerializeField] private CombatHealth health; // 미믹 공통 체력
        [SerializeField] private MonsterBrain brain; // 변신 완료 뒤 사용하는 공통 추적 AI
        [SerializeField] private MonsterMotor motor; // 변신 전 이동 정지용 공통 이동 기능
        [SerializeField] private MonsterMeleeAttack meleeAttack; // 변신 후 공통 근접 공격
        [SerializeField] private Transform lidRoot; // 상자 뚜껑 변신 모션 루트
        [SerializeField] private GameObject hiddenMonsterParts; // 이빨·혀·눈·다리 등 변신 후 보이는 시각 루트
        [SerializeField] private float revealDistance = 3.2f; // 플레이어 접근 시 자동 변신 거리
        [SerializeField] private float revealDuration = 0.72f; // 뚜껑이 열리며 변신하는 시간
        private Transform playerTarget; // 현재 플레이어 Transform
        private Quaternion lidBaseRotation = Quaternion.identity; // 상자 뚜껑 기본 닫힘 회전
        private bool disguised = true; // 현재 상자 위장 상태
        private bool revealing; // 현재 변신 모션 진행 여부
        private float revealStartedTime; // 변신 시작 시각
        private float nextPlayerLookupTime; // 플레이어 전역 검색 최소 간격

        public bool IsDisguised => disguised; // F1 진단용 위장 상태 공개
        public bool IsRevealing => revealing; // F1 진단용 변신 진행 여부 공개
        public float RevealProgress => revealing ? Mathf.Clamp01((Time.time - revealStartedTime) / Mathf.Max(0.01f, revealDuration)) : (disguised ? 0f : 1f); // 현재 변신 진행률 공개
        public MonsterData Data => data; // 진단·Validator용 데이터 공개
        public CombatHealth Health => health; // 진단용 체력 공개
        public MonsterBrain Brain => brain; // 변신 후 공통 AI 참조 공개

        private void Awake() // 미믹 위장 상태 초기화
        {
            ResolveReferences(); // 같은 루트 공통 기능 참조 확보

            if (lidRoot != null) // 뚜껑 루트 존재 여부 확인
            {
                lidBaseRotation = lidRoot.localRotation; // 닫힌 상자 기본 회전 저장
            }

            ApplyDisguisedState(); // 시작 위장 시각·AI 상태 적용
        }

        private void OnEnable() // 미믹 활성화 시 피해 이벤트 연결
        {
            ResolveReferences(); // 활성화 직후 참조 재확인
            SubscribeHealth(); // 공격받았을 때 즉시 변신하도록 피해 이벤트 구독
            nextPlayerLookupTime = 0f; // 첫 프레임 플레이어 검색 허용
            ApplyDisguisedState(); // 런타임 Spawn 복제 직후 위장 상태 재적용
        }

        private void OnDisable() // 미믹 비활성화 시 이벤트 정리
        {
            UnsubscribeHealth(); // 피해 이벤트 구독 해제
        }

        private void Update() // 접근 감지와 변신 모션 처리
        {
            if (health == null || !health.IsAlive) // 미믹 사망 또는 체력 참조 누락 여부 확인
            {
                return; // 변신·추적 처리 중단
            }

            ResolvePlayerIfNeeded(); // 현재 플레이어 Transform 확보

            if (disguised && !revealing && playerTarget != null) // 위장 중 플레이어가 존재하는지 확인
            {
                float distance = Vector3.ProjectOnPlane(playerTarget.position - transform.position, Vector3.up).magnitude; // 플레이어까지 수평 거리 계산

                if (distance <= revealDistance) // 플레이어가 미믹 변신 거리 안에 진입했는지 확인
                {
                    BeginReveal(playerTarget); // 근접 접근을 계기로 위장 해제 시작
                }
            }

            if (revealing) // 현재 변신 모션 진행 여부 확인
            {
                UpdateReveal(); // 뚜껑 열림·몬스터 파트 노출·AI 활성화 처리
            }
        }

        public void Configure(MonsterData targetData, CombatHealth targetHealth, MonsterBrain targetBrain, MonsterMotor targetMotor, MonsterMeleeAttack targetAttack, Transform targetLidRoot, GameObject targetHiddenParts, float targetRevealDistance, float targetRevealDuration) // Day17 자동 Setup용 상자 미믹 구성
        {
            data = targetData; // 미믹 데이터 저장
            health = targetHealth; // 공통 체력 저장
            brain = targetBrain; // 변신 후 공통 AI 저장
            motor = targetMotor; // 이동 계층 저장
            meleeAttack = targetAttack; // 공통 근접 공격 저장
            lidRoot = targetLidRoot; // 상자 뚜껑 시각 루트 저장
            hiddenMonsterParts = targetHiddenParts; // 변신 후 노출 파트 저장
            revealDistance = Mathf.Max(0.5f, targetRevealDistance); // 변신 거리 최소값 보정
            revealDuration = Mathf.Max(0.15f, targetRevealDuration); // 변신 시간 최소값 보정

            if (lidRoot != null) // 뚜껑 루트 존재 여부 확인
            {
                lidBaseRotation = lidRoot.localRotation; // Setup 생성 닫힘 회전 저장
            }

            ApplyDisguisedState(); // 설정 직후 위장 상태 적용
        }

        private void BeginReveal(Transform threat) // 접근 또는 피격을 계기로 미믹 변신 시작
        {
            if (!disguised || revealing) // 이미 변신했거나 변신 중인지 확인
            {
                return; // 중복 변신 시작 차단
            }

            playerTarget = threat == null ? playerTarget : threat; // 변신 원인이 된 플레이어 대상 저장
            revealing = true; // 변신 진행 상태 활성화
            revealStartedTime = Time.time; // 변신 시작 시각 기록
            motor?.Stop(); // 뚜껑이 열리는 동안 이동 정지
            meleeAttack?.CancelAttack(); // 변신 전에 진행 중인 공격이 없도록 초기화
        }

        private void UpdateReveal() // 상자 뚜껑을 열고 숨은 파트를 노출한 뒤 공통 AI 활성화
        {
            float progress = RevealProgress; // 현재 변신 진행률 조회
            float eased = Mathf.SmoothStep(0f, 1f, progress); // 자연스러운 뚜껑 열림 보간값 계산

            if (lidRoot != null) // 상자 뚜껑 시각 루트 존재 여부 확인
            {
                lidRoot.localRotation = lidBaseRotation * Quaternion.Euler(-105f * eased, 0f, 0f); // 위로 크게 벌어지는 미믹 뚜껑 모션 적용
            }

            if (hiddenMonsterParts != null && progress >= 0.18f) // 변신 초반 이후 이빨·혀·눈 파트 노출 시점 확인
            {
                hiddenMonsterParts.SetActive(true); // 상자 내부 몬스터 파트 표시
            }

            if (progress < 1f) // 변신 완료 전 여부 확인
            {
                return; // 다음 프레임 변신 모션 계속
            }

            revealing = false; // 변신 모션 종료
            disguised = false; // 상자 위장 상태 해제

            if (brain != null) // 공통 AI Brain 존재 여부 확인
            {
                brain.enabled = true; // 변신 완료 후 추적·공격 공통 AI 활성화

                if (playerTarget != null) // 변신을 일으킨 플레이어 대상 존재 여부 확인
                {
                    brain.ForceTarget(playerTarget); // 즉시 플레이어를 기억해 추적 상태로 연결
                }
            }
        }

        private void ApplyDisguisedState() // 닫힌 상자 위장 상태와 AI 비활성화 적용
        {
            disguised = true; // 위장 상태 활성화
            revealing = false; // 변신 진행 상태 초기화

            if (lidRoot != null) // 상자 뚜껑 존재 여부 확인
            {
                lidRoot.localRotation = lidBaseRotation; // 닫힌 기본 회전 복구
            }

            if (hiddenMonsterParts != null) // 변신 후 파트 루트 존재 여부 확인
            {
                hiddenMonsterParts.SetActive(false); // 위장 중 이빨·혀·눈·다리 숨김
            }

            if (brain != null) // 공통 AI Brain 존재 여부 확인
            {
                brain.enabled = false; // 위장 상태에서는 추적 AI 완전 비활성화
            }

            motor?.Stop(); // 위장 상자는 이동하지 않도록 목적지 초기화
            meleeAttack?.CancelAttack(); // 위장 상태 공격 정리
        }

        private void HandleDamaged(DamageInfo damageInfo, float appliedDamage) // 상자 위장 중 플레이어 공격을 받으면 즉시 정체 공개
        {
            if (appliedDamage <= 0f || !disguised) // 실제 피해가 없거나 이미 변신한 상태인지 확인
            {
                return; // 위장 해제 처리 생략
            }

            Transform attacker = ResolvePlayerAttacker(damageInfo); // 피해 정보에서 플레이어 공격자 검색
            BeginReveal(attacker); // 공격받은 즉시 미믹 변신 시작
        }

        private void SubscribeHealth() // CombatHealth 피해 이벤트 연결
        {
            if (health == null) // 체력 참조 누락 여부 확인
            {
                return; // 이벤트 연결 생략
            }

            health.Damaged -= HandleDamaged; // 중복 이벤트 연결 방지
            health.Damaged += HandleDamaged; // 위장 중 피격 변신 이벤트 연결
        }

        private void UnsubscribeHealth() // CombatHealth 피해 이벤트 연결 해제
        {
            if (health != null) // 체력 참조 존재 여부 확인
            {
                health.Damaged -= HandleDamaged; // 피격 변신 이벤트 구독 해제
            }
        }

        private void ResolveReferences() // 같은 미믹 루트의 필수 컴포넌트 자동 확보
        {
            health ??= GetComponent<CombatHealth>(); // 공통 체력 자동 조회
            brain ??= GetComponent<MonsterBrain>(); // 공통 AI Brain 자동 조회
            motor ??= GetComponent<MonsterMotor>(); // 공통 이동 기능 자동 조회
            meleeAttack ??= GetComponent<MonsterMeleeAttack>(); // 공통 근접 공격 자동 조회
        }

        private void ResolvePlayerIfNeeded() // 플레이어 Transform이 필요할 때만 전역 검색
        {
            if (playerTarget != null) // 기존 플레이어 참조 유효 여부 확인
            {
                return; // 전역 검색 생략
            }

            if (Time.time < nextPlayerLookupTime) // 재검색 최소 간격 도달 여부 확인
            {
                return; // 다음 검색 시각까지 대기
            }

            nextPlayerLookupTime = Time.time + 0.75f; // 다음 전역 검색 가능 시각 설정
            PlayerDamageReceiver receiver = UnityEngine.Object.FindFirstObjectByType<PlayerDamageReceiver>(); // 활성 플레이어 피해 수신기 검색
            playerTarget = receiver == null ? null : receiver.transform; // 플레이어 루트 Transform 저장
        }

        private static Transform ResolvePlayerAttacker(DamageInfo damageInfo) // 피해 정보에서 실제 플레이어 Transform 추출
        {
            PlayerDamageReceiver receiver = damageInfo.Instigator == null ? null : damageInfo.Instigator.GetComponentInParent<PlayerDamageReceiver>(); // 공격 지시자 계층 플레이어 조회

            if (receiver != null) // 공격 지시자에서 플레이어 발견 여부 확인
            {
                return receiver.transform; // 플레이어 루트 반환
            }

            receiver = damageInfo.Source == null ? null : damageInfo.Source.GetComponentInParent<PlayerDamageReceiver>(); // 실제 무기·투사체 계층 플레이어 조회
            return receiver == null ? null : receiver.transform; // 발견된 플레이어 또는 없음 반환
        }
    }
}
