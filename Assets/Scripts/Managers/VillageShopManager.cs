using System.Collections; // Dungeon Player 검색 대기 코루틴 사용
using System.Collections.Generic; // 구매한 아이템 프리팹 목록 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // Village와 Dungeon Scene 전환 감지

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [System.Serializable] // Inspector에서 상점 재고 항목을 설정할 수 있도록 지정
    public class VillageShopStockEntry // 상점 아이템 하나의 프리팹과 재고 설정
    {
        [Tooltip("구매 후 Dungeon에 전달할 아이템 프리팹")] [SerializeField] PickupItem itemPrefab; // 구매 후 Dungeon에 전달할 아이템 프리팹
        [Tooltip("마을 방문마다 제공할 재고")] [SerializeField][Min(1)] int stockPerVisit = 1; // 마을 방문마다 제공할 재고
        [Tooltip("ItemData 가격 누락 시 사용할 가격")] [SerializeField][Min(0)] int fallbackPrice = 50; // ItemData 가격 누락 시 사용할 가격

        int remainingStock; // 현재 마을 방문에서 남은 재고

        public PickupItem ItemPrefab => itemPrefab; // 구매할 아이템 프리팹 반환
        public int RemainingStock => remainingStock; // 현재 남은 재고 반환
        public string ItemName => itemPrefab != null ? itemPrefab.DisplayName : "아이템 없음"; // 상점에 표시할 아이템 이름 반환
        public string Description => itemPrefab != null && itemPrefab.Data != null ? itemPrefab.Data.description : "아이템 설명이 없습니다."; // 상점에 표시할 설명 반환
        public string ItemKey => itemPrefab != null && itemPrefab.Data != null ? itemPrefab.Data.name : itemPrefab != null ? itemPrefab.name : string.Empty; // 저장 복원용 아이템 식별자 반환

        public int Price // ItemData 또는 대체 가격 반환
        {
            get
            {
                if (itemPrefab != null && itemPrefab.Data != null && itemPrefab.Data.buyPrice > 0) // ItemData 구매 가격 존재 여부 확인
                {
                    return itemPrefab.Data.buyPrice; // ItemData의 구매 가격 반환
                }

                return Mathf.Max(0, fallbackPrice); // ItemData 가격이 없으면 대체 가격 반환
            }
        }

        public void SetRemainingStock(int amount) // 저장 파일의 남은 재고 적용
        {
            remainingStock = Mathf.Max(0, amount); // 저장된 재고를 음수가 되지 않도록 적용
        }
        public void Restock() // 마을 방문용 재고 초기화
        {
            remainingStock = Mathf.Max(1, stockPerVisit); // 설정된 방문당 재고 적용
        }

        public bool TryConsumeOne() // 구매 성공 후 재고 한 개 차감
        {
            if (remainingStock <= 0) // 남은 재고 존재 여부 확인
            {
                return false; // 품절 상태 반환
            }

            remainingStock--; // 남은 재고 한 개 차감
            return true; // 재고 차감 성공 반환
        }
    }

    public class VillageShopManager : MonoBehaviour // 상점 재고와 다음 탐험 구매품 전달 관리
    {
        public static VillageShopManager Instance { get; private set; } // 현재 활성 상점 관리자 접근점

        [Header("Scene 설정")] // Inspector Scene 이름 설정 구분
        [Tooltip("상점 재고를 갱신할 마을 Scene 이름")] [SerializeField] string villageSceneName = "Village"; // 상점 재고를 갱신할 마을 Scene 이름
        [Tooltip("구매품을 전달할 던전 Scene 이름")] [SerializeField] string dungeonSceneName = "Dungeon"; // 구매품을 전달할 던전 Scene 이름

        [Header("상점 재고")] // Inspector 판매 아이템 설정 구분
        [Tooltip("마을 상점 판매 목록")] [SerializeField] VillageShopStockEntry[] stockEntries; // 마을 상점 판매 목록

        readonly List<PickupItem> pendingItemPrefabs = new List<PickupItem>(); // 다음 Dungeon에 전달할 구매품 프리팹 목록

        bool deliveryRunning; // 구매품 전달 코루틴 실행 여부

        public int StockCount => stockEntries != null ? stockEntries.Length : 0; // 등록된 상점 아이템 종류 수 반환
        public int PendingItemCount => pendingItemPrefabs.Count; // 다음 탐험에 전달할 구매품 수 반환

        void Awake() // 상점 관리자 싱글톤과 초기 재고 설정
        {
            if (Instance != null && Instance != this) // 기존 상점 관리자 존재 여부 확인
            {
                Destroy(gameObject); // 새 Scene에서 생성된 중복 관리자 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 상점 관리자로 저장
            transform.SetParent(null); // 영구 오브젝트 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Dungeon과 Village 이동 후에도 구매 목록 유지
            Restock(); // 첫 번째 마을 방문용 재고 설정
        }

        void OnEnable() // Scene 로드 이벤트 구독
        {
            SceneManager.sceneLoaded += HandleSceneLoaded; // 새로운 Scene 로드 후 재고와 구매품 처리 연결
        }

        void OnDisable() // Scene 로드 이벤트 구독 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // Scene 로드 이벤트 연결 해제
        }

        void OnDestroy() // 상점 관리자 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 상점 관리자인지 확인
            {
                Instance = null; // 전역 상점 관리자 참조 초기화
            }
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) // Scene 종류에 따라 재고 갱신 또는 구매품 전달
        {
            if (scene.name == villageSceneName) // Village Scene 도착 여부 확인
            {
                Restock(); // 새로운 마을 방문용 상점 재고 갱신
                return; // Dungeon 전달 검사 중단
            }

            if (scene.name == dungeonSceneName && pendingItemPrefabs.Count > 0 && !deliveryRunning) // Dungeon 도착과 구매품 존재 여부 확인
            {
                StartCoroutine(DeliverPendingItemsRoutine()); // Dungeon 플레이어에게 구매품 전달 시작
            }
        }

        public string[] GetPendingItemKeys() // 다음 탐험 구매품의 저장 식별자 목록 반환
        {
            string[] itemKeys = new string[pendingItemPrefabs.Count]; // 구매품 수만큼 식별자 배열 생성

            for (int i = 0; i < pendingItemPrefabs.Count; i++) // 모든 구매 대기 프리팹 순회
            {
                PickupItem itemPrefab = pendingItemPrefabs[i]; // 현재 구매품 프리팹 가져오기
                itemKeys[i] = GetItemKey(itemPrefab); // 현재 구매품의 저장 식별자 기록
            }

            return itemKeys; // 완성된 구매품 식별자 목록 반환
        }

        public int[] GetRemainingStocks() // 현재 상점의 남은 재고 목록 반환
        {
            if (stockEntries == null) // 상점 재고 배열 존재 여부 확인
            {
                return new int[0]; // 비어 있는 재고 배열 반환
            }

            int[] remainingStocks = new int[stockEntries.Length]; // 아이템 종류 수만큼 재고 배열 생성

            for (int i = 0; i < stockEntries.Length; i++) // 모든 상점 재고 순회
            {
                VillageShopStockEntry entry = stockEntries[i]; // 현재 재고 항목 가져오기
                remainingStocks[i] = entry != null ? entry.RemainingStock : 0; // 현재 남은 재고 기록
            }

            return remainingStocks; // 완성된 재고 목록 반환
        }

        public void RestoreSavedShopState(string[] pendingItemKeys, int[] remainingStocks) // 저장된 구매품과 상점 재고 복원
        {
            pendingItemPrefabs.Clear(); // 현재 구매 대기 목록 초기화

            if (stockEntries == null) // 상점 재고 배열 존재 여부 확인
            {
                Debug.LogError("[VillageShop] 복원할 상점 재고 설정이 없습니다."); // 재고 설정 누락 오류 출력
                return; // 상점 복원 중단
            }

            RestoreRemainingStocks(remainingStocks); // 저장된 상점 재고 복원
            RestorePendingItems(pendingItemKeys); // 저장된 구매 대기 아이템 복원

            Debug.Log($"[VillageShop] 저장된 상점 상태 복원 — 구매 대기 {pendingItemPrefabs.Count}개"); // 상점 복원 결과 출력
        }

        void RestoreRemainingStocks(int[] remainingStocks) // 저장된 남은 재고 배열 적용
        {
            if (remainingStocks == null) // 저장된 재고 목록 존재 여부 확인
            {
                return; // 재고 복원 중단
            }

            int restoreCount = Mathf.Min(stockEntries.Length, remainingStocks.Length); // 실제 복원 가능한 재고 수 계산

            for (int i = 0; i < restoreCount; i++) // 저장된 모든 재고 순회
            {
                VillageShopStockEntry entry = stockEntries[i]; // 현재 복원할 재고 항목 가져오기

                if (entry != null) // 현재 재고 항목 존재 여부 확인
                {
                    entry.SetRemainingStock(remainingStocks[i]); // 저장된 남은 재고 적용
                }
            }
        }

        void RestorePendingItems(string[] pendingItemKeys) // 저장된 구매 대기 아이템 목록 복원
        {
            if (pendingItemKeys == null) // 저장된 구매품 식별자 목록 존재 여부 확인
            {
                return; // 구매품 복원 중단
            }

            foreach (string itemKey in pendingItemKeys) // 저장된 모든 구매품 식별자 순회
            {
                PickupItem matchedPrefab = FindItemPrefabByKey(itemKey); // 식별자와 일치하는 아이템 프리팹 검색

                if (matchedPrefab != null) // 일치하는 구매품 프리팹 존재 여부 확인
                {
                    pendingItemPrefabs.Add(matchedPrefab); // 다음 탐험 구매품 목록에 복원
                }
                else // 일치하는 프리팹을 찾지 못한 경우
                {
                    Debug.LogWarning($"[VillageShop] 저장된 구매품을 찾을 수 없습니다: {itemKey}"); // 누락된 아이템 식별자 출력
                }
            }
        }

        PickupItem FindItemPrefabByKey(string itemKey) // 저장 식별자와 일치하는 상점 아이템 프리팹 검색
        {
            if (string.IsNullOrEmpty(itemKey) || stockEntries == null) // 식별자와 상점 재고 배열 확인
            {
                return null; // 검색할 수 없는 상태 반환
            }

            foreach (VillageShopStockEntry entry in stockEntries) // 모든 상점 재고 항목 순회
            {
                if (entry != null && entry.ItemKey == itemKey) // 저장 식별자가 일치하는지 확인
                {
                    return entry.ItemPrefab; // 일치하는 아이템 프리팹 반환
                }
            }

            return null; // 일치하는 아이템이 없으면 빈 값 반환
        }

        string GetItemKey(PickupItem itemPrefab) // 구매품 프리팹의 저장 식별자 반환
        {
            if (itemPrefab == null) // 아이템 프리팹 존재 여부 확인
            {
                return string.Empty; // 빈 식별자 반환
            }

            if (itemPrefab.Data != null) // ItemData 존재 여부 확인
            {
                return itemPrefab.Data.name; // ScriptableObject 이름을 식별자로 반환
            }

            return itemPrefab.name; // ItemData가 없으면 프리팹 이름 반환
        }

        public VillageShopStockEntry GetStockEntry(int index) // 지정 번호의 상점 재고 반환
        {
            if (stockEntries == null || index < 0 || index >= stockEntries.Length) // 재고 배열과 번호 범위 확인
            {
                return null; // 잘못된 번호에 빈 값 반환
            }

            return stockEntries[index]; // 정상 재고 항목 반환
        }

        public bool CanPurchase(int index) // 지정한 상점 아이템 구매 가능 여부 반환
        {
            VillageShopStockEntry entry = GetStockEntry(index); // 구매할 재고 항목 가져오기

            if (entry == null || entry.ItemPrefab == null || entry.RemainingStock <= 0) // 프리팹과 재고 존재 여부 확인
            {
                return false; // 구매 불가 반환
            }

            CampaignManager campaignManager = CampaignManager.Instance; // 현재 캠페인 관리자 가져오기

            if (campaignManager == null) // 캠페인 관리자 존재 여부 확인
            {
                return false; // 구매 불가 반환
            }

            if (campaignManager.State.CampaignWon || campaignManager.State.CampaignFailed) // 캠페인 종료 여부 확인
            {
                return false; // 캠페인 종료 후 구매 차단
            }

            return campaignManager.State.Gold >= entry.Price; // 현재 골드와 가격을 비교한 구매 가능 여부 반환
        }

        public bool TryPurchase(int index) // 상점 아이템 하나 구매 시도
        {
            if (!CanPurchase(index)) // 현재 아이템 구매 가능 여부 확인
            {
                Debug.LogWarning("[VillageShop] 재고 또는 골드가 부족해 구매할 수 없습니다."); // 구매 실패 원인 출력
                return false; // 구매 실패 반환
            }

            VillageShopStockEntry entry = GetStockEntry(index); // 구매할 재고 항목 가져오기
            CampaignManager campaignManager = CampaignManager.Instance; // 구매 비용을 지불할 캠페인 관리자 가져오기

            if (!campaignManager.TrySpendGold(entry.Price)) // 실제 구매 비용 지불 성공 여부 확인
            {
                return false; // 비용 지불 실패로 구매 중단
            }

            if (!entry.TryConsumeOne()) // 상점 재고 한 개 차감 성공 여부 확인
            {
                Debug.LogError("[VillageShop] 비용 지불 후 재고 차감에 실패했습니다."); // 예상하지 못한 재고 오류 출력
                return false; // 구매 실패 반환
            }

            pendingItemPrefabs.Add(entry.ItemPrefab); // 구매품을 다음 탐험 전달 목록에 추가

            Debug.Log($"[VillageShop] {entry.ItemName} 구매 — 다음 탐험 적재 {pendingItemPrefabs.Count}개"); // 구매 결과 출력

            if (CampaignSaveManager.Instance != null) // 저장 관리자 존재 여부 확인
            {
                CampaignSaveManager.Instance.SaveGame(); // 구매 결과와 남은 재고 자동 저장
            }

            return true; // 구매 성공 반환
        }

        public void ResetForNewCampaign() // 새 게임 시작을 위해 상점 상태 전체 초기화
        {
            StopAllCoroutines(); // 진행 중인 구매품 전달 코루틴 중단
            deliveryRunning = false; // 구매품 전달 상태 초기화
            pendingItemPrefabs.Clear(); // 이전 캠페인의 구매 대기 아이템 제거
            Restock(); // 첫 번째 마을 방문용 상점 재고 초기화

            Debug.Log("[VillageShop] 새 캠페인용 상점 상태를 초기화했습니다."); // 상점 초기화 결과 출력
        }


        [ContextMenu("상점 재고 갱신")] // Inspector 우클릭 테스트 메뉴 등록
        public void Restock() // 모든 상점 아이템의 방문당 재고 초기화
        {
            if (stockEntries == null) // 상점 재고 배열 존재 여부 확인
            {
                return; // 재고 갱신 중단
            }

            foreach (VillageShopStockEntry entry in stockEntries) // 등록된 모든 상점 재고 순회
            {
                if (entry != null) // 현재 재고 항목 존재 여부 확인
                {
                    entry.Restock(); // 현재 아이템 재고 초기화
                }
            }

            Debug.Log("[VillageShop] 마을 방문용 상점 재고를 갱신했습니다."); // 재고 갱신 결과 출력
        }

        IEnumerator DeliverPendingItemsRoutine() // Dungeon 플레이어를 기다린 뒤 구매품을 인벤토리에 전달
        {
            deliveryRunning = true; // 중복 전달 방지 상태 활성화
            PlayerInteractor playerInteractor = null; // Dungeon 플레이어 상호작용 컴포넌트 초기화

            for (int frame = 0; frame < 120 && playerInteractor == null; frame++) // 최대 120프레임 동안 플레이어 검색
            {
                playerInteractor = FindFirstObjectByType<PlayerInteractor>(); // 현재 Dungeon Player 검색

                if (playerInteractor == null) // 플레이어 검색 실패 여부 확인
                {
                    yield return null; // 다음 프레임까지 대기
                }
            }

            if (playerInteractor == null || playerInteractor.Inventory == null) // 플레이어와 인벤토리 검색 결과 확인
            {
                Debug.LogError("[VillageShop] Dungeon Player 또는 InventorySystem을 찾을 수 없습니다."); // 전달 실패 오류 출력
                deliveryRunning = false; // 다음 전달 재시도를 위해 상태 해제
                yield break; // 구매품 목록을 유지하고 전달 중단
            }

            yield return null; // DungeonGenerator의 플레이어 시작 위치 배치까지 한 프레임 추가 대기

            InventorySystem inventory = playerInteractor.Inventory; // Dungeon Player의 인벤토리 가져오기
            PickupItem[] deliveryItems = pendingItemPrefabs.ToArray(); // 전달 중 목록 변경을 방지하기 위한 복사본 생성
            Vector3 dropOrigin = playerInteractor.transform.position + playerInteractor.transform.forward * 1.2f + Vector3.up * 0.5f; // 인벤토리 초과 아이템의 드랍 위치 계산
            int deliveredCount = 0; // 인벤토리에 들어간 구매품 수 초기화

            for (int i = 0; i < deliveryItems.Length; i++) // 모든 구매품 프리팹 순회
            {
                PickupItem itemPrefab = deliveryItems[i]; // 현재 전달할 아이템 프리팹 가져오기

                if (itemPrefab == null) // 아이템 프리팹 존재 여부 확인
                {
                    continue; // 비어 있는 구매품 건너뜀
                }

                Vector3 itemPosition = dropOrigin + Vector3.right * (i * 0.35f); // 구매품별 월드 생성 위치 계산
                PickupItem createdItem = Instantiate(itemPrefab, itemPosition, Quaternion.identity); // Dungeon에 실제 아이템 생성

                if (inventory.TryAdd(createdItem)) // Dungeon Player 인벤토리에 아이템 추가 시도
                {
                    deliveredCount++; // 정상 전달 수 증가
                }
                else // 인벤토리가 가득 찬 경우
                {
                    Debug.LogWarning($"[VillageShop] {createdItem.DisplayName}을 담을 공간이 없어 플레이어 앞에 내려놓았습니다."); // 인벤토리 초과 안내 출력
                }
            }

            pendingItemPrefabs.Clear(); // 실제 생성이 끝난 구매 대기 목록 초기화
            deliveryRunning = false; // 구매품 전달 상태 종료

            Debug.Log($"[VillageShop] 구매품 전달 완료 — 인벤토리 {deliveredCount}개, 전체 {deliveryItems.Length}개"); // 최종 전달 결과 출력
        }
    }
}