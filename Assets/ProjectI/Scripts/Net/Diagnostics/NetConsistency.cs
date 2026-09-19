using System.Collections.Generic; // 목록
using System.Text; // 불일치 설명
using ProjectI.Combat; // 체력
using ProjectI.Economy; // 공동 자금
using ProjectI.Items; // 아이템
using ProjectI.Loop; // 맵 로더
using ProjectI.Persistence; // 일차
using Unity.Netcode; // 직렬화
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public struct CoopSnapshot : INetworkSerializable // 44일차: 한 대원 화면의 요약 (방장과 비교)
    {
        public int Day; // 일차
        public int Funds; // 공동 자금 (-1 = 모름)
        public byte Map; // 현재 맵
        public bool Ready; // 비교 가능 (이동 중 아님 · 저장 준비됨)
        public int ItemCount; // 바닥 아이템 수
        public int ItemHash; // 바닥 아이템 ID 해시
        public int DeviceCount; // 장치·문 수
        public int DeviceHash; // 장치·문 상태 해시
        public int HealthCount; // 체력 대상 수
        public int HealthHash; // 체력 해시 (정수로 반올림)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter // 직렬화
        {
            serializer.SerializeValue(ref Day); // 일차
            serializer.SerializeValue(ref Funds); // 자금
            serializer.SerializeValue(ref Map); // 맵
            serializer.SerializeValue(ref Ready); // 준비
            serializer.SerializeValue(ref ItemCount); // 아이템 수
            serializer.SerializeValue(ref ItemHash); // 아이템 해시
            serializer.SerializeValue(ref DeviceCount); // 장치 수
            serializer.SerializeValue(ref DeviceHash); // 장치 해시
            serializer.SerializeValue(ref HealthCount); // 체력 수
            serializer.SerializeValue(ref HealthHash); // 체력 해시
        }
    }

    public static class NetConsistency // 44일차: 참가자 화면 요약을 방장 화면과 비교 → 두 번 연속 다르면 기록하고 전체 상태를 다시 보냄
    {
        public const float ReportInterval = 30f; // 참가자 보고 간격 (자동 시험은 5초)
        private static readonly Dictionary<ulong, int> strikes = new Dictionary<ulong, int>(); // 대원별 연속 불일치
        private static readonly List<string> scratchIds = new List<string>(); // 아이템 ID 정렬용
        private static readonly List<long> scratchPairs = new List<long>(); // 장치·체력 정렬용

        public static int Checks { get; private set; } // 비교 횟수
        public static int Matches { get; private set; } // 일치 횟수
        public static int Mismatches { get; private set; } // 불일치 기록 횟수 (두 번 연속)
        public static int Heals { get; private set; } // 다시 맞춘 횟수
        public static string LastResult { get; private set; } = "아직 비교하지 않음"; // 마지막 결과
        public static float Interval { get; set; } = ReportInterval; // 보고 간격

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 플레이 반복 대비
        private static void ResetStatics() // 초기화
        {
            strikes.Clear(); // 기록
            Checks = 0; // 집계
            Matches = 0; // 집계
            Mismatches = 0; // 집계
            Heals = 0; // 집계
            LastResult = "아직 비교하지 않음"; // 결과
            Interval = ReportInterval; // 간격
        }

        public static CoopSnapshot Capture() // 내 화면 요약
        {
            CoopSnapshot snapshot = new CoopSnapshot { Funds = -1 }; // 기본
            DailySnapshotService service = DailySnapshotService.Instance; // 저장
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            snapshot.Day = service == null ? 0 : service.CurrentDay; // 일차
            snapshot.Map = loader == null ? (byte)255 : (byte)loader.CurrentDestination; // 맵
            snapshot.Ready = service != null && service.IsInitialized && !service.IsRestoreInProgress && loader != null && !loader.IsTransitioning; // 비교 가능
            CampaignEconomy economy = Object.FindAnyObjectByType<CampaignEconomy>(); // 자금
            snapshot.Funds = economy == null ? -1 : economy.SharedFunds; // 자금

            scratchIds.Clear(); // 아이템

            foreach (WorldItemIdentity identity in Object.FindObjectsByType<WorldItemIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) // 활성 아이템
            {
                WorldItem item = identity.GetComponent<WorldItem>(); // 아이템
                RecoverableValue value = identity.GetComponent<RecoverableValue>(); // 판매 여부

                if (item == null || item.IsHeld || item.IsStored || string.IsNullOrEmpty(identity.InstanceId) || (value != null && value.IsSold)) // 바닥 아이템만 (손·주머니·보관 제외)
                {
                    continue; // 다음
                }

                if (item.transform.parent != null && item.transform.parent.GetComponentInParent<WorldItem>() != null) // 다른 아이템 안
                {
                    continue; // 다음
                }

                scratchIds.Add(identity.InstanceId); // 추가
            }

            scratchIds.Sort(System.StringComparer.Ordinal); // 정렬 (순서 무관 비교)
            snapshot.ItemCount = scratchIds.Count; // 수
            snapshot.ItemHash = HashStrings(scratchIds); // 해시

            scratchPairs.Clear(); // 장치·문
            List<long> healthPairs = new List<long>(); // 체력

            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) // 활성 컴포넌트
            {
                if (behaviour is INetworkDevice device && device.NetworkState >= 0) // 장치·문
                {
                    scratchPairs.Add(((long)NetCombatSync.PathHash(behaviour.transform) << 8) ^ device.NetworkState); // 경로·상태
                }

                if (behaviour is CombatHealth health) // 체력 대상 (몬스터·허수아비·부서지는 벽)
                {
                    healthPairs.Add(((long)NetCombatSync.PathHash(health.transform) << 16) ^ Mathf.RoundToInt(health.CurrentHealth)); // 경로·체력
                }
            }

            scratchPairs.Sort(); // 정렬
            healthPairs.Sort(); // 정렬
            snapshot.DeviceCount = scratchPairs.Count; // 수
            snapshot.DeviceHash = HashLongs(scratchPairs); // 해시
            snapshot.HealthCount = healthPairs.Count; // 수
            snapshot.HealthHash = HashLongs(healthPairs); // 해시
            return snapshot; // 반환
        }

        public static void Compare(ulong guest, CoopSnapshot remote) // 방장: 참가자 요약과 내 요약 비교
        {
            CoopSnapshot local = Capture(); // 방장 화면

            if (!remote.Ready || !local.Ready || remote.Map != local.Map) // 이동 중·다른 맵
            {
                return; // 비교 안 함
            }

            Checks++; // 집계
            StringBuilder diff = new StringBuilder(); // 다른 항목
            bool items = remote.ItemCount != local.ItemCount || remote.ItemHash != local.ItemHash; // 아이템
            bool devices = remote.DeviceCount != local.DeviceCount || remote.DeviceHash != local.DeviceHash; // 장치·문
            bool health = remote.HealthCount != local.HealthCount || remote.HealthHash != local.HealthHash; // 체력

            if (remote.Day != local.Day) diff.Append($" 일차(방장 {local.Day}/대원 {remote.Day})"); // 일차
            if (local.Funds >= 0 && remote.Funds >= 0 && remote.Funds != local.Funds) diff.Append($" 자금(방장 {local.Funds}/대원 {remote.Funds})"); // 자금 (사무소에서만)
            if (items) diff.Append($" 아이템(방장 {local.ItemCount}개/대원 {remote.ItemCount}개)"); // 아이템
            if (devices) diff.Append($" 장치·문(방장 {local.DeviceCount}/대원 {remote.DeviceCount})"); // 장치
            if (health) diff.Append($" 체력(방장 {local.HealthCount}/대원 {remote.HealthCount})"); // 체력

            if (diff.Length == 0) // 일치
            {
                Matches++; // 집계
                strikes[guest] = 0; // 초기화
                LastResult = $"대원 {guest + 1} 일치 · 아이템 {local.ItemCount} · 장치 {local.DeviceCount} · 체력 {local.HealthCount}"; // 결과
                Debug.Log($"[Project I] 협동 점검 / {LastResult}"); // 기록
                return; // 종료
            }

            int count = strikes.TryGetValue(guest, out int previous) ? previous + 1 : 1; // 연속 불일치
            strikes[guest] = count; // 기록

            if (count < 2) // 한 번은 전달 중일 수 있음
            {
                LastResult = $"대원 {guest + 1} 차이 발견 (다음 점검에서 다시 확인):{diff}"; // 결과
                return; // 대기
            }

            Mismatches++; // 집계
            strikes[guest] = 0; // 초기화
            LastResult = $"대원 {guest + 1} 불일치:{diff} → 다시 맞춤"; // 결과
            Debug.LogWarning($"[Project I] 협동 불일치 / {LastResult}"); // 기록

            if (items) // 아이템
            {
                NetItemSync.ForceResync(); // 참가자 전체 목록 다시 받기
            }

            if (devices || health) // 장치·체력
            {
                NetCombatSync.PushFullStateTo(guest); // 전체 상태 보내기
            }

            Heals++; // 집계
        }

        private static int HashStrings(List<string> values) // 문자열 목록 FNV-1a
        {
            unchecked
            {
                int hash = (int)2166136261; // 시작값

                foreach (string value in values) // 문자열
                {
                    for (int index = 0; index < value.Length; index++) // 글자
                    {
                        hash = (hash ^ value[index]) * 16777619; // 섞기
                    }

                    hash = (hash ^ '|') * 16777619; // 구분
                }

                return hash; // 반환
            }
        }

        private static int HashLongs(List<long> values) // 숫자 목록 FNV-1a
        {
            unchecked
            {
                int hash = (int)2166136261; // 시작값

                foreach (long value in values) // 숫자
                {
                    hash = (hash ^ (int)value) * 16777619; // 아래 32비트
                    hash = (hash ^ (int)(value >> 32)) * 16777619; // 위 32비트
                }

                return hash; // 반환
            }
        }
    }
}
