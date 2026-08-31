using System; // 충돌 결과 거리 정렬 기능 참조
using System.Collections.Generic; // 공격별 중복 피격 대상 기록 기능 참조
using UnityEngine; // 유니티 물리 궤적 검사 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public sealed class MeleeWeaponTrace : MonoBehaviour // 이전·현재 무기 위치 사이를 검사하는 근접 공격 궤적
    {
        [SerializeField] private Transform traceStart; // 무기 손잡이 쪽 궤적 기준점
        [SerializeField] private Transform traceEnd; // 무기 끝 쪽 궤적 기준점
        [SerializeField] private LayerMask hitMask = ~0; // 근접 공격 물리 검사 LayerMask
        private readonly HashSet<int> hitTargetIds = new HashSet<int>(); // 현재 공격에서 이미 처리한 피격 대상 식별값
        private Transform instigatorRoot; // 자기 자신 충돌 제외용 공격 행위자 루트
        private Transform attackOrigin; // 벽 가림 검사 시작 기준점
        private Vector3 previousStart; // 이전 프레임 손잡이 기준점 위치
        private Vector3 previousEnd; // 이전 프레임 무기 끝 기준점 위치
        private int currentAttackId; // 현재 궤적 공격 식별값
        private bool tracing; // 현재 궤적 검사 활성 여부

        public Transform TraceStart => traceStart; // Validator용 시작 기준점 공개
        public Transform TraceEnd => traceEnd; // Validator용 끝 기준점 공개
        public bool IsTracing => tracing; // 현재 궤적 활성 여부 공개
        public int HitTargetCount => hitTargetIds.Count; // 현재 공격 중복 방지 대상 수 공개

        public void Configure(Transform startPoint, Transform endPoint, LayerMask mask) // Day14 테스트 검 궤적 자동 구성
        {
            traceStart = startPoint; // 궤적 시작 기준점 저장
            traceEnd = endPoint; // 궤적 끝 기준점 저장
            hitMask = mask; // 물리 검사 LayerMask 저장
        }

        public void BeginTrace(int attackId, Transform targetInstigatorRoot, Transform targetAttackOrigin) // 공격 Active 단계 시작 시 궤적 초기화
        {
            currentAttackId = attackId; // 현재 공격 식별값 저장
            instigatorRoot = targetInstigatorRoot; // 자기 충돌 제외용 공격자 루트 저장
            attackOrigin = targetAttackOrigin != null ? targetAttackOrigin : targetInstigatorRoot; // 벽 가림 검사 기준점 저장
            hitTargetIds.Clear(); // 새 공격의 중복 피격 기록 초기화
            previousStart = ResolveStartPosition(); // 시작 기준점 현재 위치를 이전 위치로 저장
            previousEnd = ResolveEndPosition(); // 끝 기준점 현재 위치를 이전 위치로 저장
            tracing = true; // 근접 궤적 검사 활성화
        }

        public void TickTrace(CombatController controller, AttackDefinition definition) // Active 단계 한 프레임 근접 궤적 처리
        {
            if (!tracing || controller == null || definition == null) // 유효 궤적 실행 조건 확인
            {
                return; // 궤적 처리 생략
            }

            Vector3 currentStart = ResolveStartPosition(); // 현재 손잡이 기준점 위치 조회
            Vector3 currentEnd = ResolveEndPosition(); // 현재 무기 끝 기준점 위치 조회
            bool startBlocked = SweepPoint(previousStart, currentStart, controller, definition); // 손잡이 기준점 이동 구간 검사
            bool endBlocked = SweepPoint(previousEnd, currentEnd, controller, definition); // 무기 끝 기준점 이동 구간 검사

            if (!startBlocked && !endBlocked) // 이동 구간에서 즉시 벽에 막히지 않았는지 확인
            {
                SweepCurrentBlade(currentStart, currentEnd, controller, definition); // 현재 검날 전체 Capsule 영역 추가 검사
            }

            previousStart = currentStart; // 다음 프레임용 손잡이 위치 갱신
            previousEnd = currentEnd; // 다음 프레임용 무기 끝 위치 갱신
        }

        public void EndTrace() // 공격 Active 단계 종료 시 궤적 비활성화
        {
            tracing = false; // 근접 궤적 검사 비활성화
            hitTargetIds.Clear(); // 공격별 중복 피격 기록 초기화
        }

        private bool SweepPoint(Vector3 previousPosition, Vector3 currentPosition, CombatController controller, AttackDefinition definition) // 한 기준점의 프레임 이동 구간 SphereCast 검사
        {
            Vector3 delta = currentPosition - previousPosition; // 이전·현재 위치 이동 벡터 계산
            float distance = delta.magnitude; // 이동 거리 계산

            if (distance <= 0.0001f) // 이동이 거의 없는지 확인
            {
                return false; // 이동 구간 검사 생략
            }

            RaycastHit[] hits = Physics.SphereCastAll(previousPosition, definition.TraceRadius, delta.normalized, distance, hitMask, QueryTriggerInteraction.Ignore); // 이동 구간 전체 충돌 조회
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 공격 진행 방향 기준 가까운 충돌부터 정렬

            foreach (RaycastHit hit in hits) // 정렬된 충돌 결과 순회
            {
                if (ProcessCollider(hit.collider, hit.point, hit.normal, controller, definition)) // 현재 Collider가 벽으로 궤적을 차단했는지 확인
                {
                    return true; // 첫 벽 충돌에서 해당 궤적 진행 중단
                }
            }

            return false; // 이동 구간 벽 차단 없음 반환
        }

        private void SweepCurrentBlade(Vector3 currentStart, Vector3 currentEnd, CombatController controller, AttackDefinition definition) // 현재 검날 Capsule 영역 검사
        {
            Collider[] overlaps = Physics.OverlapCapsule(currentStart, currentEnd, definition.TraceRadius, hitMask, QueryTriggerInteraction.Ignore); // 현재 검날 주변 Collider 조회

            foreach (Collider overlap in overlaps) // 현재 검날 영역 충돌 대상 순회
            {
                Vector3 hitPoint = overlap == null ? currentEnd : overlap.ClosestPoint((currentStart + currentEnd) * 0.5f); // 근사 피격 위치 계산

                if (ProcessCollider(overlap, hitPoint, Vector3.zero, controller, definition)) // 벽 차단 또는 피해 대상 처리
                {
                    return; // 검날 영역에서 벽을 만나면 나머지 뒤쪽 처리 중단
                }
            }
        }

        private bool ProcessCollider(Collider collider, Vector3 hitPoint, Vector3 hitNormal, CombatController controller, AttackDefinition definition) // 단일 Collider 피해 또는 벽 충돌 처리
        {
            if (collider == null || IsOwnedBy(collider.transform, transform) || IsOwnedBy(collider.transform, instigatorRoot)) // 무효 대상과 무기·공격자 자기 충돌 제외
            {
                return false; // 자기 충돌은 궤적 계속 진행
            }

            IDamageable damageable = DamagePipeline.FindDamageable(collider); // Collider 부모 계층에서 공통 피해 대상 조회

            if (damageable == null) // 공통 피해 대상이 아닌 물리 장애물인지 확인
            {
                controller.RecordWallHit(collider.gameObject, hitPoint); // 전투 진단에 벽 충돌 기록
                return true; // 환경 Collider를 벽 차단으로 반환
            }

            int targetId = damageable.DamageTransform.GetInstanceID(); // 공통 피해 대상 대표 식별값 조회

            if (hitTargetIds.Contains(targetId)) // 같은 공격에서 이미 처리한 대상인지 확인
            {
                return false; // 같은 공격 중복 피해 차단
            }

            if (IsOccludedByWall(damageable, collider, hitPoint, controller)) // 공격자와 대상 사이 벽 가림 여부 확인
            {
                return false; // 벽 뒤 대상 피해 차단 후 다른 궤적 검사 유지
            }

            hitTargetIds.Add(targetId); // 진영 허용 여부와 무관하게 이번 공격에서 대상 처리 완료 기록
            DamageInfo damageInfo = controller.BuildDamageInfo(gameObject, definition, hitPoint, hitNormal, currentAttackId); // 현재 공격의 공통 피해 요청 생성
            DamagePipeline.TryApply(damageInfo, damageable, out CombatHitResult result); // 공통 Damage Pipeline에 피해 적용 요청
            controller.RecordHit(result); // 전투 진단에 마지막 피격 결과 기록
            return false; // 피해 대상은 검 궤적 자체를 차단하지 않음
        }

        private bool IsOccludedByWall(IDamageable target, Collider targetCollider, Vector3 hitPoint, CombatController controller) // 공격자에서 대상까지의 환경 벽 차단 검사
        {
            Vector3 origin = attackOrigin != null ? attackOrigin.position : controller.transform.position + Vector3.up; // 가림 검사 시작 위치 계산
            Vector3 destination = hitPoint; // 실제 피격 지점을 가림 검사 목적지로 사용
            Vector3 delta = destination - origin; // 가림 검사 방향과 거리 계산용 벡터 생성
            float distance = delta.magnitude; // 가림 검사 총 거리 계산

            if (distance <= 0.001f) // 검사 거리가 너무 짧은지 확인
            {
                return false; // 벽 가림 없음 반환
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, distance + 0.05f, hitMask, QueryTriggerInteraction.Ignore); // 공격자에서 대상까지 모든 Collider 조회
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance)); // 가까운 Collider부터 정렬

            foreach (RaycastHit hit in hits) // 가림 검사 충돌 결과 순회
            {
                if (hit.collider == null || IsOwnedBy(hit.collider.transform, transform) || IsOwnedBy(hit.collider.transform, instigatorRoot)) // 무기와 공격자 자기 Collider 제외
                {
                    continue; // 자기 충돌 건너뜀
                }

                IDamageable firstDamageable = DamagePipeline.FindDamageable(hit.collider); // 현재 가림 Collider의 피해 대상 조회

                if (firstDamageable != null && firstDamageable.DamageTransform == target.DamageTransform) // 첫 피해 대상이 목표 대상인지 확인
                {
                    return false; // 목표 대상보다 앞선 벽이 없으므로 가림 없음 반환
                }

                if (hit.collider == targetCollider) // 목표 Collider 자체인지 추가 확인
                {
                    return false; // 목표 Collider 도달 시 가림 없음 반환
                }

                controller.RecordWallHit(hit.collider.gameObject, hit.point); // 목표보다 앞선 환경 Collider를 벽 충돌로 기록
                return true; // 벽 뒤 대상 피해 차단
            }

            return false; // 검사 구간에 벽 없음 반환
        }

        private Vector3 ResolveStartPosition() // 궤적 시작 기준점 월드 위치 조회
        {
            return traceStart != null ? traceStart.position : transform.position; // 설정 기준점 또는 무기 루트 위치 반환
        }

        private Vector3 ResolveEndPosition() // 궤적 끝 기준점 월드 위치 조회
        {
            return traceEnd != null ? traceEnd.position : transform.position; // 설정 기준점 또는 무기 루트 위치 반환
        }

        private static bool IsOwnedBy(Transform target, Transform ownerRoot) // Transform이 지정 루트 소속인지 확인
        {
            if (target == null || ownerRoot == null) // 대상 또는 루트 누락 여부 확인
            {
                return false; // 소속 아님 반환
            }

            return target == ownerRoot || target.IsChildOf(ownerRoot); // 자기 자신 또는 자식 계층 여부 반환
        }
    }
}
