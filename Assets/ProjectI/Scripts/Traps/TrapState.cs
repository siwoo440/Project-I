namespace ProjectI.Traps // 함정 공통 시스템 네임스페이스
{
    public enum TrapState // 모든 Day18 함정이 공유하는 동작 상태
    {
        Ready, // 외부 작동 입력을 받을 수 있는 대기 상태
        Waiting, // 자동 반복 함정의 주기 대기 상태
        Warning, // 작동 직전 경고 상태
        Triggered, // 실제 이동·공격 동작이 시작된 상태
        Active, // 피해 판정이 유지되는 상태
        Resetting, // 함정을 초기 위치로 되돌리는 상태
        Cooldown // 다음 작동까지 재사용을 막는 상태
    }
}
