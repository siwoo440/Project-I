using System.Net; // 주소 확인
using System.Net.NetworkInformation; // 내 IP
using System.Net.Sockets; // IPv4 구분
using System.Security.Cryptography; // 코드 난수
using System.Text; // 코드 조립

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public enum RoomVisibility // 41일차: 방 공개 범위
    {
        Public, // 서버 목록에 표시 + 코드 입장
        CodeOnly, // 목록에서 숨김 · 코드·초대로만 입장
    }

    public static class RoomCode // 41일차: 방 코드 만들기·정리·구분 (K7Q-2MX), 직접 IP 주소 해석
    {
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 헷갈리는 0·O·1·I 제외 (32자)
        public const int Length = 6; // 코드 길이

        public static string Generate() // 새 코드 (정리된 6자리)
        {
            byte[] bytes = new byte[Length]; // 난수
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) // 암호 난수 (예측 방지)
            {
                random.GetBytes(bytes); // 채우기
            }

            StringBuilder builder = new StringBuilder(Length); // 결과

            foreach (byte value in bytes) // 글자
            {
                builder.Append(Alphabet[value % Alphabet.Length]); // 32로 나누어떨어져 치우침 없음
            }

            return builder.ToString(); // 반환
        }

        public static string Format(string code) // 표시 형식 (K7Q-2MX)
        {
            return string.IsNullOrEmpty(code) || code.Length != Length ? code ?? string.Empty : $"{code.Substring(0, 3)}-{code.Substring(3)}"; // 가운데 하이픈
        }

        public static string Normalize(string input) // 대문자 · 하이픈/공백 제거
        {
            if (string.IsNullOrEmpty(input)) // 없음
            {
                return string.Empty; // 빈 값
            }

            StringBuilder builder = new StringBuilder(input.Length); // 결과

            foreach (char character in input.Trim()) // 글자
            {
                if (character == '-' || char.IsWhiteSpace(character)) // 구분 글자
                {
                    continue; // 무시
                }

                builder.Append(char.ToUpperInvariant(character)); // 대문자
            }

            return builder.ToString(); // 반환
        }

        public static bool TryParseCode(string input, out string code) // 방 코드 형식인지 (주소 기호가 있으면 아님)
        {
            code = string.Empty; // 기본

            if (string.IsNullOrWhiteSpace(input) || input.IndexOf('.') >= 0 || input.IndexOf(':') >= 0) // 주소 모양
            {
                return false; // 코드 아님
            }

            string normalized = Normalize(input); // 정리

            if (normalized.Length != Length) // 길이
            {
                return false; // 코드 아님
            }

            foreach (char character in normalized) // 글자
            {
                if (Alphabet.IndexOf(character) < 0) // 허용 글자 아님
                {
                    return false; // 코드 아님
                }
            }

            code = normalized; // 결과
            return true; // 코드
        }

        public static bool LooksLikeCode(string input) // 주소 기호가 없는 입력 (localhost 제외) → 코드로 보고 형식 안내
        {
            if (string.IsNullOrWhiteSpace(input) || input.IndexOf('.') >= 0 || input.IndexOf(':') >= 0) // 빈칸·주소
            {
                return false; // 아님
            }

            return !string.Equals(input.Trim(), "localhost", System.StringComparison.OrdinalIgnoreCase); // 같은 컴퓨터 이름 제외
        }

        public static bool TryParseAddress(string input, ushort fallbackPort, out string host, out ushort port) // "주소" 또는 "주소:포트" (빈칸이면 같은 컴퓨터)
        {
            host = "127.0.0.1"; // 기본
            port = fallbackPort; // 기본
            string text = string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim(); // 정리

            if (text.Length == 0) // 빈칸
            {
                return true; // 같은 컴퓨터
            }

            int colon = text.LastIndexOf(':'); // 포트 구분

            if (colon > 0 && text.IndexOf(':') == colon) // IPv4·이름 + 포트 (IPv6 는 포트 칸 사용)
            {
                if (!ushort.TryParse(text.Substring(colon + 1), out ushort parsed) || parsed <= 1024) // 잘못된 포트
                {
                    return false; // 실패
                }

                port = parsed; // 포트
                text = text.Substring(0, colon); // 주소
            }

            if (text.Length == 0 || text.IndexOf(' ') >= 0) // 빈 주소·공백
            {
                return false; // 실패
            }

            host = text; // 주소 (이름 확인은 연결 모듈이 처리)
            return true; // 성공
        }

        public static string LocalIPv4() // 같은 네트워크에서 알려 줄 내 IPv4 (없으면 127.0.0.1)
        {
            try
            {
                foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces()) // 네트워크 장치
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback) // 꺼짐·루프백
                    {
                        continue; // 제외
                    }

                    foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses) // 주소
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address)) // IPv4
                        {
                            return address.Address.ToString(); // 첫 주소
                        }
                    }
                }
            }
            catch (System.Exception) // 조회 실패 (플랫폼 미지원 포함)
            {
            }

            return "127.0.0.1"; // 기본
        }
    }
}
