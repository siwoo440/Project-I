using System.Collections.Generic; // 생성한 슬롯 UI 목록 사용
using TMPro; // TextMeshPro UI 글자 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.UI; // 무게 게이지 Image 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class InventoryHUD : MonoBehaviour // Canvas 인벤토리와 핫바 관리 컴포넌트
    {
        [Header("게임 참조")] // 인벤토리 시스템 참조 구분
        [SerializeField] InventorySystem inventorySystem; // 현재 플레이어 인벤토리

        [Header("슬롯 생성")] // 동적 슬롯 생성 설정 구분
        [SerializeField] InventorySlotUI slotPrefab; // 생성할 슬롯 UI 프리팹
        [SerializeField] Transform slotContainer; // 생성한 슬롯을 배치할 부모 Transform

        [Header("인벤토리 정보")] // 인벤토리 정보 UI 참조 구분
        [SerializeField] TMP_Text inventorySummaryText; // 사용 슬롯과 전체 슬롯 정보 글자
        [SerializeField] TMP_Text weightText; // 현재 무게와 제한 무게 글자
        [SerializeField] Image weightFillImage; // 현재 무게 비율 게이지
        [SerializeField] TMP_Text selectedItemText; // 현재 선택 아이템 상세 정보
        [SerializeField] TMP_Text twoHandText; // 두손 운반 상태 안내 글자

        [Header("무게 색상")] // 무게 상태별 게이지 색상 구분
        [SerializeField] Color normalWeightColor = new Color(0.18f, 0.68f, 0.47f, 1f); // 가벼운 무게 게이지 색상
        [SerializeField] Color warningWeightColor = new Color(0.92f, 0.65f, 0.12f, 1f); // 무거운 상태 경고 색상
        [SerializeField] Color overweightColor = new Color(0.9f, 0.13f, 0.08f, 1f); // 무게 제한 초과 색상

        readonly List<InventorySlotUI> slotViews = new List<InventorySlotUI>(); // 현재 생성된 모든 슬롯 UI 목록

        float nextReferenceSearchTime; // 다음 InventorySystem 검색 시각

        void Awake() // 인벤토리 참조와 초기 슬롯 화면 설정
        {
            ResolveInventoryReference(); // 현재 플레이어 인벤토리 검색
            EnsureSlotCount(); // 초기 전체 슬롯 수만큼 UI 생성
            RefreshInventory(); // 초기 인벤토리 정보 표시
        }

        void Update() // 매 프레임 인벤토리 상태와 슬롯 화면 갱신
        {
            if (Time.unscaledTime >= nextReferenceSearchTime) // 인벤토리 참조 재검색 시각 확인
            {
                ResolveInventoryReference(); // Scene의 인벤토리 참조 다시 검색
                nextReferenceSearchTime = Time.unscaledTime + 1f; // 다음 검색을 1초 후로 예약
            }

            EnsureSlotCount(); // 가방 획득과 제거에 따른 전체 슬롯 수 반영
            RefreshInventory(); // 현재 인벤토리와 선택 상태를 화면에 반영
        }

        void ResolveInventoryReference() // 현재 Scene의 InventorySystem 검색
        {
            if (inventorySystem == null) // 인벤토리 참조 존재 여부 확인
            {
                inventorySystem = FindFirstObjectByType<InventorySystem>(); // 현재 플레이어 인벤토리 검색
            }
        }

        void EnsureSlotCount() // 현재 전체 슬롯 수에 맞게 슬롯 UI 개수 조절
        {
            if (inventorySystem == null || slotPrefab == null || slotContainer == null) // 슬롯 생성 필수 참조 확인
            {
                return; // 슬롯 생성 중단
            }

            int targetSlotCount = Mathf.Max(0, inventorySystem.TotalSlots); // 현재 인벤토리 전체 슬롯 수 가져오기

            while (slotViews.Count < targetSlotCount) // 부족한 슬롯 UI 존재 여부 확인
            {
                InventorySlotUI newSlot = Instantiate(slotPrefab, slotContainer); // 슬롯 프리팹을 컨테이너 자식으로 생성
                newSlot.gameObject.SetActive(true); // 생성한 슬롯 UI 활성화
                slotViews.Add(newSlot); // 생성한 슬롯을 관리 목록에 추가
            }

            while (slotViews.Count > targetSlotCount) // 불필요한 슬롯 UI 존재 여부 확인
            {
                int lastIndex = slotViews.Count - 1; // 마지막 슬롯 인덱스 계산
                InventorySlotUI removedSlot = slotViews[lastIndex]; // 제거할 마지막 슬롯 가져오기

                slotViews.RemoveAt(lastIndex); // 관리 목록에서 마지막 슬롯 제거

                if (removedSlot != null) // 제거할 슬롯 오브젝트 존재 여부 확인
                {
                    Destroy(removedSlot.gameObject); // 불필요한 슬롯 UI 제거
                }
            }
        }

        void RefreshInventory() // 현재 인벤토리 슬롯과 상세 정보 갱신
        {
            if (inventorySystem == null) // 인벤토리 참조 존재 여부 확인
            {
                ShowDisconnectedState(); // 인벤토리 연결 대기 상태 표시
                return; // 인벤토리 화면 갱신 중단
            }

            RefreshSlots(); // 실제 슬롯 점유와 선택 표시 갱신
            RefreshSummary(); // 슬롯과 무게 요약 정보 갱신
            RefreshSelectedItem(); // 현재 선택 아이템 상세 정보 갱신
            RefreshTwoHandState(); // 두손 운반 상태 갱신
        }

        void RefreshSlots() // 일반 소지품을 실제 사용 슬롯 수에 맞게 표시
        {
            IReadOnlyList<ICarryable> items = inventorySystem.Items; // 현재 일반 소지품 목록 가져오기
            int physicalSlotIndex = 0; // 현재 표시할 실제 슬롯 인덱스 초기화

            for (int itemIndex = 0; itemIndex < items.Count && physicalSlotIndex < slotViews.Count; itemIndex++) // 모든 일반 소지품 순회
            {
                ICarryable item = items[itemIndex]; // 현재 표시할 소지품 가져오기

                if (item == null) // 유효하지 않은 소지품 여부 확인
                {
                    continue; // 다음 소지품으로 이동
                }

                int occupiedSlotCount = Mathf.Max(1, item.Slots); // 현재 소지품이 사용하는 실제 슬롯 수 계산
                bool selectedItem = !inventorySystem.CarryingTwoHand && itemIndex == inventorySystem.SelectedIndex; // 현재 일반 선택 아이템 여부 확인

                for (int usedSlot = 0; usedSlot < occupiedSlotCount && physicalSlotIndex < slotViews.Count; usedSlot++) // 아이템이 사용하는 모든 슬롯 순회
                {
                    bool continuation = usedSlot > 0; // 첫 슬롯 이후의 연결 슬롯 여부 확인
                    string slotLabel = continuation ? "·" : Abbreviate(item.DisplayName); // 첫 슬롯에는 이름, 나머지에는 연결 기호 설정

                    slotViews[physicalSlotIndex].SetSlot(physicalSlotIndex + 1, slotLabel, true, selectedItem, continuation); // 현재 슬롯 UI에 점유와 선택 상태 적용
                    physicalSlotIndex++; // 다음 실제 슬롯으로 이동
                }
            }

            while (physicalSlotIndex < slotViews.Count) // 남은 빈 슬롯 순회
            {
                slotViews[physicalSlotIndex].SetSlot(physicalSlotIndex + 1, string.Empty, false, false, false); // 현재 슬롯을 빈 상태로 표시
                physicalSlotIndex++; // 다음 빈 슬롯으로 이동
            }
        }

        void RefreshSummary() // 현재 슬롯 사용량과 무게 상태 갱신
        {
            if (inventorySummaryText != null) // 인벤토리 요약 글자 존재 여부 확인
            {
                inventorySummaryText.text = $"인벤토리 {inventorySystem.UsedSlots}/{inventorySystem.TotalSlots}칸 · 가방 +{inventorySystem.BonusSlots}칸"; // 사용 슬롯과 가방 보너스 표시
            }

            float weightRatio = inventorySystem.WeightRatio; // 무게 제한 대비 현재 무게 비율 가져오기

            if (weightText != null) // 무게 글자 존재 여부 확인
            {
                weightText.text = $"무게 {inventorySystem.CurrentWeight:F1}/{inventorySystem.WeightLimit:F1}kg · {weightRatio * 100f:F0}%"; // 현재 무게와 비율 표시
            }

            if (weightFillImage != null) // 무게 게이지 이미지 존재 여부 확인
            {
                weightFillImage.fillAmount = Mathf.Clamp01(weightRatio); // 화면 범위 안으로 제한한 무게 비율 적용
                weightFillImage.color = GetWeightColor(weightRatio); // 현재 무게 상태에 맞는 색상 적용
            }
        }

        void RefreshSelectedItem() // 현재 선택 아이템 이름과 무게 및 슬롯 정보 갱신
        {
            if (selectedItemText == null) // 선택 아이템 글자 존재 여부 확인
            {
                return; // 선택 아이템 정보 갱신 중단
            }

            ICarryable selectedItem = inventorySystem.SelectedItem; // 현재 일반 또는 두손 선택 아이템 가져오기

            if (selectedItem == null) // 선택 아이템 존재 여부 확인
            {
                selectedItemText.text = "선택 아이템 없음"; // 비어 있는 선택 상태 표시
                return; // 선택 아이템 상세 계산 중단
            }

            string valueText = selectedItem is Treasure treasure ? $" · {treasure.Value}골드" : string.Empty; // 보물일 경우 현재 가치 문구 계산
            selectedItemText.text = $"선택: {selectedItem.DisplayName} · {selectedItem.Weight:F1}kg · {selectedItem.Slots}칸{valueText}"; // 현재 선택 아이템 상세 정보 표시
        }

        void RefreshTwoHandState() // 두손 운반 안내 표시 상태 갱신
        {
            if (twoHandText == null) // 두손 운반 글자 존재 여부 확인
            {
                return; // 두손 운반 UI 갱신 중단
            }

            ICarryable twoHandItem = inventorySystem.TwoHandItem; // 현재 두손 운반 소지품 가져오기
            bool carryingTwoHand = twoHandItem != null; // 두손 운반 여부 확인

            twoHandText.gameObject.SetActive(carryingTwoHand); // 두손 운반 중일 때만 안내 활성화

            if (carryingTwoHand) // 실제 두손 소지품 존재 여부 확인
            {
                twoHandText.text = $"두손 운반: {twoHandItem.DisplayName} · [Q] 내려놓기"; // 두손 운반 아이템과 버리기 안내 표시
            }
        }

        void ShowDisconnectedState() // InventorySystem을 찾기 전 대기 화면 표시
        {
            if (inventorySummaryText != null) // 인벤토리 요약 글자 존재 여부 확인
            {
                inventorySummaryText.text = "인벤토리 연결 중"; // 인벤토리 검색 상태 표시
            }

            if (weightText != null) // 무게 글자 존재 여부 확인
            {
                weightText.text = "무게 --/--kg"; // 무게 대기 상태 표시
            }

            if (selectedItemText != null) // 선택 아이템 글자 존재 여부 확인
            {
                selectedItemText.text = "선택 아이템 없음"; // 선택 아이템 대기 상태 표시
            }

            if (weightFillImage != null) // 무게 게이지 존재 여부 확인
            {
                weightFillImage.fillAmount = 0f; // 연결 전 무게 게이지 비우기
            }

            if (twoHandText != null) // 두손 운반 글자 존재 여부 확인
            {
                twoHandText.gameObject.SetActive(false); // 연결 전 두손 안내 숨기기
            }
        }

        Color GetWeightColor(float weightRatio) // 현재 무게 비율에 맞는 게이지 색상 계산
        {
            if (weightRatio > 1f) // 무게 제한 초과 여부 확인
            {
                return overweightColor; // 제한 초과용 붉은색 반환
            }

            if (weightRatio <= 0.5f) // 무게 비율 50% 이하 확인
            {
                return normalWeightColor; // 정상 무게 색상 반환
            }

            float warningRatio = (weightRatio - 0.5f) / 0.5f; // 50%부터 100% 사이 경고 비율 계산
            return Color.Lerp(normalWeightColor, warningWeightColor, warningRatio); // 무게 증가에 따른 경고 색상 반환
        }

        static string Abbreviate(string itemName) // 핫바에 표시할 아이템 이름을 여섯 글자로 축약
        {
            if (string.IsNullOrWhiteSpace(itemName)) // 아이템 이름 존재 여부 확인
            {
                return string.Empty; // 빈 이름 반환
            }

            return itemName.Length <= 6 ? itemName : itemName.Substring(0, 6); // 여섯 글자 이하 유지 또는 앞 여섯 글자 반환
        }
    }
}