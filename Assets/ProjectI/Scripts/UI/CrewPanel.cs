using System.Collections.Generic; // 목록
using System.Text; // 목록 비교
using ProjectI.Net; // 협동 세션
using ProjectI.Net.Voice; // 43일차: 음성
using ProjectI.Settings; // 음성 설정
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class CrewPanel : MonoBehaviour // 42~43일차: Esc 화면 오른쪽 원정대원 패널 (목록·지연·말하기 · 음량·음소거 · 방장: 내보내기·차단·방 잠그기)
    {
        private const float RowHeight = 124f; // 대원 줄 높이
        private const float RowGap = 10f; // 줄 간격
        private const float RowWidth = 820f; // 줄 폭
        private const float FirstRowTop = -100f; // 첫 줄 위치
        private const float StatusInterval = 0.2f; // 말하기·쓰러짐 갱신 간격
        private const float PingInterval = 1f; // 지연 갱신 간격
        private const float ConfirmSeconds = 3f; // 한 번 더 눌러 확인하는 시간

        private RectTransform panel; // 패널
        private RectTransform rows; // 줄 묶음
        private Text titleLabel; // 제목
        private Text statusLabel; // 안내 줄
        private Button lockButton; // 방 잠그기
        private RetroHover lockHover; // 잠금 글자
        private readonly Dictionary<ulong, RowRefs> rowRefs = new Dictionary<ulong, RowRefs>(); // 줄 참조 (제자리 갱신)
        private string shownSignature = string.Empty; // 표시 중인 목록
        private string armedAction = string.Empty; // 확인 대기 중인 동작 (번호|종류)
        private float armedUntil; // 확인 대기 끝
        private float nextStatus; // 다음 상태 갱신
        private float nextPing; // 다음 지연 갱신

        private sealed class RowRefs // 한 줄에서 바뀌는 글자·막대
        {
            public string VoiceKey; // 음량 저장 키
            public Text State; // 말하는 중·쓰러짐
            public Text Ping; // 지연
            public RectTransform Meter; // 내 마이크 막대
            public RetroHover MicHover; // 내 마이크 버튼
            public Slider Volume; // 음량
            public Text VolumeValue; // 음량 값
            public RetroHover MuteHover; // 음소거 버튼
        }

        public bool IsShown => panel != null && panel.gameObject.activeSelf; // 표시 여부
        public int ShownCount => rowRefs.Count; // 검증용

        public static CrewPanel Create(Transform canvas) // 생성 (처음엔 숨김)
        {
            RectTransform root = RetroUi.Rect(canvas, "CrewPanel"); // 영역
            RetroUi.Place(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, -20f), new Vector2(880f, 820f)); // 화면 오른쪽
            CrewPanel crew = root.gameObject.AddComponent<CrewPanel>(); // 패널
            crew.panel = root; // 참조
            crew.Build(root); // 구성
            root.gameObject.SetActive(false); // 숨김
            return crew; // 반환
        }

        public void Show() // 표시 (협동 중일 때만)
        {
            if (!NetworkSession.IsOnline) // 혼자 하기
            {
                Hide(); // 숨김
                return; // 종료
            }

            panel.gameObject.SetActive(true); // 표시
            armedAction = string.Empty; // 확인 초기화
            statusLabel.text = NetworkSession.IsHost ? "내보내기·차단은 한 번 더 눌러야 실행됩니다" : "음량·음소거는 내 화면에만 적용됩니다"; // 안내
            NetworkSession.CrewChanged -= Rebuild; // 중복 방지
            NetworkSession.CrewChanged += Rebuild; // 목록 변화
            Rebuild(); // 목록
        }

        public void Hide() // 숨김
        {
            NetworkSession.CrewChanged -= Rebuild; // 해제

            if (panel != null) // 있음
            {
                panel.gameObject.SetActive(false); // 숨김
            }

            PlayerPrefs.Save(); // 음량 기록
        }

        private void OnDestroy() // 파괴
        {
            NetworkSession.CrewChanged -= Rebuild; // 해제
        }

        private void Update() // 제자리 갱신
        {
            if (!NetworkSession.IsOnline) // 연결 끊김
            {
                Hide(); // 숨김
                return; // 종료
            }

            UpdateMeter(); // 내 마이크 막대 (매 프레임)

            if (Time.unscaledTime < nextStatus) // 간격
            {
                return; // 생략
            }

            nextStatus = Time.unscaledTime + StatusInterval; // 다음

            if (armedAction.Length > 0 && Time.unscaledTime > armedUntil) // 확인 시간 지남
            {
                armedAction = string.Empty; // 취소
                Rebuild(); // 버튼 글자 복구
                return; // 종료
            }

            List<NetworkSession.CrewEntry> crew = NetworkSession.GetCrew(); // 목록

            if (Signature(crew) != shownSignature) // 대원이 바뀜
            {
                Rebuild(); // 다시 그리기
                return; // 종료
            }

            bool pingTick = Time.unscaledTime >= nextPing; // 지연 갱신
            nextPing = pingTick ? Time.unscaledTime + PingInterval : nextPing; // 다음

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                if (!rowRefs.TryGetValue(entry.ClientId, out RowRefs refs)) // 줄 없음
                {
                    continue; // 다음
                }

                NetPlayerAvatar avatar = NetPlayerAvatar.Find(entry.ClientId); // 몸체
                refs.State.text = StateText(avatar); // 말하기·쓰러짐
                refs.State.color = avatar != null && avatar.IsDeadRemote ? RetroUi.Red : RetroUi.Green; // 색

                if (pingTick) // 지연
                {
                    refs.Ping.text = PingText(entry); // 글자
                    refs.Ping.color = ServerListFilter.PingColor(entry.PingMs); // 색
                }

                if (refs.MicHover != null) // 내 줄
                {
                    refs.MicHover.SetText(MicText()); // 마이크 버튼
                }
            }
        }

        private void Build(RectTransform root) // 패널 구성
        {
            RetroUi.Solid(root, "Fill", new Color(0.03f, 0.02f, 0.015f, 0.92f), true); // 바탕 (클릭 받음)
            RetroUi.Frame(root, RetroUi.Orange, 3f); // 테두리

            titleLabel = RetroUi.Label(root, "Title", "원정대원", 34, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, -48f), new Vector2(420f, 56f)); // 왼쪽 위

            lockButton = RetroUi.BoxButton(root, "Lock", string.Empty, 22, ToggleLock); // 방 잠그기
            RetroUi.Place((RectTransform)lockButton.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-30f, -48f), new Vector2(240f, 46f)); // 오른쪽 위
            lockHover = lockButton.GetComponent<RetroHover>(); // 글자

            Image divider = RetroUi.Solid(root, "Divider", RetroUi.OrangeDim); // 구분선
            RetroUi.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(RowWidth, 2f)); // 제목 아래

            rows = RetroUi.Rect(root, "Rows"); // 줄 묶음

            statusLabel = RetroUi.Label(root, "Status", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(30f, 36f), new Vector2(820f, 30f)); // 아래
        }

        private void Rebuild() // 목록 다시 그리기
        {
            if (!IsShown) // 숨김
            {
                return; // 생략
            }

            for (int index = rows.childCount - 1; index >= 0; index--) // 비우기
            {
                Transform child = rows.GetChild(index); // 줄
                child.SetParent(null, false); // 즉시 빠짐
                Destroy(child.gameObject); // 제거
            }

            rowRefs.Clear(); // 참조
            List<NetworkSession.CrewEntry> crew = NetworkSession.GetCrew(); // 목록
            bool host = NetworkSession.IsHost; // 방장
            shownSignature = Signature(crew); // 표시 목록
            titleLabel.text = $"원정대원  {crew.Count}/{NetworkSession.MaxPlayers}"; // 제목
            lockButton.gameObject.SetActive(host); // 방장만
            lockHover.SetText(NetworkSession.RoomLocked ? "방 잠김 [X]" : "방 잠그기 [ ]"); // 잠금
            float top = FirstRowTop; // 줄 위치

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                CreateRow(entry, host, top); // 한 줄
                top -= RowHeight + RowGap; // 다음
            }

            for (int empty = crew.Count; empty < NetworkSession.MaxPlayers; empty++) // 빈 자리
            {
                CreateEmptyRow(top); // 빈 줄
                top -= 60f + RowGap; // 다음
            }

            nextPing = 0f; // 지연 바로 갱신
            nextStatus = 0f; // 상태 바로 갱신
        }

        private RectTransform RowFrame(string name, float top, float height, Color fill, Color border) // 줄 틀
        {
            RectTransform row = RetroUi.Rect(rows, name); // 줄
            RetroUi.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, top), new Vector2(RowWidth, height)); // 위치
            RetroUi.Solid(row, "Fill", fill); // 바탕
            RetroUi.Frame(row, border, 2f); // 테두리
            return row; // 반환
        }

        private void CreateRow(NetworkSession.CrewEntry entry, bool host, float top) // 대원 한 줄 (두 단)
        {
            NetPlayerAvatar avatar = NetPlayerAvatar.Find(entry.ClientId); // 몸체
            string key = avatar == null ? VoicePreferences.KeyOf(0, entry.Name) : avatar.VoiceKey; // 음량 키
            bool muted = !entry.IsSelf && VoicePreferences.IsMuted(key); // 음소거
            RectTransform row = RowFrame($"Crew_{entry.ClientId}", top, RowHeight, new Color(RetroUi.EntryFill.r, RetroUi.EntryFill.g, RetroUi.EntryFill.b, muted ? 0.35f : 0.6f), RetroUi.OrangeDim); // 틀
            RowRefs refs = new RowRefs { VoiceKey = key }; // 참조

            string role = entry.IsHost ? "  [방장]" : string.Empty; // 역할
            string self = entry.IsSelf ? "  (나)" : string.Empty; // 나
            Text name = RetroUi.Label(row, "Name", $"{entry.Name}{role}{self}", 26, muted ? RetroUi.Disabled : RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 이름
            RetroUi.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -32f), new Vector2(470f, 40f)); // 윗단 왼쪽
            refs.State = RetroUi.Label(row, "State", string.Empty, 20, RetroUi.Green, TextAnchor.MiddleRight); // 말하기·쓰러짐
            RetroUi.Place(refs.State.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-130f, -32f), new Vector2(200f, 40f)); // 윗단 오른쪽
            refs.Ping = RetroUi.Label(row, "Ping", PingText(entry), 20, ServerListFilter.PingColor(entry.PingMs), TextAnchor.MiddleRight); // 지연
            RetroUi.Place(refs.Ping.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, -32f), new Vector2(100f, 40f)); // 오른쪽 끝

            if (entry.IsSelf) // 내 줄: 마이크 막대 + 켜기·끄기
            {
                Text mic = RetroUi.Label(row, "MicLabel", "내 마이크", 20, RetroUi.Orange, TextAnchor.MiddleLeft); // 이름
                RetroUi.Place(mic.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -88f), new Vector2(110f, 36f)); // 아랫단
                RectTransform meter = RetroUi.Rect(row, "Meter"); // 막대
                RetroUi.Place(meter, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(130f, -88f), new Vector2(380f, 18f)); // 위치
                RetroUi.Solid(meter, "Back", new Color(0f, 0f, 0f, 0.6f)); // 바탕
                refs.Meter = RetroUi.Solid(meter, "Fill", RetroUi.Green).rectTransform; // 채움
                refs.Meter.anchorMax = new Vector2(0f, 1f); // 0
                RetroUi.Frame(meter, RetroUi.OrangeDim, 1f); // 테두리
                Button toggle = RetroUi.BoxButton(row, "MicToggle", MicText(), 20, ToggleMic); // 마이크 켜기·끄기
                RetroUi.Place((RectTransform)toggle.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(526f, -88f), new Vector2(276f, 40f)); // 오른쪽
                refs.MicHover = toggle.GetComponent<RetroHover>(); // 글자
                rowRefs[entry.ClientId] = refs; // 등록
                return; // 종료
            }

            Text volumeTitle = RetroUi.Label(row, "VolumeLabel", "음량", 20, RetroUi.Orange, TextAnchor.MiddleLeft); // 음량
            RetroUi.Place(volumeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -88f), new Vector2(60f, 36f)); // 아랫단
            refs.Volume = RetroUi.SliderBar(row, "Volume", 0f, VoicePreferences.MaxPlayerVolume, VoicePreferences.GetVolume(key)); // 슬라이더
            RetroUi.Place((RectTransform)refs.Volume.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(80f, -88f), new Vector2(230f, 30f)); // 위치
            refs.Volume.interactable = !muted; // 음소거 중 잠금
            refs.VolumeValue = RetroUi.Label(row, "VolumeValue", muted ? "음소거" : Percent(refs.Volume.value), 18, muted ? RetroUi.Disabled : RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 값
            RetroUi.Place(refs.VolumeValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(318f, -88f), new Vector2(80f, 36f)); // 위치
            refs.Volume.onValueChanged.AddListener(value => { VoicePreferences.SetVolume(key, value); refs.VolumeValue.text = Percent(value); }); // 저장

            Button mute = RetroUi.BoxButton(row, "Mute", muted ? "소리 켜기" : "음소거", 20, () => ToggleMute(key)); // 음소거
            RetroUi.Place((RectTransform)mute.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(404f, -88f), new Vector2(122f, 40f)); // 위치
            refs.MuteHover = mute.GetComponent<RetroHover>(); // 글자
            rowRefs[entry.ClientId] = refs; // 등록

            if (!host) // 방장만 관리 버튼
            {
                return; // 종료
            }

            ulong clientId = entry.ClientId; // 캡처
            string kickKey = $"{clientId}|kick"; // 내보내기 확인 키
            string banKey = $"{clientId}|ban"; // 차단 확인 키
            Button kick = RetroUi.BoxButton(row, "Kick", armedAction == kickKey ? "확인?" : "내보내기", 20, () => Arm(kickKey, () => NetworkSession.Kick(clientId, false))); // 내보내기
            RetroUi.Place((RectTransform)kick.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(536f, -88f), new Vector2(136f, 40f)); // 위치
            Button ban = RetroUi.BoxButton(row, "Ban", armedAction == banKey ? "확인?" : "차단", 20, () => Arm(banKey, () => NetworkSession.Kick(clientId, true))); // 차단
            RetroUi.Place((RectTransform)ban.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(682f, -88f), new Vector2(120f, 40f)); // 위치
            TintDanger(kick, armedAction == kickKey); // 빨강
            TintDanger(ban, armedAction == banKey); // 빨강
        }

        private void CreateEmptyRow(float top) // 빈 자리
        {
            RectTransform row = RowFrame("Empty", top, 60f, new Color(0f, 0f, 0f, 0.2f), new Color(RetroUi.OrangeDim.r, RetroUi.OrangeDim.g, RetroUi.OrangeDim.b, 0.5f)); // 흐린 틀
            string share = NetworkSession.ShareCode; // 코드·주소
            string hint = NetworkSession.RoomLocked ? "빈 자리 — 방이 잠겨 있습니다" : string.IsNullOrEmpty(share) ? "빈 자리" : $"빈 자리 — {(NetworkSession.ShareIsCode ? "코드" : "주소")} {share}"; // 안내
            RetroUi.Label(row, "Hint", hint, 20, RetroUi.OrangeDim, TextAnchor.MiddleCenter); // 글자
        }

        private static void TintDanger(Button button, bool armed) // 내보내기·차단 빨간 표시
        {
            Text label = button.GetComponentInChildren<Text>(); // 글자

            if (label != null) // 있음
            {
                label.color = armed ? Color.white : new Color(1f, 0.42f, 0.37f); // 색
            }

            Image fill = button.GetComponent<Image>(); // 바탕

            if (fill != null && armed) // 확인 대기
            {
                fill.color = RetroUi.Red; // 빨강
            }
        }

        private void UpdateMeter() // 내 마이크 막대
        {
            foreach (RowRefs refs in rowRefs.Values) // 줄
            {
                if (refs.Meter != null) // 내 줄
                {
                    bool live = GameSettings.VoiceEnabled && !VoiceCapture.SelfMuted; // 마이크 사용 중
                    refs.Meter.anchorMax = new Vector2(live ? Mathf.Clamp01(VoiceCapture.InputLevel) : 0f, 1f); // 크기
                    return; // 하나뿐
                }
            }
        }

        private static string StateText(NetPlayerAvatar avatar) // 말하기·쓰러짐 글자
        {
            if (avatar == null) // 몸체 없음
            {
                return string.Empty; // 없음
            }

            if (avatar.IsDeadRemote) // 쓰러짐
            {
                return "쓰러짐"; // 글자
            }

            return avatar.IsSpeaking ? "♪ 말하는 중" : string.Empty; // 말하기
        }

        private static string MicText() // 내 마이크 버튼 글자
        {
            if (!GameSettings.VoiceEnabled) // 설정에서 끔
            {
                return "음성 꺼짐 (설정)"; // 글자
            }

            return VoiceCapture.SelfMuted ? "마이크 켜기" : "마이크 끄기"; // 글자
        }

        private static string PingText(NetworkSession.CrewEntry entry) // 지연 글자
        {
            if (entry.IsSelf && entry.IsHost) // 방장 자신
            {
                return "방장"; // 지연 없음
            }

            return entry.PingMs > 0 ? $"{entry.PingMs}ms" : "—"; // 모르면 대시
        }

        private static string Percent(float value) // 백분율
        {
            return $"{Mathf.RoundToInt(value * 100f)}%"; // 글자
        }

        private static string Signature(List<NetworkSession.CrewEntry> crew) // 목록 비교용
        {
            StringBuilder builder = new StringBuilder(NetworkSession.RoomLocked ? "L" : "U"); // 잠금
            builder.Append(NetworkSession.IsHost ? 'H' : 'G').Append(NetworkSession.ShareCode); // 방장·코드

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                builder.Append('|').Append(entry.ClientId).Append(':').Append(entry.Name); // 번호·이름
            }

            return builder.ToString(); // 반환
        }

        private void ToggleMic() // 내 마이크 켜기·끄기
        {
            if (!GameSettings.VoiceEnabled) // 설정에서 꺼짐
            {
                statusLabel.text = "설정 > 음성 채팅을 먼저 켜 주세요"; // 안내
                return; // 종료
            }

            VoiceCapture.SelfMuted = !VoiceCapture.SelfMuted; // 전환
            statusLabel.text = VoiceCapture.SelfMuted ? "마이크를 껐습니다" : "마이크를 켰습니다"; // 안내
            nextStatus = 0f; // 바로 갱신
        }

        private void ToggleMute(string key) // 대원 음소거 전환 (내 화면만)
        {
            VoicePreferences.SetMuted(key, !VoicePreferences.IsMuted(key)); // 전환
            Rebuild(); // 줄 색·버튼
        }

        private void Arm(string key, System.Action action) // 두 번 눌러 실행 (실수 방지)
        {
            if (armedAction == key && Time.unscaledTime <= armedUntil) // 확인
            {
                armedAction = string.Empty; // 초기화
                action?.Invoke(); // 실행
                statusLabel.text = "처리했습니다"; // 안내
            }
            else
            {
                armedAction = key; // 대기
                armedUntil = Time.unscaledTime + ConfirmSeconds; // 시간
                statusLabel.text = "한 번 더 누르면 실행됩니다 (3초)"; // 안내
            }

            Rebuild(); // 버튼 글자
        }

        private void ToggleLock() // 방 잠그기 전환
        {
            NetworkSession.SetRoomLocked(!NetworkSession.RoomLocked); // 전환
            statusLabel.text = NetworkSession.RoomLocked ? "새 입장을 막았습니다 (이미 들어왔던 대원은 다시 연결 가능)" : "새 입장을 허용합니다"; // 안내
            Rebuild(); // 갱신
        }
    }
}
