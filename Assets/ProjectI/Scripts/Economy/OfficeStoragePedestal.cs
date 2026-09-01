using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using ProjectI.Items; // 기존 빠른 슬롯과 WorldItem 기능 참조
using UnityEngine; // 유니티 Transform·Renderer 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 플레이어 시선 F 상호작용용 Collider 필수 지정
    public sealed class OfficeStoragePedestal : MonoBehaviour, IInteractable // 가격 제한을 가진 1개 회수품 영구 보관 단상
    {
        [SerializeField] private Transform displayPoint; // 실제 회수품을 단상 위에 보여줄 위치
        [SerializeField] private int maxStorageValue = 1000; // 이 가격 이상 회수품은 보관할 수 없는 상한
        [SerializeField] private WorldItem storedItem; // 현재 단상에 보관 중인 실제 WorldItem

        public string Prompt => BuildPrompt(); // 현재 상태에 맞는 F 상호작용 문구
        public InteractionType InteractionType => InteractionType.Press; // 단상은 F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요
        public int MaxStorageValue => maxStorageValue; // Validator와 UI용 가격 상한 공개
        public WorldItem StoredItem => storedItem; // 현재 단상 보관 아이템 공개
        public bool IsOccupied => storedItem != null; // 단상 사용 중 여부 공개

        private void Awake() // 단상 런타임 참조 초기화
        {
            EnsureDisplayPoint(); // 프리팹 참조 누락 시 표시 위치 자동 확보
        }

        public void Configure(Transform targetDisplayPoint, int targetMaxStorageValue) // 공통 프리팹 생성용 값 지정
        {
            displayPoint = targetDisplayPoint; // 단상 위 표시 위치 저장
            maxStorageValue = Mathf.Max(1, targetMaxStorageValue); // 최소 1 이상의 가격 상한 보장
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 플레이어가 보관 또는 회수 동작을 수행할 수 있는지 확인
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 상호작용 플레이어의 기존 인벤토리 조회

            if (inventory == null) // 인벤토리 존재 여부 확인
            {
                return false; // 인벤토리가 없으면 단상 사용 불가
            }

            if (storedItem != null) // 이미 단상에 회수품이 있는지 확인
            {
                return !inventory.IsFull; // 빈 슬롯이 있을 때만 단상 보관품 회수 허용
            }

            WorldItem selectedItem = inventory.SelectedItem; // 현재 빠른 슬롯 선택 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 선택 아이템의 회수품 가격 상태 조회
            return recoverable != null && !recoverable.IsSold; // 가격 데이터가 있는 미판매 회수품이면 가격 초과 여부와 관계없이 안내·상호작용 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 빈 단상 보관 또는 사용 중 단상 회수
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 플레이어 인벤토리 조회

            if (inventory == null) // 인벤토리 누락 확인
            {
                return; // 단상 상호작용 중단
            }

            if (storedItem != null) // 현재 단상 사용 상태 확인
            {
                RetrieveStoredItem(inventory); // 보관 중 회수품을 플레이어 빠른 슬롯으로 회수
                return; // 회수 처리 후 종료
            }

            StoreSelectedItem(inventory); // 현재 선택 회수품을 가격 검사 후 단상에 보관
        }

        private void StoreSelectedItem(PlayerInventory inventory) // 현재 선택 회수품을 실제 단상 위에 영구 보관
        {
            EnsureDisplayPoint(); // 단상 표시 위치 참조 확보
            WorldItem selectedItem = inventory.SelectedItem; // 현재 선택 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 현재 선택 아이템 가격 데이터 조회

            if (selectedItem == null || recoverable == null || recoverable.IsSold || displayPoint == null) // 보관 필수 조건 확인
            {
                return; // 회수품이 아니거나 표시 위치가 없으면 보관 중단
            }

            if (recoverable.Value >= maxStorageValue) // 특정 가격 이상 회수품 보관 금지 규칙 확인
            {
                Debug.LogWarning($"[Project I] {selectedItem.DisplayName} 보관 거부 / 가치 {recoverable.Value} / {maxStorageValue} 미만만 보관 가능", this); // 개발용 가격 제한 결과 출력
                return; // 가격 상한 이상이면 플레이어 슬롯을 유지한 채 보관 거부
            }

            if (!inventory.TryStoreSelectedItem(displayPoint, out WorldItem movedItem) || movedItem == null) // 기존 빠른 슬롯에서 단상 표시 위치로 아이템 이동 시도
            {
                return; // 외부 보관 이동 실패 시 중단
            }

            storedItem = movedItem; // 단상이 소유한 실제 WorldItem 참조 저장
            OfficeStoredItemState state = storedItem.GetComponent<OfficeStoredItemState>(); // 기존 사무소 보관 상태 조회

            if (state == null) // 아직 사무소 보관 상태 컴포넌트가 없는지 확인
            {
                state = storedItem.gameObject.AddComponent<OfficeStoredItemState>(); // 기존 WorldItem에 영구 보관 상태 최소 추가
            }

            state.SetStored(this, true); // 전멸·원정 손실에서 제외되는 사무소 보관 상태 활성화
            storedItem.transform.SetParent(displayPoint, false); // 실제 회수품을 단상 표시 위치에 고정
            storedItem.transform.localPosition = Vector3.zero; // 표시 위치 중심에 회수품 배치
            storedItem.transform.localRotation = Quaternion.identity; // 단상 기준 기본 회전 적용
            SetItemRenderers(storedItem, true); // WorldItem.Store가 숨긴 실제 회수품 외형을 단상 위에 다시 표시
        }

        private void RetrieveStoredItem(PlayerInventory inventory) // 단상 보관품을 기존 빠른 슬롯으로 다시 회수
        {
            if (storedItem == null || inventory == null || inventory.IsFull) // 회수 대상과 빈 슬롯 조건 확인
            {
                return; // 회수 불가 상태에서는 중단
            }

            WorldItem itemToReturn = storedItem; // 상태 변경 전 현재 보관 아이템 참조 저장
            OfficeStoredItemState state = itemToReturn.GetComponent<OfficeStoredItemState>(); // 사무소 보관 보호 상태 조회

            if (state != null) // 보호 상태 존재 여부 확인
            {
                state.SetStored(null, false); // 플레이어가 가져가기 전에 영구 보관 보호 상태 해제
            }

            if (inventory.TryReceiveStoredItem(itemToReturn)) // 기존 빠른 슬롯으로 회수 시도
            {
                storedItem = null; // 회수 성공 시 단상 비움
                return; // 정상 회수 완료
            }

            if (state != null) // 회수 실패 시 원래 보호 상태 복구 가능 여부 확인
            {
                state.SetStored(this, true); // 단상 보관 보호 상태 원복
            }
        }

        private string BuildPrompt() // 현재 단상과 선택 아이템 상태에 맞는 안내 문구 생성
        {
            if (storedItem != null) // 단상에 아이템이 있는지 확인
            {
                return $"{storedItem.DisplayName} 회수"; // 보관품 이름을 포함한 회수 안내 반환
            }

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 현재 싱글 플레이어 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 현재 선택 아이템 조회
            RecoverableValue recoverable = selectedItem == null ? null : selectedItem.GetComponent<RecoverableValue>(); // 선택 회수품 가격 조회

            if (selectedItem == null || recoverable == null) // 회수품 선택 여부 확인
            {
                return $"회수품 보관 / 가치 {maxStorageValue} 미만"; // 기본 단상 규칙 안내 반환
            }

            if (recoverable.Value >= maxStorageValue) // 가격 상한 이상 여부 확인
            {
                return $"보관 불가: {selectedItem.DisplayName} {recoverable.Value} / {maxStorageValue} 미만"; // 가격 초과 상태를 즉시 안내
            }

            return $"{selectedItem.DisplayName} 보관 / 가치 {recoverable.Value}"; // 정상 보관 가능 상태 안내
        }

        private void EnsureDisplayPoint() // 단상 위 실제 아이템 표시 위치 자동 확보
        {
            if (displayPoint != null) // 이미 표시 위치가 연결됐는지 확인
            {
                return; // 추가 검색 불필요
            }

            displayPoint = transform.Find("DisplayPoint"); // 공통 프리팹 자식 표시 위치 조회
        }

        private static void SetItemRenderers(WorldItem item, bool enabled) // Store 상태의 실제 아이템 외형 표시 여부 제어
        {
            if (item == null) // 아이템 유효성 확인
            {
                return; // 렌더러 처리 중단
            }

            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true); // 실제 회수품 전체 Renderer 조회

            foreach (Renderer renderer in renderers) // 회수품 Renderer 전체 순회
            {
                if (renderer != null) // 유효 Renderer 확인
                {
                    renderer.enabled = enabled; // 단상 보관 중 실제 아이템 외형 표시 상태 적용
                }
            }
        }

        private void OnValidate() // 인스펙터 단상 값 검증
        {
            maxStorageValue = Mathf.Max(1, maxStorageValue); // 가격 상한 최소값 보정
        }
    }
}
