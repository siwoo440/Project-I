using UnityEngine; // 유니티 컴포넌트와 직렬화 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [DisallowMultipleComponent] // 한 회수품에 가격 상태 중복 부착 방지
    public sealed class RecoverableValue : MonoBehaviour // 회수품의 확정 가격과 판매 완료 상태 저장
    {
        [SerializeField] private int value = 100; // 생성 이후 변하지 않는 현재 회수품 가격
        [SerializeField] private bool isSold; // 판매 완료로 원정·보관 대상에서 제외된 상태

        public int Value => value; // 판매·보관 가격 판정용 값 공개
        public bool IsSold => isSold; // 중복 판매 방지용 판매 완료 상태 공개

        public void Configure(int targetValue) // 에디터 자동 설정과 향후 생성 시스템용 가격 지정
        {
            value = Mathf.Max(0, targetValue); // 음수 가격을 방지하면서 회수품 가격 저장
            isSold = false; // 새로 구성한 회수품을 미판매 상태로 초기화
        }

        public void MarkSold() // 판매 완료 상태 기록
        {
            isSold = true; // 이후 판매와 원정 손실 판정에서 제외하도록 상태 변경
        }

        private void OnValidate() // 인스펙터 가격 값 검증
        {
            value = Mathf.Max(0, value); // 음수 가격 입력 방지
        }
    }
}
