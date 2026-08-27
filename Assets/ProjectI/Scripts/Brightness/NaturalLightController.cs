using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public sealed class NaturalLightController : MonoBehaviour // 외부에서만 사용하는 태양·달 자연광 값 관리
    {
        [SerializeField, Range(0f, 1f)] private float sunBrightness = 0.55f; // 현재 태양의 게임용 밝기
        [SerializeField, Range(0f, 1f)] private float moonBrightness = 0.05f; // 현재 달빛의 게임용 밝기
        [SerializeField] private bool sunEnabled = true; // 태양 밝기 계산 활성 여부
        [SerializeField] private bool moonEnabled = true; // 달빛 밝기 계산 활성 여부

        public float SunBrightness => sunEnabled ? sunBrightness : 0f; // 현재 태양 기여도 반환
        public float MoonBrightness => moonEnabled ? moonBrightness : 0f; // 현재 달빛 기여도 반환
        public float CurrentBrightness => Mathf.Clamp01(SunBrightness + MoonBrightness); // 태양과 달빛의 현재 자연광 합계 반환

        public void Configure(float sunValue, float moonValue) // 에디터 자동 테스트용 자연광 값 지정
        {
            sunBrightness = Mathf.Clamp01(sunValue); // 태양 밝기를 0~1 범위로 저장
            moonBrightness = Mathf.Clamp01(moonValue); // 달빛 밝기를 0~1 범위로 저장
            sunEnabled = true; // 테스트 기본 상태에서 태양 활성화
            moonEnabled = true; // 테스트 기본 상태에서 달빛 활성화
        }

        public void SetSunBrightness(float value) // 이후 시간 시스템에서 태양 밝기 변경
        {
            sunBrightness = Mathf.Clamp01(value); // 태양 밝기를 0~1 범위로 저장
        }

        public void SetMoonBrightness(float value) // 이후 시간 시스템에서 달빛 밝기 변경
        {
            moonBrightness = Mathf.Clamp01(value); // 달빛 밝기를 0~1 범위로 저장
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            sunBrightness = Mathf.Clamp01(sunBrightness); // 태양 밝기 범위 보정
            moonBrightness = Mathf.Clamp01(moonBrightness); // 달빛 밝기 범위 보정
        }
    }
}
