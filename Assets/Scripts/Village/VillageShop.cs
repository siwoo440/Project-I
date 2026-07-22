using UnityEngine; // Unity 기본 기능과 OnGUI 사용
using UnityEngine.InputSystem; // Escape 저수준 키 입력 사용
using UnityEngine.SceneManagement; // Scene 이동 후 영구 매니저 참조 재연결
namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageShop : MonoBehaviour, IInteractable // 플레이어 상호작용형 마을 상점
    {
        [Header("상점 설정")] // Inspector 상점 설정 구분
        [Tooltip("화면과 상호작용에 표시할 상점 이름")] [SerializeField] string shopName = "도굴단 물자 상점"; // 화면과 상호작용에 표시할 상점 이름
        [Tooltip("재고와 구매품 전달을 관리할 매니저")] [SerializeField] VillageShopManager shopManager; // 재고와 구매품 전달을 관리할 매니저
        [Tooltip("정산 창 상태 확인용 UI")] [SerializeField] VillageSettlementUI settlementUI; // 정산 창 상태 확인용 UI

        PlayerInteractor customerInteractor; // 현재 상점을 이용하는 플레이어 상호작용 컴포넌트
        PlayerController customerController; // 현재 상점을 이용하는 플레이어 이동 컴포넌트
        PlayerCombat customerCombat; // 현재 상점을 이용하는 플레이어 전투 컴포넌트
        CampaignManager campaignManager; // 보유 골드와 캠페인 상태 확인용 매니저

        bool interactorWasEnabled; // 상점 진입 전 상호작용 활성 상태
        bool controllerWasEnabled; // 상점 진입 전 이동 활성 상태
        bool combatWasEnabled; // 상점 진입 전 전투 활성 상태
        bool windowOpen; // 현재 상점 창 표시 여부
        string lastMessage = "구매한 물자는 다음 탐험 시작 시 지급됩니다."; // 최근 구매 결과 안내

        GUIStyle titleStyle; // 상점 제목 표시 스타일
        GUIStyle itemStyle; // 상품 정보 표시 스타일
        GUIStyle messageStyle; // 구매 결과 표시 스타일

        public bool IsWindowOpen => windowOpen; // 다른 마을 시스템에서 상점 창 상태 확인

        void Reset() // 컴포넌트 추가 시 상점 관련 참조 자동 검색
        {
            shopManager = FindFirstObjectByType<VillageShopManager>(); // 상점 매니저 자동 검색
            settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 정산 UI 자동 검색
        }

        void Start() // 실행 시 현재 활성 상점과 캠페인 매니저 연결
        {
            ResolveReferences(); // Scene 전환 후 살아 있는 싱글톤 참조로 보정
        }
        void OnEnable() // Scene 전환 후 참조 재연결 이벤트 구독
        {
            SceneManager.sceneLoaded += HandleSceneLoaded; // 새로운 Scene 로드 완료 이벤트 연결
        }

        void OnDisable() // Scene 전환 이벤트 구독 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // Scene 로드 이벤트 연결 해제
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) // 새로운 Scene 로드 후 영구 매니저 참조 보정
        {
            ResolveReferences(); // 새 Village Scene의 상점을 기존 영구 매니저에 연결
        }

        void ResolveReferences() // 제거된 중복 매니저 대신 현재 살아 있는 싱글톤 연결
        {
            if (VillageShopManager.Instance != null) // 유지 중인 영구 상점 매니저 존재 여부 확인
            {
                shopManager = VillageShopManager.Instance; // 기존 Inspector 참조를 실제 싱글톤으로 교체
            }
            else if (shopManager == null) // 싱글톤과 Inspector 참조가 모두 없는지 확인
            {
                shopManager = FindFirstObjectByType<VillageShopManager>(); // 현재 Scene에서 상점 매니저 검색
            }

            if (CampaignManager.Instance != null) // 유지 중인 영구 캠페인 매니저 존재 여부 확인
            {
                campaignManager = CampaignManager.Instance; // 실제 캠페인 싱글톤으로 참조 교체
            }
            else if (campaignManager == null) // 캠페인 싱글톤과 기존 참조가 모두 없는지 확인
            {
                campaignManager = FindFirstObjectByType<CampaignManager>(); // 현재 Scene에서 캠페인 매니저 검색
            }

            if (settlementUI == null) // 현재 Village Scene의 정산 UI 참조 확인
            {
                settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 새로 생성된 Village 정산 UI 검색
            }
        }
        void Update() // 상점 창 Escape 닫기 입력 처리
        {
            if (!windowOpen) // 현재 상점 창 표시 여부 확인
            {
                return; // 닫기 입력 처리 중단
            }

            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) // Escape 입력 여부 확인
            {
                CloseShop(); // 상점 창 닫기
            }
        }

        public string GetPrompt() // 현재 상점 상태에 맞는 상호작용 문구 반환
        {
            ResolveReferences(); // 상점 문구를 표시하기 전에 제거된 매니저 참조 보정

            if (windowOpen) // 현재 상점 이용 중인지 확인
            {
                return "상점 이용 중"; // 중복 상호작용 방지 문구 반환
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 마을 정산 창 상태 확인
            {
                return "먼저 마을 정산을 완료하세요"; // 정산 우선 안내 반환
            }

            if (campaignManager == null) // 캠페인 매니저 연결 여부 확인
            {
                return "CampaignManager 연결 필요"; // 매니저 누락 안내 반환
            }

            if (campaignManager.State.CampaignWon || campaignManager.State.CampaignFailed) // 캠페인 종료 여부 확인
            {
                return campaignManager.GetDeadlineMessage(); // 캠페인 결과 문구 반환
            }

            return $"[E] {shopName} 이용"; // 정상 상점 상호작용 문구 반환
        }

        public void Interact(PlayerInteractor interactor) // E 입력으로 상점 창 열기
        {
            ResolveReferences(); // 상점을 열기 전에 현재 영구 매니저 참조 재연결
            if (windowOpen || interactor == null) // 중복 실행과 플레이어 존재 여부 확인
            {
                return; // 상점 열기 중단
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창 상태 확인
            {
                Debug.LogWarning("[VillageShop] 마을 정산을 먼저 완료해야 합니다."); // 정산 미완료 출력
                return; // 상점 이용 중단
            }

            if (shopManager == null || campaignManager == null) // 필수 매니저 존재 여부 확인
            {
                Debug.LogError("[VillageShop] ShopManager 또는 CampaignManager가 없습니다."); // 참조 누락 오류 출력
                return; // 상점 이용 중단
            }

            if (campaignManager.State.CampaignWon || campaignManager.State.CampaignFailed) // 캠페인 종료 여부 확인
            {
                Debug.LogWarning("[VillageShop] 캠페인 종료 상태에서는 상점을 이용할 수 없습니다."); // 상점 이용 불가 출력
                return; // 상점 이용 중단
            }

            customerInteractor = interactor; // 상호작용한 플레이어 저장
            customerController = interactor.GetComponent<PlayerController>(); // 플레이어 이동 컴포넌트 가져오기
            customerCombat = interactor.GetComponent<PlayerCombat>(); // 플레이어 전투 컴포넌트 가져오기

            interactorWasEnabled = customerInteractor.enabled; // 기존 상호작용 활성 상태 저장
            controllerWasEnabled = customerController != null && customerController.enabled; // 기존 이동 활성 상태 저장
            combatWasEnabled = customerCombat != null && customerCombat.enabled; // 기존 전투 활성 상태 저장

            customerInteractor.enabled = false; // 상점 이용 중 월드 상호작용 차단

            if (customerController != null) // 플레이어 이동 컴포넌트 존재 여부 확인
            {
                customerController.enabled = false; // 상점 이용 중 이동과 시점 입력 차단
            }

            if (customerCombat != null) // 플레이어 전투 컴포넌트 존재 여부 확인
            {
                customerCombat.enabled = false; // 상점 이용 중 공격과 방어 차단
            }

            windowOpen = true; // 상점 창 표시 상태 활성화
            Cursor.lockState = CursorLockMode.None; // 상품 버튼 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시

            Debug.Log("[VillageShop] 상점 창을 열었습니다."); // 상점 열기 결과 출력
        }

        void CloseShop() // 상점 창을 닫고 플레이어 조작 복구
        {
            windowOpen = false; // 상점 화면 표시 상태 해제

            if (customerInteractor != null) // 플레이어 상호작용 컴포넌트 존재 여부 확인
            {
                customerInteractor.enabled = interactorWasEnabled; // 기존 상호작용 활성 상태 복구
            }

            if (customerController != null) // 플레이어 이동 컴포넌트 존재 여부 확인
            {
                customerController.enabled = controllerWasEnabled; // 기존 이동 활성 상태 복구
            }

            if (customerCombat != null) // 플레이어 전투 컴포넌트 존재 여부 확인
            {
                customerCombat.enabled = combatWasEnabled; // 기존 전투 활성 상태 복구
            }

            Cursor.lockState = CursorLockMode.Locked; // 마을 탐색을 위해 커서 다시 잠금
            Cursor.visible = false; // 마우스 커서 숨김

            customerInteractor = null; // 현재 상점 이용 플레이어 참조 초기화
            customerController = null; // 현재 플레이어 이동 참조 초기화
            customerCombat = null; // 현재 플레이어 전투 참조 초기화

            Debug.Log("[VillageShop] 상점 창을 닫았습니다."); // 상점 닫기 결과 출력
        }

        void OnGUI() // 임시 마을 상점 상품 목록 표시
        {
            if (!windowOpen) // 상점 창 표시 여부 확인
            {
                return; // 상점 화면 표시 중단
            }

            ResolveReferences(); // 상점 화면을 그리기 전에 현재 매니저 참조 보정

            if (shopManager == null || campaignManager == null) // 필수 매니저 존재 여부 확인
            {
                GUI.Box(new Rect(20f, 20f, 500f, 80f), "VillageShopManager 또는 CampaignManager가 없습니다."); // 참조 누락 안내 표시
                return; // 상점 화면 표시 중단
            }

            InitializeStyles(); // 상점 GUI 스타일 초기화

            float width = 780f; // 상점 패널 너비
            float height = 540f; // 상점 패널 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 상점 패널 배경 표시
            GUI.Label(new Rect(x + 20f, y + 15f, width - 40f, 40f), shopName, titleStyle); // 상점 이름 표시
            GUI.Label(new Rect(x + 20f, y + 55f, width - 40f, 25f), $"보유 골드: {campaignManager.State.Gold}골드", titleStyle); // 현재 보유 골드 표시

            int visibleCount = Mathf.Min(shopManager.StockCount, 6); // 임시 화면에 표시할 최대 상품 수 계산

            for (int i = 0; i < visibleCount; i++) // 표시 가능한 상점 재고 순회
            {
                DrawStockEntry(i, x, y + 95f + i * 62f, width); // 현재 상품 행 표시
            }

            GUI.Label(new Rect(x + 20f, y + 470f, width - 40f, 25f), lastMessage, messageStyle); // 최근 구매 결과 안내 표시
            GUI.Label(new Rect(x + 20f, y + 495f, width - 40f, 20f), $"마차 적재 대기: {shopManager.PendingItemCount}개", messageStyle); // 다음 탐험 구매품 수 표시

            if (GUI.Button(new Rect(x + width - 130f, y + 495f, 100f, 30f), "닫기")) // 상점 닫기 버튼 입력 확인
            {
                CloseShop(); // 상점 창 닫기
            }
        }

        void InitializeStyles() // 상점 GUI 스타일을 한 번만 생성
        {
            if (titleStyle != null) // 스타일 생성 완료 여부 확인
            {
                return; // 중복 스타일 생성 방지
            }

            titleStyle = new GUIStyle(GUI.skin.label); // 상점 제목 스타일 생성
            titleStyle.fontSize = 22; // 상점 제목 글자 크기 설정
            titleStyle.alignment = TextAnchor.MiddleCenter; // 상점 제목 중앙 정렬

            itemStyle = new GUIStyle(GUI.skin.label); // 상품 정보 스타일 생성
            itemStyle.fontSize = 15; // 상품 정보 글자 크기 설정
            itemStyle.alignment = TextAnchor.MiddleLeft; // 상품 정보 왼쪽 정렬
            itemStyle.wordWrap = true; // 긴 상품 설명 자동 줄바꿈

            messageStyle = new GUIStyle(GUI.skin.label); // 구매 결과 스타일 생성
            messageStyle.fontSize = 15; // 구매 결과 글자 크기 설정
            messageStyle.alignment = TextAnchor.MiddleCenter; // 구매 결과 중앙 정렬
        }

        void DrawStockEntry(int index, float x, float rowY, float width) // 상품 한 줄의 정보와 구매 버튼 표시
        {
            VillageShopStockEntry entry = shopManager.GetStockEntry(index); // 현재 번호의 상품 정보 가져오기

            if (entry == null) // 상품 정보 존재 여부 확인
            {
                return; // 비어 있는 상품 표시 중단
            }

            GUI.Box(new Rect(x + 25f, rowY, width - 50f, 56f), string.Empty); // 현재 상품 행 배경 표시
            GUI.Label(new Rect(x + 40f, rowY + 5f, 150f, 22f), entry.ItemName, itemStyle); // 상품 이름 표시
            GUI.Label(new Rect(x + 190f, rowY + 5f, 300f, 45f), entry.Description, itemStyle); // 상품 설명 표시
            GUI.Label(new Rect(x + 500f, rowY + 5f, 100f, 22f), $"{entry.Price}골드", itemStyle); // 상품 구매 가격 표시
            GUI.Label(new Rect(x + 500f, rowY + 28f, 100f, 22f), $"재고 {entry.RemainingStock}", itemStyle); // 남은 상품 재고 표시

            bool previousEnabled = GUI.enabled; // 기존 GUI 활성 상태 저장
            bool canPurchase = shopManager.CanPurchase(index); // 현재 상품 구매 가능 여부 확인
            GUI.enabled = canPurchase; // 골드와 재고 상태에 따라 구매 버튼 활성화

            if (GUI.Button(new Rect(x + 620f, rowY + 10f, 105f, 36f), "구매")) // 현재 상품 구매 버튼 입력 확인
            {
                if (shopManager.TryPurchase(index)) // 실제 상품 구매 성공 여부 확인
                {
                    lastMessage = $"{entry.ItemName} 구매 완료 — 다음 탐험 마차에 적재했습니다."; // 구매 성공 안내 저장
                }
                else // 구매 실패 처리
                {
                    lastMessage = "골드가 부족하거나 상품이 품절됐습니다."; // 구매 실패 안내 저장
                }
            }

            GUI.enabled = previousEnabled; // 다른 GUI 버튼의 활성 상태 복구
        }
    }
}