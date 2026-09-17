using System.Collections; // 여닫이 연출 Coroutine 사용
using ProjectI.Audio; // 효과음
using ProjectI.Generation; // 출입구 규격 참조
using ProjectI.Interaction; // 상호작용 규약 참조
using ProjectI.Items; // 열쇠 확인용 인벤토리 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonDoor : MonoBehaviour, IInteractable // 출입구에 놓이는 여닫이문 (F로 열고 닫음)
    {
        [SerializeField] private Transform leaf; // 문짝
        [SerializeField] private float openAngle = 95f; // 열렸을 때 각도
        [SerializeField] private float swingDuration = 0.35f; // 여닫는 시간
        [SerializeField] private bool isLocked; // 잠김 여부
        [SerializeField] private string requiredKeyId = "key.basic"; // 필요한 열쇠 ID
        private bool isOpen; // 열림 여부
        private bool isMoving; // 연출 중 여부
        private float closedYaw; // 닫힌 각도
        private int swingSign = 1; // 열리는 방향

        public bool IsOpen => isOpen; // 열림 여부 공개
        public bool IsLocked => isLocked; // 잠김 여부 공개
        public Transform Leaf => leaf; // 문짝 공개

        public string Prompt => isLocked ? "잠긴 문 — 열쇠 필요" : isOpen ? "문 닫기" : "문 열기"; // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // 누르기
        public float HoldDuration => 0f; // 길게 누르기 없음

        private void Awake() // 기준 각도 기록
        {
            leaf = leaf != null ? leaf : transform.childCount > 0 ? transform.GetChild(0) : transform; // 참조 보정
            closedYaw = leaf.localEulerAngles.y; // 닫힌 각도
        }

        public void Configure(Transform doorLeaf, bool locked, string keyId) // 구성 (생성기에서 호출)
        {
            leaf = doorLeaf; // 문짝
            isLocked = locked; // 잠김
            requiredKeyId = string.IsNullOrEmpty(keyId) ? requiredKeyId : keyId; // 열쇠 ID
            closedYaw = leaf == null ? 0f : leaf.localEulerAngles.y; // 닫힌 각도
        }

        public bool CanInteract(PlayerInteractor interactor) // 조작 가능 여부
        {
            return !isMoving; // 연출 중이 아니면 가능
        }

        public void Interact(PlayerInteractor interactor) // 열기·닫기
        {
            if (isMoving) // 연출 중
            {
                return; // 무시
            }

            if (isLocked) // 잠긴 문
            {
                if (!TryUnlock(interactor)) // 열쇠 확인
                {
                    SoundPlayer.PlayAt(SoundId.UiDenied, transform.position, 0.6f); // 잠김 소리
                    return; // 그대로 잠김
                }

                isLocked = false; // 잠금 해제
            }

            swingSign = isOpen ? swingSign : ChooseSwingSign(interactor); // 열 때는 미는 방향으로
            SoundPlayer.PlayAt(isOpen ? SoundId.DoorClose : SoundId.DoorOpen, transform.position + Vector3.up, 0.8f); // 문 소리
            StartCoroutine(SwingRoutine(!isOpen)); // 연출 시작
        }

        public void SetOpenImmediate(bool open) // 즉시 상태 지정 (검증·초기화용)
        {
            isOpen = open; // 상태
            isMoving = false; // 연출 없음

            if (leaf != null) // 문짝 확인
            {
                Vector3 angles = leaf.localEulerAngles; // 각도
                angles.y = closedYaw + (open ? openAngle * swingSign : 0f); // 목표 각도
                leaf.localEulerAngles = angles; // 적용
            }
        }

        private bool TryUnlock(PlayerInteractor interactor) // 열쇠를 가지고 있는지 확인하고 소모
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 인벤토리
            int slot = FindKeySlot(inventory); // 열쇠 슬롯

            if (slot < 0) // 열쇠 없음
            {
                Debug.Log("[Project I] 잠긴 문 — 열쇠가 필요합니다.", this); // 안내
                return false; // 실패
            }

            if (inventory.SelectedIndex != slot && !inventory.SelectSlot(slot)) // 열쇠 슬롯 선택
            {
                Debug.Log("[Project I] 양손 물건을 내려놓은 뒤 열쇠를 사용하세요.", this); // 안내
                return false; // 실패
            }

            if (!inventory.TryStoreSelectedItem(transform, out WorldItem key) || key == null) // 열쇠를 인벤토리에서 꺼냄
            {
                return false; // 실패
            }

            Destroy(key.gameObject); // 열쇠 소모
            return true; // 성공
        }

        public bool HasKey(PlayerInventory inventory) // 열쇠 소지 여부 (검증용)
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

                if (identity != null && identity.Definition != null && identity.Definition.Matches(requiredKeyId)) // 열쇠 확인 (과거 ID 포함)
                {
                    return index; // 슬롯 반환
                }
            }

            return -1; // 없음
        }

        private int ChooseSwingSign(PlayerInteractor interactor) // 플레이어가 바라보는 방향으로 밀려 열리게 방향 결정
        {
            if (interactor == null) // 기준 없음
            {
                return 1; // 기본
            }

            Vector3 look = interactor.transform.forward; // 바라보는 방향
            look.y = 0f; // 수평 성분만

            if (look.sqrMagnitude < 0.0001f) // 방향을 못 구하면 위치로 대신
            {
                Vector3 toPlayer = interactor.transform.position - transform.position; // 플레이어 쪽
                return Vector3.Dot(toPlayer, transform.forward) > 0f ? 1 : -1; // 플레이어 반대쪽으로 밀림
            }

            return Vector3.Dot(look, transform.forward) > 0f ? -1 : 1; // 문짝이 바라보는 쪽으로 밀려 열림 (+각도는 문 안쪽, -각도는 바깥쪽)
        }

        private IEnumerator SwingRoutine(bool open) // 문짝 회전 연출
        {
            isMoving = true; // 연출 중
            float from = leaf.localEulerAngles.y; // 시작 각도
            float to = closedYaw + (open ? openAngle * swingSign : 0f); // 목표 각도
            float elapsed = 0f; // 경과

            while (elapsed < swingDuration) // 연출
            {
                elapsed += Time.deltaTime; // 시간 누적
                Vector3 angles = leaf.localEulerAngles; // 각도
                angles.y = Mathf.LerpAngle(from, to, Mathf.SmoothStep(0f, 1f, elapsed / swingDuration)); // 회전
                leaf.localEulerAngles = angles; // 적용
                yield return null; // 다음 프레임
            }

            Vector3 final = leaf.localEulerAngles; // 마무리 각도
            final.y = to; // 목표
            leaf.localEulerAngles = final; // 적용
            isOpen = open; // 상태
            isMoving = false; // 연출 종료
        }
    }
}
