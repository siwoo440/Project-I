using System.Collections; // 문 열림 연출 Coroutine 사용
using ProjectI.Interaction; // F 상호작용 인터페이스 참조
using ProjectI.Items; // 인벤토리·아이템 식별자 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 통로를 막는 충돌체
    public sealed class LockedRoomDoor : MonoBehaviour, IInteractable // 열쇠를 소모해 여는 잠긴 방 문
    {
        [SerializeField] private string keyItemId = "key.basic"; // 필요한 열쇠 ItemId
        [SerializeField] private float openDuration = 0.6f; // 열림 연출 시간
        private bool isOpen; // 열림 여부

        public bool IsOpen => isOpen; // 열림 여부 공개
        public string Prompt => BuildPrompt(); // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(string targetKeyItemId) // 생성기 구성
        {
            keyItemId = string.IsNullOrWhiteSpace(targetKeyItemId) ? "key.basic" : targetKeyItemId; // 열쇠 ID
        }

        public bool CanInteract(PlayerInteractor interactor) // 닫혀 있을 때만 사용
        {
            return !isOpen && interactor != null; // 조건
        }

        public void Interact(PlayerInteractor interactor) // 열쇠를 사용해 문 열기
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 인벤토리
            int slot = FindKeySlot(inventory); // 열쇠 슬롯

            if (slot < 0) // 열쇠 없음
            {
                Debug.Log("[Project I] 잠긴 문 — 열쇠가 필요합니다.", this); // 안내
                return; // 종료
            }

            if (inventory.SelectedIndex != slot && !inventory.SelectSlot(slot)) // 열쇠 슬롯 선택
            {
                Debug.Log("[Project I] 양손 물건을 내려놓은 뒤 열쇠를 사용하세요.", this); // 안내
                return; // 종료
            }

            if (!inventory.TryStoreSelectedItem(transform, out WorldItem key) || key == null) // 열쇠를 인벤토리에서 꺼냄
            {
                return; // 실패
            }

            ProjectI.Net.NetItemSync.NotifyDestroyed(key); // 협동: 모두의 화면에서 열쇠 제거
            Destroy(key.gameObject); // 열쇠 소모
            Open(); // 문 열기
        }

        public void Open() // 문 열림 (테스트에서도 사용)
        {
            if (isOpen) // 이미 열림
            {
                return; // 종료
            }

            isOpen = true; // 열림 기록
            StartCoroutine(OpenRoutine()); // 연출
        }

        public bool HasKey(PlayerInventory inventory) // 열쇠 소지 여부
        {
            return FindKeySlot(inventory) >= 0; // 슬롯 확인
        }

        private int FindKeySlot(PlayerInventory inventory) // 열쇠가 든 슬롯 번호
        {
            if (inventory == null) // 인벤토리 확인
            {
                return -1; // 없음
            }

            for (int index = 0; index < inventory.SlotCount; index++) // 슬롯 순회
            {
                WorldItem item = inventory.GetItem(index); // 슬롯 아이템
                WorldItemIdentity identity = item == null ? null : item.GetComponent<WorldItemIdentity>(); // 식별자

                if (identity != null && identity.Definition != null && identity.Definition.Matches(keyItemId)) // 열쇠 확인 (과거 ID 포함)
                {
                    return index; // 슬롯 반환
                }
            }

            return -1; // 없음
        }

        private IEnumerator OpenRoutine() // 문을 바닥 아래로 내리며 통로 개방
        {
            Collider blocker = GetComponent<Collider>(); // 통로 차단 충돌체

            if (blocker != null) // 확인
            {
                blocker.enabled = false; // 즉시 통과 가능
            }

            Vector3 start = transform.localPosition; // 시작 위치
            Vector3 end = start + Vector3.down * (transform.localScale.y + 0.1f); // 바닥 아래
            float elapsed = 0f; // 경과

            while (elapsed < openDuration) // 연출
            {
                elapsed += Time.deltaTime; // 시간 누적
                transform.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / openDuration)); // 내려감
                yield return null; // 다음 프레임
            }

            gameObject.SetActive(false); // 완전히 치움
        }

        private string BuildPrompt() // 상태별 안내
        {
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 플레이어 인벤토리
            return HasKey(inventory) ? "열쇠로 잠긴 문 열기" : "잠긴 문 — 열쇠 필요"; // 문구
        }
    }
}
