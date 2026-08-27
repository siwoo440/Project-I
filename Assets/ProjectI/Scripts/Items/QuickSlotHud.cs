using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // Canvas UI Image와 Text 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    public sealed class QuickSlotHud : MonoBehaviour // Canvas 기반 빠른 슬롯 6칸 HUD 갱신
    {
        [SerializeField] private PlayerInventory inventory; // 표시할 플레이어 인벤토리
        [SerializeField] private Image[] slotBackgrounds = new Image[PlayerInventory.Capacity]; // 슬롯 배경 Image 목록
        [SerializeField] private Text[] slotNumbers = new Text[PlayerInventory.Capacity]; // 왼쪽 위 슬롯 번호 Text 목록
        [SerializeField] private Text[] itemNames = new Text[PlayerInventory.Capacity]; // 슬롯 중앙 아이템 이름 Text 목록
        [SerializeField] private Text[] lockLabels = new Text[PlayerInventory.Capacity]; // 양손 잠금 표시 Text 목록
        [SerializeField] private Color normalColor = new Color(0.07f, 0.07f, 0.07f, 0.86f); // 일반 슬롯 배경색
        [SerializeField] private Color selectedColor = new Color(0.42f, 0.42f, 0.42f, 0.96f); // 선택 슬롯 배경색
        [SerializeField] private Color lockedColor = new Color(0.28f, 0.12f, 0.12f, 0.96f); // 양손 잠금 슬롯 배경색

        private void Awake() // HUD 초기화
        {
            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                inventory = GetComponent<PlayerInventory>(); // 같은 플레이어의 인벤토리 자동 조회
            }

            Refresh(); // 시작 화면 상태 즉시 갱신
        }

        private void LateUpdate() // 인벤토리 처리 뒤 Canvas 상태 갱신
        {
            Refresh(); // 현재 슬롯 데이터와 선택 상태 반영
        }

        public void Configure(PlayerInventory targetInventory) // 기존 Day 6 Setup 호환용 인벤토리 지정
        {
            inventory = targetInventory; // HUD 대상 인벤토리 저장
        }

        public void Configure(PlayerInventory targetInventory, Image[] backgrounds, Text[] numbers, Text[] names, Text[] locks) // Canvas UI 참조 전체 지정
        {
            inventory = targetInventory; // HUD 대상 인벤토리 저장
            slotBackgrounds = backgrounds; // 슬롯 배경 Image 목록 저장
            slotNumbers = numbers; // 왼쪽 위 숫자 Text 목록 저장
            itemNames = names; // 아이템 이름 Text 목록 저장
            lockLabels = locks; // LOCK Text 목록 저장
            Refresh(); // 연결 직후 Canvas 내용 갱신
        }

        private void Refresh() // 빠른 슬롯 6칸 Canvas 내용 갱신
        {
            if (inventory == null) // 인벤토리 참조 누락 확인
            {
                return; // HUD 갱신 중단
            }

            for (int index = 0; index < PlayerInventory.Capacity; index++) // 1번부터 6번 슬롯 순회
            {
                WorldItem item = inventory.GetItem(index); // 현재 슬롯 아이템 조회
                bool selected = index == inventory.SelectedIndex; // 현재 선택 슬롯 여부 확인
                bool locked = selected && inventory.IsSelectionLocked; // 선택 슬롯 양손 잠금 여부 확인

                if (slotNumbers != null && index < slotNumbers.Length && slotNumbers[index] != null) // 슬롯 번호 Text 참조 유효성 확인
                {
                    slotNumbers[index].text = (index + 1).ToString(); // 슬롯 왼쪽 위에 1~6 숫자 표시
                }

                if (itemNames != null && index < itemNames.Length && itemNames[index] != null) // 아이템 이름 Text 참조 유효성 확인
                {
                    itemNames[index].text = item == null ? string.Empty : item.DisplayName; // 빈 슬롯은 이름을 숨기고 아이템이 있으면 이름 표시
                }

                if (lockLabels != null && index < lockLabels.Length && lockLabels[index] != null) // LOCK Text 참조 유효성 확인
                {
                    lockLabels[index].text = locked ? "LOCK" : string.Empty; // 양손 운반 중 선택 슬롯에만 LOCK 표시
                }

                if (slotBackgrounds != null && index < slotBackgrounds.Length && slotBackgrounds[index] != null) // 슬롯 배경 Image 참조 유효성 확인
                {
                    slotBackgrounds[index].color = locked ? lockedColor : selected ? selectedColor : normalColor; // 잠금·선택·일반 상태에 맞는 배경색 적용
                }
            }
        }
    }
}
