using System.Collections.Generic; // 소지품 목록 자료형 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // 새 입력 시스템 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// 일반 아이템과 보물을 ICarryable 규격으로 함께 관리.
    /// 기본 슬롯, 가방 보너스 슬롯, 두손 운반, 무게 페널티, 핫바 선택을 처리.
    /// 사망 또는 유기 시 모든 소지품을 플레이어 주변 월드에 드랍.
    /// </summary>
    public class InventorySystem : MonoBehaviour // 플레이어 인벤토리 관리 컴포넌트
    {
        [Header("인벤토리 기본 설정")] // 기본 인벤토리 설정 구분
        [SerializeField] int baseSlots = 5; // 기본 인벤토리 슬롯 수
        [SerializeField] float weightLimit = 30f; // 무게 페널티 기준 무게

        [Header("전체 아이템 드랍")] // 전체 드랍 설정 구분
        [SerializeField] float dropRadius = 0.8f; // 플레이어 주변 드랍 반경
        [SerializeField] float dropImpulse = 2.5f; // 드랍 아이템의 튀어나가는 힘

        readonly List<ICarryable> items = new List<ICarryable>(); // 일반 슬롯 소지품 목록
        ICarryable twoHand; // 현재 두손으로 운반하는 소지품
        int selected = -1; // 현재 선택한 소지품 인덱스
        PlayerHand hand; // 소지품 표시용 플레이어 손
        Transform storage; // 숨긴 소지품 보관 위치

        public int BonusSlots // 가방으로 증가한 슬롯 수
        {
            get // 보너스 슬롯 합계 계산
            {
                int bonusSlots = 0; // 보너스 슬롯 합계 초기화

                foreach (ICarryable item in items) // 일반 슬롯 소지품 순회
                {
                    bonusSlots += item.BonusSlots; // 소지품의 보너스 슬롯 합산
                }

                return bonusSlots; // 최종 보너스 슬롯 반환
            }
        }

        public int TotalSlots => baseSlots + BonusSlots; // 사용할 수 있는 전체 슬롯 수

        public int UsedSlots // 현재 사용 중인 슬롯 수
        {
            get // 사용 중인 슬롯 합계 계산
            {
                int usedSlots = 0; // 사용 슬롯 합계 초기화

                foreach (ICarryable item in items) // 일반 슬롯 소지품 순회
                {
                    usedSlots += item.Slots; // 소지품의 사용 슬롯 합산
                }

                return usedSlots; // 최종 사용 슬롯 반환
            }
        }

        public float CurrentWeight // 현재 전체 소지품 무게
        {
            get // 현재 소지품 무게 합계 계산
            {
                float currentWeight = 0f; // 현재 무게 합계 초기화

                foreach (ICarryable item in items) // 일반 슬롯 소지품 순회
                {
                    currentWeight += item.Weight; // 소지품 무게 합산
                }

                if (twoHand != null) // 두손 소지품 존재 여부 확인
                {
                    currentWeight += twoHand.Weight; // 두손 소지품 무게 합산
                }

                return currentWeight; // 최종 현재 무게 반환
            }
        }

        public float WeightRatio => weightLimit > 0f ? CurrentWeight / weightLimit : 0f; // 무게 제한 대비 현재 무게 비율
        public bool CarryingTwoHand => twoHand != null; // 두손 운반 여부

        void Awake() // 플레이어 손과 내부 보관 위치 초기화
        {
            hand = GetComponent<PlayerHand>(); // 플레이어 손 컴포넌트 가져오기
            GameObject storageObject = new GameObject("Inventory_Storage"); // 숨긴 소지품 보관 오브젝트 생성

            storage = storageObject.transform; // 보관 오브젝트 Transform 저장
            storage.SetParent(transform); // 보관 위치를 플레이어 자식으로 설정
        }

        void Update() // 마우스 휠로 선택 소지품 변경
        {
            if (twoHand != null) // 두손 운반 여부 확인
            {
                return; // 두손 운반 중 슬롯 변경 차단
            }

            Mouse mouse = Mouse.current; // 현재 마우스 입력 가져오기

            if (mouse == null || items.Count == 0) // 마우스와 소지품 존재 여부 확인
            {
                return; // 슬롯 변경 처리 중단
            }

            float scroll = mouse.scroll.ReadValue().y; // 마우스 휠 입력값 가져오기

            if (scroll > 0.01f) // 위쪽 휠 입력 확인
            {
                Select((selected + 1) % items.Count); // 다음 소지품 선택
            }
            else if (scroll < -0.01f) // 아래쪽 휠 입력 확인
            {
                Select((selected - 1 + items.Count) % items.Count); // 이전 소지품 선택
            }
        }

        public bool TryAdd(ICarryable carryable) // 소지품을 인벤토리에 추가
        {
            if (carryable == null) // 추가할 소지품 존재 여부 확인
            {
                return false; // 추가 실패 반환
            }

            if (twoHand != null) // 두손 운반 여부 확인
            {
                Debug.Log("[Inventory] 두손 운반 중 — 먼저 [Q]로 내려놓으세요"); // 획득 불가 이유 출력
                return false; // 추가 실패 반환
            }

            if (carryable.TwoHanded) // 두손 소지품 여부 확인
            {
                if (selected >= 0 && selected < items.Count) // 기존 선택 소지품 확인
                {
                    items[selected].HideInHand(); // 기존 선택 소지품 숨기기
                }

                twoHand = carryable; // 두손 소지품 저장
                carryable.EnterInventory(storage); // 두손 소지품을 인벤토리 상태로 전환

                if (hand != null) // 플레이어 손 존재 여부 확인
                {
                    carryable.ShowInHand(hand.Anchor); // 두손 소지품을 손에 표시
                }

                return true; // 추가 성공 반환
            }

            if (UsedSlots + carryable.Slots > TotalSlots) // 남은 슬롯 충분 여부 확인
            {
                Debug.Log($"[Inventory] 슬롯 부족 ({UsedSlots}/{TotalSlots}, 필요 {carryable.Slots})"); // 슬롯 부족 결과 출력
                return false; // 추가 실패 반환
            }

            carryable.EnterInventory(storage); // 소지품을 인벤토리 상태로 전환
            items.Add(carryable); // 일반 슬롯 소지품 목록에 추가

            if (twoHand == null) // 일반 슬롯 선택 가능 여부 확인
            {
                Select(items.Count - 1); // 새로 획득한 소지품 선택
            }

            return true; // 추가 성공 반환
        }

        public void DropSelected() // 현재 선택 소지품을 월드에 버리기
        {
            if (twoHand != null) // 두손 소지품 존재 여부 확인
            {
                ICarryable twoHandItem = twoHand; // 버릴 두손 소지품 임시 저장

                twoHand = null; // 두손 운반 상태 해제
                Toss(twoHandItem); // 두손 소지품을 전방으로 버리기

                if (items.Count > 0) // 일반 슬롯 소지품 존재 여부 확인
                {
                    Select(Mathf.Clamp(selected, 0, items.Count - 1)); // 기존 슬롯 범위에서 소지품 다시 선택
                }

                return; // 일반 슬롯 버리기 처리 방지
            }

            if (selected < 0 || selected >= items.Count) // 선택 인덱스 유효 여부 확인
            {
                return; // 버리기 처리 중단
            }

            ICarryable selectedItem = items[selected]; // 현재 선택 소지품 가져오기

            items.RemoveAt(selected); // 선택 소지품을 목록에서 제거
            Toss(selectedItem); // 선택 소지품을 전방으로 버리기

            if (items.Count == 0) // 남은 소지품 존재 여부 확인
            {
                selected = -1; // 선택 상태 초기화
            }
            else // 남은 소지품 선택 처리
            {
                Select(Mathf.Clamp(selected, 0, items.Count - 1)); // 유효 범위의 소지품 선택
            }
        }

        public ICarryable TakeSelected() // 현재 선택 소지품을 월드 드랍 없이 꺼내기
        {
            if (twoHand != null) // 두손 소지품 존재 여부 확인
            {
                ICarryable twoHandItem = twoHand; // 꺼낼 두손 소지품 임시 저장

                twoHand = null; // 두손 운반 상태 해제
                twoHandItem.HideInHand(); // 두손 소지품 손 표시 해제

                if (items.Count > 0) // 일반 슬롯 소지품 존재 여부 확인
                {
                    Select(Mathf.Clamp(selected, 0, items.Count - 1)); // 기존 슬롯 범위에서 소지품 다시 선택
                }

                return twoHandItem; // 꺼낸 두손 소지품 반환
            }

            if (selected < 0 || selected >= items.Count) // 선택 인덱스 유효 여부 확인
            {
                return null; // 선택 소지품 없음 반환
            }

            ICarryable selectedItem = items[selected]; // 현재 선택 소지품 가져오기

            items.RemoveAt(selected); // 선택 소지품을 목록에서 제거
            selectedItem.HideInHand(); // 선택 소지품 손 표시 해제

            if (items.Count == 0) // 남은 소지품 존재 여부 확인
            {
                selected = -1; // 선택 상태 초기화
            }
            else // 남은 소지품 선택 처리
            {
                Select(Mathf.Clamp(selected, 0, items.Count - 1)); // 유효 범위의 소지품 선택
            }

            return selectedItem; // 꺼낸 소지품 반환
        }

        public bool ConsumeItemByName(string itemName) // 이름이 일치하는 일반 아이템 하나 소모
        {
            for (int i = 0; i < items.Count; i++) // 일반 슬롯 소지품 순회
            {
                if (items[i] is PickupItem pickupItem && pickupItem.DisplayName == itemName) // 이름이 일치하는 PickupItem 확인
                {
                    MonoBehaviour itemBehaviour = items[i] as MonoBehaviour; // 제거할 소지품의 Unity 컴포넌트 가져오기

                    items.RemoveAt(i); // 소모한 소지품을 목록에서 제거

                    if (itemBehaviour != null) // Unity 컴포넌트 존재 여부 확인
                    {
                        Destroy(itemBehaviour.gameObject); // 소모한 소지품 오브젝트 제거
                    }

                    if (items.Count == 0) // 남은 소지품 존재 여부 확인
                    {
                        selected = -1; // 선택 상태 초기화
                    }
                    else // 남은 소지품 선택 처리
                    {
                        Select(Mathf.Clamp(selected, 0, items.Count - 1)); // 유효 범위의 소지품 선택
                    }

                    return true; // 소모 성공 반환
                }
            }

            return false; // 일치하는 소지품 없음 반환
        }

        public List<ICarryable> TakeAll() // 모든 슬롯과 두손 소지품을 꺼내고 인벤토리 비우기
        {
            List<ICarryable> allItems = new List<ICarryable>(); // 꺼낸 전체 소지품 목록 생성

            if (twoHand != null) // 두손 소지품 존재 여부 확인
            {
                twoHand.HideInHand(); // 두손 소지품 손 표시 해제
                allItems.Add(twoHand); // 전체 소지품 목록에 두손 소지품 추가
                twoHand = null; // 두손 운반 상태 해제
            }

            foreach (ICarryable item in items) // 일반 슬롯 소지품 순회
            {
                item.HideInHand(); // 소지품 손 표시 해제
                allItems.Add(item); // 전체 소지품 목록에 추가
            }

            items.Clear(); // 일반 슬롯 소지품 목록 비우기
            selected = -1; // 선택 상태 초기화

            return allItems; // 꺼낸 전체 소지품 목록 반환
        }

        public int DropAll(Vector3 dropPosition) // 모든 소지품을 지정된 월드 위치에 드랍
        {
            List<ICarryable> droppedItems = TakeAll(); // 인벤토리와 두손 소지품 모두 꺼내기

            for (int i = 0; i < droppedItems.Count; i++) // 꺼낸 모든 소지품 순회
            {
                ICarryable carryable = droppedItems[i]; // 현재 드랍할 소지품 가져오기
                Vector2 randomOffset = Random.insideUnitCircle * dropRadius; // 원형 범위의 임의 위치 계산
                Vector3 itemPosition = dropPosition + new Vector3(randomOffset.x, 0.5f, randomOffset.y); // 플레이어 주변 드랍 위치 계산

                if (carryable is MonoBehaviour carryableBehaviour) // 소지품의 Unity 오브젝트 확인
                {
                    carryableBehaviour.transform.position = itemPosition; // 소지품을 드랍 위치로 이동
                }

                Vector3 impulseDirection = new Vector3(randomOffset.x, 1f, randomOffset.y).normalized; // 위쪽을 포함한 드랍 방향 계산

                carryable.ExitToWorld(impulseDirection * dropImpulse); // 소지품 물리와 렌더러 활성화
            }

            if (droppedItems.Count > 0) // 실제 드랍된 소지품 확인
            {
                Debug.Log($"[Inventory] 모든 소지품 드랍: {droppedItems.Count}개"); // 전체 드랍 결과 출력
            }

            return droppedItems.Count; // 드랍한 소지품 개수 반환
        }

        void Toss(ICarryable carryable) // 소지품을 플레이어 전방으로 버리기
        {
            Vector3 direction = hand != null && hand.Anchor != null ? hand.Anchor.forward : transform.forward; // 손 또는 플레이어 기준 버리기 방향 결정

            carryable.ExitToWorld(direction * 2.5f); // 소지품을 월드 상태로 전환하고 힘 적용
        }

        void Select(int index) // 지정한 일반 슬롯 소지품 선택
        {
            if (twoHand != null) // 두손 운반 여부 확인
            {
                return; // 일반 슬롯 선택 차단
            }

            if (selected >= 0 && selected < items.Count) // 기존 선택 인덱스 유효 여부 확인
            {
                items[selected].HideInHand(); // 기존 선택 소지품 숨기기
            }

            selected = index; // 새 선택 인덱스 저장

            if (selected >= 0 && selected < items.Count && hand != null) // 새 선택 소지품과 손 존재 여부 확인
            {
                items[selected].ShowInHand(hand.Anchor); // 새 선택 소지품을 손에 표시
            }
        }

        public float SpeedMultiplier // 현재 무게에 따른 이동속도 배율
        {
            get // 이동속도 배율 계산
            {
                float ratio = WeightRatio; // 현재 무게 비율 가져오기

                if (ratio <= 0.5f) // 무게 비율 50% 이하 확인
                {
                    return 1f; // 이동속도 100% 반환
                }

                if (ratio <= 0.8f) // 무게 비율 80% 이하 확인
                {
                    return 0.85f; // 이동속도 85% 반환
                }

                if (ratio <= 1f) // 무게 비율 100% 이하 확인
                {
                    return 0.7f; // 이동속도 70% 반환
                }

                return 0.4f; // 무게 초과 시 이동속도 40% 반환
            }
        }

        public float StaminaRegenMultiplier // 현재 무게에 따른 스태미너 회복 배율
        {
            get // 스태미너 회복 배율 계산
            {
                float ratio = WeightRatio; // 현재 무게 비율 가져오기

                if (ratio <= 0.5f) // 무게 비율 50% 이하 확인
                {
                    return 1f; // 회복속도 100% 반환
                }

                if (ratio <= 0.8f) // 무게 비율 80% 이하 확인
                {
                    return 0.75f; // 회복속도 75% 반환
                }

                if (ratio <= 1f) // 무게 비율 100% 이하 확인
                {
                    return 0.5f; // 회복속도 50% 반환
                }

                return 0.1f; // 무게 초과 시 회복속도 10% 반환
            }
        }

        void OnGUI() // 임시 인벤토리 정보와 핫바 표시
        {
            GUI.Label(new Rect(10f, 100f, 640f, 20f), $"인벤토리: {UsedSlots}/{TotalSlots}칸   무게: {CurrentWeight:F1}/{weightLimit:F0}kg ({WeightRatio * 100f:F0}%)"); // 슬롯과 무게 정보 표시

            if (twoHand != null) // 두손 운반 여부 확인
            {
                GUI.Label(new Rect(10f, 120f, 640f, 20f), $"두손 운반 중: {twoHand.DisplayName}   [Q] 내려놓기"); // 두손 운반 정보 표시
            }

            DrawHotbar(); // 하단 인벤토리 핫바 표시
        }

        void DrawHotbar() // 하단 중앙 슬롯 핫바 그리기
        {
            int totalSlots = TotalSlots; // 전체 슬롯 수 가져오기
            float boxWidth = 48f; // 슬롯 상자 너비
            float gap = 4f; // 슬롯 사이 간격
            float boxHeight = 48f; // 슬롯 상자 높이
            float totalWidth = totalSlots * boxWidth + (totalSlots - 1) * gap; // 전체 핫바 너비 계산
            float startX = (Screen.width - totalWidth) / 2f; // 핫바 시작 가로 위치 계산
            float y = Screen.height - boxHeight - 12f; // 핫바 세로 위치 계산
            int slot = 0; // 현재 그릴 슬롯 인덱스

            for (int itemIndex = 0; itemIndex < items.Count && slot < totalSlots; itemIndex++) // 일반 슬롯 소지품 순회
            {
                for (int itemSlot = 0; itemSlot < items[itemIndex].Slots && slot < totalSlots; itemSlot++, slot++) // 소지품이 사용하는 슬롯 수만큼 순회
                {
                    string label = itemSlot == 0 ? Abbreviate(items[itemIndex].DisplayName) : "·"; // 첫 슬롯에만 소지품 이름 설정

                    DrawSlot(startX + slot * (boxWidth + gap), y, boxWidth, boxHeight, itemIndex == selected, label); // 사용 중인 슬롯 표시
                }
            }

            for (; slot < totalSlots; slot++) // 남은 빈 슬롯 순회
            {
                DrawSlot(startX + slot * (boxWidth + gap), y, boxWidth, boxHeight, false, ""); // 빈 슬롯 표시
            }
        }

        void DrawSlot(float x, float y, float width, float height, bool selectedSlot, string label) // 개별 핫바 슬롯 그리기
        {
            Color previousColor = GUI.color; // 기존 GUI 색상 저장

            GUI.color = selectedSlot ? new Color(1f, 0.9f, 0.4f, 0.95f) : new Color(1f, 1f, 1f, 0.55f); // 선택 여부에 따른 슬롯 색상 설정
            GUI.Box(new Rect(x, y, width, height), label); // 슬롯 상자와 이름 표시
            GUI.color = previousColor; // 기존 GUI 색상 복구
        }

        static string Abbreviate(string text) // 핫바 표시 이름을 네 글자로 축약
        {
            if (string.IsNullOrEmpty(text)) // 표시 이름 존재 여부 확인
            {
                return ""; // 빈 문자열 반환
            }

            return text.Length <= 4 ? text : text.Substring(0, 4); // 네 글자 이하 유지 또는 앞 네 글자 반환
        }
    }
}