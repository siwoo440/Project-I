using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public readonly struct BrightnessSample // 플레이어 위치에서 계산된 밝기 결과 묶음
    {
        public BrightnessSample(BrightnessAreaType areaType, string areaName, float naturalBrightness, float localBrightness, float totalBrightness, BrightnessLevel level) // 밝기 결과 생성
        {
            AreaType = areaType; // 외부 또는 내부 공간 종류 저장
            AreaName = areaName; // 현재 방 또는 Outdoor 이름 저장
            NaturalBrightness = naturalBrightness; // 자연광 기여도 저장
            LocalBrightness = localBrightness; // 주변 광원 기여도 저장
            TotalBrightness = totalBrightness; // 최종 밝기 저장
            Level = level; // 5단계 밝기 등급 저장
        }

        public BrightnessAreaType AreaType { get; } // 외부 또는 내부 공간 종류
        public string AreaName { get; } // 현재 영역 표시 이름
        public float NaturalBrightness { get; } // 태양·달 자연광 합계
        public float LocalBrightness { get; } // 현재 규칙에 포함된 일반 광원 합계
        public float TotalBrightness { get; } // 0~1 최종 밝기
        public BrightnessLevel Level { get; } // 게임 로직용 밝기 단계
    }

    public sealed class BrightnessManager : MonoBehaviour // 외부·내부 규칙을 분리해 최종 밝기를 계산하는 관리자
    {
        [SerializeField] private NaturalLightController naturalLightController; // 외부 자연광 값 제공자
        [SerializeField, Range(0f, 1f)] private float darknessThreshold = 0.15f; // 암흑 상한
        [SerializeField, Range(0f, 1f)] private float veryDarkThreshold = 0.35f; // 매우 어두움 상한
        [SerializeField, Range(0f, 1f)] private float darkThreshold = 0.55f; // 어두움 상한
        [SerializeField, Range(0f, 1f)] private float brightThreshold = 0.80f; // 밝음 상한

        public NaturalLightController NaturalLight => naturalLightController; // 디버그와 검증용 자연광 컨트롤러 공개

        private void Awake() // 밝기 관리자 초기화
        {
            if (naturalLightController == null) // 자연광 컨트롤러 참조 누락 확인
            {
                naturalLightController = Object.FindFirstObjectByType<NaturalLightController>(); // 현재 씬의 자연광 컨트롤러 자동 조회
            }
        }

        public void Configure(NaturalLightController naturalController) // 에디터 자동 설정용 자연광 컨트롤러 연결
        {
            naturalLightController = naturalController; // 자연광 컨트롤러 참조 저장
        }

        public BrightnessSample SampleBrightness(Vector3 worldPosition) // 지정 위치의 외부 또는 내부 밝기 계산
        {
            IndoorBrightnessArea indoorArea = IndoorBrightnessArea.FindContaining(worldPosition); // 현재 위치를 포함하는 내부 방 검색

            if (indoorArea != null) // 플레이어가 내부 방 영역에 있는지 확인
            {
                float indoorLocal = CalculateLocalBrightness(worldPosition, indoorArea); // 현재 방 소속 광원만 합산
                float indoorTotal = BrightnessMath.Combine(0f, indoorLocal); // 내부에서는 태양·달 자연광을 직접 더하지 않음
                BrightnessLevel indoorLevel = Classify(indoorTotal); // 내부 최종 밝기 단계 판정
                return new BrightnessSample(BrightnessAreaType.Indoor, indoorArea.AreaName, 0f, indoorLocal, indoorTotal, indoorLevel); // 내부 밝기 결과 반환
            }

            float naturalBrightness = naturalLightController == null ? 0f : naturalLightController.CurrentBrightness; // 외부 태양+달 자연광 계산
            float outdoorLocal = CalculateLocalBrightness(worldPosition, null); // 내부 방에 속하지 않은 외부 광원만 합산
            float outdoorTotal = BrightnessMath.Combine(naturalBrightness, outdoorLocal); // 자연광과 외부 광원을 합쳐 최종 외부 밝기 계산
            BrightnessLevel outdoorLevel = Classify(outdoorTotal); // 외부 최종 밝기 단계 판정
            return new BrightnessSample(BrightnessAreaType.Outdoor, "Outdoor", naturalBrightness, outdoorLocal, outdoorTotal, outdoorLevel); // 외부 밝기 결과 반환
        }

        public BrightnessLevel Classify(float brightnessValue) // 0~1 밝기 수치를 5단계 등급으로 변환
        {
            float value = Mathf.Clamp01(brightnessValue); // 입력 밝기를 안전한 범위로 제한

            if (value < darknessThreshold) // 암흑 기준 미만인지 확인
            {
                return BrightnessLevel.Darkness; // 암흑 반환
            }

            if (value < veryDarkThreshold) // 매우 어두움 기준 미만인지 확인
            {
                return BrightnessLevel.VeryDark; // 매우 어두움 반환
            }

            if (value < darkThreshold) // 어두움 기준 미만인지 확인
            {
                return BrightnessLevel.Dark; // 어두움 반환
            }

            if (value < brightThreshold) // 밝음 기준 미만인지 확인
            {
                return BrightnessLevel.Bright; // 밝음 반환
            }

            return BrightnessLevel.VeryBright; // 마지막 구간은 매우 밝음 반환
        }

        private static float CalculateLocalBrightness(Vector3 worldPosition, IndoorBrightnessArea targetArea) // 현재 공간 규칙에 포함되는 일반 광원 밝기 합산
        {
            float total = 0f; // 일반 광원 합계 초기화

            foreach (BrightnessSource source in BrightnessSource.Sources) // 현재 활성화된 모든 게임용 광원 순회
            {
                if (source == null) // 유효하지 않은 광원 확인
                {
                    continue; // 다음 광원 검사
                }

                if (source.OwnerArea != targetArea) // 외부는 null 소속, 내부는 현재 방 소속만 허용
                {
                    continue; // 다른 공간의 광원은 현재 밝기 계산에서 제외
                }

                total += source.GetContribution(worldPosition); // 현재 위치에 도달하는 거리 감쇠 밝기 합산
            }

            return Mathf.Clamp01(total); // 일반 광원 합계를 0~1 범위로 제한
        }

        private void OnValidate() // 인스펙터 임계값 검증
        {
            darknessThreshold = Mathf.Clamp01(darknessThreshold); // 암흑 임계값 범위 보정
            veryDarkThreshold = Mathf.Clamp(veryDarkThreshold, darknessThreshold, 1f); // 매우 어두움 임계값 순서 보정
            darkThreshold = Mathf.Clamp(darkThreshold, veryDarkThreshold, 1f); // 어두움 임계값 순서 보정
            brightThreshold = Mathf.Clamp(brightThreshold, darkThreshold, 1f); // 밝음 임계값 순서 보정
        }
    }
}
