using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using ProjectI.Items; // 기존 빠른 슬롯과 WorldItem 기능 참조
using UnityEngine; // 유니티 Transform과 컴포넌트 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 판매대 F 상호작용용 Collider 필수 지정
    public sealed class OfficeSaleCounter : MonoBehaviour, IInteractable // 선택 중인 회수품을 판매해 공동 자금으로 전환
    {
        [SerializeField] private CampaignEconomy economy; // 판매 수익을 반영할 공동 경제 상태
        [SerializeField] private Transform soldItemsRoot; // 판매 완료 아이템을 숨겨둘 내부 루트

        public string Prompt => BuildPrompt(); // 현재 선택 회수품 판매 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // 판매는 F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요

        private void Awake() // 판매대 런타임 참조 초기화
        {
            ResolveReferences(); // 공동 경제 상태와 판매 완료 보관 루트 확보
        }

        public void Configure(CampaignEconomy targetEconomy, Transform targetSoldItemsRoot) // 에디터 자동 구성용 참조 지정
        {
            economy = targetEconomy; // 공동 경제 상태 저장
            soldItemsRoot = targetSoldItemsRoot; // 판매 완료 숨김 루트 저장
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 선택 아이템을 판매할 수 있는지 확인
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 상호작용 플레이어의 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 현재 선택 빠른 슬롯 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 회수품 가격 상태 조회
            OfficeStoredItemState officeState = selectedItem == null ? null : selectedItem.GetComponent<OfficeStoredItemState>(); // 사무소 단상 보관 상태 조회
            return recoverable != null && !recoverable.IsSold && (officeState == null || !officeState.IsOfficeStored); // 미판매 회수품이며 단상 보호 상태가 아닐 때 판매 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 회수품 판매
        {
            ResolveReferences(); // 판매 시점 최신 경제 참조 확보
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 플레이어 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 판매 대상 현재 선택 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 가격 데이터 조회

            if (inventory == null || selectedItem == null || recoverable == null || recoverable.IsSold || economy == null || soldItemsRoot == null) // 판매 필수 조건 확인
            {
                return; // 판매 불가 상태에서는 중단
            }

            int salePrice = economy.CalculateSalePrice(recoverable); // 현재 배율을 적용한 최종 판매 금액 계산

            if (salePrice <= 0) // 유효 판매 금액 여부 확인
            {
                return; // 0 이하 판매는 중단
            }

            if (!inventory.TryStoreSelectedItem(soldItemsRoot, out WorldItem soldItem) || soldItem == null) // 기존 빠른 슬롯에서 판매 완료 루트로 아이템 이동
            {
                return; // 플레이어 인벤토리 이동 실패 시 판매 취소
            }

            OfficeStoredItemState officeState = soldItem.GetComponent<OfficeStoredItemState>(); // 혹시 남아 있는 사무소 보호 상태 조회

            if (officeState != null) // 보호 상태 컴포넌트 존재 여부 확인
            {
                officeState.SetStored(null, false); // 판매 완료품은 사무소 단상 보호 상태 해제
            }

            recoverable.MarkSold(); // 동일 아이템 중복 판매를 막기 위해 판매 완료 상태 기록
            economy.AddFunds(salePrice); // 판매 금액을 사무소 공동 자금에 한 번만 반영
            soldItem.gameObject.SetActive(false); // 판매 완료 회수품을 현재 월드에서 제거
            Debug.Log($"[Project I] {soldItem.DisplayName} 판매 완료 / +{salePrice} / 공동 자금 {economy.SharedFunds}", this); // 개발용 판매 결과 로그 출력
        }

        private string BuildPrompt() // 현재 선택 회수품 기준 판매 안내 문구 생성
        {
            ResolveReferences(); // 안내 생성 시 공동 경제 상태 참조 확보
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 현재 싱글 플레이어 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 현재 선택 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 가격 데이터 조회

            if (selectedItem == null || recoverable == null || economy == null) // 판매 가능한 회수품 선택 여부 확인
            {
                return "회수품 판매"; // 기본 판매대 안내 반환
            }

            int price = economy.CalculateSalePrice(recoverable); // 현재 선택 회수품의 판매 예상 금액 계산
            return $"{selectedItem.DisplayName} 판매 / {price}"; // 아이템 이름과 판매 금액 안내 반환
        }

        private void ResolveReferences() // 판매대 경제 상태와 숨김 루트 자동 확보
        {
            if (economy == null) // 공동 경제 상태 누락 확인
            {
                economy = Object.FindFirstObjectByType<CampaignEconomy>(); // 현재 씬의 단일 공동 경제 상태 조회
            }

            if (soldItemsRoot != null) // 이미 판매 완료 루트가 연결됐는지 확인
            {
                return; // 추가 생성 불필요
            }

            Transform existing = transform.Find("SoldItems"); // 기존 판매 완료 아이템 루트 검색

            if (existing != null) // 기존 루트 존재 여부 확인
            {
                soldItemsRoot = existing; // 기존 루트 재사용
                return; // 새 루트 생성 불필요
            }

            GameObject root = new GameObject("SoldItems"); // 런타임 안전용 판매 완료 아이템 숨김 루트 생성
            soldItemsRoot = root.transform; // 새 Transform 참조 저장
            soldItemsRoot.SetParent(transform, false); // 판매대 자식으로 연결
        }
    }
}
