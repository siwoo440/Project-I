namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public enum DistributionBoardButtonAction // 배전반과 문 옆 단일 토글 스위치 실행 종류
    {
        MainPowerToggle, // 시설 메인 전원 요청 상태 반전
        RoomPowerToggle, // 지정 방 전원 요청 상태 반전
        DoorToggle // 지정 철제문 열림·닫힘 요청 상태 반전
    }
}
