using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class MainDistributionBoardController : MonoBehaviour // 발전기 입력과 방별 전력 요청을 연결하는 중앙 배전반
    {
        [SerializeField] private GeneratorController generator; // 시설 전력을 공급하는 기존 11일차 발전기
        [SerializeField] private bool mainPowerRequested = true; // 배전반 메인 전원 요청 상태
        [SerializeField] private RoomPowerZone[] roomZones; // 배전반에서 제어할 방 전력 구역 목록
        [SerializeField] private PoweredIronDoor[] controlledDoors; // 배전반에서 원격 제어할 철제문 목록
        [SerializeField] private GameObject[] mainPoweredVisuals; // 실제 메인 통전 상태 표시 요소
        [SerializeField] private GameObject[] mainUnpoweredVisuals; // 실제 메인 정전 상태 표시 요소
        [SerializeField] private GameObject[] roomPoweredVisuals; // 방별 실제 통전 녹색 표시 요소
        [SerializeField] private GameObject[] roomUnpoweredVisuals; // 방별 실제 정전 빨간 표시 요소
        [SerializeField] private GameObject[] doorOpenVisuals; // 문별 열림 상태 표시 요소
        [SerializeField] private GameObject[] doorClosedVisuals; // 문별 닫힘 상태 표시 요소
        [SerializeField] private GameObject[] doorMovingVisuals; // 문별 이동 중 상태 표시 요소
        [SerializeField] private GameObject[] doorNoPowerVisuals; // 문별 정전 상태 표시 요소

        public event System.Action StateChanged; // 메인 전력 요청·실제 공급 상태 변경 이벤트 공개
        public GeneratorController Generator => generator; // Validator용 발전기 참조 공개
        public bool MainPowerRequested => mainPowerRequested; // 배전반 메인 전원 요청 상태 공개
        public bool GeneratorAvailable => generator != null && generator.IsRunning; // 발전기 실제 가동 여부 공개
        public bool FacilityPowerAvailable => GeneratorAvailable && mainPowerRequested; // 시설 메인 실제 통전 여부 공개
        public int RoomZoneCount => roomZones == null ? 0 : roomZones.Length; // 연결된 방 전력 구역 개수 공개
        public int ControlledDoorCount => controlledDoors == null ? 0 : controlledDoors.Length; // 연결된 철제문 개수 공개
        public RoomPowerZone[] RoomZones => roomZones; // Validator용 방 전력 구역 목록 공개
        public PoweredIronDoor[] ControlledDoors => controlledDoors; // Validator용 철제문 목록 공개

        private void Awake() // 중앙 배전반 초기화
        {
            RefreshPowerState(); // 저장된 발전기와 메인 전원 상태를 방에 적용
        }

        private void OnEnable() // 중앙 배전반 활성화 처리
        {
            SubscribeSources(); // 발전기·방·문 상태 변경 이벤트 구독
            RefreshPowerState(); // 활성화 직후 전체 전력 상태 동기화
        }

        private void OnDisable() // 중앙 배전반 비활성화 처리
        {
            UnsubscribeSources(); // 비활성 상태의 이벤트 구독 해제
        }

        public void Configure(GeneratorController targetGenerator, bool startMainPowerRequested, RoomPowerZone[] targetRoomZones, PoweredIronDoor[] targetDoors, GameObject[] mainOnVisuals, GameObject[] mainOffVisuals, GameObject[] roomOnVisuals, GameObject[] roomOffVisuals, GameObject[] openVisuals, GameObject[] closedVisuals, GameObject[] movingVisuals, GameObject[] noPowerVisuals) // 자동 Setup용 중앙 배전반 설정
        {
            UnsubscribeSources(); // 이전 연결 대상 이벤트 구독 해제
            generator = targetGenerator; // 기존 발전기 연결
            mainPowerRequested = startMainPowerRequested; // 시작 메인 전원 요청 상태 저장
            roomZones = targetRoomZones; // 방 전력 구역 목록 저장
            controlledDoors = targetDoors; // 원격 제어 철제문 목록 저장
            mainPoweredVisuals = mainOnVisuals; // 메인 통전 표시 연결
            mainUnpoweredVisuals = mainOffVisuals; // 메인 정전 표시 연결
            roomPoweredVisuals = roomOnVisuals; // 방별 통전 표시 연결
            roomUnpoweredVisuals = roomOffVisuals; // 방별 정전 표시 연결
            doorOpenVisuals = openVisuals; // 문별 열림 표시 연결
            doorClosedVisuals = closedVisuals; // 문별 닫힘 표시 연결
            doorMovingVisuals = movingVisuals; // 문별 이동 표시 연결
            doorNoPowerVisuals = noPowerVisuals; // 문별 정전 표시 연결

            if (isActiveAndEnabled) // 현재 배전반 활성 상태 확인
            {
                SubscribeSources(); // 새 연결 대상 이벤트 구독
            }

            RefreshPowerState(); // 새 설정을 방과 표시등에 즉시 반영
        }

        public void SetMainPowerRequested(bool powered) // 메인 토글 스위치에서 시설 전원 요청 변경
        {
            if (mainPowerRequested == powered) // 기존 메인 요청 상태와 동일한지 확인
            {
                return; // 중복 전체 갱신과 이벤트 호출 방지
            }

            mainPowerRequested = powered; // 새 메인 전원 요청 상태 저장
            RefreshPowerState(); // 모든 방과 배전반 표시 즉시 갱신
            NotifyStateChanged(); // 메인 토글 레버와 진단 기능에 상태 변경 전달
        }

        public void RefreshPowerState() // 발전기와 메인 요청 상태를 전체 방에 전달
        {
            bool facilityPower = FacilityPowerAvailable; // 현재 시설 실제 통전 여부 계산

            if (roomZones != null) // 방 전력 구역 배열 존재 여부 확인
            {
                foreach (RoomPowerZone roomZone in roomZones) // 연결된 모든 방 순회
                {
                    if (roomZone == null) // 유효 방 전력 구역 여부 확인
                    {
                        continue; // 누락 방 건너뜀
                    }

                    roomZone.SetFacilityPowerAvailable(facilityPower); // 발전기와 메인 전원 결과를 방에 전달
                }
            }

            SetVisualArrayState(mainPoweredVisuals, facilityPower); // 실제 시설 통전 상태 녹색 표시
            SetVisualArrayState(mainUnpoweredVisuals, !facilityPower); // 실제 시설 정전 상태 빨간 표시
            UpdateRoomIndicators(); // 방별 실제 통전 상태 표시 갱신
            UpdateDoorIndicators(); // 문별 현재 상태 표시 갱신
        }

        private void SubscribeSources() // 연결된 전력 상태 이벤트 전체 구독
        {
            UnsubscribeSources(); // 중복 이벤트 등록 예방

            if (generator != null) // 발전기 참조 존재 여부 확인
            {
                generator.StateChanged += HandleGeneratorStateChanged; // 발전기 가동 상태 이벤트 구독
            }

            if (roomZones != null) // 방 전력 구역 목록 존재 여부 확인
            {
                foreach (RoomPowerZone roomZone in roomZones) // 연결된 방 전체 순회
                {
                    if (roomZone != null) // 유효 방 전력 구역 여부 확인
                    {
                        roomZone.StateChanged += HandleRoomStateChanged; // 방 상태 변경 이벤트 구독
                    }
                }
            }

            if (controlledDoors != null) // 철제문 목록 존재 여부 확인
            {
                foreach (PoweredIronDoor door in controlledDoors) // 원격 제어 문 전체 순회
                {
                    if (door != null) // 유효 철제문 여부 확인
                    {
                        door.StateChanged += HandleDoorStateChanged; // 문 상태 변경 이벤트 구독
                    }
                }
            }
        }

        private void UnsubscribeSources() // 연결된 전력 상태 이벤트 전체 구독 해제
        {
            if (generator != null) // 발전기 참조 존재 여부 확인
            {
                generator.StateChanged -= HandleGeneratorStateChanged; // 발전기 이벤트 구독 해제
            }

            if (roomZones != null) // 방 전력 구역 목록 존재 여부 확인
            {
                foreach (RoomPowerZone roomZone in roomZones) // 연결된 방 전체 순회
                {
                    if (roomZone != null) // 유효 방 전력 구역 여부 확인
                    {
                        roomZone.StateChanged -= HandleRoomStateChanged; // 방 상태 이벤트 구독 해제
                    }
                }
            }

            if (controlledDoors != null) // 철제문 목록 존재 여부 확인
            {
                foreach (PoweredIronDoor door in controlledDoors) // 원격 제어 문 전체 순회
                {
                    if (door != null) // 유효 철제문 여부 확인
                    {
                        door.StateChanged -= HandleDoorStateChanged; // 문 상태 이벤트 구독 해제
                    }
                }
            }
        }

        private void HandleGeneratorStateChanged() // 발전기 가동 상태 변경 처리
        {
            RefreshPowerState(); // 발전기 변경 결과를 전체 방에 전달
            NotifyStateChanged(); // 메인 스위치와 외부 진단 기능에 변경 전달
        }

        private void HandleRoomStateChanged() // 방 전력 상태 변경 처리
        {
            UpdateRoomIndicators(); // 변경 시점에만 방별 상태등 갱신
        }

        private void HandleDoorStateChanged() // 철제문 상태 변경 처리
        {
            UpdateDoorIndicators(); // 변경 시점에만 문별 상태등 갱신
        }

        private void UpdateRoomIndicators() // 배전반 방별 상태등 갱신
        {
            if (roomZones == null) // 방 전력 구역 목록 존재 여부 확인
            {
                return; // 방 상태 표시 생략
            }

            for (int index = 0; index < roomZones.Length; index++) // 방 전력 구역 전체 순회
            {
                RoomPowerZone roomZone = roomZones[index]; // 현재 방 전력 구역 조회
                bool powered = roomZone != null && roomZone.ActualPower; // 현재 방 실제 통전 여부 계산
                SetIndexedVisualState(roomPoweredVisuals, index, powered); // 현재 방 녹색 통전 표시 갱신
                SetIndexedVisualState(roomUnpoweredVisuals, index, !powered); // 현재 방 빨간 정전 표시 갱신
            }
        }

        private void UpdateDoorIndicators() // 배전반 철제문 상태등 갱신
        {
            if (controlledDoors == null) // 철제문 목록 존재 여부 확인
            {
                return; // 문 상태 표시 생략
            }

            for (int index = 0; index < controlledDoors.Length; index++) // 원격 제어 철제문 전체 순회
            {
                PoweredIronDoor door = controlledDoors[index]; // 현재 철제문 조회
                bool hasDoor = door != null; // 유효 철제문 존재 여부 계산
                bool hasPower = hasDoor && door.HasPower; // 현재 철제문 통전 여부 계산
                SetIndexedVisualState(doorOpenVisuals, index, hasPower && door.IsOpen); // 통전 + 열림 상태 표시
                SetIndexedVisualState(doorClosedVisuals, index, hasPower && door.IsClosed); // 통전 + 닫힘 상태 표시
                SetIndexedVisualState(doorMovingVisuals, index, hasPower && door.IsMoving); // 통전 + 이동 상태 표시
                SetIndexedVisualState(doorNoPowerVisuals, index, !hasPower); // 정전 상태 표시
            }
        }

        private static void SetVisualArrayState(GameObject[] visuals, bool activeState) // 시각 요소 배열 전체 활성화 처리
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

                visual.SetActive(activeState); // 지정 상태로 시각 요소 전환
            }
        }

        private static void SetIndexedVisualState(GameObject[] visuals, int index, bool activeState) // 인덱스에 해당하는 상태 요소 활성화 처리
        {
            if (visuals == null || index < 0 || index >= visuals.Length) // 배열과 인덱스 유효성 확인
            {
                return; // 표시 대상이 없으면 처리 생략
            }

            GameObject visual = visuals[index]; // 지정 인덱스 시각 요소 조회

            if (visual != null) // 유효 시각 요소 여부 확인
            {
                visual.SetActive(activeState); // 지정 상태로 표시 전환
            }
        }

        private void NotifyStateChanged() // 메인 배전 상태 변경 알림 실행
        {
            StateChanged?.Invoke(); // 토글 스위치와 상태 복구 시스템에 변경 전달
        }

        private void OnValidate() // 인스펙터 변경 시 전력 상태 동기화
        {
            RefreshPowerState(); // 에디터에서도 배전반 상태를 전체 방에 적용
        }
    }
}
