using System; // 콜백
using System.Collections.Generic; // 목록
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    [Serializable]
    public struct ServerListing // 서버(로비) 한 개
    {
        public string Id; // 식별자
        public string Name; // 이름
        public int Players; // 현재 인원
        public int MaxPlayers; // 최대 인원
        public bool ChallengeMode; // 도전 원정 서버
        public int PingMs; // 지연 시간
        public string Region; // 지역

        public bool IsFull => Players >= MaxPlayers; // 가득 참
    }

    public enum ServerSortMode // 정렬 방식
    {
        Worldwide, // 전 세계 (받은 순서)
        Nearest, // 가까운 순 (지연 시간)
        MostPlayers, // 인원 많은 순
        Name, // 이름순
    }

    public interface IServerListProvider // 서버 목록 공급자 (멀티플레이 일차에 Steam 로비로 교체)
    {
        bool IsOnline { get; } // 실제 온라인 목록인지
        void Request(Action<IReadOnlyList<ServerListing>> onResult); // 목록 요청
        void Join(ServerListing listing, Action<bool, string> onResult); // 참가 요청 (성공 여부, 안내 문구)
    }

    public sealed class SampleServerListProvider : IServerListProvider // 온라인 연결 전 화면 확인용 예시 목록
    {
        private static readonly string[] Names = // 예시 이름
        {
            "사무소 3호점", "korea Rebang", "[MODDED] 지하묘지 하드", "원정대 모집중~", "Going to 수문도시 (Korea)",
            "y'all good", "Bossome09's Crew", "초보 환영 1일차부터", "붉은맥 폐광 파밍", "조용히 할 사람",
            "Night Shift", "마차 타고 떠나요", "보스 잡으러 감", "EU casual", "새벽 원정",
        };

        private static readonly string[] Regions = { "KR", "KR", "KR", "JP", "US", "EU" }; // 예시 지역

        public bool IsOnline => false; // 예시 목록

        public void Request(Action<IReadOnlyList<ServerListing>> onResult) // 매번 조금씩 다른 목록
        {
            List<ServerListing> result = new List<ServerListing>(); // 결과
            System.Random random = new System.Random(Environment.TickCount); // 새로고침마다 다름

            for (int index = 0; index < Names.Length; index++) // 이름마다
            {
                if (random.NextDouble() < 0.15) // 일부는 목록에서 빠짐
                {
                    continue; // 생략
                }

                string region = Regions[random.Next(Regions.Length)]; // 지역
                result.Add(new ServerListing
                {
                    Id = $"sample-{index}", // 식별자
                    Name = Names[index], // 이름
                    MaxPlayers = 4, // 최대 4명
                    Players = random.Next(1, 5), // 1~4명
                    ChallengeMode = index % 4 == 2, // 일부 도전 원정
                    Region = region, // 지역
                    PingMs = region == "KR" ? random.Next(8, 60) : random.Next(90, 280), // 지연
                });
            }

            onResult?.Invoke(result); // 전달
        }

        public void Join(ServerListing listing, Action<bool, string> onResult) // 참가 (아직 연결 안 됨)
        {
            string message = listing.IsFull ? $"'{listing.Name}' 서버가 가득 찼습니다." : $"'{listing.Name}' — 온라인 참가는 멀티플레이 일차에 연결됩니다. (지금은 예시 목록)"; // 안내
            onResult?.Invoke(false, message); // 실패 안내
        }
    }

    public static class ServerListFilter // 검색·도전 원정·정렬
    {
        public static List<ServerListing> Apply(IEnumerable<ServerListing> source, string search, bool includeChallenge, ServerSortMode sort) // 적용
        {
            List<ServerListing> result = new List<ServerListing>(); // 결과
            string keyword = string.IsNullOrWhiteSpace(search) ? null : search.Trim(); // 검색어

            foreach (ServerListing listing in source) // 원본
            {
                if (!includeChallenge && listing.ChallengeMode) // 도전 원정 제외
                {
                    continue; // 생략
                }

                if (keyword != null && (listing.Name == null || listing.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)) // 이름 검색
                {
                    continue; // 생략
                }

                result.Add(listing); // 추가
            }

            switch (sort) // 정렬
            {
                case ServerSortMode.Nearest:
                    result.Sort((a, b) => a.PingMs.CompareTo(b.PingMs)); // 지연 짧은 순
                    break;
                case ServerSortMode.MostPlayers:
                    result.Sort((a, b) => b.Players != a.Players ? b.Players.CompareTo(a.Players) : string.CompareOrdinal(a.Name, b.Name)); // 인원 많은 순
                    break;
                case ServerSortMode.Name:
                    result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase)); // 이름순
                    break;
            }

            return result; // 반환
        }

        public static string Describe(ServerSortMode sort) // 표시 이름
        {
            switch (sort)
            {
                case ServerSortMode.Nearest: return "가까운 순";
                case ServerSortMode.MostPlayers: return "인원 많은 순";
                case ServerSortMode.Name: return "이름순";
                default: return "전 세계";
            }
        }

        public static Color PingColor(int pingMs) // 지연 색
        {
            return pingMs < 70 ? RetroUi.Green : pingMs < 150 ? RetroUi.OrangeBright : RetroUi.Red; // 좋음·보통·나쁨
        }
    }
}
