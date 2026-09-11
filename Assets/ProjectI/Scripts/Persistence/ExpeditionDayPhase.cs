namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    public enum ExpeditionDayPhase // 하루 안에서의 원정 진행 단계
    {
        OfficePrep = 0, // 사무소 준비 단계 (오늘 원정 출발 가능)
        OnExpedition = 1, // 원정 진행 중 (저장하지 않는 런타임 전용 단계)
        Returned = 2 // 오늘 원정 귀환 완료 (일차 마감 전까지 재출발 불가)
    }
}
