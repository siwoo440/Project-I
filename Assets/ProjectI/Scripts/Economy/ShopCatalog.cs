using System; // 직렬화 특성 사용
using System.Collections.Generic; // 목록 사용
using ProjectI.Items; // 아이템 정의 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public enum ShopShelfTier // 진열대 칸
    {
        Lower = 0, // 아래 칸
        Upper = 1, // 위 칸
    }

    [Serializable]
    public sealed class ShopEntry // 상품 하나
    {
        public ItemDefinition item; // 판매할 아이템 종류
        public int price = 100; // 개당 가격
        public int maxPerPurchase = 5; // 한 번에 살 수 있는 최대 수량
        public ShopShelfTier tier = ShopShelfTier.Upper; // 진열 칸

        public string DisplayName => item == null ? "(비어 있음)" : item.DisplayName; // 표시 이름
        public bool IsValid => item != null && item.RecoveryPrefab != null && price > 0; // 판매 가능 여부
    }

    [CreateAssetMenu(menuName = "Project I/Shop Catalog", fileName = "OfficeShopCatalog")]
    public sealed class ShopCatalog : ScriptableObject // 사무소 상점 상품 목록 (33일차)
    {
        [SerializeField] private List<ShopEntry> entries = new List<ShopEntry>(); // 상품

        public IReadOnlyList<ShopEntry> Entries => entries; // 공개

        public void Configure(List<ShopEntry> values) // 에디터 자동 구성
        {
            entries = values ?? new List<ShopEntry>(); // 저장
        }

        public List<ShopEntry> EntriesOn(ShopShelfTier tier) // 칸별 상품 (목록 순서 유지)
        {
            List<ShopEntry> result = new List<ShopEntry>(); // 결과

            foreach (ShopEntry entry in entries) // 순회
            {
                if (entry != null && entry.IsValid && entry.tier == tier) // 해당 칸
                {
                    result.Add(entry); // 등록
                }
            }

            return result; // 반환
        }

        private void OnValidate() // 값 보정
        {
            foreach (ShopEntry entry in entries) // 순회
            {
                if (entry == null) // 빈 항목
                {
                    continue; // 다음
                }

                entry.price = Mathf.Max(1, entry.price); // 최소 가격
                entry.maxPerPurchase = Mathf.Clamp(entry.maxPerPurchase, 1, 20); // 수량 범위
            }
        }
    }
}
