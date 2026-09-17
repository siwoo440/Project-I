using System.Collections.Generic; // 목록 사용
using ProjectI.Audio; // 효과음
using ProjectI.Interaction; // 조작 잠금 참조
using ProjectI.Items; // WorldItem 참조
using ProjectI.UI; // 정식 창 모양
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public sealed class OfficeShopPanel : MonoBehaviour // 구매 수량과 구매 여부를 묻는 창 (33일차 기능 · 36일차 정식 창 모양)
    {
        private const float NoticeSeconds = 2.5f; // 결과 안내 표시 시간

        private OfficeShopShelf shelf; // 진열대
        private ShopEntry entry; // 선택 상품
        private PlayerInteractor interactor; // 연 플레이어
        private int openedFrame = -1; // 연 프레임
        private string notice = string.Empty; // 결과 안내
        private float noticeUntil; // 안내 종료 시각
        private int cachedMax; // 프레임 단위 최대 수량 캐시 (OnGUI 여러 번 호출 대비)
        private int cachedMaxFrame = -1; // 캐시 프레임
        private Canvas canvas; // 창 캔버스
        private Text nameLabel; // 상품 이름
        private Text priceLabel; // 단가
        private Text quantityLabel; // 수량
        private Text maxLabel; // 최대 수량
        private Text totalLabel; // 합계
        private Text fundsLabel; // 자금 변화
        private Text messageLabel; // 질문·이유·안내
        private Button buyButton; // 구매 버튼
        private Button minusButton; // 감소
        private Button plusButton; // 증가

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
            notice = string.Empty; // 이전 안내 정리
            PlayerControlLock.Acquire(this, interactor); // 조작 정지
            BuildView(); // 창 구성
            RetroUi.EnsureEventSystem(); // UI 입력
            canvas.gameObject.SetActive(true); // 표시
            RefreshView(); // 내용
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
                GameHud.ShowNotice(notice); // 화면 알림
            }
            else
            {
                SoundPlayer.Play(SoundId.UiDenied, 0.6f); // 실패 소리
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

            if (canvas != null) // 창
            {
                canvas.gameObject.SetActive(false); // 숨김
            }
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
                return; // 종료
            }

            RefreshView(); // 창 내용
        }

        private void OnDisable() // 비활성화 시 조작 복구
        {
            Close(); // 닫기
        }

        private void RefreshView() // 창 내용 갱신
        {
            if (!IsOpen || canvas == null) // 닫힘
            {
                return; // 생략
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
            int limit = Mathf.Max(1, entry.maxPerPurchase); // 한도
            nameLabel.text = entry.DisplayName; // 이름
            priceLabel.text = $"가격  {entry.price:N0} / 개"; // 단가
            quantityLabel.text = Quantity.ToString(); // 수량
            maxLabel.text = $"(지금 최대 {max}개 · 한 번에 {limit}개까지)"; // 한도
            totalLabel.text = $"합계  {total:N0}"; // 합계
            fundsLabel.text = $"공동 자금  {funds:N0} → {Mathf.Max(0, funds - total):N0}"; // 자금 변화
            minusButton.interactable = Quantity > 1; // 감소
            plusButton.interactable = Quantity < limit; // 증가
            buyButton.interactable = canBuy; // 구매

            if (!canBuy) // 살 수 없는 이유
            {
                messageLabel.color = RetroUi.Red; // 빨강
                messageLabel.text = funds < total ? "자금이 부족합니다" : "수령대 자리가 부족합니다"; // 이유
            }
            else if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil) // 직전 실패 안내
            {
                messageLabel.color = RetroUi.Red; // 빨강
                messageLabel.text = notice; // 안내
            }
            else // 확인 질문
            {
                messageLabel.color = RetroUi.OrangeBright; // 강조
                messageLabel.text = $"{entry.DisplayName} {Quantity}개를 구매하시겠습니까?"; // 질문
            }
        }

        private void BuildView() // 창 구성 (처음 한 번)
        {
            if (canvas != null) // 이미 있음
            {
                return; // 생략
            }

            canvas = RetroUi.CreateCanvas("ShopPanelCanvas", 300, transform); // 캔버스
            RectTransform root = (RectTransform)canvas.transform; // 루트
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f)); // 가운데
            RetroUi.Solid(window, "Fill", RetroUi.Backdrop, true); // 바탕
            RetroUi.Frame(window, RetroUi.Orange, 3f); // 테두리

            nameLabel = Row(window, "Name", 38, RetroUi.Orange, -50f); // 이름
            priceLabel = Row(window, "Price", 24, RetroUi.OrangeDim, -100f); // 단가

            Text quantityTitle = Row(window, "QuantityTitle", 26, RetroUi.Orange, -170f); // 수량 제목
            quantityTitle.text = "수량"; // 글자
            minusButton = RetroUi.BoxButton(window, "Minus", "-", 30, () => SetQuantity(Quantity - 1)); // 감소
            RetroUi.Place((RectTransform)minusButton.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(150f, -170f), new Vector2(60f, 54f)); // 위치
            quantityLabel = RetroUi.Label(window, "Quantity", "1", 32, RetroUi.OrangeBright, TextAnchor.MiddleCenter); // 수량
            RetroUi.Place(quantityLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(214f, -170f), new Vector2(80f, 54f)); // 위치
            plusButton = RetroUi.BoxButton(window, "Plus", "+", 30, () => SetQuantity(Quantity + 1)); // 증가
            RetroUi.Place((RectTransform)plusButton.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(298f, -170f), new Vector2(60f, 54f)); // 위치
            maxLabel = RetroUi.Label(window, "Max", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 한도
            RetroUi.Place(maxLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(380f, -170f), new Vector2(360f, 40f)); // 위치

            totalLabel = Row(window, "Total", 28, RetroUi.OrangeBright, -240f); // 합계
            fundsLabel = Row(window, "Funds", 24, RetroUi.Orange, -285f); // 자금 변화
            messageLabel = Row(window, "Message", 24, RetroUi.OrangeBright, -345f); // 질문

            buyButton = RetroUi.BoxButton(window, "Buy", "구매", 28, () => Confirm()); // 구매
            RetroUi.Place((RectTransform)buyButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-250f, 60f), new Vector2(200f, 58f)); // 오른쪽 아래
            Button cancel = RetroUi.BoxButton(window, "Cancel", "취소", 28, Close); // 취소
            RetroUi.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-36f, 60f), new Vector2(190f, 58f)); // 오른쪽 아래
            canvas.gameObject.SetActive(false); // 처음엔 숨김
        }

        private static Text Row(RectTransform window, string name, int size, Color color, float y) // 한 줄 글자
        {
            Text text = RetroUi.Label(window, name, string.Empty, size, color, TextAnchor.MiddleLeft); // 글자
            RetroUi.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(36f, y), new Vector2(690f, size + 16f)); // 위치
            return text; // 반환
        }
    }
}
