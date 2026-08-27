namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public interface IProjectService // 공통 서비스 규약
    {
        void Initialize(); // 서비스 초기화
        void Shutdown(); // 서비스 종료
    }
}
