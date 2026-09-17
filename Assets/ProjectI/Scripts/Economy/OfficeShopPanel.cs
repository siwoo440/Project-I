using System.Collections.Generic; // 목록 사용
using ProjectI.Interaction; // 조작 잠금 참조
using ProjectI.Items; // WorldItem 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public sealed class OfficeShopPanel : MonoBehaviour // 구매 수량과 구매 여부를 묻는 창 (33일차, 정식 HUD 전까지 즉시 모드 GUI)
    {
        private const float PanelWidth = 380f; // 창 너비
        private const float PanelHeight = 250f; // 창 높이
        private const float NoticeSeconds = 2.5f; // 결과 안내 표시 시간

        private OfficeShopShelf shelf; // 진열대
        private ShopEntry entry; // 선택 상품
        private PlayerInteractor interactor; // 연 플레이어
        private int openedFrame = -1; // 연 프레임
        private string notice = string.Empty; // 결과 안내
        private float noticeUntil; // 안내 종료 시각
        private int cachedMax; // 프레임 단위 최대 수량 캐시 (OnGUI 여러 번 호출 대비)
        private int cachedMaxFrame = -1; // 캐시 프레임

        public bool IsOpen { get; private set; } // 열림 여부
        public int Quantity { get; private set; } = 1; // 선택 수량
        public ShopEntry Entry => entry; // 선택 상품 (검증용)
        public ShopPurchaseResult LastResult { get; private set; } = ShopPurchaseResult.Success; // 마지막 결과 (검증용)
        public List<WorldItem> LastDelivered { get; private set; } = new List<WorldItem>(); // 마지막 구매품 (검증용)
        public int MaxQuantity => ComputeMax(); // 지금 살 수 있는 최대 수량

        public void Open(OfficeShopShelf targetShelf, ShopEntry targetEntry, PlayerInteractor targetInteractor) // 창 열기
        {
            if (IsOpen || targetShelf == null || targetEntry == null || PlayerControlLock.IsLocked) // 중복·다른 창
            {
                return; // 중단
            }

            shelf = targetShelf; // 진열대
            entry = targetEntry; // 상품
            interactor = targetInteractor; // 플레이어
            Quantity = 1; // 기본 1개
            IsOpen = true; // 열림
            openedFrame = Time.frameCount; // 연 프레임
            PlayerControlLock.Acquire(this, interactor); // 조작 정지
        }

        public void SetQuantity(int value) // 수량 변경 (1 ~ 상품 한도)
        {
            int limit = entry == null ? 1 : entry.maxPerPurchase; // 한도
            Quantity = Mathf.Clamp(value, 1, Mathf.Max(1, limit)); // 적용
        }

        public ShopPurchaseResult Confirm() // 구매 확정
        {
            if (!IsOpen || shelf == null) // 확인
            {
                return ShopPurchaseResult.InvalidEntry; // 실패
            }

            LastResult = shelf.Purchase(entry, Quantity, out List<WorldItem> delivered); // 구매
            cachedMaxFrame = -1; // 자금·자리 변화 반영
            LastDelivered = delivered; // 기록
            ShowNotice(OfficeShopShelf.Describe(LastResult)); // 안내

            if (LastResult == ShopPurchaseResult.Success) // 성공하면 닫기
            {
                Close(); // 닫기
            }

            return LastResult; // 결과
        }

        public void Close() // 구매 없이 닫기
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 중단
            }

            IsOpen = false; // 닫힘
            PlayerControlLock.Release(this); // 조작 복구
        }

        private int ComputeMax() // 최대 수량 (프레임당 한 번 계산)
        {
            if (cachedMaxFrame != Time.frameCount) // 새 프레임
            {
                cachedMax = shelf == null ? 0 : shelf.MaxAffordable(entry); // 계산
                cachedMaxFrame = Time.frameCount; // 기록
            }

            return cachedMax; // 반환
        }

        private void ShowNotice(string text) // 결과 안내
        {
            notice = text; // 문구
            noticeUntil = Time.unscaledTime + NoticeSeconds; // 표시 시간
        }

        private void Update() // Esc 취소 처리
        {
            if (IsOpen && Time.frameCount > openedFrame + 1 && Cursor.lockState == CursorLockMode.Locked) // Esc로 커서를 다시 잠그면 취소
            {
                Close(); // 닫기
            }
        }

        private void OnDisable() // 비활성화 시 조작 복구
        {
            Close(); // 닫기
        }

        private void OnGUI() // 창 그리기
        {
            if (!IsOpen) // 닫혀 있으면 결과 안내만
            {
                if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil) // 안내 표시 중
                {
                    GUI.Box(new Rect((Screen.width - 360f) * 0.5f, (Screen.height * 0.5f) + 96f, 360f, 30f), notice); // 조준점 아래
                }

                return; // 종료
            }

            if (entry == null || shelf == null) // 상품 없음
            {
                Close(); // 닫기
                return; // 종료
            }

            int max = MaxQuantity; // 살 수 있는 최대
            int total = entry.price * Quantity; // 합계
            int funds = shelf.Funds; // 자금
            bool canBuy = Quantity <= max; // 구매 가능
            Rect area = new Rect((Screen.width - PanelWidth) * 0.5f, (Screen.height - PanelHeight) * 0.5f, PanelWidth, PanelHeight); // 화면 중앙
            GUI.Box(area, string.Empty); // 배경
            GUILayout.BeginArea(new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f)); // 안쪽
            GUILayout.Label(entry.DisplayName); // 상품 이름
            GUILayout.Label($"가격 {entry.price} / 개"); // 단가

            GUILayout.BeginHorizontal(); // 수량 줄
            GUILayout.Label("수량", GUILayout.Width(60f)); // 제목
            if (GUILayout.Button("-", GUILayout.Width(36f))) // 감소
            {
                SetQuantity(Quantity - 1); // 적용
            }

            GUILayout.Label($"{Quantity}", GUILayout.Width(40f)); // 현재 수량
            if (GUILayout.Button("+", GUILayout.Width(36f))) // 증가
            {
                SetQuantity(Quantity + 1); // 적용
            }

            GUILayout.Label($"(지금 최대 {max}개)"); // 한도
            GUILayout.EndHorizontal(); // 줄 끝

            GUILayout.Label($"합계 {total}"); // 합계
            GUILayout.Label($"공동 자금 {funds} → {Mathf.Max(0, funds - total)}"); // 자금 변화

            if (!canBuy) // 살 수 없는 이유
            {
                GUILayout.Label(funds < total ? "자금이 부족합니다" : "수령대 자리가 부족합니다"); // 이유
            }
            else if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil) // 직전 실패 안내
            {
                GUILayout.Label(notice); // 안내
            }
            else // 확인 질문
            {
                GUILayout.Label($"{entry.DisplayName} {Quantity}개를 구매하시겠습니까?"); // 질문
            }

            GUILayout.FlexibleSpace(); // 아래로
            GUILayout.BeginHorizontal(); // 버튼 줄
            GUI.enabled = canBuy; // 가능할 때만
            if (GUILayout.Button("구매", GUILayout.Height(32f))) // 구매
            {
                Confirm(); // 확정
            }

            GUI.enabled = true; // 복구
            if (GUILayout.Button("취소", GUILayout.Height(32f))) // 취소
            {
                Close(); // 닫기
            }
            GUILayout.EndHorizontal(); // 줄 끝
            GUILayout.EndArea(); // 안쪽 끝
        }
    }
}
