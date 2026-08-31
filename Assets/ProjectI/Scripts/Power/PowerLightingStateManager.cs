using ProjectI.Items; // 플레이어 인벤토리 기반 휴대 조명 상태 복구 참조
using ProjectI.Lighting; // 고정·휴대 조명 상태 참조
using UnityEngine; // 유니티 기본 기능 참조
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // 신규 입력 시스템 F6·F7 디버그 입력 참조
#endif

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class PowerLightingStateManager : MonoBehaviour // 조명·전력 런타임 상태 캡처와 복구 관리
    {
        [SerializeField] private GeneratorController generator; // 저장할 발전기 참조
        [SerializeField] private MainDistributionBoardController distributionBoard; // 저장할 중앙 배전반 참조
        [SerializeField] private FixedLightController[] fixedLights; // 저장할 벽 횃불·화로 목록
        [SerializeField] private PortableLightItem[] portableLights; // 저장할 휴대 횃불·랜턴 목록
        [SerializeField] private bool enableDebugHotkeys = true; // F6 캡처·F7 복구 디버그 단축키 활성 여부
        private PlayerInventory playerInventory; // 휴대 조명 점화 상태 복구용 플레이어 인벤토리
        private RuntimeSnapshot snapshot; // 현재 메모리에 보관된 마지막 스냅샷

        public GeneratorController Generator => generator; // F1 전력 페이지용 발전기 참조 공개
        public MainDistributionBoardController DistributionBoard => distributionBoard; // F1 전력 페이지용 배전반 참조 공개
        public FixedLightController[] FixedLights => fixedLights; // F1 전력 페이지용 고정 조명 목록 공개
        public PortableLightItem[] PortableLights => portableLights; // F1 전력 페이지용 휴대 조명 목록 공개
        public bool HasSnapshot => snapshot != null; // 현재 복구 가능한 스냅샷 존재 여부 공개
        public float LastCaptureTime => snapshot == null ? -1f : snapshot.capturedTime; // 마지막 캡처 게임 시간 공개
        public bool IsConfigured => generator != null && distributionBoard != null; // 핵심 전력 참조 구성 완료 여부 공개

        private void Awake() // 상태 관리자 초기화
        {
            ResolveReferences(); // 누락된 전력·조명 참조 자동 확보
        }

        private void OnEnable() // 상태 관리자 활성화 처리
        {
            ResolveReferences(); // 씬 활성화 직후 참조 재확인
        }

        private void Update() // 디버그 스냅샷 단축키 입력 처리
        {
            if (!enableDebugHotkeys) // 디버그 단축키 비활성 여부 확인
            {
                return; // 키 입력 검사 생략
            }

            bool capturePressed = false; // 이번 프레임 F6 입력 상태 초기화
            bool restorePressed = false; // 이번 프레임 F7 입력 상태 초기화
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current; // 현재 신규 입력 시스템 키보드 조회

            if (keyboard != null) // 키보드 장치 연결 여부 확인
            {
                capturePressed |= keyboard.f6Key.wasPressedThisFrame; // 신규 입력 시스템 F6 캡처 입력 확인
                restorePressed |= keyboard.f7Key.wasPressedThisFrame; // 신규 입력 시스템 F7 복구 입력 확인
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            capturePressed |= Input.GetKeyDown(KeyCode.F6); // 레거시 입력 시스템 F6 캡처 입력 확인
            restorePressed |= Input.GetKeyDown(KeyCode.F7); // 레거시 입력 시스템 F7 복구 입력 확인
#endif

            if (capturePressed) // 상태 캡처 입력 여부 확인
            {
                CaptureSnapshot(); // 현재 조명·전력 상태 메모리 저장
            }

            if (restorePressed) // 상태 복구 입력 여부 확인
            {
                RestoreSnapshot(); // 마지막 저장 상태 복구
            }
        }

        public void Configure(GeneratorController targetGenerator, MainDistributionBoardController targetBoard, FixedLightController[] targetFixedLights, PortableLightItem[] targetPortableLights) // Day13 자동 Setup용 상태 관리자 구성
        {
            generator = targetGenerator; // 발전기 참조 저장
            distributionBoard = targetBoard; // 중앙 배전반 참조 저장
            fixedLights = targetFixedLights; // 고정 조명 목록 저장
            portableLights = targetPortableLights; // 휴대 조명 목록 저장
            ResolveReferences(); // 누락 참조와 플레이어 인벤토리 추가 확보
        }

        public bool CaptureSnapshot() // 현재 조명·전력 상태를 런타임 메모리에 저장
        {
            ResolveReferences(); // 캡처 직전 최신 참조 확보

            if (!IsConfigured) // 발전기와 배전반 핵심 참조 존재 여부 확인
            {
                Debug.LogWarning("[Project I][Day13] 발전기 또는 배전반이 없어 상태를 캡처할 수 없습니다."); // 캡처 실패 원인 출력
                return false; // 캡처 실패 반환
            }

            RuntimeSnapshot newSnapshot = new RuntimeSnapshot(); // 새 런타임 스냅샷 생성
            newSnapshot.generatorFuel = generator.CurrentFuel; // 발전기 현재 연료 저장
            newSnapshot.generatorRunning = generator.IsRunning; // 발전기 현재 가동 상태 저장
            newSnapshot.mainPowerRequested = distributionBoard.MainPowerRequested; // 배전반 메인 요청 상태 저장
            newSnapshot.roomStates = CaptureRoomStates(); // 방별 요청 전력 상태 저장
            newSnapshot.doorStates = CaptureDoorStates(); // 철제문 안정 최종 상태 저장
            newSnapshot.fixedLightStates = CaptureFixedLightStates(); // 벽 횃불·화로 점화 상태 저장
            newSnapshot.portableLightStates = CapturePortableLightStates(); // 휴대 조명 연료·점화 상태 저장
            newSnapshot.capturedTime = Time.time; // 캡처 시점 게임 시간 저장
            snapshot = newSnapshot; // 마지막 복구 대상으로 새 스냅샷 교체
            Debug.Log($"[Project I][Day13] 조명·전력 상태 캡처 완료 · Room {newSnapshot.roomStates.Length} · Door {newSnapshot.doorStates.Length} · Fixed {newSnapshot.fixedLightStates.Length} · Portable {newSnapshot.portableLightStates.Length}"); // 캡처 결과 로그 출력
            return true; // 캡처 성공 반환
        }

        public bool RestoreSnapshot() // 마지막 런타임 스냅샷 상태 복구
        {
            ResolveReferences(); // 복구 직전 최신 참조 확보

            if (snapshot == null) // 저장된 스냅샷 존재 여부 확인
            {
                Debug.LogWarning("[Project I][Day13] 복구할 조명·전력 스냅샷이 없습니다."); // 복구 대상 없음 안내
                return false; // 복구 실패 반환
            }

            if (generator != null) // 발전기 참조 존재 여부 확인
            {
                generator.StopGenerator(); // 복구 중간 상태 전달을 막기 위한 일시 발전기 정지
            }

            if (distributionBoard != null) // 중앙 배전반 참조 존재 여부 확인
            {
                distributionBoard.SetMainPowerRequested(snapshot.mainPowerRequested); // 저장된 메인 요청 상태 복구
            }

            RestoreRoomStates(snapshot.roomStates); // 방별 요청 전력 상태 복구
            RestoreDoorStates(snapshot.doorStates); // 철제문을 안정된 열림·닫힘 최종 상태로 복구
            RestoreFixedLightStates(snapshot.fixedLightStates); // 벽 횃불·화로 점화 상태 복구
            RestorePortableLightStates(snapshot.portableLightStates); // 휴대 조명 연료·점화 상태 복구

            if (generator != null) // 발전기 참조 존재 여부 확인
            {
                generator.RestoreState(snapshot.generatorFuel, snapshot.generatorRunning); // 저장된 연료와 가동 상태 마지막 복구
            }

            if (distributionBoard != null) // 배전반 참조 존재 여부 확인
            {
                distributionBoard.RefreshPowerState(); // 최종 발전기 상태 기준 방 전력과 상태등 재동기화
            }

            Debug.Log("[Project I][Day13] 조명·전력 상태 복구 완료"); // 복구 완료 로그 출력
            return true; // 복구 성공 반환
        }

        private RoomRuntimeState[] CaptureRoomStates() // 배전반 연결 방 요청 상태 배열 생성
        {
            RoomPowerZone[] rooms = distributionBoard == null ? null : distributionBoard.RoomZones; // 현재 배전반 방 목록 조회

            if (rooms == null) // 방 목록 존재 여부 확인
            {
                return new RoomRuntimeState[0]; // 빈 방 상태 배열 반환
            }

            RoomRuntimeState[] states = new RoomRuntimeState[rooms.Length]; // 방 개수만큼 스냅샷 배열 생성

            for (int index = 0; index < rooms.Length; index++) // 모든 방 전력 구역 순회
            {
                RoomPowerZone room = rooms[index]; // 현재 방 참조 조회
                states[index] = new RoomRuntimeState(room, room != null && room.RequestedPower); // 방 참조와 요청 상태 저장
            }

            return states; // 완성된 방 스냅샷 반환
        }

        private DoorRuntimeState[] CaptureDoorStates() // 배전반 연결 철제문 안정 상태 배열 생성
        {
            PoweredIronDoor[] doors = distributionBoard == null ? null : distributionBoard.ControlledDoors; // 현재 배전반 문 목록 조회

            if (doors == null) // 문 목록 존재 여부 확인
            {
                return new DoorRuntimeState[0]; // 빈 문 상태 배열 반환
            }

            DoorRuntimeState[] states = new DoorRuntimeState[doors.Length]; // 문 개수만큼 스냅샷 배열 생성

            for (int index = 0; index < doors.Length; index++) // 모든 철제문 순회
            {
                PoweredIronDoor door = doors[index]; // 현재 철제문 참조 조회
                bool restoreOpen = door != null && (door.IsOpen || door.State == PoweredIronDoorState.Opening); // 이동 중 상태를 안정된 최종 방향으로 변환
                states[index] = new DoorRuntimeState(door, restoreOpen); // 문 참조와 최종 열림 여부 저장
            }

            return states; // 완성된 문 스냅샷 반환
        }

        private FixedLightRuntimeState[] CaptureFixedLightStates() // 고정 조명 점화 상태 배열 생성
        {
            FixedLightController[] lights = fixedLights ?? new FixedLightController[0]; // null 방지 고정 조명 배열 확보
            FixedLightRuntimeState[] states = new FixedLightRuntimeState[lights.Length]; // 고정 조명 개수만큼 상태 배열 생성

            for (int index = 0; index < lights.Length; index++) // 모든 고정 조명 순회
            {
                FixedLightController light = lights[index]; // 현재 고정 조명 참조 조회
                states[index] = new FixedLightRuntimeState(light, light != null && light.IsLit); // 조명 참조와 점화 상태 저장
            }

            return states; // 완성된 고정 조명 스냅샷 반환
        }

        private PortableLightRuntimeState[] CapturePortableLightStates() // 휴대 조명 연료·점화 상태 배열 생성
        {
            PortableLightItem[] lights = portableLights ?? new PortableLightItem[0]; // null 방지 휴대 조명 배열 확보
            PortableLightRuntimeState[] states = new PortableLightRuntimeState[lights.Length]; // 휴대 조명 개수만큼 상태 배열 생성

            for (int index = 0; index < lights.Length; index++) // 모든 휴대 조명 순회
            {
                PortableLightItem light = lights[index]; // 현재 휴대 조명 참조 조회
                float fuel = light == null ? 0f : light.CurrentFuel; // 현재 남은 연료 저장값 계산
                bool ignited = light != null && light.IsIgnited; // 현재 사용자 점화 요청 상태 계산
                states[index] = new PortableLightRuntimeState(light, fuel, ignited); // 조명 참조와 연료·점화 상태 저장
            }

            return states; // 완성된 휴대 조명 스냅샷 반환
        }

        private static void RestoreRoomStates(RoomRuntimeState[] states) // 저장된 방 요청 상태 복구
        {
            if (states == null) // 방 상태 배열 존재 여부 확인
            {
                return; // 복구 대상 없음 처리
            }

            foreach (RoomRuntimeState state in states) // 저장된 방 상태 전체 순회
            {
                if (state.room != null) // 유효 방 참조 여부 확인
                {
                    state.room.SetRequestedPower(state.requestedPower); // 저장된 방 요청 전력 상태 복구
                }
            }
        }

        private static void RestoreDoorStates(DoorRuntimeState[] states) // 저장된 철제문 안정 상태 복구
        {
            if (states == null) // 문 상태 배열 존재 여부 확인
            {
                return; // 복구 대상 없음 처리
            }

            foreach (DoorRuntimeState state in states) // 저장된 철제문 상태 전체 순회
            {
                if (state.door != null) // 유효 문 참조 여부 확인
                {
                    state.door.RestoreStableState(state.open); // 열림 또는 닫힘 최종 위치 즉시 복구
                }
            }
        }

        private static void RestoreFixedLightStates(FixedLightRuntimeState[] states) // 저장된 벽 횃불·화로 상태 복구
        {
            if (states == null) // 고정 조명 상태 배열 존재 여부 확인
            {
                return; // 복구 대상 없음 처리
            }

            foreach (FixedLightRuntimeState state in states) // 저장된 고정 조명 상태 전체 순회
            {
                if (state.light == null) // 유효 고정 조명 참조 여부 확인
                {
                    continue; // 누락 조명 건너뜀
                }

                if (state.lit) // 저장된 점화 상태 확인
                {
                    state.light.TurnOn(); // 점화 상태 복구
                }
                else // 저장된 소화 상태 처리
                {
                    state.light.TurnOff(); // 소화 상태 복구
                }
            }
        }

        private void RestorePortableLightStates(PortableLightRuntimeState[] states) // 저장된 휴대 조명 상태 복구
        {
            if (states == null) // 휴대 조명 상태 배열 존재 여부 확인
            {
                return; // 복구 대상 없음 처리
            }

            ResolvePlayerInventory(); // 휴대 조명 점화 토글에 사용할 플레이어 인벤토리 확보

            foreach (PortableLightRuntimeState state in states) // 저장된 휴대 조명 상태 전체 순회
            {
                if (state.light == null) // 유효 휴대 조명 참조 여부 확인
                {
                    continue; // 누락 조명 건너뜀
                }

                state.light.SetFuel(state.fuel); // 저장된 휴대 조명 연료량 복구

                if (state.light.IsIgnited != state.ignited && playerInventory != null) // 점화 요청 상태가 다르고 인벤토리가 존재하는지 확인
                {
                    state.light.Use(playerInventory); // 기존 공개 사용 인터페이스로 점화·소화 상태 반전
                }
            }
        }

        private void ResolveReferences() // 상태 관리자 필수 참조 자동 확보
        {
            if (generator == null) // 발전기 참조 누락 여부 확인
            {
                generator = Object.FindFirstObjectByType<GeneratorController>(); // 현재 씬 활성 발전기 자동 조회
            }

            if (distributionBoard == null) // 중앙 배전반 참조 누락 여부 확인
            {
                distributionBoard = Object.FindFirstObjectByType<MainDistributionBoardController>(); // 현재 씬 활성 배전반 자동 조회
            }

            if (fixedLights == null || fixedLights.Length == 0) // 고정 조명 목록 누락 여부 확인
            {
                fixedLights = Object.FindObjectsByType<FixedLightController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 현재 씬 활성 벽 횃불·화로 자동 조회
            }

            if (portableLights == null || portableLights.Length == 0) // 휴대 조명 목록 누락 여부 확인
            {
                portableLights = Object.FindObjectsByType<PortableLightItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 현재 씬 활성 횃불·랜턴 자동 조회
            }

            ResolvePlayerInventory(); // 휴대 조명 복구용 인벤토리 참조 확보
        }

        private void ResolvePlayerInventory() // 플레이어 인벤토리 참조 자동 확보
        {
            if (playerInventory == null) // 인벤토리 참조 누락 여부 확인
            {
                playerInventory = Object.FindFirstObjectByType<PlayerInventory>(); // 현재 씬 플레이어 인벤토리 조회
            }
        }

        private sealed class RuntimeSnapshot // 메모리 전용 전체 조명·전력 상태 묶음
        {
            public float generatorFuel; // 발전기 저장 연료량
            public bool generatorRunning; // 발전기 저장 가동 상태
            public bool mainPowerRequested; // 배전반 저장 메인 요청 상태
            public RoomRuntimeState[] roomStates; // 방별 저장 요청 상태 배열
            public DoorRuntimeState[] doorStates; // 철제문 저장 안정 상태 배열
            public FixedLightRuntimeState[] fixedLightStates; // 고정 조명 저장 점화 상태 배열
            public PortableLightRuntimeState[] portableLightStates; // 휴대 조명 저장 연료·점화 상태 배열
            public float capturedTime; // 스냅샷 생성 게임 시간
        }

        private sealed class RoomRuntimeState // 방 하나의 런타임 저장 상태
        {
            public readonly RoomPowerZone room; // 복구 대상 방 참조
            public readonly bool requestedPower; // 저장된 방 전원 요청 상태

            public RoomRuntimeState(RoomPowerZone targetRoom, bool powerRequested) // 방 상태 생성자
            {
                room = targetRoom; // 복구 대상 방 참조 저장
                requestedPower = powerRequested; // 요청 전력 상태 저장
            }
        }

        private sealed class DoorRuntimeState // 철제문 하나의 런타임 저장 상태
        {
            public readonly PoweredIronDoor door; // 복구 대상 철제문 참조
            public readonly bool open; // 저장된 안정 열림 여부

            public DoorRuntimeState(PoweredIronDoor targetDoor, bool restoreOpen) // 철제문 상태 생성자
            {
                door = targetDoor; // 복구 대상 철제문 참조 저장
                open = restoreOpen; // 안정 열림 상태 저장
            }
        }

        private sealed class FixedLightRuntimeState // 고정 조명 하나의 런타임 저장 상태
        {
            public readonly FixedLightController light; // 복구 대상 고정 조명 참조
            public readonly bool lit; // 저장된 점화 여부

            public FixedLightRuntimeState(FixedLightController targetLight, bool isLit) // 고정 조명 상태 생성자
            {
                light = targetLight; // 복구 대상 고정 조명 참조 저장
                lit = isLit; // 점화 상태 저장
            }
        }

        private sealed class PortableLightRuntimeState // 휴대 조명 하나의 런타임 저장 상태
        {
            public readonly PortableLightItem light; // 복구 대상 휴대 조명 참조
            public readonly float fuel; // 저장된 현재 연료량
            public readonly bool ignited; // 저장된 사용자 점화 상태

            public PortableLightRuntimeState(PortableLightItem targetLight, float currentFuel, bool isIgnited) // 휴대 조명 상태 생성자
            {
                light = targetLight; // 복구 대상 휴대 조명 참조 저장
                fuel = currentFuel; // 현재 연료량 저장
                ignited = isIgnited; // 사용자 점화 상태 저장
            }
        }
    }
}
