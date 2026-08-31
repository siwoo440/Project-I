using UnityEngine; // Transform 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public interface ICombatReactionReceiver // 피해 적용 후 경직·넉백 반응을 받는 공통 인터페이스
    {
        Transform ReactionTransform { get; } // 대표 반응 Transform 공개
        bool IsStaggered { get; } // 현재 경직 상태 공개
        float CurrentStagger { get; } // 현재 누적 경직 수치 공개
        float StaggerThreshold { get; } // 경직 발동 한계 수치 공개
        float KnockbackResistance { get; } // 넉백 저항 비율 공개
        void ReceiveReaction(DamageInfo damageInfo); // Damage Pipeline 승인 피해의 후속 반응 수신
    }
}
