namespace ProjectI.Player // 플레이어 입력 기능 네임스페이스
{
    public static class GameplayInputActions // 설정·게임플레이에서 함께 사용하는 Input Action 이름 모음
    {
        public const string Map = "Player"; // 플레이어 Input Action Map 이름
        public const string Move = "Move"; // 이동 액션 이름
        public const string Look = "Look"; // 시점 액션 이름
        public const string Sprint = "Sprint"; // 달리기 액션 이름
        public const string Jump = "Jump"; // 점프 액션 이름
        public const string Crouch = "Crouch"; // 웅크리기 액션 이름
        public const string Interact = "Interact"; // 상호작용 액션 이름
        public const string Use = "Use"; // 선택 아이템 사용 액션 이름
        public const string Drop = "Drop"; // 선택 아이템 버리기 액션 이름
        public const string SlotScroll = "SlotScroll"; // 빠른 슬롯 휠 전환 액션 이름
        public const string Pause = "Pause"; // 커서 잠금·일시정지 액션 이름

        public static string QuickSlot(int index) // 빠른 슬롯 직접 선택 액션 이름 생성
        {
            return $"Slot{index + 1}"; // 0 기반 인덱스를 Slot1~Slot6 이름으로 변환
        }
    }
}
