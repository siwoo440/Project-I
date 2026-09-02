namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    public interface IRemoteDailySnapshotStore // 향후 서버·클라우드 백업 구현이 맞출 공통 계약
    {
        bool Upload(string snapshotKey, string envelopeJson); // 일차 스냅샷 원문 서버 업로드 성공 여부 반환
        bool TryDownload(string snapshotKey, out string envelopeJson); // 서버에서 특정 스냅샷 원문 조회
    }
}
