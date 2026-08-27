using UnityEngine; // MonoBehaviour 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public abstract class DebugPageProvider : MonoBehaviour // F1 디버그 창에 자동 등록되는 페이지 기본 클래스
    {
        public abstract string PageName { get; } // 디버그 창 상단에 표시할 페이지 이름
        public abstract int SortOrder { get; } // 디버그 페이지 정렬 순서
        public abstract string BuildDebugText(); // 현재 프레임에 표시할 페이지 내용 생성

        protected virtual void OnEnable() // 디버그 페이지 활성화 처리
        {
            DebugPageRegistry.Register(this); // 공통 디버그 페이지 목록에 현재 페이지 자동 등록
        }

        protected virtual void OnDisable() // 디버그 페이지 비활성화 처리
        {
            DebugPageRegistry.Unregister(this); // 공통 디버그 페이지 목록에서 현재 페이지 제거
        }
    }
}
