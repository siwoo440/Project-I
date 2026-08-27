using System; // 타입 기능 참조
using System.Collections.Generic; // 사전 자료형 참조

namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public static class ProjectServices // 공통 서비스 저장소
    {
        private static readonly Dictionary<Type, IProjectService> Services = new Dictionary<Type, IProjectService>(); // 서비스 목록 저장

        public static void Register<T>(T service) where T : class, IProjectService // 서비스 등록
        {
            Services[typeof(T)] = service; // 타입별 서비스 저장
        }

        public static bool TryGet<T>(out T service) where T : class, IProjectService // 서비스 조회 시도
        {
            if (Services.TryGetValue(typeof(T), out IProjectService storedService)) // 등록 서비스 확인
            {
                service = storedService as T; // 요청 타입 변환
                return service != null; // 조회 성공 반환
            }

            service = null; // 빈 결과 설정
            return false; // 조회 실패 반환
        }

        public static T Get<T>() where T : class, IProjectService // 필수 서비스 조회
        {
            if (TryGet(out T service)) // 등록 서비스 확인
            {
                return service; // 등록 서비스 반환
            }

            throw new InvalidOperationException($"등록되지 않은 서비스: {typeof(T).Name}"); // 누락 서비스 오류
        }

        public static void Unregister<T>() where T : class, IProjectService // 서비스 등록 해제
        {
            Services.Remove(typeof(T)); // 타입별 서비스 삭제
        }

        public static void Clear() // 전체 서비스 정리
        {
            Services.Clear(); // 서비스 목록 초기화
        }
    }
}
