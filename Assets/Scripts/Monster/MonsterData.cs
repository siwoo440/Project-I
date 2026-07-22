using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 몬스터 정의(핵심 스탯). (기획서 PART 7.2)
    /// ※ 몬스터.csv는 컬럼이 복잡(공격력 다중값 등)해 전용 임포터는 이후 별도 작성.
    ///   지금은 이 SO를 수동 생성하거나 MonsterAI의 fallback 값으로 동작.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterData", menuName = "ProjectI/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        [Tooltip("게임 화면과 디버그 정보에 표시할 몬스터 이름")] public string displayName = "몬스터";
        [Tooltip("몬스터가 처치되기 전까지 가질 수 있는 최대 체력")] public float maxHealth = 80f;
        [Tooltip("피격 시 들어오는 피해에서 고정으로 감소시킬 방어력")] public float defense = 0f;      // 받는 피해 감소 (데미지 공식)
        [Tooltip("플레이어를 추적할 때 적용할 이동 속도(m/s)")] public float moveSpeed = 3.5f;
        [Tooltip("공격이 플레이어에게 명중했을 때 적용할 피해량")] public float attackDamage = 20f;
        [Tooltip("플레이어에게 공격을 시작할 최대 거리(m)")] public float attackRange = 1.6f;
        [Tooltip("공격 후 다음 공격까지 기다릴 시간(초)")] public float attackCooldown = 1.5f;
        [Tooltip("몬스터가 시각으로 플레이어를 감지할 최대 거리(m)")] public float sightRange = 12f;
        [Tooltip("몬스터 정면을 기준으로 플레이어를 감지할 시야각(도)")] [Range(0, 180)] public float sightAngle = 110f;
        [Tooltip("플레이어가 낸 소리를 감지할 최대 거리(m)")] public float hearingRange = 12f;
        [Tooltip("어두운 환경에서 몬스터의 감지 범위를 강화할지 여부")] public bool aggressiveInDark = true;   // 어두우면 감지력↑(공격성↑)
    }
}
