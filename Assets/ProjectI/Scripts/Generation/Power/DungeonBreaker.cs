using ProjectI.Interaction; // 상호작용 규약 참조
using ProjectI.Net; // 39일차 협동 장치
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonBreaker : MonoBehaviour, IInteractable, INetworkDevice // 배전반 — 담당 구역의 전기를 올리고 내림
    {
        [SerializeField] private Transform lever; // 손잡이 (상태에 따라 기울어짐)
        [SerializeField] private Renderer indicator; // 표시등
        [SerializeField] private float leverAngle = 38f; // 올렸을 때 각도
        private DungeonPowerZone zone; // 담당 구역
        private Material indicatorMaterial; // 표시등 재질 인스턴스

        public DungeonPowerZone Zone => zone; // 구역 공개
        public bool IsOn => zone != null && zone.BreakerOn; // 차단기 상태 공개
        public string Prompt => zone == null ? "배전반 — 연결되지 않음" : IsOn ? $"{zone.ZoneId + 1}구역 배전반 내리기" : $"{zone.ZoneId + 1}구역 배전반 올리기"; // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // 누르기
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(Transform leverTransform, Renderer indicatorRenderer) // 구성 (프리팹 제작기에서 호출)
        {
            lever = leverTransform; // 손잡이
            indicator = indicatorRenderer; // 표시등
        }

        public void Bind(DungeonPowerZone target) // 구역 연결 (생성기에서 호출)
        {
            zone = target; // 구역

            if (zone != null) // 확인
            {
                zone.PowerChanged += HandlePowerChanged; // 상태 변화 구독
            }

            Refresh(); // 표시 반영
        }

        private void OnDestroy() // 구독 해제
        {
            if (zone != null) // 확인
            {
                zone.PowerChanged -= HandlePowerChanged; // 해제
            }
        }

        public bool CanInteract(PlayerInteractor interactor) // 조작 가능 여부
        {
            return zone != null; // 구역이 연결되어 있어야 함
        }

        public void Interact(PlayerInteractor interactor) // 차단기 올리기·내리기
        {
            if (zone == null) // 연결 없음
            {
                return; // 종료
            }

            zone.SetBreaker(!zone.BreakerOn); // 전환
            Refresh(); // 표시 반영
            NetCombatSync.NotifyDeviceChanged(this); // 협동: 모두 같은 차단기
        }

        public int NetworkState => zone == null ? -1 : (zone.BreakerOn ? 1 : 0); // 협동 상태 (차단기)

        public void ApplyNetworkState(int state) // 협동: 차단기 상태 적용
        {
            if (zone == null || zone.BreakerOn == (state == 1)) // 같음
            {
                return; // 생략
            }

            zone.SetBreaker(state == 1); // 적용
            Refresh(); // 표시
        }

        private void HandlePowerChanged(DungeonPowerZone changed) // 구역 상태 변화
        {
            Refresh(); // 표시 반영
        }

        private void Refresh() // 손잡이 각도·표시등 색 갱신
        {
            bool on = IsOn; // 상태

            if (lever != null) // 손잡이
            {
                lever.localRotation = Quaternion.Euler(on ? -leverAngle : leverAngle, 0f, 0f); // 기울기
            }

            if (indicator == null) // 표시등 없음
            {
                return; // 종료
            }

            indicatorMaterial = indicatorMaterial != null ? indicatorMaterial : indicator.material; // 인스턴스 재질
            bool powered = zone != null && zone.HasPower; // 실제 전기 여부
            indicatorMaterial.color = powered ? new Color(0.35f, 1f, 0.45f) : on ? new Color(1f, 0.78f, 0.25f) : new Color(0.9f, 0.25f, 0.25f); // 녹색=정상 / 노랑=발전기 정지 / 빨강=차단
        }
    }
}
