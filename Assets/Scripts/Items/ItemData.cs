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
        [Tooltip("게임 화면과 상점에 표시할 아이템 이름")] public string displayName = "아이템";
        [Tooltip("상점과 정보 UI에 표시할 아이템 설명")] [TextArea] public string description;
        [Tooltip("소모품과 장비 등 아이템을 구분하는 분류 이름")] public string category = "소모품";        // 분류

        [Header("무게·슬롯")]
        [Tooltip("아이템 하나가 더하는 운반 무게(kg)")] public float weightKg = 1f;               // 무게
        [Tooltip("아이템 하나가 차지하는 인벤토리 슬롯 수")] public int inventorySlots = 1;            // 차지 칸
        [Tooltip("가방처럼 소지 시 추가로 제공하는 인벤토리 슬롯 수")] public int bonusSlots = 0;                // 가방류: 인벤토리 칸 추가(가방=4)

        [Header("속성")]
        [Tooltip("아이템 사용 시 발생하는 소음 단계(0: 무음, 3: 최대)")] [Range(0, 3)] public int noise = 0;       // 소음 (0무음~3대)
        [Tooltip("아이템 작동에 배터리를 소비하는지 여부")] public bool usesBattery = false;          // 배터리 사용
        [Tooltip("F키로 실행하는 아이템 전용 상호작용 이름")] public string interaction = "없음";       // F 상호작용

        [Header("가격")]
        [Tooltip("마을 상점에서 아이템을 구매할 때 지불할 가격")] public int buyPrice = 0;
        [Tooltip("마을 상점에서 아이템을 판매할 때 받을 가격")] public int sellPrice = 0;                 // = buyPrice × 0.8
    }
}
