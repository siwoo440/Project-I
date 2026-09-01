using UnityEngine; // Quaternion 보간·시간 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class SwingingAxeTrap : TrapControllerBase // 좌우 180도를 끊임없이 왕복하는 회전 도끼 함정
    {
        private const float StartAngle = -90f; // 왼쪽 끝 회전 각도
        private const float EndAngle = 90f; // 오른쪽 끝 회전 각도
        private const float SweepDuration = 1.70f; // 절반 속도로 한쪽 끝에서 반대편까지 이동하는 시간
        [SerializeField] private Transform pivot; // 도끼 자루·날 전체 회전 기준점
        [SerializeField] private float damage = 55f; // 도끼 기본 피해량
        [SerializeField] private float staggerPower = 40f; // 도끼 경직 힘
        [SerializeField] private float knockbackForce = 2f; // 도끼 강한 횡방향 넉백
        private float sweepTime; // 현재 왕복 구간 경과 시간
        private bool movingToEnd = true; // 현재 오른쪽 끝으로 이동 중인지 여부

        public float Damage => damage; // Validator·F1용 피해량 공개

        private void Awake() // 시작 시 도끼를 왼쪽 끝 위치로 정렬
        {
            SetPivotAngle(StartAngle); // 정확한 -90도 시작 각도 적용
            state = TrapState.Triggered; // 자동 왕복 상태 지정
        }

        private void Start() // 모든 컴포넌트 초기화 후 자동 왕복 시작
        {
            BeginAutomaticSweep(); // 첫 번째 180도 왕복 구간 시작
        }

        private void Update() // 도끼의 연속 좌우 180도 왕복 처리
        {
            if (pivot == null) // Pivot 연결 여부 확인
            {
                return; // Pivot이 없으면 회전 처리 중단
            }

            sweepTime += Time.deltaTime; // 현재 왕복 구간 시간 누적
            float progress = Mathf.Clamp01(sweepTime / SweepDuration); // 현재 180도 이동 진행률 계산
            float eased = Mathf.SmoothStep(0f, 1f, progress); // 끝점에서 자연스럽게 방향을 바꾸는 보간 적용
            float fromAngle = movingToEnd ? StartAngle : EndAngle; // 현재 출발 각도 결정
            float toAngle = movingToEnd ? EndAngle : StartAngle; // 현재 도착 각도 결정
            float angle = Mathf.Lerp(fromAngle, toAngle, eased); // -90도와 +90도 사이 현재 각도 계산
            SetPivotAngle(angle); // 계산된 로컬 Z 회전 적용

            if (progress < 1f) // 현재 180도 이동이 아직 남았는지 확인
            {
                return; // 끝점에 도달할 때까지 현재 방향 유지
            }

            damageSource?.EndDamageWindow(); // 현재 방향의 피해 중복 기록 종료
            movingToEnd = !movingToEnd; // 도달 즉시 반대 방향으로 전환
            sweepTime = 0f; // 새 180도 이동 시간 초기화
            BeginSweepDamageWindow(); // 반대 방향 이동용 새 피해 창 시작
        }

        public override bool TriggerTrap(GameObject triggerSource = null) // 기존 Trigger 인터페이스 호환 유지
        {
            return false; // 자동 연속 왕복형이므로 외부 Trigger 요청 미사용
        }

        public void Configure(string targetName, TrapDamageSource source, Transform targetPivot, float targetDamage, float targetStagger, float targetKnockback) // Editor Setup용 도끼 함정 구성
        {
            ConfigureBase(targetName, source); // 공통 이름·피해 소스 저장
            pivot = targetPivot; // 회전 기준 Transform 저장
            damage = Mathf.Max(0f, targetDamage); // 피해량 저장
            staggerPower = Mathf.Max(0f, targetStagger); // 경직 값 저장
            knockbackForce = Mathf.Max(0f, targetKnockback); // 넉백 값 저장
            state = TrapState.Triggered; // 자동 왕복형 상태 지정
            sweepTime = 0f; // 왕복 시간 초기화
            movingToEnd = true; // 첫 이동 방향을 오른쪽으로 설정
            SetPivotAngle(StartAngle); // Edit Mode에서도 -90도 시작 위치 정렬
        }

        private void BeginAutomaticSweep() // 런타임 자동 왕복 시작
        {
            state = TrapState.Triggered; // 항상 작동 중인 도끼 상태 지정
            sweepTime = 0f; // 첫 왕복 구간 시간 초기화
            movingToEnd = true; // -90도에서 +90도로 첫 이동 설정
            SetPivotAngle(StartAngle); // 정확한 시작 각도 보정
            BeginSweepDamageWindow(); // 첫 이동 피해 창 활성화
        }

        private void BeginSweepDamageWindow() // 현재 왕복 방향별 새 피해 구간 시작
        {
            int attackId = BeginActivation(gameObject); // 왕복 한 번마다 새로운 공격 ID 생성
            Vector3 forceDirection = movingToEnd ? transform.right : -transform.right; // 도끼 이동 방향에 맞춘 넉백 방향 결정
            damageSource?.BeginDamageWindow(DisplayName, damage, staggerPower, knockbackForce, forceDirection, attackId); // 현재 180도 이동 전체에서 피해 판정 활성화
        }

        private void OnDisable() // 오브젝트 비활성화 시 피해 판정 정리
        {
            damageSource?.EndDamageWindow(); // 비활성 상태에서 잔여 피해 판정 차단
        }

        private void SetPivotAngle(float angle) // 도끼 Pivot 로컬 Z 회전 안전 적용
        {
            if (pivot != null) // Pivot 존재 여부 확인
            {
                pivot.localRotation = Quaternion.Euler(0f, 0f, angle); // 정확한 좌우 180도 회전 범위 적용
            }
        }
    }
}
