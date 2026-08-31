using UnityEngine; // Transform 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public interface IDamageable // 플레이어·몬스터·환경 오브젝트 공통 피해 대상 규격
    {
        CombatFaction Faction { get; } // 피격 대상 진영 공개
        bool IsAlive { get; } // 현재 피해 적용 가능 생존 여부 공개
        Transform DamageTransform { get; } // 피격 대상 대표 Transform 공개
        float ApplyDamage(DamageInfo damageInfo); // Damage Pipeline 승인 후 실제 피해 적용
    }
}
