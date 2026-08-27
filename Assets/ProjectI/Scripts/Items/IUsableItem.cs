namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    public interface IUsableItem // 좌클릭 사용 가능한 아이템 공통 인터페이스
    {
        bool CanUse(PlayerInventory inventory); // 현재 인벤토리 상태에서 사용 가능 여부 반환
        void Use(PlayerInventory inventory); // 선택 아이템 사용 처리
    }
}
