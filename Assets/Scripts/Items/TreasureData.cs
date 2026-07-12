using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 보물 정의(데이터). (기획서 PART 8.3 / 데이터: 보물.csv 컬럼과 대응)
    /// 우클릭 → Create → ProjectI → Treasure Data.
    /// </summary>
    [CreateAssetMenu(fileName = "TreasureData", menuName = "ProjectI/Treasure Data")]
    public class TreasureData : ScriptableObject
    {
        public string displayName = "보물";
        public int minValue = 100;        // 최소가
        public int maxValue = 300;        // 최대가
        public float weightKg = 1f;
        public int inventorySlots = 1;
        public bool twoHanded = false;    // 두손 운반
        [Range(0, 3)] public int noise = 0;
    }
}
