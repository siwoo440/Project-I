using System.Collections.Generic; // 원정대원 목록
using System.Text; // 글자 조립
using ProjectI.Diagnostics; // F1 디버그 페이지
using ProjectI.Loop; // 맵 로더
using ProjectI.Net.Steam; // Steam 로비
using ProjectI.Net.Voice; // 음성
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed class NetDebugPage : DebugPageProvider // 44일차: F1 협동 점검 페이지 + 60초마다 Player.log 요약 한 줄
    {
        private const float SummaryInterval = 60f; // 요약 기록 간격
        private static NetDebugPage instance; // 실행 중
        private float nextSummary; // 다음 요약

        public override string PageName => "Coop"; // 페이지 이름
        public override int SortOrder => 90; // 뒤쪽

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 게임 시작 시
        private static void Boot() // 유지 오브젝트 생성 (F1 페이지 · 자동 시험)
        {
            if (instance != null) // 이미 있음
            {
                return; // 생략
            }

            GameObject runner = new GameObject("===ProjectI Coop Diagnostics==="); // 오브젝트
            DontDestroyOnLoad(runner); // 유지
            instance = runner.AddComponent<NetDebugPage>(); // F1 페이지
            CoopAutoTest.AttachIfRequested(runner); // 실행 인자에 자동 시험이 있으면 추가
        }

        private void Update() // 요약 기록
        {
            if (!NetworkSession.IsOnline || Time.unscaledTime < nextSummary) // 혼자·간격
            {
                return; // 생략
            }

            nextSummary = Time.unscaledTime + SummaryInterval; // 다음
            Debug.Log($"[Project I] 협동 요약 / {SummaryLine()}"); // 한 줄
        }

        public static string SummaryLine() // 한 줄 요약 (로그·자동 시험 결과)
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            string map = loader == null ? "-" : (loader.IsTransitioning ? $"{loader.CurrentDestination}→이동 중" : loader.CurrentDestination.ToString()); // 맵
            NetPlayerAvatar local = NetPlayerAvatar.Local; // 내 몸체
            int voiceIn = 0; // 받은 음성

            foreach (NetPlayerAvatar avatar in NetPlayerAvatar.All) // 대원
            {
                voiceIn += avatar == null ? 0 : avatar.VoicePacketsReceived; // 누적
            }

            string role = NetworkSession.IsHost ? "방장" : NetworkSession.IsGuest ? "참가" : "혼자"; // 역할
            return $"{role}·{NetworkSession.Transport} · 인원 {NetworkSession.PlayerCount} · 맵 {map} · 아이템 목록 {(NetItemSync.Instance == null ? 0 : NetItemSync.Instance.LastSnapshotCount)} · 몬스터 따라가기 {(NetCombatSync.Instance == null ? 0 : NetCombatSync.Instance.PuppetCount)} · 점검 {NetConsistency.Matches}/{NetConsistency.Checks} 치료 {NetConsistency.Heals} · 거절 {NetGuard.TotalRejected} 내보냄 {NetGuard.TotalKicked} · 음성 보냄 {VoiceCapture.PacketsSent} 받음 {voiceIn} 중계 {NetPlayerAvatar.VoicePacketsRelayed}{(local == null ? " · 몸체 없음" : string.Empty)}"; // 요약
        }

        public override string BuildDebugText() // F1 페이지 내용
        {
            StringBuilder builder = new StringBuilder(1024); // 글자
            builder.AppendLine("COOP / NETWORK"); // 제목
            builder.AppendLine("────────────────────────────"); // 구분선

            if (!NetworkSession.IsOnline) // 혼자
            {
                builder.AppendLine("혼자 하기 (협동 연결 없음)"); // 안내
                builder.AppendLine($"Steam : {(SteamService.Initialized ? $"연결됨 · {SteamService.PersonaName}" : $"미연결 ({SteamService.FailureReason})")}"); // Steam
                return builder.ToString(); // 반환
            }

            builder.AppendLine($"역할   : {(NetworkSession.IsHost ? "방장" : "참가자")} · {NetworkSession.Transport} · 접속 버전 {NetworkSession.ProtocolTag}"); // 역할
            builder.AppendLine($"방     : {(string.IsNullOrEmpty(NetworkSession.ShareCode) ? "-" : NetworkSession.ShareCode)}{(NetworkSession.RoomLocked ? " · 잠김" : string.Empty)}{(SteamLobbyService.InLobby ? " · Steam 로비" : string.Empty)}"); // 방
            builder.AppendLine(); // 여백
            builder.AppendLine("[원정대원]"); // 제목
            List<NetworkSession.CrewEntry> crew = NetworkSession.GetCrew(); // 목록

            foreach (NetworkSession.CrewEntry entry in crew) // 대원
            {
                NetPlayerAvatar avatar = NetPlayerAvatar.Find(entry.ClientId); // 몸체
                string position = avatar != null && avatar.TryGetNetworkWorldPosition(out Vector3 world) ? $"({world.x:0},{world.y:0},{world.z:0})" : "(다른 맵)"; // 위치
                string state = avatar == null ? "몸체 없음" : $"{(avatar.IsDeadRemote ? "쓰러짐" : "생존")}{(avatar.IsAboard ? " · 탑승" : string.Empty)}{(avatar.IsSpeaking ? " · 말하는 중" : string.Empty)}"; // 상태
                builder.AppendLine($" {entry.ClientId + 1}. {entry.Name}{(entry.IsHost ? " [방장]" : string.Empty)}{(entry.IsSelf ? " (나)" : string.Empty)}  {(entry.PingMs > 0 ? entry.PingMs + "ms" : "-")}  {state}  {position}"); // 한 줄
            }

            builder.AppendLine(); // 여백
            builder.AppendLine("[동기화]"); // 제목
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            NetWorldState world2 = NetWorldState.Instance; // 월드 상태
            builder.AppendLine($"맵     : 내 {(loader == null ? "-" : loader.CurrentDestination.ToString())}{(loader != null && loader.IsTransitioning ? " (이동 중)" : string.Empty)} · 방장 목적지 {(world2 == null ? "-" : world2.Destination.ToString())} · 일차 {(world2 == null ? 0 : world2.Day)}"); // 맵
            builder.AppendLine($"아이템 : 마지막 목록 {(NetItemSync.Instance == null ? 0 : NetItemSync.Instance.LastSnapshotCount)}개"); // 아이템
            builder.AppendLine($"전투   : 몬스터 따라가기 {(NetCombatSync.Instance == null ? 0 : NetCombatSync.Instance.PuppetCount)}"); // 전투
            CoopSnapshot mine = NetConsistency.Capture(); // 내 화면 요약
            builder.AppendLine($"내 화면: 바닥 아이템 {mine.ItemCount} · 장치·문 {mine.DeviceCount} · 체력 대상 {mine.HealthCount} · 해시 {mine.ItemHash:X8}/{mine.DeviceHash:X8}/{mine.HealthHash:X8}"); // 요약
            builder.AppendLine($"점검   : 일치 {NetConsistency.Matches}/{NetConsistency.Checks} · 불일치 {NetConsistency.Mismatches} · 치료 {NetConsistency.Heals}"); // 점검
            builder.AppendLine($"         {NetConsistency.LastResult}"); // 마지막
            builder.AppendLine(); // 여백
            builder.AppendLine("[보안·음성]"); // 제목
            builder.AppendLine($"거절 {NetGuard.TotalRejected} · 자동 내보냄 {NetGuard.TotalKicked}"); // 보안
            builder.AppendLine($"음성 {(Settings.GameSettings.VoiceEnabled ? "켜짐" : "꺼짐")} · {(VoiceCapture.UsingSteam ? "Steam" : "유니티 마이크")} · 보냄 {VoiceCapture.PacketsSent} · 방장 중계 {NetPlayerAvatar.VoicePacketsRelayed}"); // 음성
            return builder.ToString(); // 반환
        }
    }
}
