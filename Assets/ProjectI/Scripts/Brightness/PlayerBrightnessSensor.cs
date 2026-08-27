using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public sealed class PlayerBrightnessSensor : MonoBehaviour // 플레이어 위치의 현재 게임용 밝기를 지속 측정
    {
        [SerializeField] private BrightnessManager brightnessManager; // 밝기 계산 관리자
        [SerializeField] private float updateInterval = 0.10f; // 밝기 재계산 간격
        private float updateTimer; // 다음 계산까지 남은 시간
        private BrightnessSample currentSample; // 마지막으로 계산된 현재 밝기 결과

        public BrightnessSample CurrentSample => currentSample; // UI와 이후 몬스터 시스템용 현재 결과 공개
        public float CurrentBrightness => currentSample.TotalBrightness; // 0~1 현재 밝기 공개
        public BrightnessLevel CurrentLevel => currentSample.Level; // 현재 밝기 단계 공개
        public BrightnessAreaType CurrentAreaType => currentSample.AreaType; // 현재 외부·내부 상태 공개

        private void Awake() // 센서 초기화
        {
            if (brightnessManager == null) // 밝기 관리자 참조 누락 확인
            {
                brightnessManager = Object.FindFirstObjectByType<BrightnessManager>(); // 현재 씬 밝기 관리자 자동 조회
            }
        }

        private void Start() // 플레이 시작 처리
        {
            SampleNow(); // 시작 위치의 밝기를 즉시 한 번 계산
        }

        private void Update() // 주기적인 현재 위치 밝기 계산
        {
            updateTimer -= Time.deltaTime; // 남은 계산 대기 시간 감소

            if (updateTimer > 0f) // 아직 다음 계산 시간이 아닌지 확인
            {
                return; // 이번 프레임 계산 생략
            }

            SampleNow(); // 현재 위치 밝기 다시 계산
            updateTimer = updateInterval; // 다음 계산까지 대기 시간 재설정
        }

        public void Configure(BrightnessManager manager) // 에디터 자동 설정용 밝기 관리자 연결
        {
            brightnessManager = manager; // 밝기 관리자 참조 저장
        }

        public void SampleNow() // 현재 플레이어 위치 밝기를 즉시 계산
        {
            if (brightnessManager == null) // 밝기 관리자 존재 여부 확인
            {
                return; // 계산 중단
            }

            currentSample = brightnessManager.SampleBrightness(transform.position); // 현재 월드 위치의 외부·내부 밝기 결과 저장
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            updateInterval = Mathf.Clamp(updateInterval, 0.02f, 1f); // 계산 주기를 과도하지 않은 범위로 제한
        }
    }
}
