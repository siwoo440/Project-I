namespace ProjectI
{
    /// <summary>
    /// E키로 상호작용 가능한 오브젝트의 공통 규격. (기획서 PART 8.1 상호작용 속성)
    /// 레버·문·서랍·보물·아이템 등이 이 인터페이스를 구현한다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>화면에 표시할 상호작용 안내 문구 (예: "[E] 줍기: 랜턴")</summary>
        string GetPrompt();

        /// <summary>상호작용 실행.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
