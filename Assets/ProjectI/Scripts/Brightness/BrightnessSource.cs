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
        [SerializeField] private Light visualLight; // 실제 화면 표현용 Unity Light 선택 참조
        private IndoorBrightnessArea ownerArea; // 부모 구조를 기준으로 연결된 내부 방 영역

        public static IEnumerable<BrightnessSource> Sources => ActiveSources; // BrightnessManager가 순회할 활성 광원 목록 공개
        public float Brightness => brightness; // 검증과 디버그용 기본 밝기 공개
        public float Range => range; // 검증과 디버그용 영향 거리 공개
        public bool SourceEnabled => sourceEnabled; // 논리 광원 활성 여부 공개
        public IndoorBrightnessArea OwnerArea => ownerArea; // null이면 외부, 값이 있으면 해당 방 내부 광원으로 공개

        private void Awake() // 광원 초기화
        {
            RefreshOwnerArea(); // 현재 부모 구조에서 내부 방 소속 확인
        }

        private void OnEnable() // 광원 활성화 처리
        {
            RefreshOwnerArea(); // 현재 부모 구조에서 내부 방 소속 갱신
            ActiveSources.Add(this); // 활성 광원 목록에 현재 광원 등록
        }

        private void OnDisable() // 광원 비활성화 처리
        {
            ActiveSources.Remove(this); // 활성 광원 목록에서 현재 광원 제거
        }

        private void OnTransformParentChanged() // 광원 부모 구조 변경 처리
        {
            RefreshOwnerArea(); // 새 부모 기준으로 외부 또는 내부 소속 갱신
        }

        public void Configure(float sourceBrightness, float sourceRange, bool enabledState, Light linkedLight) // 에디터 자동 설정용 광원 값 지정
        {
            brightness = Mathf.Clamp01(sourceBrightness); // 기본 밝기를 0~1 범위로 저장
            range = Mathf.Max(0.1f, sourceRange); // 광원 영향 거리 최소값 보정
            sourceEnabled = enabledState; // 논리 활성 상태 저장
            visualLight = linkedLight; // 화면 표현용 Unity Light 참조 저장
            RefreshOwnerArea(); // 현재 부모 구조 기준 방 소속 갱신
            SyncVisualLight(); // 논리 활성 상태와 실제 Light 활성 상태 동기화
        }

        public void SetSourceEnabled(bool enabledState) // 횃불·전기 시스템에서 사용할 광원 켜기/끄기
        {
            sourceEnabled = enabledState; // 논리 광원 활성 상태 변경
            SyncVisualLight(); // 실제 화면용 Light 활성 상태 동기화
        }

        public float GetContribution(Vector3 samplePosition) // 특정 위치에 현재 광원이 주는 밝기 계산
        {
            if (!sourceEnabled || !isActiveAndEnabled) // 논리 또는 컴포넌트 비활성 상태 확인
            {
                return 0f; // 꺼진 광원은 밝기 영향 없음 반환
            }

            float distance = Vector3.Distance(transform.position, samplePosition); // 광원과 측정 위치 사이 거리 계산
            return BrightnessMath.CalculateContribution(brightness, distance, range); // 거리 감쇠가 적용된 실제 밝기 반환
        }

        private void RefreshOwnerArea() // 부모 구조를 기준으로 내부 방 소속 갱신
        {
            ownerArea = GetComponentInParent<IndoorBrightnessArea>(); // 가장 가까운 부모 IndoorBrightnessArea 조회
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
            RefreshOwnerArea(); // 에디터 부모 변경을 소속 정보에 반영
            SyncVisualLight(); // 인스펙터 활성 상태를 실제 Light에 반영
        }
    }
}
