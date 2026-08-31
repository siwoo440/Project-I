using ProjectI.Brightness; // 게임 판정용 밝기 광원 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class ElectricLightController : MonoBehaviour, IPowerStateReceiver // 발전기 또는 방 전력을 받아 전기등 상태를 관리
    {
        [SerializeField] private string displayName = "전기등"; // 디버그 표시 이름
        [SerializeField] private bool isPowered; // 현재 전력 공급 상태
        [SerializeField] private BrightnessSource[] brightnessSources; // 실제 화면과 게임 밝기를 제어할 광원 목록
        [SerializeField] private GameObject[] poweredVisuals; // 전력 공급 중 표시 시각 요소
        [SerializeField] private GameObject[] unpoweredVisuals; // 정전 상태 표시 시각 요소

        public string DisplayName => displayName; // 전기등 표시 이름 공개
        public bool IsPowered => isPowered; // 현재 전력 공급 상태 공개
        public BrightnessSource[] BrightnessSources => brightnessSources; // Validator용 광원 목록 공개

        private void Awake() // 전기등 초기화
        {
            ResolveSources(); // 연결 밝기 광원 확보
            ApplyState(); // 저장 전력 상태 적용
        }

        private void OnEnable() // 전기등 활성화 처리
        {
            ResolveSources(); // 활성화 직후 광원 목록 확보
            ApplyState(); // 현재 전력 상태 재적용
        }

        public void Configure(string targetDisplayName, bool startPowered, BrightnessSource[] sources, GameObject[] activeVisuals, GameObject[] inactiveVisuals) // 에디터 자동 구성용 전기등 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 표시 이름 저장
            isPowered = startPowered; // 시작 전력 상태 저장
            brightnessSources = sources; // 제어할 밝기 광원 저장
            poweredVisuals = activeVisuals; // 점등 시각 요소 저장
            unpoweredVisuals = inactiveVisuals; // 소등 시각 요소 저장
            ResolveSources(); // 누락된 밝기 광원 자동 조회
            ApplyState(); // 새 설정을 즉시 실제 상태에 적용
        }

        public void SetPowered(bool powered) // 발전기 또는 전력 소비자에서 전력 상태 전달
        {
            isPowered = powered; // 현재 전력 상태 저장
            ApplyState(); // 실제 Light와 시각 요소 동기화
        }

        public void OnPowerStateChanged(bool hasPower) // 공통 PowerConsumer의 방 전력 상태 변경 수신
        {
            SetPowered(hasPower); // 공통 전력 상태를 기존 전기등 동작에 연결
        }

        private void ApplyState() // 전력 상태를 밝기와 시각 요소에 적용
        {
            ResolveSources(); // 제어할 광원 목록 확보

            foreach (BrightnessSource source in brightnessSources) // 연결된 밝기 광원 전체 순회
            {
                if (source == null) // 유효 광원 여부 확인
                {
                    continue; // 누락 광원 건너뜀
                }

                source.SetSourceEnabled(isPowered); // 전력 상태에 맞춰 실제 Light와 게임 밝기 동기화
            }

            SetVisualArrayState(poweredVisuals, isPowered); // 점등 표시 요소 상태 적용
            SetVisualArrayState(unpoweredVisuals, !isPowered); // 소등 표시 요소 상태 적용
        }

        private void ResolveSources() // 자식 구조에서 밝기 광원 목록 확보
        {
            if (brightnessSources == null || brightnessSources.Length == 0) // 직렬화된 광원 목록 누락 여부 확인
            {
                brightnessSources = GetComponentsInChildren<BrightnessSource>(true); // 현재 전기등 아래 광원 자동 조회
            }
        }

        private void SetVisualArrayState(GameObject[] visuals, bool activeState) // 시각 요소 배열 활성화 처리
        {
            if (visuals == null) // 시각 요소 배열 존재 여부 확인
            {
                return; // 배열이 없으면 처리 생략
            }

            foreach (GameObject visual in visuals) // 시각 요소 전체 순회
            {
                if (visual == null) // 유효 시각 요소 여부 확인
                {
                    continue; // 누락 요소 건너뜀
                }

                visual.SetActive(activeState); // 현재 전력 상태에 맞춰 시각 요소 활성화
            }
        }

        private void OnValidate() // 인스펙터 변경 시 상태 동기화
        {
            ResolveSources(); // 에디터에서도 광원 목록 확보
            ApplyState(); // 인스펙터 전력 상태 즉시 반영
        }
    }
}
