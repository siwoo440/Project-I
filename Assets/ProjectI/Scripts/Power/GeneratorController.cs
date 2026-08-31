using ProjectI.Interaction; // 공통 상호작용 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class GeneratorController : MonoBehaviour, IInteractable // 발전기 작동·연료·전력 공급 관리
    {
        [SerializeField] private string displayName = "발전기"; // 상호작용 표시 이름
        [SerializeField] private float maxFuel = 100f; // 최대 연료량
        [SerializeField] private float currentFuel = 100f; // 현재 연료량
        [SerializeField] private float fuelConsumptionPerSecond = 0.25f; // 초당 연료 소비량
        [SerializeField] private bool isRunning; // 현재 발전기 작동 상태
        [SerializeField] private ElectricLightController[] connectedLights; // 직접 연결된 11일차 전기등 목록
        [SerializeField] private GameObject[] runningVisuals; // 작동 중 표시 시각 요소
        [SerializeField] private GameObject[] stoppedVisuals; // 정지 중 표시 시각 요소
        [SerializeField] private GameObject[] fuelGaugeSegments; // 연료량 표시 게이지 조각
        [SerializeField] private Transform[] rotatingParts; // 작동 중 회전할 발전기 부품
        [SerializeField] private float rotationSpeed = 480f; // 회전 부품 초당 회전 속도

        public string DisplayName => displayName; // 디버그 표시 이름 공개
        public float MaxFuel => maxFuel; // 최대 연료량 공개
        public float CurrentFuel => currentFuel; // 현재 연료량 공개
        public float FuelRatio => maxFuel <= 0f ? 0f : Mathf.Clamp01(currentFuel / maxFuel); // 현재 연료 비율 공개
        public float FuelConsumptionPerSecond => fuelConsumptionPerSecond; // 초당 소비량 공개
        public bool IsRunning => isRunning; // 현재 작동 상태 공개
        public int ConnectedLightCount => connectedLights == null ? 0 : connectedLights.Length; // 연결 전기등 개수 공개
        public string Prompt => BuildPrompt(); // 현재 상태 기반 상호작용 문구 반환
        public InteractionType InteractionType => InteractionType.Toggle; // F 입력마다 가동·정지 전환
        public float HoldDuration => 0f; // 즉시 상호작용 시간

        private void Awake() // 발전기 초기화
        {
            ClampFuelValues(); // 연료 데이터 안전 범위 보정
            ApplyState(); // 저장 상태를 전기등과 시각 요소에 적용
        }

        private void OnEnable() // 발전기 활성화 처리
        {
            ClampFuelValues(); // 활성화 시 연료 데이터 보정
            ApplyState(); // 현재 상태 재적용
        }

        private void OnDisable() // 발전기 비활성화 처리
        {
            SetConnectedLightsPowered(false); // 비활성 발전기의 전력 공급 차단
        }

        private void Update() // 프레임별 발전기 처리
        {
            if (!isRunning) // 정지 상태 확인
            {
                return; // 정지 중 연료와 회전 처리 생략
            }

            ConsumeFuel(Time.deltaTime); // 실제 경과 시간만큼 연료 소비
            RotateMovingParts(Time.deltaTime); // 발전기 회전 부품 애니메이션 처리
        }

        public bool CanInteract(PlayerInteractor interactor) // 플레이어 상호작용 가능 여부 반환
        {
            return interactor != null && isActiveAndEnabled; // 활성 발전기면 상태와 무관하게 안내 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 발전기 상태 전환
        {
            if (!CanInteract(interactor)) // 유효 상호작용 여부 확인
            {
                return; // 사용할 수 없으면 상태 유지
            }

            if (isRunning) // 현재 작동 중인지 확인
            {
                StopGenerator(); // 작동 중이면 발전기 정지
                return; // 정지 처리 후 종료
            }

            StartGenerator(); // 정지 중이면 연료 확인 후 발전기 가동
        }

        public void Configure(string targetDisplayName, float targetMaxFuel, float startFuel, float consumptionRate, bool startRunning, ElectricLightController[] lights, GameObject[] activeVisuals, GameObject[] inactiveVisuals, GameObject[] gaugeSegments, Transform[] movingParts, float movingPartSpeed) // 에디터 자동 구성용 발전기 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 표시 이름 저장
            maxFuel = Mathf.Max(1f, targetMaxFuel); // 최대 연료 최소값 보정
            currentFuel = Mathf.Clamp(startFuel, 0f, maxFuel); // 시작 연료 안전 범위 저장
            fuelConsumptionPerSecond = Mathf.Max(0f, consumptionRate); // 연료 소비량 음수 방지
            isRunning = startRunning && currentFuel > 0f; // 연료가 있을 때만 시작 가동 허용
            connectedLights = lights; // 직접 연결된 전기등 저장
            runningVisuals = activeVisuals; // 작동 시각 요소 저장
            stoppedVisuals = inactiveVisuals; // 정지 시각 요소 저장
            fuelGaugeSegments = gaugeSegments; // 연료 게이지 조각 저장
            rotatingParts = movingParts; // 회전 부품 저장
            rotationSpeed = Mathf.Max(0f, movingPartSpeed); // 회전 속도 음수 방지
            ApplyState(); // 새 설정을 즉시 실제 상태에 반영
        }

        public bool StartGenerator() // 외부 시스템에서 발전기 가동 시도
        {
            if (currentFuel <= 0f) // 사용 가능한 연료 여부 확인
            {
                isRunning = false; // 연료 없음 상태에서 정지 유지
                ApplyState(); // 정지 상태 시각 요소 동기화
                return false; // 가동 실패 반환
            }

            isRunning = true; // 발전기 가동 상태 저장
            ApplyState(); // 전력 공급과 시각 요소 활성화
            return true; // 가동 성공 반환
        }

        public void StopGenerator() // 외부 시스템에서 발전기 정지
        {
            isRunning = false; // 발전기 정지 상태 저장
            ApplyState(); // 전력 공급과 시각 요소 비활성화
        }

        private void ConsumeFuel(float deltaTime) // 작동 중 연료 소비 처리
        {
            currentFuel = Mathf.Max(0f, currentFuel - fuelConsumptionPerSecond * Mathf.Max(0f, deltaTime)); // 경과 시간 기반 연료 차감
            UpdateFuelGauge(); // 변화한 연료량을 게이지에 반영

            if (currentFuel > 0f) // 아직 연료가 남았는지 확인
            {
                return; // 발전기 작동 유지
            }

            StopGenerator(); // 연료 고갈 시 발전기 자동 정지
        }

        private void RotateMovingParts(float deltaTime) // 작동 중 회전 장식 처리
        {
            if (rotatingParts == null) // 회전 부품 배열 존재 여부 확인
            {
                return; // 부품이 없으면 처리 생략
            }

            foreach (Transform rotatingPart in rotatingParts) // 회전 부품 전체 순회
            {
                if (rotatingPart == null) // 유효 부품 여부 확인
                {
                    continue; // 누락 부품 건너뜀
                }

                rotatingPart.Rotate(Vector3.up, rotationSpeed * deltaTime, Space.Self); // 로컬 축 기준 회전 애니메이션 적용
            }
        }

        private void ApplyState() // 발전기 상태를 연결 대상에 적용
        {
            if (currentFuel <= 0f) // 연료 고갈 상태 확인
            {
                isRunning = false; // 연료가 없으면 강제 정지
            }

            SetConnectedLightsPowered(isRunning); // 발전기 작동 상태를 전기등 전력으로 전달
            SetVisualArrayState(runningVisuals, isRunning); // 작동 표시 요소 상태 적용
            SetVisualArrayState(stoppedVisuals, !isRunning); // 정지 표시 요소 상태 적용
            UpdateFuelGauge(); // 현재 연료량 게이지 반영
        }

        private void SetConnectedLightsPowered(bool powered) // 연결 전기등 전력 상태 적용
        {
            if (connectedLights == null) // 연결 배열 존재 여부 확인
            {
                return; // 연결 대상이 없으면 처리 생략
            }

            foreach (ElectricLightController lightController in connectedLights) // 연결 전기등 전체 순회
            {
                if (lightController == null) // 유효 전기등 여부 확인
                {
                    continue; // 누락 전기등 건너뜀
                }

                lightController.SetPowered(powered); // 발전기 상태에 맞춘 전력 전달
            }
        }

        private void SetVisualArrayState(GameObject[] visuals, bool activeState) // 상태 시각 요소 배열 활성화 처리
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

                visual.SetActive(activeState); // 현재 발전기 상태에 맞춰 활성화
            }
        }

        private void UpdateFuelGauge() // 연료 비율 기반 게이지 표시 갱신
        {
            if (fuelGaugeSegments == null || fuelGaugeSegments.Length == 0) // 연료 게이지 존재 여부 확인
            {
                return; // 게이지가 없으면 처리 생략
            }

            int visibleCount = Mathf.CeilToInt(FuelRatio * fuelGaugeSegments.Length); // 현재 연료량에 맞는 표시 칸 수 계산

            for (int index = 0; index < fuelGaugeSegments.Length; index++) // 게이지 조각 전체 순회
            {
                GameObject segment = fuelGaugeSegments[index]; // 현재 게이지 조각 조회

                if (segment == null) // 유효 게이지 조각 여부 확인
                {
                    continue; // 누락 조각 건너뜀
                }

                segment.SetActive(index < visibleCount); // 현재 연료 비율 안쪽 조각만 표시
            }
        }

        private string BuildPrompt() // 현재 상태 기반 F 안내 문구 생성
        {
            int fuelPercent = Mathf.RoundToInt(FuelRatio * 100f); // 화면 표시용 연료 백분율 계산

            if (isRunning) // 발전기 작동 상태 확인
            {
                return $"{displayName} 정지 · 연료 {fuelPercent}%"; // 작동 중 정지 안내 반환
            }

            if (currentFuel <= 0f) // 연료 고갈 상태 확인
            {
                return $"{displayName} · 연료 없음"; // 연료 없음 안내 반환
            }

            return $"{displayName} 가동 · 연료 {fuelPercent}%"; // 정지 상태 가동 안내 반환
        }

        private void ClampFuelValues() // 발전기 수치 안전 범위 보정
        {
            maxFuel = Mathf.Max(1f, maxFuel); // 최대 연료 최소값 보정
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel); // 현재 연료 범위 보정
            fuelConsumptionPerSecond = Mathf.Max(0f, fuelConsumptionPerSecond); // 소비량 음수 방지
            rotationSpeed = Mathf.Max(0f, rotationSpeed); // 회전 속도 음수 방지
        }

        private void OnValidate() // 인스펙터 값 변경 시 상태 보정
        {
            ClampFuelValues(); // 에디터 수치 안전 범위 보정
            ApplyState(); // 인스펙터 상태를 연결 대상에 반영
        }
    }
}
