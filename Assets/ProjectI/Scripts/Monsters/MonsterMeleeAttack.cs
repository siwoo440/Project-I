using ProjectI.Combat; // 공통 Damage Pipeline과 플레이어 피해 수신기 참조
using UnityEngine; // MonoBehaviour·Transform·물리 검사 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class MonsterMeleeAttack : MonoBehaviour // 부패한 망자·석상·미믹이 공유하는 단발 근접 공격 기능
    {
        [SerializeField] private MonsterData data; // 공격 피해·사거리·쿨타임 데이터
        [SerializeField] private MonsterSensor sensor; // 벽 뒤 공격 차단용 시각 감각 참조
        [SerializeField] private Transform attackOrigin; // 근접 공격 시작 위치
        [SerializeField] private Transform attackVisualRoot; // 팔·턱 등 공격 모션 시각 루트
        [SerializeField] private Vector3 windupEuler = new Vector3(-22f, 0f, 24f); // 공격 준비 시각 회전
        [SerializeField] private Vector3 strikeEuler = new Vector3(34f, 0f, -44f); // 실제 타격 시각 회전
        private Transform target; // 현재 공격 대상
        private Quaternion visualBaseRotation = Quaternion.identity; // 공격 시각 기본 회전
        private float attackStartedTime; // 현재 공격 시작 시각
        private float nextAttackTime; // 다음 공격 가능 시각
        private bool attacking; // 현재 공격 진행 여부
        private bool damageApplied; // 이번 공격 피해 적용 완료 여부
        private int attackSequence; // Damage Pipeline 공격 식별 번호

        public bool IsBusy => attacking; // 현재 공격 모션 진행 여부 공개
        public bool CanStartAttack => !attacking && Time.time >= nextAttackTime; // 현재 새 공격 시작 가능 여부 공개
        public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time); // 남은 공격 쿨타임 공개
        public float AttackProgress => !attacking || data == null ? 0f : Mathf.Clamp01((Time.time - attackStartedTime) / Mathf.Max(0.01f, data.AimTime + 0.34f)); // 현재 근접 공격 진행률 공개
        public float Damage => data == null ? 0f : data.AttackDamage; // 진단용 근접 피해량 공개

        private void Awake() // 근접 공격 초기화
        {
            CaptureBasePose(); // 현재 공격 시각 기본 자세 저장
        }

        private void OnDisable() // 몬스터 비활성화 시 공격 정리
        {
            CancelAttack(); // 진행 중 공격과 시각 상태 초기화
        }

        private void Update() // 공격 Windup·Strike·Recovery 모션과 피해 적용 갱신
        {
            if (!attacking || data == null) // 현재 공격 진행 상태 확인
            {
                return; // 공격 중이 아니면 처리 생략
            }

            float windup = Mathf.Max(0.08f, data.AimTime); // MonsterData의 AimTime을 근접 Windup 시간으로 재사용
            float active = 0.12f; // 실제 타격 판정 구간 길이 지정
            float recovery = 0.22f; // 타격 후 기본 자세 복귀 시간 지정
            float elapsed = Time.time - attackStartedTime; // 공격 시작 후 경과 시간 계산

            if (elapsed < windup) // 공격 준비 구간 여부 확인
            {
                float progress = Mathf.Clamp01(elapsed / windup); // 준비 모션 진행률 계산
                ApplyVisualPose(Vector3.Lerp(Vector3.zero, windupEuler, Mathf.SmoothStep(0f, 1f, progress))); // 팔·턱을 뒤로 당기는 준비 자세 적용
                return; // 준비 구간 처리 완료
            }

            if (elapsed < windup + active) // 실제 타격 구간 여부 확인
            {
                float progress = Mathf.Clamp01((elapsed - windup) / active); // 타격 모션 진행률 계산
                ApplyVisualPose(Vector3.Lerp(windupEuler, strikeEuler, Mathf.SmoothStep(0f, 1f, progress))); // 앞으로 단순하게 휘두르는 타격 자세 적용

                if (!damageApplied && progress >= 0.35f) // 타격 중간 지점에서 한 번만 피해 적용 여부 확인
                {
                    damageApplied = true; // 이번 공격 중복 피해 방지 상태 저장
                    TryApplyDamage(); // 현재 대상에 공통 Enemy Damage Pipeline 피해 적용
                }

                return; // 타격 구간 처리 완료
            }

            if (elapsed < windup + active + recovery) // 공격 후 복구 구간 여부 확인
            {
                float progress = Mathf.Clamp01((elapsed - windup - active) / recovery); // 복구 모션 진행률 계산
                ApplyVisualPose(Vector3.Lerp(strikeEuler, Vector3.zero, Mathf.SmoothStep(0f, 1f, progress))); // 공격 자세에서 기본 자세로 복귀
                return; // 복구 구간 처리 완료
            }

            attacking = false; // 공격 진행 상태 종료
            target = null; // 현재 공격 대상 참조 정리
            RestoreBasePose(); // 공격 시각 기본 자세 복구
            nextAttackTime = Time.time + Mathf.Max(0.1f, data.AttackCooldown); // 다음 공격 가능 시각 계산
        }

        public void Configure(MonsterData targetData, MonsterSensor targetSensor, Transform targetOrigin, Transform targetVisualRoot, Vector3 targetWindupEuler, Vector3 targetStrikeEuler) // Day17 자동 Setup용 근접 공격 구성
        {
            data = targetData; // 공격 데이터 저장
            sensor = targetSensor; // 벽 차단용 감각 저장
            attackOrigin = targetOrigin; // 공격 시작 위치 저장
            attackVisualRoot = targetVisualRoot; // 공격 시각 루트 저장
            windupEuler = targetWindupEuler; // 몬스터별 준비 자세 저장
            strikeEuler = targetStrikeEuler; // 몬스터별 타격 자세 저장
            CaptureBasePose(); // 생성된 모델의 현재 자세를 기본 회전으로 저장
        }

        public bool TryStartAttack(Transform attackTarget) // Brain 또는 특수 행동에서 단발 근접 공격 시작
        {
            if (!CanStartAttack || attackTarget == null || data == null) // 공격 가능 상태·대상·데이터 확인
            {
                return false; // 공격 시작 실패 반환
            }

            float distance = Vector3.ProjectOnPlane(attackTarget.position - transform.position, Vector3.up).magnitude; // 대상까지 수평 거리 계산

            if (distance > data.AttackRange + 0.35f) // 근접 공격 허용 거리 초과 여부 확인
            {
                return false; // 사거리 밖 공격 시작 차단
            }

            target = attackTarget; // 현재 공격 대상 저장
            attackStartedTime = Time.time; // 공격 시작 시각 기록
            damageApplied = false; // 이번 공격 피해 미적용 상태 초기화
            attacking = true; // 공격 모션 활성화
            return true; // 공격 시작 성공 반환
        }

        public void CancelAttack() // 경직·관찰·사망 등으로 현재 근접 공격 취소
        {
            attacking = false; // 공격 진행 상태 종료
            damageApplied = false; // 피해 적용 상태 초기화
            target = null; // 공격 대상 참조 제거
            RestoreBasePose(); // 시각 기본 자세 복구
        }

        private void TryApplyDamage() // 공격 시점에 대상 거리·벽 차단 확인 후 실제 피해 적용
        {
            if (target == null || data == null) // 피해 대상·데이터 누락 여부 확인
            {
                return; // 피해 처리 중단
            }

            Vector3 targetPoint = ResolveTargetPoint(target); // 플레이어 몸 중심 목표 위치 계산
            Vector3 origin = attackOrigin == null ? transform.position + (Vector3.up * 1.1f) : attackOrigin.position; // 공격 시작점 결정
            Vector3 delta = targetPoint - origin; // 공격 시작점에서 대상까지 방향 계산
            float distance = delta.magnitude; // 실제 공격 직선 거리 계산

            if (distance > data.AttackRange + 0.55f || distance < 0.01f) // 피해 시점 사거리 유효성 확인
            {
                return; // 사거리 밖 또는 잘못된 대상 피해 차단
            }

            if (sensor != null && !sensor.CanSeeTarget(target)) // 일반 몬스터는 벽 뒤 공격을 시각 감각으로 차단
            {
                return; // 직접 시야가 없으면 피해 적용 차단
            }

            if (!HasClearLine(origin, target, delta.normalized, distance)) // 공격자와 대상 사이 실제 벽 존재 여부 확인
            {
                return; // 벽을 통과하는 근접 피해 차단
            }

            PlayerDamageReceiver receiver = target.GetComponentInParent<PlayerDamageReceiver>(); // 대상 계층에서 플레이어 공통 피해 수신기 조회

            if (receiver == null) // 플레이어 피해 대상이 아닌지 확인
            {
                return; // 잘못된 대상 피해 처리 중단
            }

            attackSequence++; // 새 근접 공격 식별 번호 증가
            Vector3 direction = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up).normalized; // 수평 넉백 방향 계산
            DamageInfo damageInfo = new DamageInfo(gameObject, gameObject, CombatFaction.Enemy, CombatDamageType.Physical, data.AttackDamage, targetPoint, -direction, data.StaggerPower, direction * data.KnockbackForce, attackSequence); // Enemy 근접 피해 요청 생성
            DamagePipeline.TryApply(damageInfo, receiver, out _); // 기존 공통 Damage Pipeline으로 플레이어 피해·경직 전달
        }

        private bool HasClearLine(Vector3 origin, Transform targetTransform, Vector3 direction, float distance) // 자기 Collider를 무시한 근접 벽 차단 검사
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance + 0.08f, ~0, QueryTriggerInteraction.Ignore); // 공격 경로 전체 충돌을 거리순 확인하기 위한 Raycast 수행
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 가까운 충돌부터 검사하도록 정렬

            for (int index = 0; index < hits.Length; index++) // 공격 경로 충돌 전체 순회
            {
                Collider collider = hits[index].collider; // 현재 충돌 Collider 조회

                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) // 현재 몬스터 자신의 Collider인지 확인
                {
                    continue; // 자기 몸 충돌은 공격 경로에서 무시
                }

                if (collider.transform == targetTransform || collider.transform.IsChildOf(targetTransform) || targetTransform.IsChildOf(collider.transform)) // 첫 유효 충돌이 플레이어 계층인지 확인
                {
                    return true; // 플레이어까지 벽 없이 연결된 공격 경로 반환
                }

                return false; // 플레이어보다 먼저 다른 Collider가 있으면 벽 차단 처리
            }

            return true; // Raycast 충돌이 없더라도 근거리 대상 자체는 공격 가능 처리
        }

        private void CaptureBasePose() // 공격 시각 루트의 현재 기본 회전 저장
        {
            if (attackVisualRoot != null) // 공격 시각 루트 존재 여부 확인
            {
                visualBaseRotation = attackVisualRoot.localRotation; // 현재 생성 회전을 공격 기준 자세로 저장
            }
        }

        private void ApplyVisualPose(Vector3 eulerOffset) // 기본 자세 기준 공격 회전 오프셋 적용
        {
            if (attackVisualRoot != null) // 공격 시각 루트 존재 여부 확인
            {
                attackVisualRoot.localRotation = visualBaseRotation * Quaternion.Euler(eulerOffset); // 준비·타격 회전 적용
            }
        }

        private void RestoreBasePose() // 공격 종료 후 생성 기본 회전 복구
        {
            if (attackVisualRoot != null) // 공격 시각 루트 존재 여부 확인
            {
                attackVisualRoot.localRotation = visualBaseRotation; // 공격 전 기본 회전으로 복구
            }
        }

        private static Vector3 ResolveTargetPoint(Transform targetTransform) // 근접 공격에 사용할 대상 상체 위치 계산
        {
            CharacterController controller = targetTransform == null ? null : targetTransform.GetComponent<CharacterController>(); // 플레이어 CharacterController 조회

            if (targetTransform == null) // 대상 누락 여부 확인
            {
                return Vector3.zero; // 기본 위치 반환
            }

            if (controller != null) // 캐릭터 충돌체가 존재하는 대상인지 확인
            {
                return targetTransform.TransformPoint(controller.center); // 실제 CharacterController 중심 위치 반환
            }

            return targetTransform.position + (Vector3.up * 1.0f); // 일반 대상의 상체 높이 반환
        }
    }
}
