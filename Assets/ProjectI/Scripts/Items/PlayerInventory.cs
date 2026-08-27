using ProjectI.Player; // 플레이어 입력 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInputReader))] // 입력 래퍼 필수 지정
    [RequireComponent(typeof(PlayerCarryController))] // 화면 운반 기능 필수 지정
    public sealed class PlayerInventory : MonoBehaviour // 빠른 슬롯 6칸 인벤토리와 선택 규칙 처리
    {
        public const int Capacity = 6; // 빠른 슬롯 고정 개수
        [SerializeField] private QuickSlot[] slots = new QuickSlot[Capacity]; // 빠른 슬롯 6칸 데이터
        [SerializeField] private int selectedIndex; // 현재 선택 슬롯 인덱스
        [SerializeField] private Transform storageRoot; // 선택되지 않은 아이템 숨김 보관 루트
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼
        private PlayerCarryController carryController; // 선택 아이템 화면 운반 기능

        public int SlotCount => Capacity; // HUD와 검증용 슬롯 수 공개
        public int SelectedIndex => selectedIndex; // 현재 선택 슬롯 공개
        public WorldItem SelectedItem => GetItem(selectedIndex); // 현재 선택 아이템 공개
        public bool IsSelectionLocked => SelectedItem != null && QuickSlotRules.IsSelectionLocked(SelectedItem.CarryType, carryController != null && carryController.HasItem); // 양손 운반 중 슬롯 잠금 여부 공개
        public bool IsFull => FindFirstEmptySlot() < 0; // 모든 빠른 슬롯 사용 여부 공개

        private void Awake() // 인벤토리 초기화
        {
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득
            carryController = GetComponent<PlayerCarryController>(); // 화면 운반 기능 참조 획득
            EnsureSlots(); // 6칸 슬롯 데이터 확보
            EnsureStorageRoot(); // 숨김 보관 루트 확보
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Capacity - 1); // 선택 인덱스 범위 보정
        }

        private void Start() // 씬 월드 아이템 초기 충돌 규칙 적용
        {
            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 월드 아이템 전체 조회

            foreach (WorldItem worldItem in worldItems) // 월드 아이템 순회
            {
                if (worldItem != null) // 유효 아이템 확인
                {
                    worldItem.IgnoreCollisionsWith(transform); // 모든 아이템과 플레이어 사이 충돌 무시
                }
            }

            RefreshSelectedItem(); // 시작 선택 슬롯 화면 상태 갱신
        }

        private void Update() // 프레임별 슬롯 선택·사용·내려놓기 처리
        {
            HandleSlotSelectionInput(); // 숫자키와 마우스 휠 슬롯 전환 처리

            if (inputReader != null && inputReader.DropPressed) // G 내려놓기 입력 확인
            {
                DropSelectedItem(); // 현재 선택 아이템 월드에 내려놓기
            }

            if (inputReader != null && inputReader.UsePressed) // 좌클릭 사용 입력 확인
            {
                TryUseSelectedItem(); // 현재 선택 아이템 사용 시도
            }
        }

        public void Configure(PlayerInputReader reader, PlayerCarryController carry, Transform storage) // 에디터 자동 설정용 참조 지정
        {
            inputReader = reader; // 입력 래퍼 저장
            carryController = carry; // 화면 운반 기능 저장
            storageRoot = storage; // 숨김 보관 루트 저장
            EnsureSlots(); // 슬롯 데이터 확보
        }

        public WorldItem GetItem(int index) // 지정 슬롯 아이템 조회
        {
            EnsureSlots(); // 슬롯 데이터 유효성 확보

            if (index < 0 || index >= Capacity) // 인덱스 범위 확인
            {
                return null; // 범위 밖은 아이템 없음 반환
            }

            return slots[index].Item; // 지정 슬롯 아이템 반환
        }

        public bool CanPickup(WorldItem item) // 월드 아이템을 새 빠른 슬롯에 넣을 수 있는지 확인
        {
            if (item == null || item.IsHeld || item.IsStored) // 아이템 유효성과 기존 소유 상태 확인
            {
                return false; // 획득 불가 반환
            }

            if (IsSelectionLocked) // 양손 아이템 현재 운반 여부 확인
            {
                return false; // 양손 운반 중 추가 획득 차단
            }

            return FindFirstEmptySlot() >= 0; // 빈 슬롯이 있으면 획득 허용
        }

        public bool TryPickup(WorldItem item) // F로 월드 아이템을 첫 빈 빠른 슬롯에 저장
        {
            if (!CanPickup(item)) // 현재 획득 가능 여부 확인
            {
                return false; // 획득 실패 반환
            }

            int emptyIndex = FindFirstEmptySlot(); // 첫 빈 슬롯 조회

            if (emptyIndex < 0) // 빈 슬롯 누락 확인
            {
                return false; // 획득 실패 반환
            }

            item.IgnoreCollisionsWith(transform); // 플레이어와 아이템 충돌 무시 적용
            item.Store(storageRoot); // 먼저 인벤토리 숨김 보관 상태로 전환
            slots[emptyIndex].SetItem(item); // 첫 빈 슬롯에 아이템 저장
            SelectSlot(emptyIndex); // 새로 획득한 슬롯을 즉시 선택하여 손에 표시
            return true; // 획득 성공 반환
        }

        public bool SelectSlot(int index) // 숫자키 또는 휠로 빠른 슬롯 선택
        {
            EnsureSlots(); // 슬롯 데이터 확보

            if (index < 0 || index >= Capacity) // 대상 인덱스 범위 확인
            {
                return false; // 잘못된 선택 실패 반환
            }

            if (index == selectedIndex) // 현재 선택 슬롯과 같은지 확인
            {
                RefreshSelectedItem(); // 같은 슬롯도 표시 상태 재동기화
                return true; // 선택 성공 반환
            }

            if (IsSelectionLocked) // 현재 양손 아이템 운반 잠금 확인
            {
                return false; // 내려놓기 전 슬롯 전환 차단
            }

            carryController?.HolsterHeldItem(storageRoot); // 기존 한손 선택 아이템을 슬롯 보관 상태로 숨김
            selectedIndex = index; // 새 선택 슬롯 인덱스 저장
            RefreshSelectedItem(); // 새 선택 아이템 화면 표시
            return true; // 슬롯 선택 성공 반환
        }

        public bool DropSelectedItem() // 현재 선택 슬롯 아이템을 플레이어 바로 앞에 내려놓기
        {
            WorldItem selectedItem = SelectedItem; // 현재 선택 아이템 조회

            if (selectedItem == null || carryController == null) // 아이템 또는 운반 기능 누락 확인
            {
                return false; // 내려놓기 실패 반환
            }

            if (carryController.HeldItem != selectedItem) // 선택 아이템이 현재 화면에 들려 있지 않은지 확인
            {
                RefreshSelectedItem(); // 선택 아이템 표시 상태 복구
            }

            WorldItem droppedItem = carryController.DropHeldItem(); // CarryPoint에서 분리하여 월드에 내려놓기

            if (droppedItem == null) // 실제 내려놓기 성공 여부 확인
            {
                return false; // 내려놓기 실패 반환
            }

            slots[selectedIndex].Clear(); // 현재 빠른 슬롯 비우기
            return true; // 내려놓기 성공 반환
        }

        public bool TryUseSelectedItem() // 좌클릭으로 현재 선택 아이템 사용 시도
        {
            WorldItem selectedItem = SelectedItem; // 현재 선택 아이템 조회

            if (selectedItem == null || carryController == null || carryController.HeldItem != selectedItem) // 선택 아이템이 실제로 들려 있는지 확인
            {
                return false; // 사용 실패 반환
            }

            MonoBehaviour[] behaviours = selectedItem.GetComponents<MonoBehaviour>(); // 아이템 루트의 기능 컴포넌트 목록 조회

            foreach (MonoBehaviour behaviour in behaviours) // 아이템 기능 컴포넌트 순회
            {
                if (!(behaviour is IUsableItem usableItem)) // 사용 인터페이스 구현 여부 확인
                {
                    continue; // 다음 기능 검사
                }

                if (!usableItem.CanUse(this)) // 현재 상태에서 사용 가능 여부 확인
                {
                    continue; // 사용할 수 없는 기능 건너뜀
                }

                usableItem.Use(this); // 선택 아이템 사용 실행
                return true; // 첫 유효 사용 기능 실행 후 성공 반환
            }

            return false; // 사용 기능이 없는 아이템 반환
        }

        private void HandleSlotSelectionInput() // 숫자키와 마우스 휠 슬롯 선택 처리
        {
            if (inputReader == null) // 입력 래퍼 누락 확인
            {
                return; // 슬롯 입력 처리 중단
            }

            int directIndex = inputReader.DirectSlotPressed; // 숫자키 1~6 선택값 읽기

            if (directIndex >= 0) // 직접 선택 입력 존재 확인
            {
                SelectSlot(directIndex); // 해당 슬롯 선택
                return; // 같은 프레임 휠 입력 중복 방지
            }

            float scrollDelta = inputReader.SlotScrollDelta; // 마우스 휠 입력 읽기

            if (Mathf.Abs(scrollDelta) < 0.01f) // 유효 휠 입력 여부 확인
            {
                return; // 슬롯 전환 불필요
            }

            int direction = scrollDelta > 0f ? -1 : 1; // 휠 위는 이전 슬롯, 아래는 다음 슬롯으로 계산
            int targetIndex = QuickSlotRules.WrapIndex(selectedIndex + direction, Capacity); // 슬롯 인덱스를 1~6 원형 구조로 보정
            SelectSlot(targetIndex); // 계산된 슬롯 선택 시도
        }

        private void RefreshSelectedItem() // 현재 선택 슬롯의 화면 표시 상태 동기화
        {
            EnsureStorageRoot(); // 숨김 보관 루트 확보
            WorldItem selectedItem = SelectedItem; // 현재 선택 아이템 조회

            if (carryController == null) // 화면 운반 기능 누락 확인
            {
                return; // 표시 갱신 중단
            }

            if (selectedItem == null) // 현재 슬롯이 비어 있는지 확인
            {
                carryController.HolsterHeldItem(storageRoot); // 혹시 남은 화면 아이템을 숨김 처리
                return; // 빈 손 상태 유지
            }

            if (carryController.HeldItem == selectedItem) // 이미 올바른 아이템이 들려 있는지 확인
            {
                return; // 추가 변경 불필요
            }

            carryController.HolsterHeldItem(storageRoot); // 다른 슬롯 아이템이 들려 있으면 숨김 보관
            carryController.EquipItem(selectedItem); // 현재 선택 슬롯 아이템 화면 표시
        }

        private int FindFirstEmptySlot() // 첫 번째 빈 빠른 슬롯 인덱스 조회
        {
            EnsureSlots(); // 슬롯 데이터 확보

            for (int index = 0; index < Capacity; index++) // 1번부터 6번 슬롯 순회
            {
                if (slots[index].IsEmpty) // 현재 슬롯이 비어 있는지 확인
                {
                    return index; // 첫 빈 슬롯 인덱스 반환
                }
            }

            return -1; // 빈 슬롯 없음 반환
        }

        private void EnsureSlots() // 빠른 슬롯 배열과 각 슬롯 객체 확보
        {
            if (slots == null || slots.Length != Capacity) // 정확한 6칸 배열인지 확인
            {
                slots = new QuickSlot[Capacity]; // 빠른 슬롯 배열을 정확히 6칸으로 재생성
            }

            for (int index = 0; index < Capacity; index++) // 모든 슬롯 순회
            {
                if (slots[index] == null) // 슬롯 객체 누락 확인
                {
                    slots[index] = new QuickSlot(); // 빈 슬롯 객체 생성
                }
            }
        }

        private void EnsureStorageRoot() // 선택되지 않은 아이템 숨김 보관 루트 확보
        {
            if (storageRoot != null) // 이미 보관 루트가 있는지 확인
            {
                return; // 추가 생성 불필요
            }

            Transform existing = transform.Find("InventoryStorage"); // 플레이어 자식의 기존 보관 루트 조회

            if (existing != null) // 기존 루트 존재 확인
            {
                storageRoot = existing; // 기존 루트 사용
                return; // 생성 불필요
            }

            GameObject storageObject = new GameObject("InventoryStorage"); // 런타임 안전용 보관 루트 생성
            storageRoot = storageObject.transform; // 새 보관 트랜스폼 참조
            storageRoot.SetParent(transform, false); // 플레이어 자식으로 연결
            storageRoot.localPosition = Vector3.zero; // 로컬 위치 초기화
            storageRoot.localRotation = Quaternion.identity; // 로컬 회전 초기화
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Capacity - 1); // 선택 슬롯 범위 보정
        }
    }
}
