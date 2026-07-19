using UnityEngine;

namespace ProjectI
{
    public enum WeaponType { Sword, Axe, Bow, Shield }

    /// <summary>
    /// 무기 정의(데이터). (기획서 PART 4.5) 손에 든 무기의 공격 방식·수치를 결정.
    /// 검=빠름 / 도끼=느림+치명타 / 활=원거리(화살 소모) / 방패=우클릭 막기.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "ProjectI/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "무기";
        public WeaponType type = WeaponType.Sword;
        public float attackDamage = 30f;
        public float attackCooldown = 0.5f;
        public float attackRange = 2.5f;      // 근접 사거리 / 활은 크게(예: 20)
        [Range(0f, 1f)] public float critChance = 0.15f;
        public float critMultiplier = 1.5f;
        [Range(0f, 1f)] public float blockReduction = 0.6f; // 방패: 막을 때 피해 감소율
    }
}
