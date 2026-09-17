using System; // 콜백
using System.Collections.Generic; // 해상도 목록
using ProjectI.Settings; // 게임 설정
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class SettingsPanel : MonoBehaviour // 설정 창 (메인 메뉴·일시정지 공용)
    {
        private const float RowHeight = 64f; // 줄 높이
        private Slider sensitivitySlider; // 감도
        private Text sensitivityValue; // 감도 값
        private Slider volumeSlider; // 음량
        private Text volumeValue; // 음량 값
        private RetroHover fullscreenHover; // 전체 화면
        private RetroHover resolutionHover; // 해상도
        private RetroHover vSyncHover; // 수직 동기화
        private Action onClosed; // 닫힘 콜백

        public bool IsOpen => gameObject.activeSelf; // 열림 여부

        public static SettingsPanel Create(Transform canvas) // 생성 (처음엔 닫힘)
        {
            RectTransform root = RetroUi.Rect(canvas, "SettingsPanel"); // 전체 화면
            SettingsPanel panel = root.gameObject.AddComponent<SettingsPanel>(); // 창
            panel.Build(root); // 구성
            root.gameObject.SetActive(false); // 닫힘
            return panel; // 반환
        }

        public void Open(Action closed) // 열기
        {
            onClosed = closed; // 콜백
            gameObject.SetActive(true); // 표시
            transform.SetAsLastSibling(); // 맨 앞
            RefreshValues(); // 현재 값
        }

        public void Close() // 닫기 (저장)
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 생략
            }

            GameSettings.Save(); // 저장
            gameObject.SetActive(false); // 숨김
            Action callback = onClosed; // 콜백
            onClosed = null; // 정리
            callback?.Invoke(); // 알림
        }

        private void Build(RectTransform root) // 화면 구성
        {
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면 어둡게 (클릭 차단)
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 560f)); // 가운데
            RetroUi.Solid(window, "Fill", RetroUi.Backdrop, true); // 바탕
            RetroUi.Frame(window, RetroUi.Orange, 3f); // 테두리

            Text title = RetroUi.Label(window, "Title", "설정", 40, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, -50f), new Vector2(300f, 60f)); // 왼쪽 위

            float y = -130f; // 첫 줄
            sensitivitySlider = SliderRow(window, "마우스 감도", y, GameSettings.MinLookSensitivity, GameSettings.MaxLookSensitivity, out sensitivityValue); // 감도
            sensitivitySlider.onValueChanged.AddListener(value => { GameSettings.LookSensitivity = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            volumeSlider = SliderRow(window, "전체 음량", y, 0f, 1f, out volumeValue); // 음량
            volumeSlider.onValueChanged.AddListener(value => { GameSettings.MasterVolume = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            fullscreenHover = ButtonRow(window, "전체 화면", y, () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; RefreshValues(); }); // 전체 화면
            y -= RowHeight; // 다음 줄
            resolutionHover = ButtonRow(window, "해상도", y, CycleResolution); // 해상도
            y -= RowHeight; // 다음 줄
            vSyncHover = ButtonRow(window, "수직 동기화", y, () => { GameSettings.VSync = !GameSettings.VSync; RefreshValues(); }); // 수직 동기화

            Text note = RetroUi.Label(window, "Note", "해상도·전체 화면은 빌드한 게임에서만 바뀝니다.", 18, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(40f, 110f), new Vector2(700f, 30f)); // 아래

            Button reset = RetroUi.BoxButton(window, "Reset", "기본값", 24, () => { GameSettings.ResetToDefaults(); RefreshValues(); }); // 기본값
            RetroUi.Place((RectTransform)reset.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-250f, 50f), new Vector2(180f, 50f)); // 오른쪽 아래
            Button close = RetroUi.BoxButton(window, "Close", "닫기", 24, Close); // 닫기
            RetroUi.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-40f, 50f), new Vector2(180f, 50f)); // 오른쪽 아래
        }

        private static Slider SliderRow(RectTransform window, string label, float y, float min, float max, out Text value) // 슬라이더 줄
        {
            RowLabel(window, label, y); // 이름
            Slider slider = RetroUi.SliderBar(window, label, min, max, min); // 슬라이더
            RetroUi.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(300f, y), new Vector2(360f, 40f)); // 위치
            value = RetroUi.Label(window, label + "_Value", string.Empty, 24, RetroUi.OrangeBright, TextAnchor.MiddleRight); // 값
            RetroUi.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, y), new Vector2(140f, 40f)); // 오른쪽
            return slider; // 반환
        }

        private static RetroHover ButtonRow(RectTransform window, string label, float y, Action onClick) // 누르면 바뀌는 줄
        {
            RowLabel(window, label, y); // 이름
            Button button = RetroUi.BoxButton(window, label, string.Empty, 22, onClick); // 버튼
            RetroUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(300f, y), new Vector2(360f, 46f)); // 위치
            return button.GetComponent<RetroHover>(); // 글자 변경용
        }

        private static void RowLabel(RectTransform window, string label, float y) // 줄 이름
        {
            Text text = RetroUi.Label(window, label + "_Label", label, 26, RetroUi.Orange, TextAnchor.MiddleLeft); // 이름
            RetroUi.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, y), new Vector2(240f, 40f)); // 왼쪽
        }

        private void CycleResolution() // 다음 해상도
        {
            IReadOnlyList<Vector2Int> list = GameSettings.AvailableResolutions(); // 목록
            Vector2Int current = GameSettings.Resolution; // 현재
            int index = 0; // 다음 위치

            for (int i = 0; i < list.Count; i++) // 현재보다 큰 첫 해상도
            {
                if (list[i] == current) // 현재
                {
                    index = (i + 1) % list.Count; // 다음
                    break; // 종료
                }
            }

            GameSettings.Resolution = list[index]; // 적용
            RefreshValues(); // 표시
        }

        private void RefreshValues() // 현재 값 표시
        {
            sensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity); // 감도
            sensitivityValue.text = $"x{GameSettings.LookSensitivity:0.00}"; // 감도 값
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume); // 음량
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%"; // 음량 값
            fullscreenHover.SetText(GameSettings.Fullscreen ? "[ 켜짐 ]" : "[ 꺼짐 ]"); // 전체 화면
            Vector2Int size = GameSettings.Resolution; // 해상도
            resolutionHover.SetText($"{size.x} x {size.y} ▸"); // 해상도
            vSyncHover.SetText(GameSettings.VSync ? "[ 켜짐 ]" : "[ 꺼짐 ]"); // 수직 동기화
        }
    }

    public sealed class RetroDialog : MonoBehaviour // 예/아니오 확인 창
    {
        private Text messageLabel; // 문구
        private RetroHover confirmHover; // 확인 버튼 글자
        private RetroHover cancelHover; // 취소 버튼 글자
        private Action onConfirm; // 확인
        private Action onCancel; // 취소

        public bool IsOpen => gameObject.activeSelf; // 열림 여부

        public static RetroDialog Create(Transform canvas) // 생성 (처음엔 닫힘)
        {
            RectTransform root = RetroUi.Rect(canvas, "Dialog"); // 전체 화면
            RetroDialog dialog = root.gameObject.AddComponent<RetroDialog>(); // 창
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면 (클릭 차단)
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 300f)); // 가운데
            RetroUi.Solid(window, "Fill", RetroUi.Backdrop, true); // 바탕
            RetroUi.Frame(window, RetroUi.Red, 3f); // 테두리
            dialog.messageLabel = RetroUi.Label(window, "Message", string.Empty, 26, RetroUi.Orange, TextAnchor.MiddleCenter); // 문구
            RetroUi.Stretch(dialog.messageLabel.rectTransform, 40f, 40f, 30f, 110f); // 위쪽
            dialog.messageLabel.horizontalOverflow = HorizontalWrapMode.Wrap; // 줄바꿈
            Button confirm = RetroUi.BoxButton(window, "Confirm", string.Empty, 24, dialog.Confirm); // 확인
            RetroUi.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(-20f, 60f), new Vector2(240f, 54f)); // 왼쪽
            Button cancel = RetroUi.BoxButton(window, "Cancel", string.Empty, 24, dialog.Cancel); // 취소
            RetroUi.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(20f, 60f), new Vector2(240f, 54f)); // 오른쪽
            dialog.confirmHover = confirm.GetComponent<RetroHover>(); // 글자
            dialog.cancelHover = cancel.GetComponent<RetroHover>(); // 글자
            root.gameObject.SetActive(false); // 닫힘
            return dialog; // 반환
        }

        public void Show(string message, string confirmText, Action confirmed, string cancelText = "취소", Action cancelled = null) // 열기
        {
            messageLabel.text = message; // 문구
            confirmHover.SetText(confirmText); // 확인 글자
            cancelHover.SetText(cancelText); // 취소 글자
            onConfirm = confirmed; // 확인
            onCancel = cancelled; // 취소
            gameObject.SetActive(true); // 표시
            transform.SetAsLastSibling(); // 맨 앞
        }

        public void Confirm() // 확인
        {
            Action callback = onConfirm; // 콜백
            Hide(); // 닫기
            callback?.Invoke(); // 실행
        }

        public void Cancel() // 취소
        {
            Action callback = onCancel; // 콜백
            Hide(); // 닫기
            callback?.Invoke(); // 실행
        }

        private void Hide() // 닫기
        {
            onConfirm = null; // 정리
            onCancel = null; // 정리
            gameObject.SetActive(false); // 숨김
        }
    }
}
