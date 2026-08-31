using UnityEngine; // 유니티 오브젝트와 벡터 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public readonly struct CombatHitResult // 단일 Damage Pipeline 처리 결과
    {
        public CombatHitResult(bool allowed, GameObject targetObject, float requestedDamage, float appliedDamage, bool fatal, string reason, Vector3 hitPoint) // 피해 처리 결과 생성
        {
            Allowed = allowed; // 진영·상태 규칙상 허용 여부 저장
            TargetObject = targetObject; // 실제 피격 대상 오브젝트 저장
            RequestedDamage = requestedDamage; // 요청 피해량 저장
            AppliedDamage = appliedDamage; // 실제 적용 피해량 저장
            Fatal = fatal; // 이번 피해 후 사망 여부 저장
            Reason = reason ?? string.Empty; // 처리 결과 사유 저장
            HitPoint = hitPoint; // 실제 피격 위치 저장
        }

        public bool Allowed { get; } // 피해 허용 여부 공개
        public GameObject TargetObject { get; } // 피격 대상 오브젝트 공개
        public float RequestedDamage { get; } // 요청 피해량 공개
        public float AppliedDamage { get; } // 실제 적용 피해량 공개
        public bool Fatal { get; } // 치명 피해 여부 공개
        public string Reason { get; } // 처리 사유 공개
        public Vector3 HitPoint { get; } // 피격 위치 공개
    }
}
