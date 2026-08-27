namespace ProjectI.Lighting // 휴대 조명 기능 네임스페이스
{
    public enum PortableLightState // 디버그와 향후 UI에서 사용할 휴대 조명 상태
    {
        Extinguished, // 연료가 남아 있지만 현재 소화된 상태
        Ignited, // 손 또는 월드에서 실제로 빛과 연료 소비가 활성화된 상태
        StoredPaused, // 점화 상태를 기억한 채 빠른 슬롯 보관으로 빛과 연료 소비가 일시 정지된 상태
        Empty // 연료가 모두 소모되어 점화할 수 없는 상태
    }
}
