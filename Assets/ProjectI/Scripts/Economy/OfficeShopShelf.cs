using System.Collections.Generic; // 목록 사용
using ProjectI.Audio; // 효과음
using ProjectI.Items; // WorldItem 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public enum ShopPurchaseResult // 구매 결과
    {
        Success, // 성공
        InvalidEntry, // 상품 오류
        InvalidQuantity, // 수량 오류
        NotEnoughFunds, // 자금 부족
        TrayFull, // 수령대 자리 부족
    }

    public sealed class OfficeShopShelf : MonoBehaviour // 위·아래 2칸 진열대: 구매 처리와 수령대 연결 (33일차)
    {
        [SerializeField] private ShopCatalog catalog; // 상품 목록
        [SerializeField] private OfficeShopPickupTray tray; // 수령대
        [SerializeField] private OfficeShopPanel panel; // 구매 창
        [SerializeField] private CampaignEconomy economy; // 공동 자금

        public ShopCatalog Catalog => catalog; // 공개
        public OfficeShopPickupTray Tray => tray; // 공개
        public OfficeShopPanel Panel => panel; // 공개
        public CampaignEconomy Economy => ResolveEconomy(); // 공개
        public int Funds => ResolveEconomy() == null ? 0 : economy.SharedFunds; // 현재 자금

        public void Configure(ShopCatalog targetCatalog, OfficeShopPickupTray targetTray, OfficeShopPanel targetPanel) // 에디터 자동 구성
        {
            catalog = targetCatalog; // 목록
            tray = targetTray; // 수령대
            panel = targetPanel; // 창
        }

        public int MaxAffordable(ShopEntry entry) // 지금 살 수 있는 최대 수량 (한도·자금·수령대 자리 중 최소)
        {
            if (entry == null || !entry.IsValid || tray == null) // 확인
            {
                return 0; // 불가
            }

            int byFunds = Funds / entry.price; // 자금 기준
            int byTray = tray.FreeSlotCount(entry.maxPerPurchase); // 자리 기준
            return Mathf.Max(0, Mathf.Min(entry.maxPerPurchase, Mathf.Min(byFunds, byTray))); // 최소
        }

        public ShopPurchaseResult Purchase(ShopEntry entry, int quantity, out List<WorldItem> delivered) // 구매 (결제 → 수령대 생성)
        {
            delivered = new List<WorldItem>(); // 결과

            if (entry == null || !entry.IsValid || tray == null || ResolveEconomy() == null) // 상품·참조
            {
                return ShopPurchaseResult.InvalidEntry; // 실패
            }

            if (quantity <= 0 || quantity > entry.maxPerPurchase) // 수량
            {
                return ShopPurchaseResult.InvalidQuantity; // 실패
            }

            if (tray.FreeSlotCount(quantity) < quantity) // 자리 먼저 확인 (결제 후 실패 방지)
            {
                return ShopPurchaseResult.TrayFull; // 실패
            }

            int total = entry.price * quantity; // 합계

            if (!economy.TrySpend(total)) // 결제
            {
                return ShopPurchaseResult.NotEnoughFunds; // 실패
            }

            delivered = tray.Deliver(entry.item, quantity); // 수령대에 생성

            if (delivered.Count < quantity) // 일부만 생성됨 (프리팹 이상 등) → 차액 환불
            {
                economy.AddFunds(entry.price * (quantity - delivered.Count)); // 환불
            }

            SoundPlayer.PlayAt(SoundId.Purchase, tray.transform.position, 0.9f); // 동전 소리
            Debug.Log($"[Project I] 상점 구매 / {entry.DisplayName} x{delivered.Count} / -{entry.price * delivered.Count} / 공동 자금 {economy.SharedFunds}", this); // 개발용 로그
            return ShopPurchaseResult.Success; // 성공
        }

        public static string Describe(ShopPurchaseResult result) // 결과 안내 문구
        {
            switch (result) // 결과별
            {
                case ShopPurchaseResult.Success: return "구매 완료 — 수령대에서 가져가세요"; // 성공
                case ShopPurchaseResult.NotEnoughFunds: return "공동 자금이 부족합니다"; // 자금
                case ShopPurchaseResult.TrayFull: return "수령대가 가득 찼습니다 — 먼저 물건을 가져가세요"; // 자리
                case ShopPurchaseResult.InvalidQuantity: return "수량을 다시 선택하세요"; // 수량
                default: return "구매할 수 없는 상품입니다"; // 기타
            }
        }

        private CampaignEconomy ResolveEconomy() // 공동 자금 확보
        {
            if (economy == null) // 누락
            {
                economy = Object.FindFirstObjectByType<CampaignEconomy>(); // 씬에서 조회
            }

            return economy; // 반환
        }

        private void Awake() // 참조 확보
        {
            ResolveEconomy(); // 자금

            if (panel == null) // 창 누락
            {
                panel = GetComponent<OfficeShopPanel>(); // 같은 오브젝트

                if (panel == null) // 없으면 추가
                {
                    panel = gameObject.AddComponent<OfficeShopPanel>(); // 런타임 안전용
                }
            }
        }
    }
}
