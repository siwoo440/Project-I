namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    public enum InteractionType // 상호작용 입력 방식
    {
        Press, // 한 번 누르는 즉시 실행
        Hold, // 일정 시간 누른 뒤 실행
        Toggle // 누를 때마다 상태 전환
    }
}
