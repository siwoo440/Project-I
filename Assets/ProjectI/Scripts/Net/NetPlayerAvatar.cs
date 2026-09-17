using System.Collections.Generic; // 원정대원 목록
using ProjectI.Loop; // 맵 로더·마차
using ProjectI.Player; // 사망·웅크리기
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed class NetPlayerAvatar : NetworkBehaviour // 다른 원정대원에게 보이는 몸체 (각자 자기 위치를 보냄 · 자신에게는 숨김)
    {
        private const float SendInterval = 0.05f; // 전송 간격 (초당 20회)
        private const float SnapDistance = 4f; // 이 거리 이상 차이나면 즉시 이동
        public static readonly List<NetPlayerAvatar> All = new List<NetPlayerAvatar>(); // 현재 원정대원

        private readonly NetworkVariable<Vector3> position = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 위치 (마차 동승 중이면 마차 기준)
        private readonly NetworkVariable<float> yaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 좌우 방향
        private readonly NetworkVariable<bool> riding = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 마차 기준 좌표 여부
        private readonly NetworkVariable<bool> aboard = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 마차 창고 탑승
        private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 쓰러짐
        private readonly NetworkVariable<bool> crouching = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 웅크림
        private readonly NetworkVariable<byte> destination = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 있는 맵

        [SerializeField] private Transform visualRoot; // 몸체 묶음
        [SerializeField] private Transform body; // 몸통 (웅크림 표시)
        [SerializeField] private TextMesh nameTag; // 이름표
        private Transform handPoint; // 손 (다른 대원이 든 아이템 표시)
        private Transform pocketPoint; // 주머니 (소지품 숨김)
        private Renderer[] renderers; // 표시 전환용
        private float nextSend; // 다음 전송
        private Vector3 shownPosition; // 보이는 위치
        private float shownYaw; // 보이는 방향
        private bool hasShown; // 처음 표시 여부
        private bool visible = true; // 현재 표시 상태
        private Vector3 bodyScale = Vector3.one; // 몸통 기본 크기

        public bool IsLocalOwner => IsSpawned && IsOwner; // 내 것
        public bool IsAboard => aboard.Value; // 탑승
        public bool IsDeadRemote => dead.Value; // 쓰러짐
        public int PlayerNumber => (int)OwnerClientId + 1; // 표시 번호
        public string DisplayName => $"원정대원 {PlayerNumber}"; // 표시 이름
        public Transform HandPoint => handPoint; // 손
        public Transform PocketPoint => pocketPoint; // 주머니

        public static NetPlayerAvatar Find(ulong clientId) // 대원 번호로 찾기
        {
            foreach (NetPlayerAvatar avatar in All) // 목록
            {
                if (avatar != null && avatar.IsSpawned && avatar.OwnerClientId == clientId) // 일치
                {
                    return avatar; // 반환
                }
            }

            return null; // 없음
        }

        public void Configure(Transform visual, Transform bodyTransform, TextMesh tag) // 에디터 구성용
        {
            visualRoot = visual; // 몸체
            body = bodyTransform; // 몸통
            nameTag = tag; // 이름표
        }

        private void Awake() // 준비
        {
            renderers = GetComponentsInChildren<Renderer>(true); // 표시 대상 (아이템을 붙이기 전의 몸체만)
            bodyScale = body == null ? Vector3.one : body.localScale; // 기본 크기
            handPoint = CreatePoint("Hand", new Vector3(0.32f, 1.05f, 0.42f)); // 손 (몸 앞 오른쪽)
            pocketPoint = CreatePoint("Pocket", new Vector3(0f, 1f, 0f)); // 주머니
        }

        public override void OnNetworkSpawn() // 생성
        {
            DontDestroyOnLoad(gameObject); // 맵이 바뀌어도 유지 (씬 교체는 맵 로더가 직접 함)
            gameObject.name = $"NetPlayer_{PlayerNumber}"; // 이름

            if (!All.Contains(this)) // 등록
            {
                All.Add(this); // 추가
            }

            if (nameTag != null) // 이름표
            {
                nameTag.text = DisplayName; // 글자
            }

            SetVisible(!IsOwner); // 내 몸체는 숨김 (1인칭)
        }

        public override void OnNetworkDespawn() // 제거
        {
            NetItemSync.ReleaseCarriedItems(this); // 가진 아이템을 그 자리에 떨어뜨림 (몸체와 함께 사라지지 않게)
            All.Remove(this); // 목록 정리
        }

        private Transform CreatePoint(string pointName, Vector3 localPosition) // 몸체 기준 점
        {
            Transform point = new GameObject(pointName).transform; // 점
            point.SetParent(visualRoot != null ? visualRoot : transform, false); // 몸체 아래 (쓰러지면 함께 눕기)
            point.localPosition = localPosition; // 위치
            return point; // 반환
        }

        public override void OnDestroy() // 파괴
        {
            All.Remove(this); // 목록 정리
            base.OnDestroy(); // 넷코드 정리
        }

        private void Update() // 전송·표시
        {
            if (!IsSpawned) // 연결 전
            {
                return; // 생략
            }

            if (IsOwner) // 내 것
            {
                SendLocalState(); // 전송
                return; // 종료
            }

            ShowRemoteState(); // 다른 대원 표시
        }

        private void LateUpdate() // 이름표가 카메라를 봄
        {
            if (nameTag == null || !visible) // 숨김
            {
                return; // 생략
            }

            Camera view = Camera.main; // 카메라

            if (view != null) // 있음
            {
                nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - view.transform.position, Vector3.up); // 카메라 방향
            }
        }

        private void SendLocalState() // 내 위치·상태 전송
        {
            if (Time.unscaledTime < nextSend) // 간격
            {
                return; // 대기
            }

            nextSend = Time.unscaledTime + SendInterval; // 다음
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            Transform player = loader == null ? null : loader.PlayerRoot; // 플레이어
            Transform wagon = loader == null ? null : loader.WagonRoot; // 마차

            if (player == null) // 게임 월드 준비 전
            {
                return; // 대기
            }

            bool ride = wagon != null && (loader.IsPlayerAttachedToWagon || loader.IsTransitioning); // 이동 중에는 마차 기준 좌표
            Vector3 point = ride ? wagon.InverseTransformPoint(player.position) : player.position; // 위치
            float angle = ride ? Mathf.DeltaAngle(wagon.eulerAngles.y, player.eulerAngles.y) : player.eulerAngles.y; // 방향

            if ((position.Value - point).sqrMagnitude > 0.0001f) // 바뀜
            {
                position.Value = point; // 전송
            }

            if (Mathf.Abs(Mathf.DeltaAngle(yaw.Value, angle)) > 0.5f) // 바뀜
            {
                yaw.Value = angle; // 전송
            }

            SetIfChanged(riding, ride); // 동승
            SetIfChanged(aboard, loader.IsPlayerAboard()); // 탑승
            PlayerDeathController death = player.GetComponentInChildren<PlayerDeathController>(); // 사망
            SetIfChanged(dead, death != null && death.IsDead); // 쓰러짐
            PlayerCrouch crouch = player.GetComponentInChildren<PlayerCrouch>(); // 웅크림
            SetIfChanged(crouching, crouch != null && crouch.IsCrouching); // 웅크림
            byte map = (byte)loader.CurrentDestination; // 맵

            if (destination.Value != map) // 바뀜
            {
                destination.Value = map; // 전송
            }
        }

        private void ShowRemoteState() // 받은 상태로 몸체 표시
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            Transform wagon = loader == null ? null : loader.WagonRoot; // 마차
            bool sameMap = loader != null && (riding.Value || (byte)loader.CurrentDestination == destination.Value); // 같은 맵 (동승 중이면 내 마차 기준으로 표시)
            SetVisible(sameMap && (!riding.Value || wagon != null)); // 표시

            if (!visible) // 숨김
            {
                hasShown = false; // 다음에 즉시 이동
                return; // 종료
            }

            Vector3 target = riding.Value ? wagon.TransformPoint(position.Value) : position.Value; // 목표 위치
            float targetYaw = riding.Value ? wagon.eulerAngles.y + yaw.Value : yaw.Value; // 목표 방향

            if (!hasShown || (shownPosition - target).sqrMagnitude > SnapDistance * SnapDistance) // 처음·순간이동
            {
                shownPosition = target; // 즉시
                shownYaw = targetYaw; // 즉시
                hasShown = true; // 기록
            }
            else
            {
                float blend = 1f - Mathf.Exp(-14f * Time.deltaTime); // 부드럽게 따라감
                shownPosition = Vector3.Lerp(shownPosition, target, blend); // 위치
                shownYaw = Mathf.LerpAngle(shownYaw, targetYaw, blend); // 방향
            }

            transform.SetPositionAndRotation(shownPosition, Quaternion.Euler(0f, shownYaw, 0f)); // 적용

            if (visualRoot != null) // 쓰러짐 표시
            {
                visualRoot.localRotation = dead.Value ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity; // 눕기
                visualRoot.localPosition = dead.Value ? new Vector3(0f, 0.25f, 0f) : Vector3.zero; // 바닥에
            }

            if (body != null) // 웅크림 표시
            {
                body.localScale = crouching.Value && !dead.Value ? Vector3.Scale(bodyScale, new Vector3(1f, 0.7f, 1f)) : bodyScale; // 낮춤
            }
        }

        private void SetVisible(bool show) // 몸체 표시 전환
        {
            if (visible == show) // 같음
            {
                return; // 생략
            }

            visible = show; // 기록

            if (handPoint != null) // 손에 든 아이템도 함께 숨김
            {
                handPoint.localScale = show ? Vector3.one : Vector3.zero; // 크기로 숨김 (비활성화하면 아이템 검색에서 빠짐)
            }

            foreach (Renderer part in renderers) // 표시 대상
            {
                if (part != null) // 유효
                {
                    part.enabled = show; // 적용
                }
            }
        }

        private static void SetIfChanged(NetworkVariable<bool> variable, bool value) // 바뀔 때만 전송
        {
            if (variable.Value != value) // 다름
            {
                variable.Value = value; // 전송
            }
        }
    }
}
