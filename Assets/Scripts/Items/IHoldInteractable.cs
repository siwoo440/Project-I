namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public interface IHoldInteractable // 길게 누르기 상호작용 공통 규격
    {
        float HoldDuration { get; } // 필요한 입력 유지시간

        string GetHoldPrompt(float progress); // 진행률이 포함된 안내 문구 반환

        void CompleteHold(PlayerInteractor interactor); // 길게 누르기 완료 처리
    }
}