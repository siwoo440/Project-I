using System; // 콜백
using System.Collections.Generic; // 해상도 목록
using ProjectI.Net.Voice; // 43일차: 마이크 시험
using ProjectI.Settings; // 게임 설정
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class SettingsPanel : MonoBehaviour // 설정 창 (메인 메뉴·일시정지 공용)
    {
        private const float RowHeight = 58f; // 줄 높이
        private Slider sensitivitySlider; // 감도
        private Text sensitivityValue; // 감도 값
        private Slider volumeSlider; // 음량
        private Text volumeValue; // 음량 값
        private Slider musicSlider; // 음악
        private Text musicValue; // 음악 값
        private Slider sfxSlider; // 효과음
        private Text sfxValue; // 효과음 값
        private Slider ambienceSlider; // 환경음
        private Text ambienceValue; // 환경음 값
        private RetroHover fullscreenHover; // 전체 화면
        private RetroHover resolutionHover; // 해상도
        private RetroHover vSyncHover; // 수직 동기화
        private RetroHover voiceHover; // 43일차: 음성 켜기
        private RetroHover talkModeHover; // 말하기 방식
        private Slider voiceVolumeSlider; // 음성 음량
        private Text voiceVolumeValue; // 값
        private Slider micGainSlider; // 마이크 음량
        private Text micGainValue; // 값
        private Slider thresholdSlider; // 감지 기준
        private Text thresholdValue; // 값
        private RetroHover deviceHover; // 마이크 장치
        private RetroHover testHover; // 마이크 시험
        private RectTransform meterFill; // 입력 크기 막대
        private Image meterImage; // 막대 색
        private Text voiceNote; // 음성 안내
        private Action onClosed; // 닫힘 콜백
        private const float RightColumn = 800f; // 오른쪽 칸 시작

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
            VoiceCapture.LoopbackTest = false; // 43일차: 마이크 시험 종료
            gameObject.SetActive(false); // 숨김
            Action callback = onClosed; // 콜백
            onClosed = null; // 정리
            callback?.Invoke(); // 알림
        }

        private void Build(RectTransform root) // 화면 구성
        {
            RetroUi.Solid(root, "Shade", RetroUi.Shade, true); // 뒤 화면 어둡게 (클릭 차단)
            RectTransform window = RetroUi.Rect(root, "Window"); // 창
            RetroUi.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 800f)); // 가운데 (43일차: 오른쪽 음성 칸)
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
            musicSlider = SliderRow(window, "음악", y, 0f, 1f, out musicValue); // 음악
            musicSlider.onValueChanged.AddListener(value => { GameSettings.MusicVolume = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            sfxSlider = SliderRow(window, "효과음", y, 0f, 1f, out sfxValue); // 효과음
            sfxSlider.onValueChanged.AddListener(value => { GameSettings.SfxVolume = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            ambienceSlider = SliderRow(window, "환경음", y, 0f, 1f, out ambienceValue); // 환경음
            ambienceSlider.onValueChanged.AddListener(value => { GameSettings.AmbienceVolume = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            fullscreenHover = ButtonRow(window, "전체 화면", y, () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; RefreshValues(); }); // 전체 화면
            y -= RowHeight; // 다음 줄
            resolutionHover = ButtonRow(window, "해상도", y, CycleResolution); // 해상도
            y -= RowHeight; // 다음 줄
            vSyncHover = ButtonRow(window, "수직 동기화", y, () => { GameSettings.VSync = !GameSettings.VSync; RefreshValues(); }); // 수직 동기화
            BuildVoiceColumn(window); // 43일차: 음성 칸

            Text note = RetroUi.Label(window, "Note", "해상도·전체 화면은 빌드한 게임에서만 바뀝니다.", 18, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(40f, 110f), new Vector2(700f, 30f)); // 아래

            Button reset = RetroUi.BoxButton(window, "Reset", "기본값", 24, () => { GameSettings.ResetToDefaults(); RefreshValues(); }); // 기본값
            RetroUi.Place((RectTransform)reset.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-250f, 50f), new Vector2(180f, 50f)); // 오른쪽 아래
            Button close = RetroUi.BoxButton(window, "Close", "닫기", 24, Close); // 닫기
            RetroUi.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-40f, 50f), new Vector2(180f, 50f)); // 오른쪽 아래
        }

        private static Slider SliderRow(RectTransform window, string label, float y, float min, float max, out Text value, float column = 0f) // 슬라이더 줄 (column: 칸 시작 x)
        {
            RowLabel(window, label, y, column); // 이름
            Slider slider = RetroUi.SliderBar(window, label, min, max, min); // 슬라이더
            RetroUi.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(column + 300f, y), new Vector2(360f, 40f)); // 위치
            value = RetroUi.Label(window, label + "_Value", string.Empty, 24, RetroUi.OrangeBright, TextAnchor.MiddleRight); // 값
            RetroUi.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(column + 820f, y), new Vector2(140f, 40f)); // 칸 오른쪽
            return slider; // 반환
        }

        private static RetroHover ButtonRow(RectTransform window, string label, float y, Action onClick, float column = 0f) // 누르면 바뀌는 줄
        {
            RowLabel(window, label, y, column); // 이름
            Button button = RetroUi.BoxButton(window, label, string.Empty, 22, onClick); // 버튼
            RetroUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(column + 300f, y), new Vector2(360f, 46f)); // 위치
            return button.GetComponent<RetroHover>(); // 글자 변경용
        }

        private static void RowLabel(RectTransform window, string label, float y, float column = 0f) // 줄 이름
        {
            Text text = RetroUi.Label(window, label + "_Label", label, 26, RetroUi.Orange, TextAnchor.MiddleLeft); // 이름
            RetroUi.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(column + 40f, y), new Vector2(240f, 40f)); // 왼쪽
        }

        private void BuildVoiceColumn(RectTransform window) // 43일차: 음성 채팅 설정 칸
        {
            Image divider = RetroUi.Solid(window, "Divider", RetroUi.OrangeDim); // 칸 구분선
            RetroUi.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(RightColumn, -100f), new Vector2(2f, 560f)); // 세로선
            Text header = RetroUi.Label(window, "VoiceHeader", "음성 채팅", 30, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(RightColumn + 40f, -50f), new Vector2(400f, 60f)); // 위치

            float y = -130f; // 첫 줄
            voiceHover = ButtonRow(window, "음성 채팅", y, ToggleVoice, RightColumn); // 켜기
            y -= RowHeight; // 다음 줄
            talkModeHover = ButtonRow(window, "말하기 방식", y, () => { GameSettings.PushToTalk = !GameSettings.PushToTalk; RefreshValues(); }, RightColumn); // 방식
            y -= RowHeight; // 다음 줄
            voiceVolumeSlider = SliderRow(window, "음성 음량", y, 0f, GameSettings.MaxVoiceVolume, out voiceVolumeValue, RightColumn); // 재생 음량
            voiceVolumeSlider.onValueChanged.AddListener(value => { GameSettings.VoiceVolume = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            micGainSlider = SliderRow(window, "마이크 음량", y, 0f, GameSettings.MaxMicGain, out micGainValue, RightColumn); // 입력 음량
            micGainSlider.onValueChanged.AddListener(value => { GameSettings.MicGain = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            thresholdSlider = SliderRow(window, "감지 기준", y, 0f, GameSettings.MaxVoiceThreshold, out thresholdValue, RightColumn); // 항상 켜기 기준
            thresholdSlider.onValueChanged.AddListener(value => { GameSettings.VoiceThreshold = value; RefreshValues(); }); // 적용
            y -= RowHeight; // 다음 줄
            deviceHover = ButtonRow(window, "마이크", y, CycleDevice, RightColumn); // 장치
            y -= RowHeight; // 다음 줄
            testHover = ButtonRow(window, "마이크 시험", y, () => { VoiceCapture.LoopbackTest = !VoiceCapture.LoopbackTest; RefreshValues(); }, RightColumn); // 내 목소리 듣기
            y -= RowHeight; // 다음 줄
            RowLabel(window, "입력 크기", y, RightColumn); // 크기 막대
            RectTransform meter = RetroUi.Rect(window, "MicMeter"); // 막대
            RetroUi.Place(meter, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(RightColumn + 300f, y), new Vector2(360f, 22f)); // 위치
            RetroUi.Solid(meter, "Back", new Color(0f, 0f, 0f, 0.55f)); // 바탕
            meterImage = RetroUi.Solid(meter, "Fill", RetroUi.Green); // 채움
            meterFill = meterImage.rectTransform; // 크기 조절
            meterFill.anchorMax = new Vector2(0f, 1f); // 처음엔 0
            RetroUi.Frame(meter, RetroUi.OrangeDim, 2f); // 테두리

            voiceNote = RetroUi.Label(window, "VoiceNote", string.Empty, 18, RetroUi.OrangeDim, TextAnchor.UpperLeft); // 안내
            RetroUi.Place(voiceNote.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(RightColumn + 40f, 90f), new Vector2(760f, 60f)); // 아래
            voiceNote.horizontalOverflow = HorizontalWrapMode.Wrap; // 줄바꿈
        }

        private void ToggleVoice() // 음성 켜기·끄기
        {
            GameSettings.VoiceEnabled = !GameSettings.VoiceEnabled; // 전환

            if (!GameSettings.VoiceEnabled) // 끔
            {
                VoiceCapture.LoopbackTest = false; // 시험도 끔
            }

            RefreshValues(); // 표시
        }

        private void CycleDevice() // 다음 마이크 장치 (빈칸 = 기본)
        {
            string[] devices = VoiceCapture.Devices; // 목록
            string current = GameSettings.MicDevice; // 현재
            int index = Array.IndexOf(devices, current); // 위치 (기본이면 -1)
            GameSettings.MicDevice = index + 1 < devices.Length ? devices[index + 1] : string.Empty; // 다음 (끝이면 기본)
            RefreshValues(); // 표시
        }

        private void Update() // 43일차: 마이크 입력 크기 막대
        {
            if (meterFill == null) // 준비 전
            {
                return; // 생략
            }

            float level = Mathf.Clamp01(VoiceCapture.InputLevel); // 크기
            meterFill.anchorMax = new Vector2(level, 1f); // 막대
            bool pass = GameSettings.PushToTalk || level / 3f >= GameSettings.VoiceThreshold; // 항상 켜기: 기준을 넘는지 (표시 크기는 3배)
            meterImage.color = pass ? RetroUi.Green : RetroUi.OrangeDim; // 색
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

        private static string Percent(float value) // 백분율
        {
            return $"{Mathf.RoundToInt(value * 100f)}%"; // 표시
        }

        private void RefreshValues() // 현재 값 표시
        {
            sensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity); // 감도
            sensitivityValue.text = $"x{GameSettings.LookSensitivity:0.00}"; // 감도 값
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume); // 음량
            volumeValue.text = Percent(GameSettings.MasterVolume); // 음량 값
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume); // 음악
            musicValue.text = Percent(GameSettings.MusicVolume); // 음악 값
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume); // 효과음
            sfxValue.text = Percent(GameSettings.SfxVolume); // 효과음 값
            ambienceSlider.SetValueWithoutNotify(GameSettings.AmbienceVolume); // 환경음
            ambienceValue.text = Percent(GameSettings.AmbienceVolume); // 환경음 값
            fullscreenHover.SetText(GameSettings.Fullscreen ? "[ 켜짐 ]" : "[ 꺼짐 ]"); // 전체 화면
            Vector2Int size = GameSettings.Resolution; // 해상도
            resolutionHover.SetText($"{size.x} x {size.y} ▸"); // 해상도
            vSyncHover.SetText(GameSettings.VSync ? "[ 켜짐 ]" : "[ 꺼짐 ]"); // 수직 동기화
            voiceHover.SetText(GameSettings.VoiceEnabled ? "[ 켜짐 ]" : "[ 꺼짐 ]"); // 43일차: 음성
            talkModeHover.SetText(GameSettings.PushToTalk ? "누르고 말하기 (V)" : "항상 켜기 (말소리 감지)"); // 방식
            voiceVolumeSlider.SetValueWithoutNotify(GameSettings.VoiceVolume); // 음성 음량
            voiceVolumeValue.text = Percent(GameSettings.VoiceVolume); // 값
            micGainSlider.SetValueWithoutNotify(GameSettings.MicGain); // 마이크 음량
            micGainValue.text = Percent(GameSettings.MicGain); // 값
            thresholdSlider.SetValueWithoutNotify(GameSettings.VoiceThreshold); // 감지 기준
            thresholdValue.text = GameSettings.PushToTalk ? "—" : Percent(GameSettings.VoiceThreshold * 5f); // 값 (0.2 = 100%)
            string device = string.IsNullOrEmpty(GameSettings.MicDevice) ? "기본 장치" : GameSettings.MicDevice; // 장치
            deviceHover.SetText((device.Length > 18 ? device.Substring(0, 18) + "…" : device) + " ▸"); // 장치 이름
            testHover.SetText(VoiceCapture.LoopbackTest ? "[ 시험 중 — 끄기 ]" : "[ 내 목소리 듣기 ]"); // 시험
            string engine = VoiceCapture.UsingSteam ? "Steam 음성 사용 — 마이크 장치·말소리 감지는 Steam 설정을 따릅니다." : "유니티 마이크 사용 (Steam 미연결)."; // 방식
            voiceNote.text = GameSettings.VoiceEnabled ? engine + "\n마이크 소리는 같은 방 가까운 원정대원에게만 전송되며 저장하지 않습니다." : "음성 채팅이 꺼져 있습니다. 켜면 마이크를 사용합니다 (녹음은 저장하지 않음)."; // 안내
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
