using System.Collections.Generic; // 목록
using ProjectI.Combat; // 피해
using ProjectI.Dungeon; // 던전 장치
using ProjectI.Items; // 42일차: 든 무기 확인
using ProjectI.Loop; // 맵 로더
using ProjectI.Monsters; // 몬스터
using ProjectI.Persistence; // 저장 서비스
using ProjectI.Traps; // 함정
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public struct MonsterNetState : INetworkSerializable // 몬스터 하나의 위치·상태 (초당 10번)
    {
        public int Id; // 경로 해시
        public Vector3 Position; // 위치
        public float Yaw; // 방향
        public byte State; // AI 상태
        public byte Flags; // 1 = 미믹 위장 중

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter // 직렬화
        {
            serializer.SerializeValue(ref Id); // ID
            serializer.SerializeValue(ref Position); // 위치
            serializer.SerializeValue(ref Yaw); // 방향
            serializer.SerializeValue(ref State); // 상태
            serializer.SerializeValue(ref Flags); // 표시
        }
    }

    public struct NetValueEntry : INetworkSerializable // 경로 해시 + 값 (체력·장치 상태 목록)
    {
        public int Id; // 경로 해시
        public float Value; // 값

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter // 직렬화
        {
            serializer.SerializeValue(ref Id); // ID
            serializer.SerializeValue(ref Value); // 값
        }
    }

    public sealed class NetCombatSync : NetworkBehaviour // 39일차: 몬스터·피해·함정·장치를 방장 기준으로 맞춤 (경로 해시로 같은 오브젝트를 찾음)
    {
        private const float MonsterSendInterval = 0.1f; // 몬스터 전송 간격
        private const int MonsterBatch = 24; // 한 번에 보낼 몬스터 수
        private const float RegistryRefreshInterval = 1.5f; // 목록 다시 찾는 최소 간격 (전체 컴포넌트 검색이라 자주 하지 않음)
        private const float SyncCheckInterval = 0.5f; // 참가자 전체 상태 요청 확인 간격

        private readonly Dictionary<int, MonoBehaviour> monsters = new Dictionary<int, MonoBehaviour>(); // 움직이는 몬스터 (공통 두뇌·웃는 석상)
        private readonly Dictionary<int, IDamageable> damageables = new Dictionary<int, IDamageable>(); // 피격 대상 (체력·파괴물)
        private readonly Dictionary<int, TrapControllerBase> traps = new Dictionary<int, TrapControllerBase>(); // 함정
        private readonly Dictionary<int, INetworkDevice> devices = new Dictionary<int, INetworkDevice>(); // 장치
        private readonly Dictionary<int, MonsterNetState> puppetTargets = new Dictionary<int, MonsterNetState>(); // 참가자: 몬스터 목표 상태
        private readonly List<MonsterNetState> sendBuffer = new List<MonsterNetState>(); // 방장 전송 버퍼
        private float nextMonsterSend; // 다음 전송
        private float nextRegistryRefresh; // 다음 목록 갱신
        private float nextSyncCheck; // 다음 요청 확인
        private int syncedMap = -1; // 전체 상태를 받은 맵

        public static NetCombatSync Instance { get; private set; } // 현재 동기화
        public static bool Applying { get; private set; } // 받은 변경 적용 중
        public static bool Active => NetworkSession.IsOnline && Instance != null && Instance.IsSpawned; // 협동 중
        public static bool IsAuthority => Active && Instance.IsServer; // 방장
        public static bool PuppetMonsters => Active && !Instance.IsServer; // 참가자: 몬스터는 방장 상태를 따라 움직이기만 함
        public int PuppetCount => puppetTargets.Count; // 검증용

        public override void OnNetworkSpawn() // 생성
        {
            Instance = this; // 등록
        }

        public override void OnNetworkDespawn() // 제거
        {
            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }
        }

        public override void OnDestroy() // 파괴
        {
            if (Instance == this) // 현재
            {
                Instance = null; // 정리
            }

            base.OnDestroy(); // 넷코드 정리
        }

        private void Update() // 방장: 몬스터 전송 · 참가자: 몬스터 따라가기·전체 상태 요청
        {
            if (!IsSpawned) // 연결 전
            {
                return; // 생략
            }

            if (IsServer) // 방장
            {
                if (Time.unscaledTime >= nextMonsterSend) // 간격
                {
                    nextMonsterSend = Time.unscaledTime + MonsterSendInterval; // 다음
                    SendMonsterStates(); // 전송
                }

                return; // 종료
            }

            MovePuppets(); // 몬스터 따라가기
            CheckFullSync(); // 맵 도착 시 전체 상태 요청
        }

        // ───────────────────────── 경로 해시 · 목록 ─────────────────────────

        public static int PathHash(Transform target) // 씬 + 계층 경로 해시 (FNV-1a)
        {
            string path = NetWorldState.DoorPath(target); // 경로
            unchecked
            {
                int hash = (int)2166136261; // 시작값

                for (int index = 0; index < path.Length; index++) // 글자
                {
                    hash = (hash ^ path[index]) * 16777619; // 섞기
                }

                return hash; // 반환
            }
        }

        private void RefreshRegistry(bool force) // 몬스터·피격 대상·함정·장치 목록 다시 만들기
        {
            if (!force && Time.unscaledTime < nextRegistryRefresh) // 간격
            {
                return; // 생략
            }

            nextRegistryRefresh = Time.unscaledTime + RegistryRefreshInterval; // 다음
            monsters.Clear(); // 비우기
            damageables.Clear(); // 비우기
            traps.Clear(); // 비우기
            devices.Clear(); // 비우기

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) // 활성 컴포넌트
            {
                switch (behaviour)
                {
                    case MonsterBrain brain:
                        monsters[PathHash(brain.transform)] = brain; // 몬스터
                        break;
                    case SmilingStatueBehavior statue:
                        monsters[PathHash(statue.transform)] = statue; // 웃는 석상 (공통 두뇌 없음)
                        break;
                    case TrapControllerBase trap:
                        traps[PathHash(trap.transform)] = trap; // 함정
                        break;
                }

                if (behaviour is INetworkDevice device) // 장치
                {
                    devices[PathHash(behaviour.transform)] = device; // 등록
                }

                if (behaviour is IDamageable damageable && !(behaviour is PlayerDamageReceiver) && !(behaviour is RemotePlayerTarget)) // 플레이어가 아닌 피격 대상
                {
                    damageables[PathHash(behaviour.transform)] = damageable; // 등록
                }
            }
        }

        private T Lookup<T>(Dictionary<int, T> table, int id) where T : class // 목록 조회 (없으면 한 번 다시 만듦)
        {
            if (table.TryGetValue(id, out T found) && found != null && !(found is Object unityObject && unityObject == null)) // 있음
            {
                return found; // 반환
            }

            RefreshRegistry(false); // 다시 만들기
            return table.TryGetValue(id, out found) ? found : null; // 결과
        }

        // ───────────────────────── 몬스터 ─────────────────────────

        private void SendMonsterStates() // 방장: 같은 맵 몬스터 위치·상태 전송
        {
            RefreshRegistry(false); // 목록
            sendBuffer.Clear(); // 비우기

            foreach (KeyValuePair<int, MonoBehaviour> pair in monsters) // 몬스터
            {
                MonoBehaviour mover = pair.Value; // 몬스터

                if (mover == null) // 파괴됨
                {
                    continue; // 다음
                }

                MonsterBrain brain = mover as MonsterBrain; // 공통 두뇌
                ChestMimicBehavior mimic = mover.GetComponent<ChestMimicBehavior>(); // 미믹
                sendBuffer.Add(new MonsterNetState
                {
                    Id = pair.Key, // ID
                    Position = mover.transform.position, // 위치
                    Yaw = mover.transform.eulerAngles.y, // 방향
                    State = (byte)(brain == null ? 0 : (int)brain.State), // 상태
                    Flags = (byte)(mimic != null && mimic.IsDisguised && !mimic.IsRevealing ? 1 : 0), // 위장
                });
            }

            for (int start = 0; start < sendBuffer.Count; start += MonsterBatch) // 나눠 보내기
            {
                int count = Mathf.Min(MonsterBatch, sendBuffer.Count - start); // 개수
                MonsterStatesRpc(sendBuffer.GetRange(start, count).ToArray()); // 전송
            }
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void MonsterStatesRpc(MonsterNetState[] states) // 참가자: 몬스터 목표 상태 수신
        {
            foreach (MonsterNetState state in states) // 몬스터
            {
                puppetTargets[state.Id] = state; // 기록
            }
        }

        private void MovePuppets() // 참가자: 몬스터를 방장 상태로 부드럽게 옮김
        {
            if (puppetTargets.Count == 0) // 없음
            {
                return; // 생략
            }

            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime); // 따라가는 정도
            bool moved = false; // 이동 여부

            foreach (KeyValuePair<int, MonsterNetState> pair in puppetTargets) // 목표
            {
                MonoBehaviour mover = Lookup(monsters, pair.Key); // 내 화면 몬스터

                if (mover == null) // 없음 (아직 생성 전·다른 맵)
                {
                    continue; // 다음
                }

                MonsterNetState state = pair.Value; // 목표
                Transform body = mover.transform; // 몸
                MonsterBrain brain = mover as MonsterBrain; // 공통 두뇌
                bool far = (body.position - state.Position).sqrMagnitude > 16f; // 4m 이상
                Vector3 position = far ? state.Position : Vector3.Lerp(body.position, state.Position, blend); // 위치
                Quaternion rotation = far ? Quaternion.Euler(0f, state.Yaw, 0f) : Quaternion.Slerp(body.rotation, Quaternion.Euler(0f, state.Yaw, 0f), blend); // 방향
                body.SetPositionAndRotation(position, rotation); // 적용
                moved = true; // 기록

                if (brain != null) // 공통 두뇌
                {
                    brain.ApplyNetworkState((MonsterState)state.State); // 상태 표시
                }

                ChestMimicBehavior mimic = mover.GetComponent<ChestMimicBehavior>(); // 미믹

                if (mimic != null && (state.Flags & 1) == 0 && mimic.IsDisguised && !mimic.IsRevealing) // 방장 화면에서 정체를 드러냄
                {
                    mimic.BeginNetworkReveal(); // 변신 연출
                }
            }

            if (moved) // 이동함
            {
                Physics.SyncTransforms(); // 무기 명중 판정용 충돌 위치 반영
            }
        }

        public static void NotifyMonsterMelee(MonsterMeleeAttack attack) // 방장: 몬스터 근접 공격 시작 → 참가자 화면에 휘두르기 연출
        {
            if (!IsAuthority || attack == null) // 방장만
            {
                return; // 생략
            }

            MonsterBrain brain = attack.GetComponentInParent<MonsterBrain>(); // 몬스터
            Transform root = brain != null ? brain.transform : attack.transform; // 기준
            Instance.MonsterMeleeRpc(PathHash(root)); // 방송
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void MonsterMeleeRpc(int id) // 참가자: 휘두르기 연출 (피해 없음)
        {
            RefreshRegistry(false); // 목록
            MonoBehaviour root = Lookup(monsters, id); // 몬스터

            if (root == null) // 석상 등 몬스터 두뇌가 없는 경우
            {
                foreach (MonsterMeleeAttack candidate in FindObjectsByType<MonsterMeleeAttack>(FindObjectsSortMode.None)) // 근접 공격
                {
                    if (PathHash(candidate.transform) == id) // 일치
                    {
                        candidate.PlayNetworkSwing(); // 연출
                        return; // 종료
                    }
                }

                return; // 없음
            }

            MonsterMeleeAttack attack = root.GetComponentInChildren<MonsterMeleeAttack>(); // 근접 공격

            if (attack != null) // 있음
            {
                attack.PlayNetworkSwing(); // 연출
            }
        }

        public static void NotifyArrow(CorruptedUndeadArcherAttack archer, Vector3 position, Vector3 velocity) // 방장: 화살 발사 → 참가자 화면에 화살 (피해 없음)
        {
            if (!IsAuthority || archer == null) // 방장만
            {
                return; // 생략
            }

            MonsterBrain brain = archer.GetComponentInParent<MonsterBrain>(); // 몬스터
            Instance.ArrowRpc(PathHash(brain != null ? brain.transform : archer.transform), position, velocity); // 방송
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void ArrowRpc(int id, Vector3 position, Vector3 velocity) // 참가자: 보이기만 하는 화살
        {
            MonoBehaviour mover = Lookup(monsters, id); // 몬스터
            CorruptedUndeadArcherAttack archer = mover == null ? null : mover.GetComponentInChildren<CorruptedUndeadArcherAttack>(); // 궁수

            if (archer != null) // 있음
            {
                archer.PlayNetworkArrow(position, velocity); // 연출
            }
        }

        // ───────────────────────── 피해 ─────────────────────────

        public static bool TryRelayDamage(DamageInfo damageInfo, IDamageable target, out CombatHitResult result) // 참가자 피해 처리 규칙 (처리했으면 true)
        {
            result = default; // 기본

            if (!PuppetMonsters || target == null || target is PlayerDamageReceiver) // 방장·혼자·내 플레이어 피해
            {
                return false; // 기존 처리
            }

            GameObject targetObject = target.DamageTransform == null ? null : target.DamageTransform.gameObject; // 대상

            if (target is RemotePlayerTarget || targetObject == null) // 다른 대원 (참가자는 판정하지 않음)
            {
                result = new CombatHitResult(false, targetObject, damageInfo.BaseDamage, 0f, false, "Remote Player", damageInfo.HitPoint); // 거부
                return true; // 처리됨
            }

            if (damageInfo.SourceFaction != CombatFaction.Player) // 함정·몬스터가 몬스터를 때림 → 방장 화면에서 처리
            {
                result = new CombatHitResult(false, targetObject, damageInfo.BaseDamage, 0f, false, "Host Authority", damageInfo.HitPoint); // 거부
                return true; // 처리됨
            }

            if (!target.IsAlive || !CombatFactionRules.CanDamage(damageInfo.SourceFaction, target.Faction)) // 적용 불가
            {
                return false; // 기존 처리 (거부 결과)
            }

            MonoBehaviour behaviour = target as MonoBehaviour; // 컴포넌트
            int id = PathHash(behaviour != null ? behaviour.transform : target.DamageTransform); // 경로
            Instance.DamageRequestRpc(id, damageInfo.BaseDamage, (byte)damageInfo.DamageType, damageInfo.HitPoint, damageInfo.HitNormal, damageInfo.StaggerPower, damageInfo.Force, damageInfo.AttackId); // 방장에게 요청
            result = new CombatHitResult(true, targetObject, damageInfo.BaseDamage, damageInfo.BaseDamage, false, "Relayed", damageInfo.HitPoint); // 맞힌 것으로 표시 (체력은 방장 값)
            return true; // 처리됨
        }

        [Rpc(SendTo.Server)]
        private void DamageRequestRpc(int id, float damage, byte damageType, Vector3 hitPoint, Vector3 hitNormal, float stagger, Vector3 force, int attackId, RpcParams rpcParams = default) // 방장: 참가자 공격 적용
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Damage)) // 42일차: 횟수
            {
                return; // 무시
            }

            if (!NetGuard.Finite(damage) || damage < 0f || !NetGuard.InWorld(hitPoint) || !NetGuard.Finite(hitNormal) || !NetGuard.Finite(stagger) || !NetGuard.Finite(force) || !System.Enum.IsDefined(typeof(CombatDamageType), (int)damageType)) // 잘못된 값
            {
                NetGuard.Reject(sender, "피해", "잘못된 값", 5f); // 경고
                return; // 무시
            }

            IDamageable target = Lookup(damageables, id); // 대상

            if (target == null || target.DamageTransform == null) // 없음 (다른 맵·이미 파괴)
            {
                return; // 무시
            }

            NetPlayerAvatar attacker = NetPlayerAvatar.Find(sender); // 공격한 대원 몸체

            if (attacker == null || attacker.IsDeadRemote || !attacker.TryGetNetworkWorldPosition(out Vector3 attackerPosition)) // 몸체 없음·쓰러짐·다른 맵
            {
                NetGuard.Reject(sender, "피해", "공격자 상태", 1f); // 경고
                return; // 무시
            }

            WorldItem weapon = NetItemSync.EquippedItemOf(sender, out string weaponId); // 든 무기

            if (!NetGuard.TryWeaponProfile(weapon, out float maxDamage, out float reach, out float minInterval)) // 무기가 아님 → 방금 바꾼 무기 확인 (날아가는 화살)
            {
                weapon = NetItemSync.RecentEquippedItemOf(sender, out weaponId); // 이전 무기

                if (!NetGuard.TryWeaponProfile(weapon, out maxDamage, out reach, out minInterval)) // 무기 없음
                {
                    NetGuard.Reject(sender, "피해", "무기 없음", 1f); // 경고
                    return; // 무시
                }
            }

            Vector3 targetPosition = target.DamageTransform.position; // 대상 위치

            if ((attackerPosition - targetPosition).sqrMagnitude > reach * reach) // 사거리 밖
            {
                NetGuard.Reject(sender, "피해", "거리", 1f); // 경고
                return; // 무시
            }

            if (!NetGuard.AcceptAttack(sender, weaponId, attackId, id, minInterval)) // 중복·너무 빠름
            {
                return; // 무시
            }

            if (damage > maxDamage * 1.01f + 0.01f) // 무기보다 센 피해
            {
                NetGuard.Reject(sender, "피해", "최대치 초과", 2f); // 경고
                damage = maxDamage; // 무기 최대치로 자름
            }

            if ((hitPoint - targetPosition).sqrMagnitude > NetGuard.HitPointSlack * NetGuard.HitPointSlack) // 맞은 지점이 대상과 동떨어짐
            {
                hitPoint = targetPosition; // 대상 위치로 고침
            }

            stagger = Mathf.Clamp(stagger, 0f, NetGuard.MaxStagger); // 경직 제한
            force = Vector3.ClampMagnitude(force, NetGuard.MaxForce); // 넉백 제한
            GameObject attackerObject = attacker.gameObject; // 공격자 (몬스터가 이 대원을 쫓음)
            DamageInfo info = new DamageInfo(attackerObject, attackerObject, CombatFaction.Player, (CombatDamageType)damageType, damage, hitPoint, hitNormal, stagger, force, attackId); // 피해
            DamagePipeline.TryApply(info, target, out _); // 적용 (체력 방송은 CombatHealth 에서)
        }

        public static void SendPlayerDamage(ulong clientId, DamageInfo damageInfo) // 방장: 몬스터 공격을 그 대원에게 전달
        {
            if (!IsAuthority) // 방장만
            {
                return; // 생략
            }

            Instance.PlayerDamageRpc(damageInfo.BaseDamage, (byte)damageInfo.DamageType, damageInfo.HitPoint, damageInfo.HitNormal, damageInfo.StaggerPower, damageInfo.Force, damageInfo.AttackId, Instance.RpcTarget.Single(clientId, RpcTargetUse.Temp)); // 전송
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void PlayerDamageRpc(float damage, byte damageType, Vector3 hitPoint, Vector3 hitNormal, float stagger, Vector3 force, int attackId, RpcParams rpcParams) // 대원: 방장 몬스터의 공격을 내 플레이어에 적용
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            PlayerDamageReceiver receiver = loader == null || loader.PlayerRoot == null ? null : loader.PlayerRoot.GetComponentInChildren<PlayerDamageReceiver>(); // 내 플레이어

            if (receiver == null) // 없음
            {
                return; // 무시
            }

            DamageInfo info = new DamageInfo(null, null, CombatFaction.Enemy, (CombatDamageType)damageType, damage, hitPoint, hitNormal, stagger, force, attackId); // 피해
            DamagePipeline.TryApply(info, receiver, out _); // 적용 (경직·넉백 포함)
        }

        public static void NotifyHealth(CombatHealth health) // 방장: 체력 변화 방송 (몬스터·허수아비·부서지는 벽)
        {
            if (!IsAuthority || Applying || health == null) // 방장만
            {
                return; // 생략
            }

            Instance.HealthRpc(PathHash(health.transform), health.CurrentHealth); // 방송
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void HealthRpc(int id, float value) // 참가자: 체력 적용
        {
            ApplyHealth(id, value); // 적용
        }

        private void ApplyHealth(int id, float value) // 체력 적용 (사망·파괴 이벤트 포함)
        {
            if (Lookup(damageables, id) is CombatHealth health && !Mathf.Approximately(health.CurrentHealth, value)) // 다름
            {
                health.SetNetworkHealth(value); // 적용
            }
        }

        // ───────────────────────── 함정 ─────────────────────────

        public static bool ShouldRequestTrap(TrapControllerBase trap) // 참가자: 함정 작동을 방장에게 요청 (요청했으면 true)
        {
            if (!PuppetMonsters || Applying || trap == null) // 방장·혼자·받은 작동
            {
                return false; // 직접 작동
            }

            if (trap.CanTrigger) // 작동 가능 상태일 때만 요청
            {
                Instance.TrapRequestRpc(PathHash(trap.transform)); // 요청
            }

            return true; // 직접 작동하지 않음
        }

        public static void NotifyTrapFired(TrapControllerBase trap) // 방장: 함정 작동 → 모두 같은 연출 (피해는 각자 화면에서)
        {
            if (!IsAuthority || Applying || trap == null) // 방장만
            {
                return; // 생략
            }

            Instance.TrapFiredRpc(PathHash(trap.transform)); // 방송
        }

        [Rpc(SendTo.Server)]
        private void TrapRequestRpc(int id, RpcParams rpcParams = default) // 방장: 참가자 함정 요청
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Device)) // 42일차: 횟수
            {
                return; // 무시
            }

            TrapControllerBase trap = Lookup(traps, id); // 함정

            if (trap != null && !NetGuard.Near(sender, trap.transform.position, NetGuard.TrapReach)) // 함정에서 멂
            {
                NetGuard.Reject(sender, "함정", "거리", 1f); // 경고
                return; // 무시
            }

            if (trap != null && trap.CanTrigger) // 작동 가능
            {
                trap.TriggerTrap(null); // 작동 (방송은 TriggerTrap 안에서)
            }
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void TrapFiredRpc(int id) // 참가자: 함정 작동 연출
        {
            TrapControllerBase trap = Lookup(traps, id); // 함정

            if (trap == null) // 없음
            {
                return; // 생략
            }

            Applying = true; // 받은 작동

            try
            {
                trap.TriggerTrap(null); // 작동
            }
            finally
            {
                Applying = false; // 종료
            }
        }

        // ───────────────────────── 장치 ─────────────────────────

        public static void NotifyDeviceChanged(MonoBehaviour device) // 장치 상태가 바뀜 (조작한 대원 화면에서 먼저 적용)
        {
            if (!Active || Applying || !(device is INetworkDevice networkDevice)) // 대상 아님
            {
                return; // 생략
            }

            int id = PathHash(device.transform); // 경로
            int state = networkDevice.NetworkState; // 상태

            if (Instance.IsServer) // 방장
            {
                Instance.DeviceStateRpc(id, state, Instance.NetworkManager.LocalClientId); // 방송
                return; // 종료
            }

            Instance.DeviceChangedRpc(id, state); // 방장에게 알림
        }

        [Rpc(SendTo.Server)]
        private void DeviceChangedRpc(int id, int state, RpcParams rpcParams = default) // 방장: 참가자 장치 조작 적용 후 방송
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (!NetGuard.Allow(sender, NetChannel.Device)) // 42일차: 횟수
            {
                return; // 무시
            }

            if (state < 0 || state > MaxDeviceState) // 잘못된 상태 번호
            {
                NetGuard.Reject(sender, "장치", "잘못된 값", 5f); // 경고
                return; // 무시
            }

            INetworkDevice device = Lookup(devices, id); // 장치
            MonoBehaviour behaviour = device as MonoBehaviour; // 컴포넌트

            if (device == null || behaviour == null) // 없음
            {
                return; // 무시
            }

            float reach = behaviour is DungeonElevator ? NetGuard.ElevatorReach : NetGuard.InteractReach; // 승강기는 다른 층 버튼
            string denied = null; // 거절 이유

            if (!NetGuard.Near(sender, behaviour.transform.position, reach)) // 멂
            {
                denied = "거리"; // 이유
            }
            else if (behaviour is LockedRoomDoor lockedDoor && !lockedDoor.IsOpen && state == 1 && !NetItemSync.ConsumeKeyCredit(sender, lockedDoor.KeyItemId)) // 열쇠 없이 잠긴 방 열기
            {
                denied = "열쇠 없음"; // 이유
            }

            if (denied != null) // 거절
            {
                NetGuard.Reject(sender, "장치", denied, denied == "거리" ? 1f : 2f); // 경고
                DeviceCorrectionRpc(id, device.NetworkState, RpcTarget.Single(sender, RpcTargetUse.Temp)); // 그 대원 화면을 방장 상태로 되돌림
                return; // 종료
            }

            ApplyDevice(id, state); // 적용
            DeviceStateRpc(id, state, sender); // 방송
        }

        private const int MaxDeviceState = 64; // 장치 상태 번호 상한 (승강기 층 포함)

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void DeviceCorrectionRpc(int id, int state, RpcParams rpcParams) // 참가자: 거절된 조작을 방장 상태로 되돌림
        {
            ApplyDevice(id, state); // 적용 (잠긴 문은 다시 잠기지 않음)
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        private void DeviceStateRpc(int id, int state, ulong actor) // 참가자: 장치 상태 적용
        {
            if (actor == NetworkManager.LocalClientId) // 내가 조작함
            {
                return; // 이미 적용됨
            }

            ApplyDevice(id, state); // 적용
        }

        private void ApplyDevice(int id, int state) // 장치 상태 적용
        {
            INetworkDevice device = Lookup(devices, id); // 장치

            if (device == null || state < 0 || device.NetworkState == state) // 없음·같음
            {
                return; // 생략
            }

            Applying = true; // 받은 변경

            try
            {
                device.ApplyNetworkState(state); // 적용
            }
            finally
            {
                Applying = false; // 종료
            }
        }

        // ───────────────────────── 늦게 들어온 대원 ─────────────────────────

        private void CheckFullSync() // 참가자: 방장과 같은 맵에 도착하면 체력·장치 상태 전체 요청
        {
            if (Time.unscaledTime < nextSyncCheck) // 간격
            {
                return; // 대기
            }

            nextSyncCheck = Time.unscaledTime + SyncCheckInterval; // 다음
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            DailySnapshotService service = DailySnapshotService.Instance; // 저장
            NetWorldState world = NetWorldState.Instance; // 월드

            if (loader == null || service == null || world == null || !service.IsInitialized || loader.IsTransitioning || loader.CurrentDestination != world.Destination) // 아직
            {
                if (loader != null && loader.IsTransitioning) // 이동 중
                {
                    syncedMap = -1; // 도착하면 다시 받기
                    puppetTargets.Clear(); // 이전 맵 몬스터 정리
                }

                return; // 대기
            }

            if (syncedMap == (int)loader.CurrentDestination) // 이미 받음
            {
                return; // 생략
            }

            syncedMap = (int)loader.CurrentDestination; // 기록
            RefreshRegistry(true); // 새 맵 목록
            FullStateRequestRpc(); // 요청
        }

        [Rpc(SendTo.Server)]
        private void FullStateRequestRpc(RpcParams rpcParams = default) // 방장: 체력·장치 상태 전체 전송
        {
            if (!NetGuard.Allow(rpcParams.Receive.SenderClientId, NetChannel.Snapshot)) // 42일차: 횟수 (목록 만들기는 무거움)
            {
                return; // 무시
            }

            RefreshRegistry(true); // 목록
            List<NetValueEntry> health = new List<NetValueEntry>(); // 체력
            List<NetValueEntry> deviceStates = new List<NetValueEntry>(); // 장치

            foreach (KeyValuePair<int, IDamageable> pair in damageables) // 피격 대상
            {
                if (pair.Value is CombatHealth combat && combat.CurrentHealth < combat.MaxHealth) // 피해 입은 것만
                {
                    health.Add(new NetValueEntry { Id = pair.Key, Value = combat.CurrentHealth }); // 추가
                }
            }

            foreach (KeyValuePair<int, INetworkDevice> pair in devices) // 장치
            {
                int state = pair.Value.NetworkState; // 상태

                if (state >= 0) // 맞출 상태 있음
                {
                    deviceStates.Add(new NetValueEntry { Id = pair.Key, Value = state }); // 추가
                }
            }

            FullStateRpc(health.ToArray(), deviceStates.ToArray(), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)); // 전송
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void FullStateRpc(NetValueEntry[] health, NetValueEntry[] deviceStates, RpcParams rpcParams) // 참가자: 전체 상태 적용
        {
            RefreshRegistry(true); // 목록

            foreach (NetValueEntry entry in health) // 체력
            {
                ApplyHealth(entry.Id, entry.Value); // 적용
            }

            foreach (NetValueEntry entry in deviceStates) // 장치
            {
                ApplyDevice(entry.Id, Mathf.RoundToInt(entry.Value)); // 적용
            }

            Debug.Log($"[Project I] 협동 전투·장치 상태 적용 / 체력 {health.Length} · 장치 {deviceStates.Length}"); // 기록
        }

        public static void RunApplying(System.Action action) // 받은 변경으로 표시하고 실행 (장치 내부용)
        {
            bool previous = Applying; // 이전
            Applying = true; // 적용 중

            try
            {
                action?.Invoke(); // 실행
            }
            finally
            {
                Applying = previous; // 복구
            }
        }
    }
}
