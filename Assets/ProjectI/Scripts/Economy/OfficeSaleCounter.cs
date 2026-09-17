using System.Collections; // 판매 연출 코루틴 사용
using System.Collections.Generic; // 판매대 위 아이템 목록 사용
using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using ProjectI.Items; // 기존 빠른 슬롯과 WorldItem 기능 참조
using UnityEngine; // 유니티 Transform과 컴포넌트 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 판매대 F 상호작용용 Collider 필수 지정
    public sealed class OfficeSaleCounter : MonoBehaviour, IInteractable // 회수품을 올려두고, 벨로 고른 것만 판매하는 판매대 (32일차)
    {
        private const float SlotSpacing = 0.42f; // 올려둘 자리 간격
        private const float SlotEdgeMargin = 0.28f; // 판매대 가장자리 여백
        private const float PlaceDropHeight = 0.12f; // 올려둘 때 윗면에서 띄우는 높이
        private const float OccupiedRadius = 0.2f; // 자리 사용 판정 반경
        private const float ZoneHeight = 1.0f; // 판매대 위 인식 높이
        private const float SellVanishSeconds = 0.35f; // 판매 시 사라지는 연출 시간

        [SerializeField] private CampaignEconomy economy; // 판매 수익을 반영할 공동 경제 상태
        [SerializeField] private Transform soldItemsRoot; // 판매 완료 아이템을 숨겨둘 내부 루트
        private readonly List<WorldItem> itemBuffer = new List<WorldItem>(); // 판매대 위 아이템 조회 버퍼
        private Transform placeBuffer; // 올려두기 직전 잠시 거치는 루트 (판매대 크기 배율을 물려받지 않게 씬 최상위)

        public string Prompt => BuildPrompt(); // 현재 선택 회수품 올려두기 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요
        public CampaignEconomy Economy => economy; // 벨·패널에서 사용할 경제 상태 공개

        private void Awake() // 판매대 런타임 참조 초기화
        {
            ResolveReferences(); // 공동 경제 상태와 판매 완료 보관 루트 확보
        }

        public void Configure(CampaignEconomy targetEconomy, Transform targetSoldItemsRoot) // 에디터 자동 구성용 참조 지정
        {
            economy = targetEconomy; // 공동 경제 상태 저장
            soldItemsRoot = targetSoldItemsRoot; // 판매 완료 숨김 루트 저장
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 선택 회수품을 판매대에 올릴 수 있는지 확인
        {
            WorldItem selectedItem = SelectedItemOf(interactor); // 현재 선택 빠른 슬롯 아이템 조회
            return IsSellable(selectedItem) && TryFindFreeSlot(out _); // 미판매 회수품이며 빈 자리가 있을 때 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 회수품을 판매대 위에 올려둠 (판매는 벨에서)
        {
            ResolveReferences(); // 최신 참조 확보
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 플레이어 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 올려둘 대상

            if (inventory == null || !IsSellable(selectedItem) || !TryFindFreeSlot(out Vector3 slot)) // 필수 조건 확인
            {
                return; // 올려두기 불가
            }

            if (!inventory.TryStoreSelectedItem(EnsurePlaceBuffer(), out WorldItem placedItem) || placedItem == null) // 빠른 슬롯에서 꺼냄
            {
                return; // 꺼내기 실패
            }

            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f); // 판매대 방향에 맞춤
            placedItem.Release(slot + (Vector3.up * PlaceDropHeight), rotation, Vector3.zero); // 판매대 위 월드 물체로 내려놓음 (F로 다시 집을 수 있음)
            Debug.Log($"[Project I] {placedItem.DisplayName} 판매대에 올림 / 예상 {PriceOf(placedItem)} / 판매대 {CollectPlacedItems().Count}개", this); // 개발용 로그
        }

        public List<WorldItem> CollectPlacedItems() // 판매대 위에 놓인 미판매 회수품 목록
        {
            itemBuffer.Clear(); // 버퍼 초기화
            WorldItem[] items = Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None); // 활성 WorldItem 전체

            foreach (WorldItem item in items) // 아이템 순회
            {
                if (item != null && item.gameObject.scene == gameObject.scene && !item.IsHeld && !item.IsStored && IsSellable(item) && IsOnCounter(item.transform.position)) // 판매대 위 미판매 회수품
                {
                    itemBuffer.Add(item); // 등록
                }
            }

            itemBuffer.Sort((a, b) => transform.InverseTransformPoint(a.transform.position).x.CompareTo(transform.InverseTransformPoint(b.transform.position).x)); // 판매대 왼쪽부터 정렬
            return new List<WorldItem>(itemBuffer); // 사본 반환
        }

        public int PriceOf(WorldItem item) // 현재 판매 금액
        {
            ResolveReferences(); // 경제 상태 확보
            RecoverableValue recoverable = item == null ? null : item.GetComponent<RecoverableValue>(); // 가격 데이터
            return economy == null ? 0 : economy.CalculateSalePrice(recoverable); // 배율 적용 금액
        }

        public int Sell(IEnumerable<WorldItem> items) // 고른 아이템을 판매하고 사라지게 함 (판매 금액 합계 반환)
        {
            ResolveReferences(); // 최신 참조 확보
            int total = 0; // 합계

            if (economy == null || soldItemsRoot == null || items == null) // 필수 참조 확인
            {
                return 0; // 판매 불가
            }

            foreach (WorldItem item in new List<WorldItem>(items)) // 판매 대상 순회
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.IsHeld || item.IsStored || !IsSellable(item) || !IsOnCounter(item.transform.position)) // 판매대 위 미판매 회수품만
                {
                    continue; // 다음
                }

                RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 가격 데이터
                int price = economy.CalculateSalePrice(recoverable); // 판매 금액

                if (price <= 0) // 유효 금액 확인
                {
                    continue; // 다음
                }

                OfficeStoredItemState officeState = item.GetComponent<OfficeStoredItemState>(); // 남은 사무소 보호 상태

                if (officeState != null) // 보호 상태 확인
                {
                    officeState.SetStored(null, false); // 판매품은 보호 해제
                }

                recoverable.MarkSold(); // 중복 판매 방지 (즉시 기록)
                economy.AddFunds(price); // 공동 자금 반영 (즉시)
                total += price; // 합계
                Debug.Log($"[Project I] {item.DisplayName} 판매 완료 / +{price} / 공동 자금 {economy.SharedFunds}", this); // 개발용 판매 결과 로그
                StartCoroutine(VanishRoutine(item)); // 줄어들며 사라지는 연출
            }

            return total; // 합계 반환
        }

        public bool IsOnCounter(Vector3 worldPosition) // 위치가 판매대 윗면 위인지 확인
        {
            if (!TryGetTop(out Vector3 localMin, out Vector3 localMax)) // 판매대 윗면
            {
                return false; // 판정 불가
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition); // 판매대 로컬 좌표
            float worldY = worldPosition.y; // 월드 높이
            float topY = transform.TransformPoint(new Vector3(0f, localMax.y, 0f)).y; // 윗면 월드 높이
            return local.x >= localMin.x && local.x <= localMax.x && local.z >= localMin.z && local.z <= localMax.z && worldY >= topY - 0.05f && worldY <= topY + ZoneHeight; // 윗면 영역 안
        }

        private IEnumerator VanishRoutine(WorldItem item) // 판매된 아이템이 줄어들며 사라짐
        {
            Rigidbody body = item.Body; // 물리

            if (body != null) // 물리 정지
            {
                if (!body.isKinematic) // Dynamic 상태에서만 속도 초기화
                {
                    body.linearVelocity = Vector3.zero; // 속도 제거
                    body.angularVelocity = Vector3.zero; // 회전 제거
                }

                body.isKinematic = true; // 고정
                body.detectCollisions = false; // 충돌 제외
            }

            foreach (Collider itemCollider in item.GetComponentsInChildren<Collider>()) // 사라지는 동안 다시 집지 못하게
            {
                itemCollider.enabled = false; // 조사 판정 제외
            }

            Transform itemTransform = item.transform; // 대상
            Vector3 startScale = itemTransform.localScale; // 원래 크기
            float elapsed = 0f; // 경과

            while (elapsed < SellVanishSeconds && item != null) // 줄어드는 연출
            {
                elapsed += Time.deltaTime; // 경과
                float t = Mathf.Clamp01(elapsed / SellVanishSeconds); // 진행도
                itemTransform.localScale = startScale * (1f - (t * t)); // 점점 작게
                itemTransform.position += Vector3.up * (Time.deltaTime * 0.4f); // 살짝 떠오름
                yield return null; // 다음 프레임
            }

            if (item == null) // 이미 제거됨
            {
                yield break; // 종료
            }

            item.Store(soldItemsRoot); // 판매 완료 루트로 이동 (월드 표시·물리 해제)
            itemTransform.localScale = startScale; // 크기 원복 (비활성 보관)
            item.gameObject.SetActive(false); // 현재 월드에서 제거
        }

        private Transform EnsurePlaceBuffer() // 배율 없는 거치 루트 확보
        {
            if (placeBuffer != null) // 이미 있음
            {
                return placeBuffer; // 반환
            }

            GameObject bufferObject = new GameObject("SaleCounter_PlaceBuffer"); // 거치 루트
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bufferObject, gameObject.scene); // 판매대와 같은 씬
            placeBuffer = bufferObject.transform; // 저장
            return placeBuffer; // 반환
        }

        private void OnDestroy() // 거치 루트 정리
        {
            if (placeBuffer != null) // 존재
            {
                Destroy(placeBuffer.gameObject); // 제거
            }
        }

        private bool TryFindFreeSlot(out Vector3 slot) // 비어 있는 올려둘 자리
        {
            slot = Vector3.zero; // 초기화

            if (!TryGetTop(out Vector3 localMin, out Vector3 localMax)) // 판매대 윗면
            {
                return false; // 자리 없음
            }

            List<WorldItem> placed = CollectPlacedItems(); // 이미 올린 아이템
            float minX = localMin.x + LocalLength(SlotEdgeMargin, Vector3.right); // 왼쪽 끝
            float maxX = localMax.x - LocalLength(SlotEdgeMargin, Vector3.right); // 오른쪽 끝
            float minZ = localMin.z + LocalLength(SlotEdgeMargin, Vector3.forward); // 앞쪽 끝
            float maxZ = localMax.z - LocalLength(SlotEdgeMargin, Vector3.forward); // 뒤쪽 끝
            float stepX = LocalLength(SlotSpacing, Vector3.right); // 가로 간격
            float stepZ = LocalLength(SlotSpacing, Vector3.forward); // 세로 간격

            for (float z = minZ; z <= maxZ + 0.0001f; z += stepZ) // 줄 순회
            {
                for (float x = minX; x <= maxX + 0.0001f; x += stepX) // 칸 순회
                {
                    Vector3 candidate = transform.TransformPoint(new Vector3(x, localMax.y, z)); // 후보 자리 (윗면 높이)
                    bool occupied = false; // 사용 여부

                    foreach (WorldItem item in placed) // 올린 아이템과 거리 비교
                    {
                        Vector3 offset = item.transform.position - candidate; // 차이
                        offset.y = 0f; // 수평 거리만

                        if (offset.magnitude < OccupiedRadius) // 가까움
                        {
                            occupied = true; // 사용 중
                            break; // 중단
                        }
                    }

                    if (!occupied) // 빈 자리
                    {
                        slot = candidate; // 반환
                        return true; // 성공
                    }
                }
            }

            return false; // 판매대가 가득 참
        }

        private float LocalLength(float worldLength, Vector3 localAxis) // 월드 길이 → 판매대 로컬 길이
        {
            float scale = transform.TransformVector(localAxis).magnitude; // 축 배율
            return scale <= 0.0001f ? worldLength : worldLength / scale; // 변환
        }

        private bool TryGetTop(out Vector3 localMin, out Vector3 localMax) // 판매대 콜라이더 로컬 범위
        {
            BoxCollider box = GetComponent<BoxCollider>(); // 상자 콜라이더

            if (box != null) // 상자 기준
            {
                localMin = box.center - (box.size * 0.5f); // 최소
                localMax = box.center + (box.size * 0.5f); // 최대
                return true; // 성공
            }

            Collider anyCollider = GetComponent<Collider>(); // 다른 콜라이더

            if (anyCollider == null) // 없음
            {
                localMin = localMax = Vector3.zero; // 초기화
                return false; // 실패
            }

            Bounds bounds = anyCollider.bounds; // 월드 범위
            localMin = transform.InverseTransformPoint(bounds.min); // 근사 최소
            localMax = transform.InverseTransformPoint(bounds.max); // 근사 최대
            return true; // 성공
        }

        private static bool IsSellable(WorldItem item) // 판매대에 올릴 수 있는 회수품인지
        {
            RecoverableValue recoverable = item == null ? null : item.GetComponent<RecoverableValue>(); // 가격 데이터
            OfficeStoredItemState officeState = item == null ? null : item.GetComponent<OfficeStoredItemState>(); // 단상 보관 상태
            return recoverable != null && !recoverable.IsSold && (officeState == null || !officeState.IsOfficeStored); // 미판매 회수품이며 단상 보호 상태가 아님
        }

        private static WorldItem SelectedItemOf(PlayerInteractor interactor) // 플레이어 선택 아이템
        {
            PlayerInventory inventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 인벤토리
            return inventory == null ? null : inventory.SelectedItem; // 선택 아이템
        }

        private string BuildPrompt() // 현재 선택 회수품 기준 안내 문구 생성
        {
            ResolveReferences(); // 경제 상태 참조 확보
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 현재 싱글 플레이어 인벤토리 조회
            WorldItem selectedItem = inventory == null ? null : inventory.SelectedItem; // 현재 선택 아이템 조회

            if (!IsSellable(selectedItem) || economy == null) // 올릴 회수품 선택 여부 확인
            {
                return "판매대 — 회수품을 올려두고 벨로 판매"; // 기본 안내
            }

            if (!TryFindFreeSlot(out _)) // 자리 없음
            {
                return "판매대가 가득 참 — 벨을 눌러 판매"; // 안내
            }

            return $"{selectedItem.DisplayName} 판매대에 올리기 / 예상 {PriceOf(selectedItem)}"; // 이름과 예상 금액 안내
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
