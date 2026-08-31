using ProjectI.Interaction; // F 상호작용 공통 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class DistributionBoardButton : MonoBehaviour, IInteractable // 배전반과 문 옆의 단일 토글 제어 스위치
    {
        [SerializeField] private string displayName = "제어 스위치"; // 상호작용 안내에 표시할 스위치 이름
        [SerializeField] private DistributionBoardButtonAction action; // 스위치 실행 종류
        [SerializeField] private MainDistributionBoardController distributionBoard; // 메인 전원 스위치 대상 배전반
        [SerializeField] private RoomPowerZone roomZone; // 방 전원 스위치 대상 방 구역
        [SerializeField] private PoweredIronDoor poweredDoor; // 문 개폐 스위치 대상 철제문
        [SerializeField] private Transform switchLever; // 현재 상태를 보여주는 움직이는 스위치 레버
        [SerializeField] private float onLeverAngle = -24f; // ON 또는 OPEN 상태 레버 각도
        [SerializeField] private float offLeverAngle = 24f; // OFF 또는 CLOSE 상태 레버 각도

        public string Prompt => BuildPrompt(); // 현재 상태 기반 F 상호작용 안내 문구 반환
        public InteractionType InteractionType => InteractionType.Toggle; // 입력마다 현재 상태 반전
        public float HoldDuration => 0f; // 즉시 스위치 입력 시간
        public DistributionBoardButtonAction Action => action; // Validator용 스위치 실행 종류 공개

        private void OnEnable() // 스위치 활성화 처리
        {
            SubscribeTarget(); // 대상 상태 변경 이벤트 구독
            UpdateSwitchVisual(); // 현재 대상 상태에 맞춰 레버 방향 동기화
        }

        private void OnDisable() // 스위치 비활성화 처리
        {
            UnsubscribeTarget(); // 비활성 상태 이벤트 구독 해제
        }

        public void Configure(string targetDisplayName, DistributionBoardButtonAction targetAction, MainDistributionBoardController board, RoomPowerZone zone, PoweredIronDoor door, Transform lever) // 자동 Setup용 토글 스위치 설정
        {
            UnsubscribeTarget(); // 이전 연결 대상 이벤트 구독 해제
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 스위치 표시 이름 저장
            action = targetAction; // 스위치 실행 종류 저장
            distributionBoard = board; // 메인 배전반 연결
            roomZone = zone; // 방 전력 구역 연결
            poweredDoor = door; // 전동 철제문 연결
            switchLever = lever; // 시각 레버 연결

            if (isActiveAndEnabled) // 현재 스위치 활성 여부 확인
            {
                SubscribeTarget(); // 새 연결 대상 이벤트 구독
            }

            UpdateSwitchVisual(); // 설정 직후 현재 상태 표시
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 플레이어가 스위치를 조작할 수 있는지 반환
        {
            return interactor != null && isActiveAndEnabled; // 유효 플레이어와 활성 스위치면 상호작용 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 대상 상태 반전
        {
            if (!CanInteract(interactor)) // 현재 스위치 조작 가능 여부 확인
            {
                return; // 조작 불가 상태에서는 실행 중단
            }

            switch (action) // 스위치 종류별 토글 실행 분기
            {
                case DistributionBoardButtonAction.MainPowerToggle: // 메인 전원 토글 처리
                    if (distributionBoard != null) // 배전반 연결 여부 확인
                    {
                        distributionBoard.SetMainPowerRequested(!distributionBoard.MainPowerRequested); // 현재 메인 요청 상태 반전
                    }
                    break; // 메인 전원 토글 처리 종료
                case DistributionBoardButtonAction.RoomPowerToggle: // 방 전원 토글 처리
                    if (roomZone != null) // 방 전력 구역 연결 여부 확인
                    {
                        roomZone.SetRequestedPower(!roomZone.RequestedPower); // 현재 방 요청 상태 반전
                    }
                    break; // 방 전원 토글 처리 종료
                case DistributionBoardButtonAction.DoorToggle: // 철제문 개폐 토글 처리
                    ToggleDoor(); // 현재 이동 방향과 상태를 반대로 전환
                    break; // 철제문 토글 처리 종료
            }

            UpdateSwitchVisual(); // 입력 직후 레버 상태 보정
        }

        private void ToggleDoor() // 철제문 열림·닫힘 상태 반전
        {
            if (poweredDoor == null) // 철제문 연결 여부 확인
            {
                return; // 대상 문이 없으면 처리 생략
            }

            bool openRequested = poweredDoor.IsOpen || poweredDoor.State == PoweredIronDoorState.Opening; // 현재 열림 방향 상태인지 계산

            if (openRequested) // 열림 또는 열리는 중 상태인지 확인
            {
                poweredDoor.RequestClose(); // 닫힘 방향으로 반전 요청
            }
            else // 닫힘 또는 닫히는 중 상태 처리
            {
                poweredDoor.RequestOpen(); // 열림 방향으로 반전 요청
            }
        }

        private void SubscribeTarget() // 현재 스위치 대상 이벤트 구독
        {
            UnsubscribeTarget(); // 중복 이벤트 등록 예방

            switch (action) // 스위치 종류별 이벤트 대상 선택
            {
                case DistributionBoardButtonAction.MainPowerToggle: // 메인 전원 스위치 대상 처리
                    if (distributionBoard != null) // 배전반 참조 존재 여부 확인
                    {
                        distributionBoard.StateChanged += HandleTargetStateChanged; // 메인 상태 변경 이벤트 구독
                    }
                    break; // 메인 이벤트 등록 종료
                case DistributionBoardButtonAction.RoomPowerToggle: // 방 전원 스위치 대상 처리
                    if (roomZone != null) // 방 전력 구역 참조 존재 여부 확인
                    {
                        roomZone.StateChanged += HandleTargetStateChanged; // 방 상태 변경 이벤트 구독
                    }
                    break; // 방 이벤트 등록 종료
                case DistributionBoardButtonAction.DoorToggle: // 철제문 스위치 대상 처리
                    if (poweredDoor != null) // 철제문 참조 존재 여부 확인
                    {
                        poweredDoor.StateChanged += HandleTargetStateChanged; // 문 상태 변경 이벤트 구독
                    }
                    break; // 문 이벤트 등록 종료
            }
        }

        private void UnsubscribeTarget() // 현재 스위치 대상 이벤트 구독 해제
        {
            if (distributionBoard != null) // 배전반 참조 존재 여부 확인
            {
                distributionBoard.StateChanged -= HandleTargetStateChanged; // 메인 상태 이벤트 구독 해제
            }

            if (roomZone != null) // 방 전력 구역 참조 존재 여부 확인
            {
                roomZone.StateChanged -= HandleTargetStateChanged; // 방 상태 이벤트 구독 해제
            }

            if (poweredDoor != null) // 철제문 참조 존재 여부 확인
            {
                poweredDoor.StateChanged -= HandleTargetStateChanged; // 문 상태 이벤트 구독 해제
            }
        }

        private void HandleTargetStateChanged() // 대상 상태 변경 이벤트 처리
        {
            UpdateSwitchVisual(); // 변경된 상태 시점에만 레버 방향 갱신
        }

        private string BuildPrompt() // 대상 상태에 맞는 F 안내 문구 생성
        {
            switch (action) // 스위치 종류별 안내 문구 분기
            {
                case DistributionBoardButtonAction.MainPowerToggle: // 메인 전원 안내 처리
                    return distributionBoard != null && distributionBoard.MainPowerRequested ? $"{displayName} 끄기" : $"{displayName} 켜기"; // 다음 동작 기준 안내 반환
                case DistributionBoardButtonAction.RoomPowerToggle: // 방 전원 안내 처리
                    return roomZone != null && roomZone.RequestedPower ? $"{displayName} 끄기" : $"{displayName} 켜기"; // 다음 동작 기준 안내 반환
                case DistributionBoardButtonAction.DoorToggle: // 철제문 안내 처리
                    string powerSuffix = poweredDoor != null && !poweredDoor.HasPower ? " · 전력 없음" : string.Empty; // 정전 상태 안내 문구 생성
                    bool openRequested = poweredDoor != null && (poweredDoor.IsOpen || poweredDoor.State == PoweredIronDoorState.Opening); // 현재 문 열림 방향 여부 계산
                    return $"{displayName} {(openRequested ? "닫기" : "열기")}{powerSuffix}"; // 다음 문 동작 안내 반환
                default: // 정의되지 않은 실행 종류 처리
                    return displayName; // 기본 스위치 이름 반환
            }
        }

        private bool IsSwitchOn() // 현재 대상이 스위치 ON 쪽 상태인지 계산
        {
            switch (action) // 스위치 종류별 현재 상태 조회
            {
                case DistributionBoardButtonAction.MainPowerToggle: // 메인 전원 상태 조회
                    return distributionBoard != null && distributionBoard.MainPowerRequested; // 메인 요청 상태 반환
                case DistributionBoardButtonAction.RoomPowerToggle: // 방 전원 상태 조회
                    return roomZone != null && roomZone.RequestedPower; // 방 요청 상태 반환
                case DistributionBoardButtonAction.DoorToggle: // 철제문 상태 조회
                    return poweredDoor != null && (poweredDoor.IsOpen || poweredDoor.State == PoweredIronDoorState.Opening); // 열림 방향을 ON 상태로 반환
                default: // 정의되지 않은 실행 종류 처리
                    return false; // 기본 OFF 상태 반환
            }
        }

        private void UpdateSwitchVisual() // 현재 대상 상태를 레버 각도로 표시
        {
            if (switchLever == null) // 시각 레버 존재 여부 확인
            {
                return; // 레버가 없으면 표시 생략
            }

            float angle = IsSwitchOn() ? onLeverAngle : offLeverAngle; // 현재 상태에 맞는 레버 각도 선택
            switchLever.localRotation = Quaternion.Euler(0f, 0f, angle); // 패널 정면에서 보이는 좌우 기울기로 상태 표현
        }

        private void OnValidate() // 인스펙터 값 변경 시 표시 상태 동기화
        {
            UpdateSwitchVisual(); // 에디터에서도 현재 대상 상태를 레버에 반영
        }
    }
}
