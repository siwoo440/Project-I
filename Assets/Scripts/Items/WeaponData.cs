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
        [Tooltip("게임 화면과 인벤토리에 표시할 무기 이름")] public string displayName = "무기";
        [Tooltip("검과 도끼 및 활과 방패 중 적용할 무기 종류")] public WeaponType type = WeaponType.Sword;
        [Tooltip("공격이 적에게 명중했을 때 적용할 기본 피해량")] public float attackDamage = 30f;
        [Tooltip("공격 후 다음 공격까지 기다릴 시간(초)")] public float attackCooldown = 0.5f;
        [Tooltip("공격 판정이 도달하는 최대 거리(m)")] public float attackRange = 2.5f;      // 근접 사거리 / 활은 크게(예: 20)
        [Tooltip("공격 한 번이 치명타가 될 확률(0~1)")] [Range(0f, 1f)] public float critChance = 0.15f;
        [Tooltip("치명타 발생 시 기본 피해에 곱할 배율")] public float critMultiplier = 1.5f;
        [Tooltip("방패 방어 중 감소시킬 피해 비율(0~1)")] [Range(0f, 1f)] public float blockReduction = 0.6f; // 방패: 막을 때 피해 감소율
    }
}
