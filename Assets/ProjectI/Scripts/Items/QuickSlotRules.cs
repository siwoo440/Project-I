namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    public static class QuickSlotRules // 빠른 슬롯 순수 규칙 모음
    {
        public static bool IsSelectionLocked(CarryType carryType, bool hasHeldItem) // 현재 운반 상태의 슬롯 잠금 여부 계산
        {
            return carryType == CarryType.TwoHand && hasHeldItem; // 양손 아이템을 실제로 들고 있을 때만 슬롯 잠금
        }

        public static int WrapIndex(int index, int count) // 슬롯 인덱스를 원형 범위로 보정
        {
            if (count <= 0) // 유효 슬롯 수 확인
            {
                return 0; // 슬롯이 없으면 기본 인덱스 반환
            }

            int wrapped = index % count; // 슬롯 수 기준 나머지 계산
            return wrapped < 0 ? wrapped + count : wrapped; // 음수 인덱스를 마지막 슬롯 방향으로 보정
        }
    }
}
