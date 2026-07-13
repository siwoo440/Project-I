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
        public string displayName = "몬스터";
        public float maxHealth = 80f;
        public float moveSpeed = 3.5f;
        public float attackDamage = 20f;
        public float attackRange = 1.6f;
        public float attackCooldown = 1.5f;
        public float sightRange = 12f;
        [Range(0, 180)] public float sightAngle = 110f;
        public float hearingRange = 12f;
        public bool aggressiveInDark = true;   // 어두우면 감지력↑(공격성↑)
    }
}
