using System.Text; // 경로 문자열
using ProjectI.Dungeon; // 던전 문
using ProjectI.Economy; // 공동 자금 (표시용)
using ProjectI.Loop; // 맵 로더·마차 종
using ProjectI.Persistence; // 일차·단계
using ProjectI.TimeOfDay; // 시각
using ProjectI.UI; // 알림
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed class NetWorldState : NetworkBehaviour // 방장이 기준인 월드 상태 (일차 · 단계 · 시드 · 시각 · 마차 목적지 · 문) — 참가자는 따라감
    {
        private const float ServerWriteInterval = 0.5f; // 방장 갱신 간격
        private const float ClockToleranceHours = 0.08f; // 이만큼 어긋나면 시각 맞춤 (약 5분)

        private readonly NetworkVariable<int> day = new NetworkVariable<int>(1); // 일차
        private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>(0); // 원정 단계
        private readonly NetworkVariable<int> seed = new NetworkVariable<int>(0); // 캠페인 시드 (같은 던전)
        private readonly NetworkVariable<float> hour = new NetworkVariable<float>(8f); // 게임 시각
        private readonly NetworkVariable<byte> destination = new NetworkVariable<byte>(0); // 마차 목적지
        private readonly NetworkVariable<bool> failureTravel = new NetworkVariable<bool>(false); // 원정 실패 귀환 여부
        private readonly NetworkVariable<int> funds = new NetworkVariable<int>(-1); // 공동 자금 (-1 = 모름)
        private float nextServerWrite; // 다음 갱신
        private GameTimeController clock; // 시각
        private CampaignEconomy economy; // 공동 자금
        private float nextLookup; // 시각·자금 오브젝트 다시 찾는 시각

        public static NetWorldState Instance { get; private set; } // 현재 월드 상태
        public int Day => day.Value; // 검증용
        public TravelDestination Destination => (TravelDestination)destination.Value; // 검증용

        public override void OnNetworkSpawn() // 생성
        {
            DontDestroyOnLoad(gameObject); // 맵이 바뀌어도 유지
            Instance = this; // 등록
            gameObject.name = "NetWorldState"; // 이름

            if (IsServer) // 방장
            {
                PersistentMapLoader.TravelStarted += HandleHostTravelStarted; // 마차 출발 알림
                WriteServerState(); // 첫 값
            }
        }

        public override void OnNetworkDespawn() // 제거
        {
            PersistentMapLoader.TravelStarted -= HandleHostTravelStarted; // 해제

            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }
        }

        public override void OnDestroy() // 파괴
        {
            PersistentMapLoader.TravelStarted -= HandleHostTravelStarted; // 해제

            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }

            base.OnDestroy(); // 넷코드 정리
        }

        private void Update() // 방장 갱신 · 참가자 적용
        {
            if (!IsSpawned) // 연결 전
            {
                return; // 생략
            }

            if (IsServer) // 방장
            {
                if (Time.unscaledTime >= nextServerWrite) // 간격
                {
                    nextServerWrite = Time.unscaledTime + ServerWriteInterval; // 다음
                    WriteServerState(); // 갱신
                }

                return; // 종료
            }

            ApplyToGuest(); // 참가자 적용
        }

        private void WriteServerState() // 방장 상태 기록
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스

            if (service != null && service.IsInitialized) // 준비됨
            {
                day.Value = service.CurrentDay; // 일차
                phase.Value = (byte)service.DayPhase; // 단계
                seed.Value = service.CampaignSeed; // 시드
            }

            LookupWorldObjects(); // 시각·자금

            if (clock != null) // 있음
            {
                hour.Value = clock.CurrentHour; // 시각
            }

            if (economy != null) // 있음
            {
                funds.Value = economy.SharedFunds; // 자금
            }

            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더

            if (loader != null && !loader.IsTransitioning) // 이동 중이 아니면 현재 맵을 기준으로
            {
                destination.Value = (byte)loader.CurrentDestination; // 목적지
            }
        }

        private void HandleHostTravelStarted(TravelDestination target, bool failure) // 방장 마차 출발 → 참가자도 출발
        {
            failureTravel.Value = failure; // 실패 귀환 여부 (목적지보다 먼저)
            destination.Value = (byte)target; // 목적지
        }

        private void ApplyToGuest() // 참가자에게 방장 상태 적용
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스
            service?.ApplyGuestState(day.Value, (ExpeditionDayPhase)phase.Value, seed.Value); // 일차·단계·시드
            LookupWorldObjects(); // 시각·자금

            if (clock != null && Mathf.Abs(Mathf.DeltaAngle(clock.CurrentHour * 15f, hour.Value * 15f)) / 15f > ClockToleranceHours) // 어긋남 (24시간을 원으로 비교)
            {
                clock.SetTime(hour.Value); // 맞춤
            }

            if (economy != null && funds.Value >= 0 && economy.SharedFunds != funds.Value) // 다름
            {
                economy.Configure(funds.Value, economy.SaleMultiplier); // 표시용으로 맞춤 (판매·구매 동기화는 38일차)
            }

            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더

            if (loader != null && service != null && service.IsInitialized && !loader.IsTransitioning && (byte)loader.CurrentDestination != destination.Value) // 방장과 다른 맵
            {
                loader.FollowNetworkTravel((TravelDestination)destination.Value, failureTravel.Value); // 따라 이동
            }
        }

        private void LookupWorldObjects() // 없을 때만 1초마다 찾기 (사무소가 내려가면 자금 오브젝트가 사라짐)
        {
            if ((clock != null && economy != null) || Time.unscaledTime < nextLookup) // 충분하거나 대기
            {
                return; // 생략
            }

            nextLookup = Time.unscaledTime + 1f; // 다음
            clock = clock != null ? clock : FindAnyObjectByType<GameTimeController>(); // 시각
            economy = economy != null ? economy : FindAnyObjectByType<CampaignEconomy>(); // 공동 자금 (사무소에 있을 때)
        }

        public static void RequestTravel() // 참가자가 마차 종을 울림 → 방장에게 요청
        {
            if (Instance != null && Instance.IsSpawned) // 연결됨
            {
                Instance.RequestTravelRpc(); // 요청
            }
        }

        public static void BroadcastBell() // 종 울림 소리·연출을 모두에게
        {
            if (Instance != null && Instance.IsSpawned) // 연결됨
            {
                Instance.BellRangRpc(); // 방송
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestTravelRpc(RpcParams rpcParams = default) // 방장: 참가자의 출발 요청 처리
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            string reason = loader == null ? "방장의 게임이 준비되지 않았습니다" : loader.RequestNetworkTravel(); // 출발 시도

            if (reason != null) // 거부
            {
                NoticeRpc($"출발 불가 — {reason}", RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)); // 요청한 대원에게 이유
                return; // 종료
            }

            BellRangRpc(); // 모두에게 종소리
        }

        [Rpc(SendTo.Everyone)]
        private void BellRangRpc() // 모두: 마차 종 연출
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            WagonTravelBellInteractable bell = loader == null || loader.WagonRoot == null ? null : loader.WagonRoot.GetComponentInChildren<WagonTravelBellInteractable>(true); // 종
            bell?.PlayRemoteRing(); // 연출 (이미 울리는 중이면 생략)
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NoticeRpc(string text, RpcParams rpcParams) // 특정 대원에게 알림
        {
            GameHud.ShowNotice(text, 3f); // 표시
        }

        [Rpc(SendTo.Everyone)]
        private void AnnounceRpc(string text) // 모두에게 알림
        {
            GameHud.ShowNotice(text, 3f); // 표시
        }

        public static void Announce(string text) // 방장 알림 방송
        {
            if (Instance != null && Instance.IsSpawned && Instance.IsServer) // 방장
            {
                Instance.AnnounceRpc(text); // 방송
            }
        }

        public static bool RelayDoor(DungeonDoor door, bool open, bool unlocked) // 던전 문 여닫기를 모두에게 맞춤 (처리했으면 true)
        {
            if (!NetworkSession.IsOnline || Instance == null || !Instance.IsSpawned || door == null) // 혼자 하기
            {
                return false; // 기존 방식
            }

            Instance.RequestDoorRpc(DoorPath(door.transform), open, unlocked); // 방장에게 요청
            return true; // 결과는 방송으로 적용
        }

        [Rpc(SendTo.Server)]
        private void RequestDoorRpc(string path, bool open, bool unlocked) // 방장: 문 요청 확인
        {
            DungeonDoor door = FindDoor(path); // 문

            if (door == null || (door.IsLocked && !unlocked)) // 없음·잠김
            {
                return; // 무시
            }

            SetDoorRpc(path, open); // 모두에게 적용
        }

        [Rpc(SendTo.Everyone)]
        private void SetDoorRpc(string path, bool open) // 모두: 문 상태 적용
        {
            FindDoor(path)?.ApplyNetworkState(open); // 적용
        }

        public static string DoorPath(Transform door) // 문 경로 (같은 시드로 만든 던전은 경로가 같음)
        {
            StringBuilder builder = new StringBuilder(); // 경로

            for (Transform current = door; current != null; current = current.parent) // 부모 방향
            {
                builder.Insert(0, $"/{current.name}#{current.GetSiblingIndex()}"); // 이름과 순서
            }

            builder.Insert(0, door.gameObject.scene.name); // 씬 이름
            return builder.ToString(); // 반환
        }

        private static DungeonDoor FindDoor(string path) // 경로로 문 찾기
        {
            foreach (DungeonDoor door in FindObjectsByType<DungeonDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // 문 목록
            {
                if (DoorPath(door.transform) == path) // 일치
                {
                    return door; // 반환
                }
            }

            return null; // 없음
        }
    }
}
