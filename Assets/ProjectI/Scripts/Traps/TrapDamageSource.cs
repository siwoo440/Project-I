using System.Collections.Generic; // 한 작동당 피격 대상 중복 방지 HashSet 참조
using ProjectI.Combat; // Damage Pipeline·공통 피해 데이터 참조
using UnityEngine; // Trigger Collider·Vector 기능 참조

namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public sealed class TrapDamageSource : MonoBehaviour // 활성 구간에 Player·Monster를 한 번씩만 공격하는 공통 함정 피해 볼륨
    {
        [SerializeField] private Transform ownerRoot; // 자기 자신과 자식 오브젝트 피해 제외 기준 루트
        private readonly HashSet<int> damagedTargets = new HashSet<int>(); // 현재 작동에서 이미 피해를 받은 대상 ID 집합
        private bool active; // 현재 피해 판정 활성 여부
        private float damage; // 현재 작동 피해량
        private float staggerPower; // 현재 작동 경직 힘
        private float knockbackForce; // 현재 작동 넉백 크기
        private Vector3 forceDirection = Vector3.up; // 현재 작동 넉백 방향
        private int attackId; // Damage Pipeline 동일 작동 식별값
        private string trapName = "Trap"; // 진단용 현재 함정 이름

        public static string LastTrapName { get; private set; } = "None"; // F1 진단용 마지막 피해 함정 이름
        public static string LastTargetName { get; private set; } = "None"; // F1 진단용 마지막 피해 대상 이름
        public static float LastAppliedDamage { get; private set; } // F1 진단용 마지막 실제 적용 피해량
        public static float LastHitTime { get; private set; } = -1f; // F1 진단용 마지막 함정 피해 시각
        public bool IsActive => active; // 현재 피해 창 활성 여부 공개
        public int DamagedTargetCount => damagedTargets.Count; // 현재 작동 중 피해 대상 수 공개
        public float Damage => damage; // 진단·Validator용 현재 피해량 공개
        public float StaggerPower => staggerPower; // 진단·Validator용 경직 값 공개
        public float KnockbackForce => knockbackForce; // 진단·Validator용 넉백 값 공개

        public void Configure(Transform targetOwnerRoot) // Editor Setup용 자기 루트 구성
        {
            ownerRoot = targetOwnerRoot; // 자기 피해 제외 기준 저장
        }

        public void BeginDamageWindow(string sourceName, float targetDamage, float targetStagger, float targetKnockback, Vector3 targetForceDirection, int targetAttackId) // 새 작동 피해 창 시작
        {
            trapName = string.IsNullOrWhiteSpace(sourceName) ? "Trap" : sourceName; // 진단용 함정 이름 저장
            damage = Mathf.Max(0f, targetDamage); // 피해량 음수 방지
            staggerPower = Mathf.Max(0f, targetStagger); // 경직 값 음수 방지
            knockbackForce = Mathf.Max(0f, targetKnockback); // 넉백 값 음수 방지
            forceDirection = targetForceDirection.sqrMagnitude < 0.001f ? Vector3.up : targetForceDirection.normalized; // 유효 넉백 방향 보정
            attackId = targetAttackId; // 동일 작동 공격 ID 저장
            damagedTargets.Clear(); // 새 작동에서 모든 대상 재피격 가능하도록 기록 초기화
            active = true; // Trigger Stay 피해 판정 활성화
        }

        public void EndDamageWindow() // 현재 작동 피해 창 종료
        {
            active = false; // 추가 Trigger 피해 차단
        }

        private void OnDisable() // 피해 볼륨 비활성화 시 안전 정리
        {
            active = false; // 비활성 상태에서 피해 차단
            damagedTargets.Clear(); // 이전 작동 대상 기록 정리
        }

        private void OnTriggerEnter(Collider other) // 피해 창 도중 새 대상 진입 처리
        {
            TryApplyDamage(other); // 공통 함정 피해 시도
        }

        private void OnTriggerStay(Collider other) // 피해 창이 켜질 때 이미 겹쳐 있던 대상 처리
        {
            TryApplyDamage(other); // 공통 함정 피해 시도
        }

        private void TryApplyDamage(Collider other) // 단일 Collider에 함정 피해 적용
        {
            if (!active || other == null) // 피해 창 활성·Collider 유효 여부 확인
            {
                return; // 비활성 상태 피해 차단
            }

            IDamageable target = DamagePipeline.FindDamageable(other); // Collider 부모에서 공통 피해 대상 검색

            if (target == null || target.DamageTransform == null) // 웃는 석상·장식 등 비피격 대상 여부 확인
            {
                return; // Damage Pipeline 대상이 아니면 무시
            }

            Transform targetTransform = target.DamageTransform; // 대표 피해 Transform 조회

            if (ownerRoot != null && (targetTransform == ownerRoot || targetTransform.IsChildOf(ownerRoot))) // 함정 자기 자신 계층 여부 확인
            {
                return; // 자기 오브젝트 피해 방지
            }

            int targetId = targetTransform.root.GetInstanceID(); // 여러 Collider를 하나의 피격 대상으로 묶는 ID 계산

            if (!damagedTargets.Add(targetId)) // 현재 작동에서 이미 피해를 받은 대상 여부 확인
            {
                return; // 한 Activation당 Target당 1회 규칙 적용
            }

            Vector3 hitPoint = other.ClosestPoint(transform.position); // Trigger 중심 기준 실제 피격 위치 계산
            Vector3 force = forceDirection * knockbackForce; // 공통 반응용 넉백 벡터 계산
            DamageInfo info = new DamageInfo(gameObject, ownerRoot == null ? gameObject : ownerRoot.gameObject, CombatFaction.Environment, CombatDamageType.Trap, damage, hitPoint, -forceDirection, staggerPower, force, attackId); // 환경 진영 Trap 피해 요청 생성
            bool applied = DamagePipeline.TryApply(info, target, out CombatHitResult result); // 공통 Damage Pipeline으로 Player·Enemy 피해 처리

            if (applied) // 실제 피해 적용 성공 여부 확인
            {
                LastTrapName = trapName; // 마지막 피해 함정 이름 저장
                LastTargetName = targetTransform.name; // 마지막 피해 대상 이름 저장
                LastAppliedDamage = result.AppliedDamage; // 실제 감소한 피해량 저장
                LastHitTime = Time.time; // 마지막 피해 시각 저장
            }
        }
    }
}
