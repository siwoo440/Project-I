using System.Collections.Generic; // 공동 보관 아이템 목록 기능 참조
using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using ProjectI.Items; // 기존 PlayerInventory·WorldItem 기능 참조
using UnityEngine; // 유니티 Transform과 컴포넌트 기능 참조

namespace ProjectI.Wagon // 마차 시스템 네임스페이스
{
    public sealed class WagonSharedStorage : MonoBehaviour, IInteractable // 기존 빠른 슬롯과 연결되는 마차 공동 보관함
    {
        [SerializeField] private Transform storageRoot; // 보관 중 아이템을 숨겨 둘 프리팹 내부 루트
        [SerializeField] private int capacity = 12; // Day21 기본 공동 보관 가능 개수
        private readonly List<WorldItem> storedItems = new List<WorldItem>(); // 현재 공동 보관함 아이템 목록

        public string Prompt => "공동 보관함 사용 (선택 아이템 보관 / 빈 슬롯 선택 시 꺼내기)"; // 상호작용 HUD 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요
        public int StoredCount => storedItems.Count; // 현재 보관 아이템 개수 공개
        public int Capacity => capacity; // 보관함 최대 개수 공개

        private void Awake() // 공동 보관함 초기화
        {
            EnsureStorageRoot(); // 숨김 보관 루트 확보
        }

        public void Configure(Transform targetStorageRoot, int targetCapacity) // 에디터 프리팹 생성용 값 지정
        {
            storageRoot = targetStorageRoot; // 숨김 보관 루트 저장
            capacity = Mathf.Max(1, targetCapacity); // 최소 한 칸 이상의 보관 용량 보장
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 플레이어가 보관 또는 회수 가능한지 확인
        {
            CleanupNullItems(); // 파괴된 아이템 목록 정리
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 상호작용 플레이어 인벤토리 조회

            if (inventory == null) // 기존 인벤토리 누락 확인
            {
                return false; // 상호작용 불가 반환
            }

            if (inventory.SelectedItem != null) // 현재 선택 슬롯에 아이템이 있는지 확인
            {
                return storedItems.Count < capacity; // 보관함에 공간이 있을 때만 보관 허용
            }

            return storedItems.Count > 0 && !inventory.IsFull; // 빈 슬롯을 선택한 상태에서 저장품이 있으면 회수 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 보관 또는 마지막 아이템 회수
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 플레이어 인벤토리 조회

            if (inventory == null) // 인벤토리 존재 여부 확인
            {
                return; // 상호작용 중단
            }

            CleanupNullItems(); // 현재 유효 저장품 목록 갱신

            if (inventory.SelectedItem != null) // 현재 손에 들 선택 아이템이 있는지 확인
            {
                if (storedItems.Count >= capacity) // 보관함 용량 초과 여부 확인
                {
                    return; // 추가 보관 중단
                }

                if (inventory.TryStoreSelectedItem(storageRoot, out WorldItem storedItem) && storedItem != null) // 기존 빠른 슬롯 아이템을 외부 보관으로 이동
                {
                    storedItems.Add(storedItem); // 공동 보관 목록에 추가
                }

                return; // 보관 처리 종료
            }

            if (storedItems.Count == 0 || inventory.IsFull) // 꺼낼 아이템 또는 빈 슬롯 존재 여부 확인
            {
                return; // 회수 처리 중단
            }

            int lastIndex = storedItems.Count - 1; // 가장 최근에 넣은 아이템 인덱스 계산
            WorldItem itemToReturn = storedItems[lastIndex]; // 회수 대상 아이템 조회

            if (inventory.TryReceiveStoredItem(itemToReturn)) // 기존 빠른 슬롯으로 아이템 회수 시도
            {
                storedItems.RemoveAt(lastIndex); // 회수 성공한 아이템을 공동 보관 목록에서 제거
            }
        }

        private void EnsureStorageRoot() // 프리팹 내부 숨김 보관 루트 확보
        {
            if (storageRoot != null) // 이미 연결된 루트 확인
            {
                return; // 추가 생성 불필요
            }

            Transform existing = transform.Find("StoredItems"); // 기존 저장 루트 검색

            if (existing != null) // 프리팹에 루트가 이미 있는지 확인
            {
                storageRoot = existing; // 기존 루트 사용
                return; // 추가 생성 불필요
            }

            GameObject storageObject = new GameObject("StoredItems"); // 런타임 안전용 보관 루트 생성
            storageRoot = storageObject.transform; // 새 Transform 참조 저장
            storageRoot.SetParent(transform, false); // 공동 보관함 자식으로 연결
        }

        private void CleanupNullItems() // 파괴되거나 제거된 저장품 목록 정리
        {
            for (int index = storedItems.Count - 1; index >= 0; index--) // 뒤에서부터 저장품 순회
            {
                if (storedItems[index] == null) // 제거된 Unity Object 확인
                {
                    storedItems.RemoveAt(index); // 유효하지 않은 목록 항목 삭제
                }
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            capacity = Mathf.Max(1, capacity); // 최소 보관 용량 보장
        }
    }
}
