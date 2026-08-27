using System.Collections.Generic; // 활성 광원 목록 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public sealed class BrightnessSource : MonoBehaviour // 게임 로직용 공통 밝기 광원
    {
        private static readonly HashSet<BrightnessSource> ActiveSources = new HashSet<BrightnessSource>(); // 현재 활성화된 게임용 광원 목록
        [SerializeField, Range(0f, 1f)] private float brightness = 0.35f; // 광원 중심의 게임용 기본 밝기
        [SerializeField] private float range = 8f; // 밝기 영향이 0이 되는 최대 거리
        [SerializeField] private bool sourceEnabled = true; // 현재 광원 논리 활성 여부
        [SerializeField] private BrightnessSourceType sourceType = BrightnessSourceType.Fixed; // 고정형 또는 이동형 광원 판정 방식
        [SerializeField] private BrightnessEmissionShape emissionShape = BrightnessEmissionShape.Omnidirectional; // 모든 방향 또는 전방 원뿔형 밝기 계산 방식
        [SerializeField, Range(1f, 179f)] private float coneAngle = 52f; // Cone 광원의 전체 게임용 조사 각도
        [SerializeField] private Light visualLight; // 실제 화면 표현용 Unity Light 선택 참조
        private IndoorBrightnessArea ownerArea; // Fixed 광원의 부모 구조 기준 내부 방 영역

        public static IEnumerable<BrightnessSource> Sources => ActiveSources; // BrightnessManager가 순회할 활성 광원 목록 공개
        public float Brightness => brightness; // 검증과 디버그용 기본 밝기 공개
        public float Range => range; // 검증과 디버그용 영향 거리 공개
        public bool SourceEnabled => sourceEnabled; // 논리 광원 활성 여부 공개
        public BrightnessSourceType SourceType => sourceType; // 광원의 고정형 또는 이동형 종류 공개
        public BrightnessEmissionShape EmissionShape => emissionShape; // 광원의 전방성 계산 형태 공개
        public float ConeAngle => coneAngle; // Cone 광원 조사 각도 공개
        public Light VisualLight => visualLight; // Validator와 향후 조명 제어용 화면 Light 참조 공개
        public IndoorBrightnessArea OwnerArea => ownerArea; // Fixed 광원의 부모 구조 기준 소속 방 공개

        private void Awake() // 광원 초기화
        {
            RefreshOwnerArea(); // 현재 부모 구조에서 Fixed 광원 소속 확인
        }

        private void OnEnable() // 광원 활성화 처리
        {
            RefreshOwnerArea(); // 현재 부모 구조에서 Fixed 광원 소속 갱신
            ActiveSources.Add(this); // 활성 광원 목록에 현재 광원 등록
        }

        private void OnDisable() // 광원 비활성화 처리
        {
            ActiveSources.Remove(this); // 활성 광원 목록에서 현재 광원 제거
        }

        private void OnTransformParentChanged() // 광원 부모 구조 변경 처리
        {
            RefreshOwnerArea(); // Fixed 광원일 때 새 부모 기준 내부 방 소속 갱신
        }

        public void Configure(float sourceBrightness, float sourceRange, bool enabledState, Light linkedLight) // 기존 고정 광원 에디터 설정 호환용 값 지정
        {
            Configure(sourceBrightness, sourceRange, enabledState, linkedLight, BrightnessSourceType.Fixed); // 기존 호출은 Fixed + Omnidirectional 광원으로 유지
        }

        public void Configure(float sourceBrightness, float sourceRange, bool enabledState, Light linkedLight, BrightnessSourceType type) // 기존 이동형 광원 설정 호환용 값 지정
        {
            Configure(sourceBrightness, sourceRange, enabledState, linkedLight, type, BrightnessEmissionShape.Omnidirectional, 52f); // 기존 호출은 모든 방향 광원으로 유지
        }

        public void Configure(float sourceBrightness, float sourceRange, bool enabledState, Light linkedLight, BrightnessSourceType type, BrightnessEmissionShape shape, float sourceConeAngle) // 광원 값과 공간·방사 판정 방식 지정
        {
            brightness = Mathf.Clamp01(sourceBrightness); // 기본 밝기를 0~1 범위로 저장
            range = Mathf.Max(0.1f, sourceRange); // 광원 영향 거리 최소값 보정
            sourceEnabled = enabledState; // 논리 활성 상태 저장
            sourceType = type; // 고정형 또는 이동형 광원 종류 저장
            emissionShape = shape; // 모든 방향 또는 전방 원뿔형 계산 방식 저장
            coneAngle = Mathf.Clamp(sourceConeAngle, 1f, 179f); // Cone 조사 각도를 안전한 범위로 저장
            visualLight = linkedLight; // 화면 표현용 Unity Light 참조 저장
            RefreshOwnerArea(); // 현재 부모 구조 기준 Fixed 광원 소속 갱신
            SyncVisualLight(); // 논리 활성 상태와 실제 Light 활성 상태 동기화
        }

        public void SetSourceEnabled(bool enabledState) // 횃불·전기 시스템에서 사용할 광원 켜기/끄기
        {
            sourceEnabled = enabledState; // 논리 광원 활성 상태 변경
            SyncVisualLight(); // 실제 화면용 Light 활성 상태 동기화
        }

        public IndoorBrightnessArea GetEffectiveArea() // 현재 광원이 실제로 기여해야 하는 내부 방 영역 반환
        {
            if (sourceType == BrightnessSourceType.Portable) // 이동형 광원인지 확인
            {
                return IndoorBrightnessArea.FindContaining(transform.position); // 횃불·랜턴의 아이템 위치를 기준으로 방 소속 계산
            }

            return ownerArea; // 고정 광원은 기존 부모 구조 기준 소속 방 반환
        }

        public float GetContribution(Vector3 samplePosition) // 특정 위치에 현재 광원이 주는 밝기 계산
        {
            if (!sourceEnabled || !isActiveAndEnabled) // 논리 또는 컴포넌트 비활성 상태 확인
            {
                return 0f; // 꺼진 광원은 밝기 영향 없음 반환
            }

            Vector3 emissionPosition = visualLight == null ? transform.position : visualLight.transform.position; // 실제 Light가 있으면 화면과 동일한 광원 시작점 사용
            float distance = Vector3.Distance(emissionPosition, samplePosition); // 실제 조사 시작점과 측정 위치 사이 거리 계산
            float distanceContribution = BrightnessMath.CalculateContribution(brightness, distance, range); // 기존 거리 감쇠 밝기 계산

            if (distanceContribution <= 0f || emissionShape == BrightnessEmissionShape.Omnidirectional) // 범위 밖이거나 모든 방향 광원인지 확인
            {
                return distanceContribution; // 거리 감쇠 결과를 그대로 반환
            }

            Vector3 toSample = samplePosition - emissionPosition; // Cone 광원 시작점에서 측정 위치로 향하는 방향 계산

            if (toSample.sqrMagnitude <= 0.0001f) // 측정 위치가 광원 중심과 사실상 같은지 확인
            {
                return distanceContribution; // 광원 중심에서는 최대 거리 기여도 반환
            }

            Vector3 emissionForward = visualLight == null ? transform.forward : visualLight.transform.forward; // 실제 Spot Light와 같은 전방 방향 사용
            float angle = Vector3.Angle(emissionForward, toSample.normalized); // 빔 중심축과 측정 위치 사이 각도 계산
            float halfConeAngle = coneAngle * 0.5f; // 전체 조사 각도에서 반각 계산

            if (angle >= halfConeAngle) // 측정 위치가 원뿔 바깥인지 확인
            {
                return 0f; // 랜턴 빔 바깥에는 주 빔 밝기 영향 없음 반환
            }

            float angleAttenuation = 1f - Mathf.Clamp01(angle / halfConeAngle); // 빔 중앙 100%에서 가장자리 0%까지 부드럽게 감쇠
            return distanceContribution * angleAttenuation; // 거리와 방향 감쇠를 모두 적용한 최종 밝기 반환
        }

        private void RefreshOwnerArea() // Fixed 광원의 부모 구조 기준 내부 방 소속 갱신
        {
            ownerArea = sourceType == BrightnessSourceType.Fixed ? GetComponentInParent<IndoorBrightnessArea>() : null; // 이동형은 위치 판정을 사용하므로 고정 소속을 비움
        }

        private void SyncVisualLight() // 게임 논리 광원과 화면용 Light 활성 상태 동기화
        {
            if (visualLight != null) // 실제 Light 참조 존재 여부 확인
            {
                visualLight.enabled = sourceEnabled; // 논리 활성 상태를 실제 Light에 적용
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            brightness = Mathf.Clamp01(brightness); // 기본 밝기 0~1 범위 보정
            range = Mathf.Max(0.1f, range); // 광원 영향 거리 최소값 보정
            coneAngle = Mathf.Clamp(coneAngle, 1f, 179f); // Cone 조사 각도 범위 보정
            RefreshOwnerArea(); // 에디터 부모 또는 종류 변경을 소속 정보에 반영
            SyncVisualLight(); // 인스펙터 활성 상태를 실제 Light에 반영
        }
    }
}
