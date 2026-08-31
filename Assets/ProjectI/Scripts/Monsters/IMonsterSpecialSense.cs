using UnityEngine; // Transform과 Vector3 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public interface IMonsterSpecialSense // 웃는 석상·미믹 등 특수 감지를 확장하기 위한 공통 감각 인터페이스
    {
        bool TrySense(MonsterBrain brain, out Transform target, out Vector3 sensedPosition); // 특수 규칙으로 목표 또는 조사 위치를 감지
    }
}
