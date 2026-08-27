using System; // 페이지 변경 이벤트 기능 참조
using System.Collections.Generic; // 디버그 페이지 목록 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public static class DebugPageRegistry // 현재 씬에서 활성화된 모든 디버그 페이지 중앙 등록소
    {
        private static readonly List<DebugPageProvider> RegisteredPages = new List<DebugPageProvider>(); // 활성 디버그 페이지 원본 목록

        public static event Action PagesChanged; // 페이지 추가·삭제 시 관리자에 알리는 이벤트

        public static void Register(DebugPageProvider page) // 디버그 페이지 등록
        {
            if (page == null || RegisteredPages.Contains(page)) // 유효하지 않거나 이미 등록된 페이지인지 확인
            {
                return; // 중복 등록 방지
            }

            RegisteredPages.Add(page); // 활성 페이지 목록에 추가
            PagesChanged?.Invoke(); // 디버그 페이지 구성이 바뀌었음을 관리자에 전달
        }

        public static void Unregister(DebugPageProvider page) // 디버그 페이지 등록 해제
        {
            if (page == null || !RegisteredPages.Remove(page)) // 제거할 페이지 존재 여부 확인
            {
                return; // 변경할 목록이 없으면 종료
            }

            PagesChanged?.Invoke(); // 디버그 페이지 구성이 바뀌었음을 관리자에 전달
        }

        public static List<DebugPageProvider> CreateSortedSnapshot() // 현재 사용할 수 있는 페이지 정렬 사본 생성
        {
            RegisteredPages.RemoveAll(page => page == null); // 파괴된 MonoBehaviour 참조 정리
            List<DebugPageProvider> snapshot = RegisteredPages.FindAll(page => page != null && page.isActiveAndEnabled); // 활성 페이지들만 새 목록으로 복사
            snapshot.Sort(ComparePages); // 지정 SortOrder와 이름을 기준으로 일정하게 정렬
            return snapshot; // 관리자에서 안전하게 사용할 페이지 사본 반환
        }

        private static int ComparePages(DebugPageProvider left, DebugPageProvider right) // 두 디버그 페이지 정렬 규칙
        {
            int orderCompare = left.SortOrder.CompareTo(right.SortOrder); // 우선 페이지 순번 비교

            if (orderCompare != 0) // 페이지 순번이 서로 다른지 확인
            {
                return orderCompare; // 지정 순번 기준 정렬 결과 반환
            }

            return string.Compare(left.PageName, right.PageName, StringComparison.Ordinal); // 같은 순번이면 페이지 이름으로 안정적으로 정렬
        }
    }
}
