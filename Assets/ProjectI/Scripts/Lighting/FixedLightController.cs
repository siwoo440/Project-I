using System.Collections.Generic; // 활성 고정 광원 목록 기능 참조
using ProjectI.Brightness; // 게임용 밝기 광원 기능 참조
using ProjectI.Interaction; // F 상호작용 공통 인터페이스 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Lighting // 조명 기능 네임스페이스
{
    public sealed class FixedLightController : MonoBehaviour, IInteractable // 벽 횃불·화로 같은 고정 환경 광원의 켜기·끄기 관리
    {
        private static readonly HashSet<FixedLightController> ActiveLights = new HashSet<FixedLightController>(); // 현재 씬의 활성 고정 조명 목록
        [SerializeField] private string displayName = "고정 광원"; // 상호작용과 디버그에 표시할 이름
        [SerializeField] private bool isLit; // 현재 점화 또는 켜짐 상태
        [SerializeField] private BrightnessSource[] brightnessSources; // 이 고정 조명이 제어하는 게임용 광원 목록
        [SerializeField] private GameObject[] litVisuals; // 켜짐 상태일 때만 표시할 불꽃·발광 시각 요소 목록

        public static IEnumerable<FixedLightController> Lights => ActiveLights; // F1 Fixed Light 페이지용 활성 목록 공개
        public string DisplayName => displayName; // 디버그 표시 이름 공개
        public bool IsLit => isLit; // 현재 켜짐 상태 공개
        public string Prompt => $"{displayName} {(isLit ? "끄기" : "켜기")}"; // F 상호작용 안내 문구 반환
        public InteractionType InteractionType => InteractionType.Toggle; // 누를 때마다 켜짐·꺼짐 상태 전환
        public float HoldDuration => 0f; // 즉시 토글이므로 Hold 시간 없음
        public BrightnessSource[] BrightnessSources // 디버그와 Validator용 광원 목록 공개
        {
            get
            {
                ResolveSources(); // 비직렬화 또는 에디터 상태에서도 광원 목록 확보
                return brightnessSources; // 현재 연결 광원 배열 반환
            }
        }

        private void Awake() // 고정 광원 초기화
        {
            ResolveSources(); // 연결된 BrightnessSource 목록 확보
            ApplyState(); // 저장된 켜짐 상태를 실제 Light와 시각 요소에 적용
        }

        private void OnEnable() // 고정 광원 활성화 처리
        {
            ResolveSources(); // 활성화 직후 광원 목록 확보
            ActiveLights.Add(this); // F1 고정 광원 목록에 등록
            ApplyState(); // 현재 켜짐 상태 동기화
        }

        private void OnDisable() // 고정 광원 비활성화 처리
        {
            ActiveLights.Remove(this); // F1 고정 광원 목록에서 제거
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 플레이어가 고정 광원을 조작할 수 있는지 반환
        {
            return interactor != null && isActiveAndEnabled; // 유효 플레이어와 활성 고정 조명일 때 상호작용 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 고정 광원 상태 전환
        {
            if (!CanInteract(interactor)) // 현재 조작 가능 여부 확인
            {
                return; // 조작 불가 상태에서는 변경 없음
            }

            Toggle(); // 켜짐·꺼짐 상태 반전
        }

        public void Configure(string targetDisplayName, bool startLit, BrightnessSource[] sources, GameObject[] visuals) // 에디터 자동 구성용 고정 광원 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 표시 이름 저장
            isLit = startLit; // 시작 점화 상태 저장
            brightnessSources = sources; // 게임용 광원 목록 저장
            litVisuals = visuals; // 점화 시각 요소 목록 저장
            ResolveSources(); // 누락된 경우 자식 광원 자동 조회
            ApplyState(); // 새 설정을 실제 상태에 즉시 적용
        }

        public void TurnOn() // 외부 시스템에서 고정 광원 켜기
        {
            isLit = true; // 켜짐 상태 저장
            ApplyState(); // 실제 Light와 게임 밝기 동기화
        }

        public void TurnOff() // 외부 시스템에서 고정 광원 끄기
        {
            isLit = false; // 꺼짐 상태 저장
            ApplyState(); // 실제 Light와 게임 밝기 동기화
        }

        public void Toggle() // F 또는 외부 시스템에서 켜짐 상태 반전
        {
            isLit = !isLit; // 현재 상태 반전
            ApplyState(); // 변경 상태 즉시 적용
        }

        private void ApplyState() // 켜짐 상태를 모든 게임용 광원과 불꽃 시각 요소에 적용
        {
            ResolveSources(); // 제어할 광원 목록 확보

            foreach (BrightnessSource source in brightnessSources) // 연결된 모든 BrightnessSource 순회
            {
                if (source != null) // 유효 광원인지 확인
                {
                    source.SetSourceEnabled(isLit); // Unity Light와 게임용 밝기를 동시에 켜거나 끄기
                }
            }

            if (litVisuals == null) // 점화 시각 요소 배열 존재 여부 확인
            {
                return; // 시각 요소 동기화 생략
            }

            foreach (GameObject visual in litVisuals) // 불꽃·발광 시각 요소 전체 순회
            {
                if (visual != null) // 유효 시각 요소인지 확인
                {
                    visual.SetActive(isLit); // 현재 켜짐 상태에 맞춰 불꽃 표시 전환
                }
            }
        }

        private void ResolveSources() // 고정 조명에 속한 BrightnessSource 목록 확보
        {
            if (brightnessSources == null || brightnessSources.Length == 0) // 직렬화된 광원 목록 누락 여부 확인
            {
                brightnessSources = GetComponentsInChildren<BrightnessSource>(true); // 현재 고정 조명 아래의 모든 게임용 광원 자동 조회
            }
        }

        private void OnValidate() // 에디터 값 변경 시 상태 동기화
        {
            ResolveSources(); // 에디터에서도 광원 목록 확보
            ApplyState(); // 인스펙터 상태를 실제 광원과 시각 요소에 적용
        }
    }
}
