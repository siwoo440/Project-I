using ProjectI.Interaction; // F 상호작용 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 시선 판정
    public sealed class OfficeShopShelfItem : MonoBehaviour, IInteractable // 진열대 한 칸의 견본 상품: F로 구매 창을 엶 (33일차)
    {
        [SerializeField] private OfficeShopShelf shelf; // 진열대
        [SerializeField] private int entryIndex = -1; // 상품 목록 번호

        public string Prompt => BuildPrompt(); // 안내
        public InteractionType InteractionType => InteractionType.Press; // F 한 번
        public float HoldDuration => 0f; // 길게 누르기 불필요
        public ShopEntry Entry => shelf == null || shelf.Catalog == null || entryIndex < 0 || entryIndex >= shelf.Catalog.Entries.Count ? null : shelf.Catalog.Entries[entryIndex]; // 상품
        public OfficeShopShelf Shelf => shelf; // 진열대

        public void Configure(OfficeShopShelf targetShelf, int targetEntryIndex) // 에디터 자동 구성
        {
            shelf = targetShelf; // 진열대
            entryIndex = targetEntryIndex; // 번호
        }

        public bool CanInteract(PlayerInteractor interactor) // 상품이 유효하고 창이 닫혀 있을 때
        {
            ShopEntry entry = Entry; // 상품
            return interactor != null && entry != null && entry.IsValid && shelf.Panel != null && !shelf.Panel.IsOpen; // 조건
        }

        public void Interact(PlayerInteractor interactor) // 구매 창 열기
        {
            if (CanInteract(interactor)) // 확인
            {
                shelf.Panel.Open(shelf, Entry, interactor); // 열기
            }
        }

        private string BuildPrompt() // 이름·가격
        {
            ShopEntry entry = Entry; // 상품
            return entry == null ? "상품" : $"{entry.DisplayName} / {entry.price} — 구매하기"; // 안내
        }
    }
}
