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
        [Tooltip("게임 화면과 정산 결과에 표시할 보물 이름")] public string displayName = "보물";
        [Tooltip("보물을 처음 생성할 때 결정할 수 있는 최소 감정 가치")] public int minValue = 100;        // 최소가
        [Tooltip("보물을 처음 생성할 때 결정할 수 있는 최대 감정 가치")] public int maxValue = 300;        // 최대가
        [Tooltip("보물 하나가 더하는 운반 무게(kg)")] public float weightKg = 1f;
        [Tooltip("보물 하나가 차지하는 인벤토리 슬롯 수")] public int inventorySlots = 1;
        [Tooltip("보물을 양손으로만 운반해야 하는지 여부")] public bool twoHanded = false;    // 두손 운반
        [Tooltip("보물 운반 시 발생하는 소음 단계(0: 무음, 3: 최대)")] [Range(0, 3)] public int noise = 0;
    }
}
