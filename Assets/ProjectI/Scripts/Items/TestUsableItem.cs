using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    public sealed class TestUsableItem : MonoBehaviour, IUsableItem // 6일차 좌클릭 사용 시험 아이템
    {
        [SerializeField] private string useLabel = "테스트 사용"; // Console에 표시할 사용 이름
        private int useCount; // 현재까지 사용 횟수

        public int UseCount => useCount; // 검증용 사용 횟수 공개

        public bool CanUse(PlayerInventory inventory) // 테스트 아이템 사용 가능 여부 반환
        {
            return inventory != null; // 유효 인벤토리에서 항상 사용 허용
        }

        public void Use(PlayerInventory inventory) // 테스트 아이템 좌클릭 사용 처리
        {
            useCount++; // 사용 횟수 증가
            Debug.Log($"[Project I] {useLabel} - 사용 횟수 {useCount}", this); // 테스트 사용 결과 Console 출력
        }

        public void Configure(string label) // 에디터 자동 설정용 사용 이름 지정
        {
            useLabel = label; // 사용 이름 저장
        }
    }
}
