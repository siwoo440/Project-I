using UnityEngine; // 유니티 오브젝트와 벡터 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public readonly struct DamageInfo // Damage Pipeline에 전달되는 단일 피해 요청 데이터
    {
        public DamageInfo(GameObject source, GameObject instigator, CombatFaction sourceFaction, CombatDamageType damageType, float baseDamage, Vector3 hitPoint, Vector3 hitNormal, Vector3 force, int attackId) // 피해 요청 데이터 생성
        {
            Source = source; // 실제 피해 발생 오브젝트 저장
            Instigator = instigator; // 공격을 지시한 행위자 저장
            SourceFaction = sourceFaction; // 공격 주체 진영 저장
            DamageType = damageType; // 피해 종류 저장
            BaseDamage = Mathf.Max(0f, baseDamage); // 기본 피해량 음수 방지
            HitPoint = hitPoint; // 피격 월드 위치 저장
            HitNormal = hitNormal; // 피격 표면 방향 저장
            Force = force; // 넉백 등 후속 반응용 힘 저장
            AttackId = attackId; // 동일 공격 묶음 식별값 저장
        }

        public GameObject Source { get; } // 실제 피해 발생 오브젝트 공개
        public GameObject Instigator { get; } // 공격 지시 행위자 공개
        public CombatFaction SourceFaction { get; } // 공격 주체 진영 공개
        public CombatDamageType DamageType { get; } // 피해 종류 공개
        public float BaseDamage { get; } // 기본 피해량 공개
        public Vector3 HitPoint { get; } // 피격 위치 공개
        public Vector3 HitNormal { get; } // 피격 표면 방향 공개
        public Vector3 Force { get; } // 후속 반응용 힘 공개
        public int AttackId { get; } // 공격 식별값 공개

        public DamageInfo WithBaseDamage(float resolvedDamage) // 공통 피해 계산 결과를 반영한 새 피해 요청 생성
        {
            return new DamageInfo(Source, Instigator, SourceFaction, DamageType, resolvedDamage, HitPoint, HitNormal, Force, AttackId); // 원본 맥락을 유지한 계산 완료 피해 데이터 반환
        }
    }
}
