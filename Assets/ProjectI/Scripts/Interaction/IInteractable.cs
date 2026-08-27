namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    public interface IInteractable // 플레이어가 바라보고 조작할 수 있는 공통 대상
    {
        string Prompt { get; } // 화면에 표시할 상호작용 문구
        InteractionType InteractionType { get; } // 대상이 요구하는 입력 방식
        float HoldDuration { get; } // 길게 누르기 완료에 필요한 시간

        bool CanInteract(PlayerInteractor interactor); // 현재 플레이어가 상호작용 가능한지 반환
        void Interact(PlayerInteractor interactor); // 실제 상호작용 실행
    }
}
