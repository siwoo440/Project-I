using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 아이템 정의(데이터). (기획서 PART 8.1 / 데이터: 아이템.csv 컬럼과 대응)
    /// 우클릭 → Create → ProjectI → Item Data 로 에셋 생성.
    /// 5일차에 CSV → 이 ScriptableObject 자동 임포트 툴로 확장 예정.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "ProjectI/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("기본")]
        public string displayName = "아이템";
        [TextArea] public string description;
        public string category = "소모품";        // 분류

        [Header("무게·슬롯")]
        public float weightKg = 1f;               // 무게
        public int inventorySlots = 1;            // 차지 칸
        public int bonusSlots = 0;                // 가방류: 인벤토리 칸 추가(가방=4)

        [Header("속성")]
        [Range(0, 3)] public int noise = 0;       // 소음 (0무음~3대)
        public bool usesBattery = false;          // 배터리 사용
        public string interaction = "없음";       // F 상호작용

        [Header("가격")]
        public int buyPrice = 0;
        public int sellPrice = 0;                 // = buyPrice × 0.8
    }
}
