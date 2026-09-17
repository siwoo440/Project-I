using System; // 직렬화
using System.Collections.Generic; // 목록
using System.Text; // 아이디 묶음
using ProjectI.Economy; // 판매·구매·단상·가격
using ProjectI.Items; // 아이템
using ProjectI.Loop; // 맵 로더
using ProjectI.Persistence; // 저장 서비스·단상 경로
using ProjectI.UI; // 알림
using ProjectI.Wagon; // 마차 보관함·적재칸
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 이동

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public enum NetItemPlace : byte // 아이템 위치 종류
    {
        Ground = 0, // 바닥 (맵 또는 마차 적재칸)
        Held = 1, // 원정대원 소지
        WagonStorage = 2, // 마차 공동 보관함
        Pedestal = 3, // 사무소 보관 단상
    }

    [Serializable]
    public sealed class NetItemState // 아이템 하나의 상태 (목록 전송용)
    {
        public string id; // 개별 ID
        public string itemId; // 종류 ID
        public string name; // 표시 이름
        public int value; // 가치
        public bool sold; // 판매됨
        public byte place; // 위치 종류
        public ulong holder; // 소지한 대원
        public bool equipped; // 손에 들고 있음
        public bool inWagon; // 마차 기준 좌표
        public string key; // 단상 경로
        public Vector3 pos; // 위치
        public Quaternion rot = Quaternion.identity; // 회전
    }

    [Serializable]
    public sealed class NetItemBatch // 아이템 목록 조각
    {
        public int request; // 요청 번호
        public bool last; // 마지막 조각
        public List<NetItemState> items = new List<NetItemState>(); // 상태
    }

    public sealed class NetItemSync : NetworkBehaviour // 38일차: 아이템·판매·구매·채무를 방장 기준으로 맞춤 (개별 ID 기준)
    {
        private const int BatchSize = 10; // 목록 조각당 아이템 수
        private const float SyncCheckInterval = 0.5f; // 참가자 목록 요청 확인 간격
        private const byte EventPickedUp = 1; // 소지
        private const byte EventDropped = 2; // 내려놓음
        private const byte EventEquipped = 3; // 손에 듦
        private const byte EventStored = 4; // 보관함·단상
        private const byte EventDestroyed = 5; // 사라짐 (열쇠 소모 등)
        private const byte EventSold = 6; // 판매됨

        private readonly NetworkVariable<int> resyncSerial = new NetworkVariable<int>(0); // 방장이 사무소를 다시 불러오면 증가 → 참가자 전체 목록 다시 받음
        private readonly Dictionary<string, ulong> holders = new Dictionary<string, ulong>(); // 방장: 아이템별 소지 대원
        private readonly Dictionary<ulong, string> equipped = new Dictionary<ulong, string>(); // 대원별 손에 든 아이템
        private readonly Dictionary<string, WorldItem> cache = new Dictionary<string, WorldItem>(); // ID → 아이템
        private readonly List<NetItemState> incoming = new List<NetItemState>(); // 받는 중인 목록
        private int syncedMap = -1; // 목록을 받은 맵
        private int syncedSerial = -1; // 목록을 받은 재동기화 번호
        private int requestNumber; // 요청 번호
        private int incomingRequest = -1; // 받는 중인 요청
        private float nextSyncCheck; // 다음 확인
        private string lastEquippedSent = "-"; // 마지막으로 보낸 손 아이템 ("-" = 아직 안 보냄)
        private readonly Dictionary<string, (ulong sender, float time)> recentRelease = new Dictionary<string, (ulong, float)>(); // 42일차: 방금 내려놓은 대원 (사망 흩뿌림 등 다시 알림 허용)
        private readonly Dictionary<ulong, (string id, float time)> previousEquipped = new Dictionary<ulong, (string, float)>(); // 42일차: 직전에 든 무기 (날아가는 화살 인정)
        private readonly Dictionary<ulong, List<(ItemDefinition definition, float time)>> keyCredits = new Dictionary<ulong, List<(ItemDefinition, float)>>(); // 42일차: 방금 소모한 열쇠 (문 열기 확인)
        private const float ReleaseMemorySeconds = 3f; // 내려놓은 뒤 다시 알림 허용 시간
        private const float KeyCreditSeconds = 6f; // 열쇠 소모 후 문을 열 수 있는 시간

        public static NetItemSync Instance { get; private set; } // 현재 동기화
        public static bool Applying { get; private set; } // 받은 변경 적용 중 (다시 보내지 않음)
        public static bool Active => NetworkSession.IsOnline && Instance != null && Instance.IsSpawned; // 협동 동기화 중
        public static bool GuestMode => Active && !Instance.IsServer; // 참가자 (경제는 방장에게 요청)
        public int LastSnapshotCount { get; private set; } // 검증용

        public override void OnNetworkSpawn() // 생성
        {
            Instance = this; // 등록

            if (IsServer) // 방장
            {
                DailySnapshotService.OfficeStateReloaded += HandleOfficeReloaded; // 사무소 다시 불러옴
            }
        }

        public override void OnNetworkDespawn() // 제거
        {
            DailySnapshotService.OfficeStateReloaded -= HandleOfficeReloaded; // 해제

            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }
        }

        public override void OnDestroy() // 파괴
        {
            DailySnapshotService.OfficeStateReloaded -= HandleOfficeReloaded; // 해제

            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }

            base.OnDestroy(); // 넷코드 정리
        }

        private void HandleOfficeReloaded() // 방장 사무소 상태가 바뀜 (하루 마감·복구)
        {
            resyncSerial.Value++; // 참가자 전체 목록 다시 받기
        }

        private void Update() // 참가자: 맵에 들어오면 방장 목록 요청
        {
            if (!IsSpawned || IsServer || Time.unscaledTime < nextSyncCheck) // 방장·대기
            {
                return; // 생략
            }

            nextSyncCheck = Time.unscaledTime + SyncCheckInterval; // 다음
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            DailySnapshotService service = DailySnapshotService.Instance; // 저장
            NetWorldState world = NetWorldState.Instance; // 월드 상태

            if (loader == null || service == null || world == null || !service.IsInitialized || loader.IsTransitioning || loader.CurrentDestination != world.Destination) // 아직 방장과 같은 맵이 아님
            {
                return; // 대기
            }

            int map = (int)loader.CurrentDestination; // 맵

            if (map == syncedMap && resyncSerial.Value == syncedSerial) // 이미 받음
            {
                return; // 생략
            }

            syncedMap = map; // 기록
            syncedSerial = resyncSerial.Value; // 기록
            requestNumber++; // 새 요청
            RequestSnapshotRpc(requestNumber); // 방장에게 요청
        }

        // ───────────────────────── 로컬 변경 알림 (게임 코드에서 호출) ─────────────────────────

        public static void NotifyPickedUp(WorldItem item) // 내가 아이템을 가져감 (바닥·보관함·단상)
        {
            if (!Ready(item, out string id)) // 대상 아님
            {
                return; // 생략
            }

            if (Instance.IsServer) // 방장
            {
                Instance.holders[id] = Instance.NetworkManager.LocalClientId; // 소지 기록
                Instance.ItemEventRpc(EventPickedUp, Instance.NetworkManager.LocalClientId, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
                return; // 종료
            }

            Instance.PickedUpRpc(id); // 방장에게 요청
        }

        public static void NotifyDropped(WorldItem item, Vector3 velocity) // 내가 아이템을 내려놓음·던짐
        {
            if (!Ready(item, out string id)) // 대상 아님
            {
                return; // 생략
            }

            bool inWagon = ToWagonSpace(item.transform.position, item.transform.rotation, out Vector3 pos, out Quaternion rot); // 마차 안이면 마차 기준

            if (Instance.IsServer) // 방장
            {
                Instance.holders.Remove(id); // 소지 해제
                Instance.ItemEventRpc(EventDropped, Instance.NetworkManager.LocalClientId, id, pos, rot, velocity, inWagon, string.Empty); // 방송
                return; // 종료
            }

            Instance.DroppedRpc(id, pos, rot, velocity, inWagon); // 방장에게 알림
        }

        public static void NotifyEquipped(WorldItem item) // 내가 손에 든 아이템이 바뀜
        {
            if (!Active || Applying) // 대상 아님
            {
                return; // 생략
            }

            string id = IdOf(item) ?? string.Empty; // 없으면 빈손

            if (id == Instance.lastEquippedSent) // 같음
            {
                return; // 생략
            }

            Instance.lastEquippedSent = id; // 기록

            if (Instance.IsServer) // 방장
            {
                ulong self = Instance.NetworkManager.LocalClientId; // 나
                Instance.equipped[self] = id; // 기록
                Instance.ItemEventRpc(EventEquipped, self, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
                return; // 종료
            }

            Instance.EquippedRpc(id); // 방장에게 알림
        }

        public static void NotifyStored(WorldItem item, NetItemPlace place, string key) // 내가 보관함·단상에 넣음
        {
            if (!Ready(item, out string id)) // 대상 아님
            {
                return; // 생략
            }

            if (Instance.IsServer) // 방장
            {
                Instance.holders.Remove(id); // 소지 해제
                Instance.ItemEventRpc(EventStored, Instance.NetworkManager.LocalClientId, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, (char)('0' + (int)place) + (key ?? string.Empty)); // 방송
                return; // 종료
            }

            Instance.StoredRpc(id, (byte)place, key ?? string.Empty); // 방장에게 알림
        }

        public static void NotifyDestroyed(WorldItem item) // 내 아이템이 사라짐 (열쇠 소모)
        {
            if (!Ready(item, out string id)) // 대상 아님
            {
                return; // 생략
            }

            if (Instance.IsServer) // 방장
            {
                Instance.holders.Remove(id); // 소지 해제
                Instance.ItemEventRpc(EventDestroyed, Instance.NetworkManager.LocalClientId, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
                return; // 종료
            }

            Instance.DestroyedRpc(id); // 방장에게 알림
        }

        public static void NotifySold(WorldItem item) // 방장: 판매 처리됨 → 모두 사라지는 연출
        {
            if (!Active || !Instance.IsServer || !Ready(item, out string id)) // 방장만
            {
                return; // 생략
            }

            Instance.holders.Remove(id); // 정리
            Instance.ItemEventRpc(EventSold, Instance.NetworkManager.LocalClientId, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
        }

        public static void NotifySpawned(WorldItem item) // 방장: 새 아이템 생김 (상점 수령대) → 모두 생성
        {
            if (!Active || !Instance.IsServer || item == null) // 방장만
            {
                return; // 생략
            }

            NetItemState state = CaptureBase(item); // 상태

            if (state == null) // 식별 없음
            {
                return; // 생략
            }

            state.place = (byte)NetItemPlace.Ground; // 바닥
            state.pos = item.transform.position; // 위치
            state.rot = item.transform.rotation; // 회전
            Instance.SpawnedRpc(JsonUtility.ToJson(state)); // 방송
        }

        public static WorldItem EquippedItemOf(ulong clientId, out string id) // 42일차: 방장이 아는 그 대원의 손 아이템
        {
            id = string.Empty; // 기본

            if (Instance == null || !Instance.equipped.TryGetValue(clientId, out string held) || string.IsNullOrEmpty(held)) // 빈손
            {
                return null; // 없음
            }

            id = held; // 아이디
            return Find(held); // 아이템
        }

        public static WorldItem RecentEquippedItemOf(ulong clientId, out string id) // 42일차: 몇 초 안에 들고 있던 이전 아이템
        {
            id = string.Empty; // 기본

            if (Instance == null || !Instance.previousEquipped.TryGetValue(clientId, out (string id, float time) previous) || Time.unscaledTime - previous.time > NetGuard.WeaponMemorySeconds || string.IsNullOrEmpty(previous.id)) // 없음·오래됨
            {
                return null; // 없음
            }

            id = previous.id; // 아이디
            return Find(previous.id); // 아이템
        }

        public static bool ConsumeKeyCredit(ulong clientId, string requiredKeyId) // 42일차: 그 대원이 방금 맞는 열쇠를 소모했는지 확인하고 사용
        {
            if (clientId == NetworkManager.ServerClientId) // 방장 (자기 화면에서 이미 확인)
            {
                return true; // 허용
            }

            if (Instance == null || !Instance.keyCredits.TryGetValue(clientId, out List<(ItemDefinition definition, float time)> credits)) // 기록 없음
            {
                return false; // 거부
            }

            for (int index = credits.Count - 1; index >= 0; index--) // 최근부터
            {
                (ItemDefinition definition, float time) credit = credits[index]; // 기록

                if (Time.unscaledTime - credit.time > KeyCreditSeconds) // 오래됨
                {
                    credits.RemoveAt(index); // 정리
                    continue; // 다음
                }

                if (credit.definition != null && credit.definition.Matches(requiredKeyId)) // 맞는 열쇠
                {
                    credits.RemoveAt(index); // 사용
                    return true; // 허용
                }
            }

            return false; // 없음
        }

        public static void ForgetClient(ulong clientId) // 42일차: 나간 대원 기록 정리
        {
            if (Instance == null) // 없음
            {
                return; // 생략
            }

            Instance.previousEquipped.Remove(clientId); // 정리
            Instance.keyCredits.Remove(clientId); // 정리
        }

        private bool HeldBy(string id, ulong sender) // 방장 기록상 그 대원이 가진 아이템 (방금 내려놓은 것 포함)
        {
            if (sender == NetworkManager.ServerClientId) // 방장
            {
                return true; // 허용
            }

            if (holders.TryGetValue(id, out ulong holder)) // 소지 기록
            {
                return holder == sender; // 결과
            }

            return recentRelease.TryGetValue(id, out (ulong sender, float time) release) && release.sender == sender && Time.unscaledTime - release.time <= ReleaseMemorySeconds; // 방금 내려놓음
        }

        private static bool TryWorldPosition(WorldItem item, out Vector3 position) // 방장 화면 아이템 위치
        {
            position = item == null ? Vector3.zero : item.transform.position; // 위치
            return item != null; // 결과
        }

        // ───────────────────────── 참가자 경제 요청 ─────────────────────────

        public static bool RequestSale(IList<WorldItem> items) // 참가자: 판매 요청 (처리했으면 true)
        {
            if (!GuestMode) // 방장·혼자
            {
                return false; // 기존 방식
            }

            StringBuilder ids = new StringBuilder(); // 아이디 묶음

            foreach (WorldItem item in items) // 판매 대상
            {
                string id = IdOf(item); // 아이디

                if (!string.IsNullOrEmpty(id)) // 유효
                {
                    ids.Append(ids.Length == 0 ? string.Empty : "|").Append(id); // 추가
                }
            }

            Instance.SaleRequestRpc(ids.ToString()); // 요청
            GameHud.ShowNotice("판매 요청 중...", 1.5f); // 안내
            return true; // 처리
        }

        public static bool RequestPurchase(OfficeShopShelf shelf, ShopEntry entry, int quantity) // 참가자: 구매 요청 (처리했으면 true)
        {
            if (!GuestMode || shelf == null || shelf.Catalog == null) // 방장·혼자
            {
                return false; // 기존 방식
            }

            int index = -1; // 상품 번호

            for (int i = 0; i < shelf.Catalog.Entries.Count; i++) // 목록
            {
                if (shelf.Catalog.Entries[i] == entry) // 일치
                {
                    index = i; // 기록
                    break; // 종료
                }
            }

            if (index < 0) // 없음
            {
                return false; // 기존 방식
            }

            Instance.PurchaseRequestRpc(index, quantity); // 요청
            GameHud.ShowNotice("구매 요청 중...", 1.5f); // 안내
            return true; // 처리
        }

        public static bool RequestDebtPayment() // 참가자: 채무 상환 요청 (처리했으면 true)
        {
            if (!GuestMode) // 방장·혼자
            {
                return false; // 기존 방식
            }

            Instance.DebtPaymentRequestRpc(); // 요청
            return true; // 처리
        }

        // ───────────────────────── 방장 처리 ─────────────────────────

        [Rpc(SendTo.Server)]
        private void PickedUpRpc(string id, RpcParams rpcParams = default) // 방장: 참가자가 가져감
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Item)) // 42일차: 횟수
            {
                return; // 무시
            }

            if (!NetGuard.ValidId(id)) // 잘못된 ID
            {
                NetGuard.Reject(sender, "줍기", "잘못된 ID", 5f); // 경고
                return; // 무시
            }

            if (holders.TryGetValue(id, out ulong holder) && holder != sender) // 다른 대원이 먼저 가짐
            {
                RevokeRpc(id, RpcTarget.Single(sender, RpcTargetUse.Temp)); // 되돌리기
                return; // 종료
            }

            WorldItem target = Find(id); // 방장 화면 아이템

            if (target == null) // 방장이 모르는 아이템 (다음 전체 목록에서 맞춤)
            {
                NetGuard.Reject(sender, "줍기", "없는 아이템", 0f); // 기록만
                return; // 무시
            }

            if (holder != sender && TryWorldPosition(target, out Vector3 itemPosition) && !NetGuard.Near(sender, itemPosition, NetGuard.InteractReach)) // 손이 닿지 않음
            {
                NetGuard.Reject(sender, "줍기", "거리", 1f); // 경고
                RevokeRpc(id, RpcTarget.Single(sender, RpcTargetUse.Temp)); // 되돌리기
                return; // 종료
            }

            recentRelease.Remove(id); // 정리
            holders[id] = sender; // 기록
            ApplyEvent(EventPickedUp, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방장 화면 적용
            ItemEventRpc(EventPickedUp, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
        }

        [Rpc(SendTo.Server)]
        private void DroppedRpc(string id, Vector3 pos, Quaternion rot, Vector3 velocity, bool inWagon, RpcParams rpcParams = default) // 방장: 참가자가 내려놓음
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Item)) // 42일차: 횟수
            {
                return; // 무시
            }

            if (!NetGuard.ValidId(id) || !NetGuard.InWorld(pos) || !NetGuard.Finite(rot) || !NetGuard.Finite(velocity)) // 잘못된 값
            {
                NetGuard.Reject(sender, "내려놓기", "잘못된 값", 5f); // 경고
                return; // 무시
            }

            if (!HeldBy(id, sender)) // 가진 적 없는 아이템
            {
                NetGuard.Reject(sender, "내려놓기", "소지 아님", 0f); // 기록만 (순서 어긋남 가능)
                return; // 무시
            }

            Transform wagon = PersistentMapLoader.Instance == null ? null : PersistentMapLoader.Instance.WagonRoot; // 마차
            Vector3 worldPosition = inWagon && wagon != null ? wagon.TransformPoint(pos) : pos; // 월드 위치

            if (!NetGuard.Near(sender, worldPosition, NetGuard.DropReach)) // 먼 곳에 내려놓음 (순간이동)
            {
                NetGuard.Reject(sender, "내려놓기", "거리", 1f); // 경고

                if (!NetGuard.TryGetPosition(sender, out Vector3 body)) // 몸체 위치 모름
                {
                    return; // 무시
                }

                inWagon = false; // 월드 좌표
                pos = body + Vector3.up * 0.5f; // 대원 발밑으로 고침
                velocity = Vector3.zero; // 던지기 취소
            }

            velocity = Vector3.ClampMagnitude(velocity, NetGuard.MaxThrowSpeed); // 던지기 속도 제한
            holders.Remove(id); // 해제
            recentRelease[id] = (sender, Time.unscaledTime); // 기록
            ApplyEvent(EventDropped, sender, id, pos, rot, velocity, inWagon, string.Empty); // 적용
            ItemEventRpc(EventDropped, sender, id, pos, rot, velocity, inWagon, string.Empty); // 방송
        }

        [Rpc(SendTo.Server)]
        private void EquippedRpc(string id, RpcParams rpcParams = default) // 방장: 참가자가 손에 든 아이템
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Item)) // 42일차: 횟수
            {
                return; // 무시
            }

            id ??= string.Empty; // 빈손

            if (id.Length > 0 && (!NetGuard.ValidId(id) || !HeldBy(id, sender))) // 가지지 않은 아이템을 손에 듦
            {
                NetGuard.Reject(sender, "손", "소지 아님", id.Length > NetGuard.MaxIdLength ? 5f : 0f); // 기록
                return; // 무시
            }

            if (equipped.TryGetValue(sender, out string before) && before != id) // 바뀜
            {
                previousEquipped[sender] = (before, Time.unscaledTime); // 이전 무기 기억
            }

            ApplyEvent(EventEquipped, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 적용
            ItemEventRpc(EventEquipped, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
        }

        [Rpc(SendTo.Server)]
        private void StoredRpc(string id, byte place, string key, RpcParams rpcParams = default) // 방장: 참가자가 보관
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Item)) // 42일차: 횟수
            {
                return; // 무시
            }

            key ??= string.Empty; // 경로

            if (!NetGuard.ValidId(id) || (place != (byte)NetItemPlace.WagonStorage && place != (byte)NetItemPlace.Pedestal) || !NetGuard.ValidPath(key, true)) // 잘못된 값
            {
                NetGuard.Reject(sender, "보관", "잘못된 값", 5f); // 경고
                return; // 무시
            }

            if (!HeldBy(id, sender)) // 가지지 않은 아이템
            {
                NetGuard.Reject(sender, "보관", "소지 아님", 0f); // 기록
                return; // 무시
            }

            Component container = place == (byte)NetItemPlace.Pedestal ? (Component)FindPedestal(key) : FindAnyObjectByType<WagonSharedStorage>(); // 보관 장소

            if (container == null || !NetGuard.Near(sender, container.transform.position, NetGuard.InteractReach)) // 없음·멂
            {
                NetGuard.Reject(sender, "보관", container == null ? "장소 없음" : "거리", container == null ? 0f : 1f); // 경고
                return; // 무시 (방장 기록상 계속 소지)
            }

            holders.Remove(id); // 해제
            recentRelease.Remove(id); // 정리
            string packed = (char)('0' + place) + key; // 위치 + 경로
            ApplyEvent(EventStored, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, packed); // 적용
            ItemEventRpc(EventStored, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, packed); // 방송
        }

        [Rpc(SendTo.Server)]
        private void DestroyedRpc(string id, RpcParams rpcParams = default) // 방장: 참가자 아이템 사라짐
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Item)) // 42일차: 횟수
            {
                return; // 무시
            }

            if (!NetGuard.ValidId(id) || !HeldBy(id, sender)) // 가지지 않은 아이템을 없앰
            {
                NetGuard.Reject(sender, "소모", NetGuard.ValidId(id) ? "소지 아님" : "잘못된 ID", NetGuard.ValidId(id) ? 1f : 5f); // 경고
                return; // 무시
            }

            WorldItem consumed = Find(id); // 소모한 아이템
            WorldItemIdentity identity = consumed == null ? null : consumed.GetComponent<WorldItemIdentity>(); // 식별

            if (identity != null && identity.Definition != null) // 종류 확인
            {
                if (!keyCredits.TryGetValue(sender, out List<(ItemDefinition definition, float time)> credits)) // 처음
                {
                    credits = new List<(ItemDefinition, float)>(); // 생성
                    keyCredits[sender] = credits; // 등록
                }

                credits.Add((identity.Definition, Time.unscaledTime)); // 문 열기 확인용

                if (credits.Count > 8) // 너무 많음
                {
                    credits.RemoveAt(0); // 오래된 것 제거
                }
            }

            holders.Remove(id); // 해제
            recentRelease.Remove(id); // 정리
            ApplyEvent(EventDestroyed, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 적용
            ItemEventRpc(EventDestroyed, sender, id, Vector3.zero, Quaternion.identity, Vector3.zero, false, string.Empty); // 방송
        }

        [Rpc(SendTo.Server)]
        private void SaleRequestRpc(string ids, RpcParams rpcParams = default) // 방장: 참가자 판매 요청
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Economy)) // 42일차: 횟수
            {
                return; // 무시
            }

            ids ??= string.Empty; // 목록

            if (ids.Length > (NetGuard.MaxIdLength + 1) * NetGuard.MaxSaleItems) // 너무 김
            {
                NetGuard.Reject(sender, "판매", "목록 길이", 5f); // 경고
                return; // 무시
            }

            OfficeSaleCounter counter = FindAnyObjectByType<OfficeSaleCounter>(); // 판매대

            if (counter == null || !NetGuard.Near(sender, counter.transform.position, NetGuard.CounterReach)) // 판매대에서 멂
            {
                NetGuard.Reject(sender, "판매", "거리", 1f); // 경고
                NoticeRpc("판매대 가까이에서 판매할 수 있습니다", RpcTarget.Single(sender, RpcTargetUse.Temp)); // 안내
                return; // 무시
            }

            List<WorldItem> items = new List<WorldItem>(); // 대상

            foreach (string id in ids.Split('|')) // 아이디
            {
                WorldItem item = Find(id); // 아이템

                if (item != null && items.Count < NetGuard.MaxSaleItems) // 있음 (최대 수)
                {
                    items.Add(item); // 추가 (판매대 위 여부는 Sell 에서 확인)
                }
            }

            int total = counter == null ? 0 : counter.Sell(items); // 판매 (판매 알림은 Sell 안에서 방송)
            NoticeRpc(total > 0 ? $"판매 완료 +{total:N0}" : "판매할 수 있는 물건이 없습니다", RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)); // 결과
        }

        [Rpc(SendTo.Server)]
        private void PurchaseRequestRpc(int entryIndex, int quantity, RpcParams rpcParams = default) // 방장: 참가자 구매 요청
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Economy)) // 42일차: 횟수
            {
                return; // 무시
            }

            OfficeShopShelf shelf = FindAnyObjectByType<OfficeShopShelf>(); // 진열대
            ShopPurchaseResult result = ShopPurchaseResult.InvalidEntry; // 결과

            if (shelf != null && !NetGuard.Near(sender, shelf.transform.position, NetGuard.CounterReach)) // 진열대에서 멂
            {
                NetGuard.Reject(sender, "구매", "거리", 1f); // 경고
                NoticeRpc("진열대 가까이에서 구매할 수 있습니다", RpcTarget.Single(sender, RpcTargetUse.Temp)); // 안내
                return; // 무시
            }

            if (shelf != null && shelf.Catalog != null && entryIndex >= 0 && entryIndex < shelf.Catalog.Entries.Count) // 상품 확인
            {
                result = shelf.Purchase(shelf.Catalog.Entries[entryIndex], quantity, out _); // 구매 (생성 알림은 수령대에서 방송)
            }

            NoticeRpc(OfficeShopShelf.Describe(result), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)); // 결과
        }

        [Rpc(SendTo.Server)]
        private void DebtPaymentRequestRpc(RpcParams rpcParams = default) // 방장: 참가자 채무 상환 요청
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Economy)) // 42일차: 횟수
            {
                return; // 무시
            }

            DebtLedger ledger = FindAnyObjectByType<DebtLedger>(); // 장부

            if (ledger != null && !NetGuard.Near(sender, ledger.transform.position, NetGuard.CounterReach)) // 장부에서 멂
            {
                NetGuard.Reject(sender, "상환", "거리", 1f); // 경고
                return; // 무시
            }

            int paid = ledger == null ? 0 : ledger.PayAvailableFunds(); // 상환 (상태는 월드 상태로 전달)
            NoticeRpc(paid > 0 ? $"채무 {paid:N0} 상환" : "상환할 수 없습니다", RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)); // 결과
        }

        [Rpc(SendTo.Server)]
        private void RequestSnapshotRpc(int request, RpcParams rpcParams = default) // 방장: 참가자에게 현재 아이템 목록 전송
        {
            ulong receiver = rpcParams.Receive.SenderClientId; // 받을 대원

            if (!NetGuard.Allow(receiver, NetChannel.Snapshot)) // 42일차: 횟수 (목록 만들기는 무거움)
            {
                return; // 무시
            }

            List<NetItemState> states = BuildSnapshot(); // 목록
            int index = 0; // 위치

            do
            {
                NetItemBatch batch = new NetItemBatch { request = request }; // 조각

                for (int count = 0; count < BatchSize && index < states.Count; count++, index++) // 조각 채우기
                {
                    batch.items.Add(states[index]); // 추가
                }

                batch.last = index >= states.Count; // 마지막
                SnapshotBatchRpc(JsonUtility.ToJson(batch), RpcTarget.Single(receiver, RpcTargetUse.Temp)); // 전송
            }
            while (index < states.Count); // 남음

            Debug.Log($"[Project I] 협동 아이템 목록 전송 / 대원 {rpcParams.Receive.SenderClientId + 1} / {states.Count}개"); // 기록
        }

        // ───────────────────────── 참가자 수신 ─────────────────────────

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void ItemEventRpc(byte kind, ulong actor, string id, Vector3 pos, Quaternion rot, Vector3 velocity, bool inWagon, string key) // 참가자: 아이템 변경 적용
        {
            if (actor == NetworkManager.LocalClientId) // 내가 한 일
            {
                return; // 이미 적용됨
            }

            ApplyEvent(kind, actor, id, pos, rot, velocity, inWagon, key); // 적용
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void SpawnedRpc(string json) // 참가자: 새 아이템 생성
        {
            NetItemState state = JsonUtility.FromJson<NetItemState>(json); // 상태

            if (state == null || Find(state.id) != null) // 이미 있음
            {
                return; // 생략
            }

            Applying = true; // 적용 중

            try
            {
                WorldItem item = Spawn(state); // 생성

                if (item != null) // 성공
                {
                    Place(item, state); // 배치
                }
            }
            finally
            {
                Applying = false; // 종료
            }
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void RevokeRpc(string id, RpcParams rpcParams) // 참가자: 다른 대원이 먼저 가져간 아이템 되돌리기
        {
            WorldItem item = Find(id); // 아이템
            PlayerInventory inventory = LocalInventory(); // 내 인벤토리

            if (item != null && inventory != null) // 있음
            {
                Applying = true; // 적용 중
                inventory.RemoveItemForNetwork(item); // 내 슬롯에서 제거 (위치는 방장 방송으로 맞춤)
                Applying = false; // 종료
            }

            GameHud.ShowNotice("다른 원정대원이 먼저 가져갔습니다", 2f); // 안내
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void NoticeRpc(string text, RpcParams rpcParams) // 특정 대원 알림
        {
            GameHud.ShowNotice(text, 2.5f); // 표시
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void SnapshotBatchRpc(string json, RpcParams rpcParams) // 참가자: 목록 조각 수신
        {
            NetItemBatch batch = JsonUtility.FromJson<NetItemBatch>(json); // 조각

            if (batch == null || batch.request != requestNumber) // 오래된 요청
            {
                return; // 무시
            }

            if (incomingRequest != batch.request) // 새 목록 시작
            {
                incomingRequest = batch.request; // 기록
                incoming.Clear(); // 비우기
            }

            incoming.AddRange(batch.items); // 누적

            if (batch.last) // 완료
            {
                ApplySnapshot(incoming); // 적용
                incoming.Clear(); // 정리
                incomingRequest = -1; // 정리
            }
        }

        // ───────────────────────── 적용 ─────────────────────────

        private void ApplyEvent(byte kind, ulong actor, string id, Vector3 pos, Quaternion rot, Vector3 velocity, bool inWagon, string key) // 변경 하나 적용
        {
            if (kind == EventEquipped) // 손 아이템
            {
                string previous = equipped.TryGetValue(actor, out string old) ? old : string.Empty; // 이전
                equipped[actor] = id ?? string.Empty; // 기록
                RefreshHeldVisual(actor, previous); // 이전 아이템 주머니로
                RefreshHeldVisual(actor, id); // 새 아이템 손으로
                return; // 종료
            }

            WorldItem item = Find(id); // 아이템

            if (item == null) // 이 대원 화면에 없음 (다음 전체 목록에서 맞춤)
            {
                return; // 생략
            }

            Applying = true; // 적용 중

            try
            {
                switch (kind)
                {
                    case EventPickedUp:
                        DetachEverywhere(item); // 보관함·단상·내 슬롯에서 빼기
                        AttachToAvatar(item, actor); // 대원 몸체에 붙임
                        break;
                    case EventDropped:
                        DetachEverywhere(item); // 빼기
                        ReleaseTo(item, pos, rot, velocity, inWagon); // 바닥으로
                        break;
                    case EventStored:
                        DetachEverywhere(item); // 빼기
                        StoreInto(item, key); // 보관함·단상
                        break;
                    case EventDestroyed:
                        DetachEverywhere(item); // 빼기
                        cache.Remove(id); // 정리
                        Destroy(item.gameObject); // 제거
                        break;
                    case EventSold:
                        OfficeSaleCounter counter = FindAnyObjectByType<OfficeSaleCounter>(); // 판매대

                        if (counter != null) // 있음
                        {
                            counter.PlayNetworkSold(item); // 판매 연출 (자금은 방장 값)
                        }

                        break;
                }
            }
            finally
            {
                Applying = false; // 종료
            }
        }

        private void ApplySnapshot(List<NetItemState> states) // 방장 목록으로 전체 맞춤
        {
            Applying = true; // 적용 중
            HashSet<string> known = new HashSet<string>(); // 방장이 아는 아이템
            PlayerInventory inventory = LocalInventory(); // 내 인벤토리
            ulong self = NetworkManager.LocalClientId; // 나
            int spawned = 0; // 생성 수
            int removed = 0; // 제거 수

            try
            {
                equipped.Clear(); // 손 아이템 다시 기록

                foreach (NetItemState state in states) // 방장 목록
                {
                    known.Add(state.id); // 기록

                    if (state.place == (byte)NetItemPlace.Held && state.equipped) // 손에 든 아이템
                    {
                        equipped[state.holder] = state.id; // 기록
                    }

                    if (state.place == (byte)NetItemPlace.Held && state.holder == self) // 내 소지품
                    {
                        continue; // 내 인벤토리 기준 유지
                    }

                    WorldItem item = Find(state.id); // 내 화면 아이템

                    if (item == null) // 없음
                    {
                        item = Spawn(state); // 생성
                        spawned += item == null ? 0 : 1; // 집계

                        if (item == null) // 실패
                        {
                            continue; // 다음
                        }
                    }

                    ApplyValue(item, state); // 가치
                    Place(item, state); // 배치
                }

                foreach (WorldItemIdentity identity in FindObjectsByType<WorldItemIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) // 내 화면의 나머지 아이템
                {
                    WorldItem item = identity.GetComponent<WorldItem>(); // 아이템

                    if (item == null || known.Contains(identity.InstanceId) || IsInLocalInventory(inventory, item) || IsSubItem(item) || IsSold(item)) // 유지 대상
                    {
                        continue; // 다음
                    }

                    cache.Remove(identity.InstanceId); // 정리
                    Destroy(item.gameObject); // 방장에게 없는 아이템 제거
                    removed++; // 집계
                }
            }
            finally
            {
                Applying = false; // 종료
            }

            LastSnapshotCount = states.Count; // 기록
            Debug.Log($"[Project I] 협동 아이템 목록 적용 / {states.Count}개 · 새로 생성 {spawned} · 제거 {removed}"); // 기록
        }

        private List<NetItemState> BuildSnapshot() // 방장: 현재 맵·마차·소지품 아이템 목록
        {
            List<NetItemState> result = new List<NetItemState>(); // 결과
            PlayerInventory hostInventory = LocalInventory(); // 방장 인벤토리
            ulong self = NetworkManager.LocalClientId; // 방장
            WagonSharedStorage storage = FindAnyObjectByType<WagonSharedStorage>(); // 마차 보관함

            foreach (WorldItemIdentity identity in FindObjectsByType<WorldItemIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) // 활성 아이템 (보관 중인 사무소 물건 제외)
            {
                WorldItem item = identity.GetComponent<WorldItem>(); // 아이템

                if (item == null || IsSubItem(item) || IsSold(item)) // 제외
                {
                    continue; // 다음
                }

                NetItemState state = CaptureBase(item); // 기본 정보
                OfficeStoredItemState stored = item.GetComponent<OfficeStoredItemState>(); // 단상

                if (holders.TryGetValue(state.id, out ulong holder)) // 참가자 소지
                {
                    state.place = (byte)NetItemPlace.Held; // 소지
                    state.holder = holder; // 대원
                    state.equipped = equipped.TryGetValue(holder, out string held) && held == state.id; // 손
                }
                else if (IsInLocalInventory(hostInventory, item)) // 방장 소지
                {
                    state.place = (byte)NetItemPlace.Held; // 소지
                    state.holder = self; // 방장
                    state.equipped = hostInventory.SelectedItem == item; // 손
                }
                else if (storage != null && Contains(storage.StoredItems, item)) // 마차 보관함
                {
                    state.place = (byte)NetItemPlace.WagonStorage; // 보관함
                }
                else if (stored != null && stored.IsOfficeStored && stored.Pedestal != null) // 단상
                {
                    state.place = (byte)NetItemPlace.Pedestal; // 단상
                    state.key = Day23SnapshotBridge.BuildTransformPath(stored.Pedestal.transform); // 경로
                }
                else if (item.IsHeld || item.IsStored) // 알 수 없는 보관 (판매대 임시 칸 등)
                {
                    continue; // 제외
                }
                else // 바닥
                {
                    state.place = (byte)NetItemPlace.Ground; // 바닥
                    state.inWagon = ToWagonSpace(item.transform.position, item.transform.rotation, out state.pos, out state.rot); // 위치
                }

                result.Add(state); // 추가
            }

            return result; // 반환
        }

        private void Place(WorldItem item, NetItemState state) // 상태대로 배치
        {
            DetachEverywhere(item); // 기존 위치에서 빼기

            switch ((NetItemPlace)state.place)
            {
                case NetItemPlace.Held:
                    AttachToAvatar(item, state.holder); // 대원 몸체
                    break;
                case NetItemPlace.WagonStorage:
                    StoreInto(item, (char)('0' + (int)NetItemPlace.WagonStorage) + string.Empty); // 보관함
                    break;
                case NetItemPlace.Pedestal:
                    StoreInto(item, (char)('0' + (int)NetItemPlace.Pedestal) + state.key); // 단상
                    break;
                default:
                    ReleaseTo(item, state.pos, state.rot, Vector3.zero, state.inWagon); // 바닥
                    break;
            }
        }

        private void AttachToAvatar(WorldItem item, ulong clientId) // 다른 대원 몸체에 붙임 (손 또는 주머니)
        {
            NetPlayerAvatar avatar = NetPlayerAvatar.Find(clientId); // 몸체

            if (avatar == null) // 몸체 없음 (접속 중)
            {
                item.Store(transform); // 월드 상태 오브젝트 아래 숨김 (맵이 바뀌어도 유지)
                return; // 종료
            }

            bool inHand = equipped.TryGetValue(clientId, out string held) && held == IdOf(item); // 손에 든 아이템

            if (inHand) // 손
            {
                item.BeginCarry(avatar.HandPoint); // 손에 표시
            }
            else
            {
                item.Store(avatar.PocketPoint); // 주머니 (숨김)
            }
        }

        private void RefreshHeldVisual(ulong clientId, string id) // 대원 소지품 표시 갱신
        {
            if (string.IsNullOrEmpty(id) || clientId == NetworkManager.LocalClientId) // 빈손·나
            {
                return; // 생략
            }

            WorldItem item = Find(id); // 아이템
            NetPlayerAvatar avatar = NetPlayerAvatar.Find(clientId); // 몸체

            if (item == null || avatar == null || !item.transform.IsChildOf(avatar.transform)) // 그 대원이 가진 아이템이 아님
            {
                return; // 생략
            }

            Applying = true; // 적용 중
            AttachToAvatar(item, clientId); // 다시 붙임
            Applying = false; // 종료
        }

        private static void ReleaseTo(WorldItem item, Vector3 pos, Quaternion rot, Vector3 velocity, bool inWagon) // 바닥으로 (마차 기준 좌표 변환)
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            Transform wagon = loader == null ? null : loader.WagonRoot; // 마차

            if (inWagon && wagon != null) // 마차 기준
            {
                pos = wagon.TransformPoint(pos); // 월드 위치
                rot = wagon.rotation * rot; // 월드 회전
            }

            item.Release(pos, rot, velocity); // 바닥으로
            Scene target = loader == null ? SceneManager.GetActiveScene() : loader.gameObject.scene; // 로컬 내려놓기와 같은 씬 (마차 밖이면 적재칸 관리자가 맵 씬으로 옮김)

            if (item.transform.parent == null && item.gameObject.scene != target && target.IsValid() && target.isLoaded) // 다른 씬 (대원 몸체 아래였음)
            {
                SceneManager.MoveGameObjectToScene(item.gameObject, target); // 이동
            }
        }

        private void StoreInto(WorldItem item, string packed) // 보관함·단상에 넣기
        {
            NetItemPlace place = string.IsNullOrEmpty(packed) ? NetItemPlace.WagonStorage : (NetItemPlace)(packed[0] - '0'); // 위치
            string key = string.IsNullOrEmpty(packed) ? string.Empty : packed.Substring(1); // 경로

            if (place == NetItemPlace.Pedestal) // 단상
            {
                ReleaseTo(item, item.transform.position, item.transform.rotation, Vector3.zero, false); // 루트로 분리

                OfficeStoragePedestal pedestal = FindPedestal(key); // 단상

                if (pedestal != null) // 있음
                {
                    if (item.gameObject.scene != pedestal.gameObject.scene) // 사무소 씬 소속
                    {
                        item.transform.SetParent(null, true); // 루트
                        SceneManager.MoveGameObjectToScene(item.gameObject, pedestal.gameObject.scene); // 이동
                    }

                    Day23SnapshotBridge.RestoreOfficeStorageItem(item, key); // 단상에 올림
                }

                return; // 종료
            }

            WagonSharedStorage storage = FindAnyObjectByType<WagonSharedStorage>(); // 마차 보관함

            if (storage != null) // 있음
            {
                storage.AcceptNetworkItem(item); // 넣기
            }
        }

        public static void ReleaseCarriedItems(NetPlayerAvatar avatar) // 대원이 나가면 그 대원이 가진 아이템을 그 자리에 떨어뜨림 (모든 대원 화면에서 같은 위치)
        {
            if (avatar == null) // 없음
            {
                return; // 생략
            }

            WorldItem[] carried = avatar.GetComponentsInChildren<WorldItem>(true); // 가진 아이템
            Applying = true; // 적용 중

            try
            {
                for (int index = 0; index < carried.Length; index++) // 순회
                {
                    WorldItem item = carried[index]; // 아이템
                    Vector3 offset = Quaternion.Euler(0f, index * 55f, 0f) * new Vector3(0.45f, 0.4f, 0f); // 흩뿌림
                    ReleaseTo(item, avatar.transform.position + offset, item.transform.rotation, Vector3.zero, false); // 바닥으로

                    if (Instance != null) // 동기화 있음
                    {
                        string id = IdOf(item); // 아이디

                        if (!string.IsNullOrEmpty(id)) // 유효
                        {
                            Instance.holders.Remove(id); // 소지 해제
                        }
                    }
                }
            }
            finally
            {
                Applying = false; // 종료
            }

            if (Instance != null) // 동기화 있음
            {
                Instance.equipped.Remove(avatar.OwnerClientId); // 정리
            }
        }

        private void DetachEverywhere(WorldItem item) // 보관함·단상·내 슬롯에서 빼기
        {
            foreach (WagonSharedStorage storage in FindObjectsByType<WagonSharedStorage>(FindObjectsSortMode.None)) // 마차 보관함
            {
                storage.ReleaseNetworkItem(item); // 목록에서 제거
            }

            OfficeStoredItemState stored = item.GetComponent<OfficeStoredItemState>(); // 단상 상태

            if (stored != null && stored.IsOfficeStored && stored.Pedestal != null) // 단상 보관
            {
                stored.Pedestal.ReleaseNetworkItem(item); // 단상 비우기
            }

            PlayerInventory inventory = LocalInventory(); // 내 인벤토리

            if (IsInLocalInventory(inventory, item)) // 내가 가짐 (방장 판정과 다름)
            {
                inventory.RemoveItemForNetwork(item); // 제거
            }
        }

        private static void ApplyValue(WorldItem item, NetItemState state) // 가치·판매 상태
        {
            RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 가치

            if (recoverable == null || recoverable.Value == state.value) // 같음
            {
                return; // 생략
            }

            recoverable.Configure(state.value); // 가치

            if (state.sold) // 판매됨
            {
                recoverable.MarkSold(); // 표시
            }
        }

        private static WorldItem Spawn(NetItemState state) // 목록 상태로 아이템 생성
        {
            ItemInstanceData data = new ItemInstanceData { instanceId = state.id, itemId = state.itemId, displayName = state.name, value = state.value, isSold = state.sold }; // 복구 데이터
            WorldItem item = ItemFactory.SpawnForRecovery(data); // 생성

            if (item != null && Instance != null) // 성공
            {
                Instance.cache[state.id] = item; // 등록
            }

            return item; // 반환
        }

        private static NetItemState CaptureBase(WorldItem item) // 기본 정보
        {
            WorldItemIdentity identity = item == null ? null : item.GetComponent<WorldItemIdentity>(); // 식별

            if (identity == null || string.IsNullOrEmpty(identity.InstanceId)) // 없음
            {
                return null; // 생략
            }

            RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 가치
            return new NetItemState
            {
                id = identity.InstanceId, // ID
                itemId = identity.ItemId, // 종류
                name = item.DisplayName, // 이름
                value = recoverable == null ? 0 : recoverable.Value, // 가치
                sold = recoverable != null && recoverable.IsSold, // 판매
            };
        }

        private static bool ToWagonSpace(Vector3 position, Quaternion rotation, out Vector3 pos, out Quaternion rot) // 마차 적재칸 안이면 마차 기준 좌표
        {
            pos = position; // 기본
            rot = rotation; // 기본
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            Transform wagon = loader == null ? null : loader.WagonRoot; // 마차
            WagonCargoArea cargo = wagon == null ? null : wagon.GetComponentInChildren<WagonCargoArea>(true); // 적재칸
            BoxCollider box = cargo == null ? null : cargo.GetComponent<BoxCollider>(); // 판정 박스

            if (box == null) // 없음
            {
                return false; // 월드 좌표
            }

            Vector3 local = box.transform.InverseTransformPoint(position) - box.center; // 박스 기준
            Vector3 half = box.size * 0.5f + (Vector3.one * 0.2f); // 여유 포함 반크기

            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) // 밖
            {
                return false; // 월드 좌표
            }

            pos = wagon.InverseTransformPoint(position); // 마차 기준 위치
            rot = Quaternion.Inverse(wagon.rotation) * rotation; // 마차 기준 회전
            return true; // 마차 기준
        }

        private static bool Ready(WorldItem item, out string id) // 알림 가능 여부
        {
            id = null; // 기본

            if (!Active || Applying || item == null) // 협동 아님·받은 변경 적용 중
            {
                return false; // 생략
            }

            id = IdOf(item); // 아이디
            return !string.IsNullOrEmpty(id); // 결과
        }

        public static string IdOf(WorldItem item) // 개별 ID
        {
            WorldItemIdentity identity = item == null ? null : item.GetComponent<WorldItemIdentity>(); // 식별
            return identity == null ? null : identity.InstanceId; // 반환
        }

        private static WorldItem Find(string id) // ID로 아이템 찾기
        {
            if (string.IsNullOrEmpty(id) || Instance == null) // 없음
            {
                return null; // 생략
            }

            if (Instance.cache.TryGetValue(id, out WorldItem cached) && cached != null && IdOf(cached) == id) // 캐시
            {
                return cached; // 반환
            }

            Instance.cache.Clear(); // 다시 만들기

            foreach (WorldItemIdentity identity in FindObjectsByType<WorldItemIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // 전체 (숨긴 것 포함)
            {
                WorldItem item = identity.GetComponent<WorldItem>(); // 아이템

                if (item != null && !string.IsNullOrEmpty(identity.InstanceId)) // 유효
                {
                    Instance.cache[identity.InstanceId] = item; // 등록
                }
            }

            return Instance.cache.TryGetValue(id, out WorldItem found) ? found : null; // 결과
        }

        private static OfficeStoragePedestal FindPedestal(string key) // 경로로 단상 찾기 (이름 대체 포함)
        {
            OfficeStoragePedestal byName = null; // 이름 일치
            string name = string.IsNullOrEmpty(key) ? string.Empty : key.Substring(key.LastIndexOf('/') + 1); // 마지막 이름

            foreach (OfficeStoragePedestal pedestal in FindObjectsByType<OfficeStoragePedestal>(FindObjectsSortMode.None)) // 단상
            {
                if (Day23SnapshotBridge.BuildTransformPath(pedestal.transform) == key) // 경로 일치
                {
                    return pedestal; // 반환
                }

                if (pedestal.name == name) // 이름 일치
                {
                    byName = pedestal; // 기록
                }
            }

            return byName; // 대체
        }

        private static PlayerInventory LocalInventory() // 내 인벤토리
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            Transform player = loader == null ? null : loader.PlayerRoot; // 플레이어
            return player == null ? null : player.GetComponentInChildren<PlayerInventory>(true); // 인벤토리
        }

        private static bool IsInLocalInventory(PlayerInventory inventory, WorldItem item) // 내 슬롯에 있음
        {
            if (inventory == null || item == null) // 없음
            {
                return false; // 아님
            }

            for (int index = 0; index < inventory.SlotCount; index++) // 슬롯
            {
                if (inventory.GetItem(index) == item) // 일치
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static bool Contains(IReadOnlyList<WorldItem> list, WorldItem item) // 목록 포함
        {
            for (int index = 0; index < list.Count; index++) // 순회
            {
                if (list[index] == item) // 일치
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static bool IsSubItem(WorldItem item) // 다른 아이템 안의 아이템
        {
            return item.transform.parent != null && item.transform.parent.GetComponentInParent<WorldItem>() != null; // 결과
        }

        private static bool IsSold(WorldItem item) // 판매됨
        {
            RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 가치
            return recoverable != null && recoverable.IsSold; // 결과
        }
    }
}
