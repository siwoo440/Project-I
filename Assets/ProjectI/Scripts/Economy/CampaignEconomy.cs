using UnityEngine; // 유니티 수치 보정과 컴포넌트 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [DisallowMultipleComponent] // 씬 경제 상태 중복 부착 방지
    public sealed class CampaignEconomy : MonoBehaviour // 공동 자금과 현재 판매 배율을 관리하는 기본 경제 상태
    {
        [SerializeField] private int sharedFunds; // 모든 플레이어가 공유하는 사무소 공동 자금
        [SerializeField] private float saleMultiplier = 1f; // 현재 판매 시 적용할 기본 배율

        public int SharedFunds => sharedFunds; // 장부·판매소에서 확인할 공동 자금 공개
        public float SaleMultiplier => saleMultiplier; // 판매 금액 계산용 현재 배율 공개

        public void Configure(int initialFunds, float targetSaleMultiplier) // 에디터 테스트 환경용 초기 경제 값 설정
        {
            sharedFunds = Mathf.Max(0, initialFunds); // 공동 자금 음수 시작 방지
            saleMultiplier = Mathf.Max(0f, targetSaleMultiplier); // 음수 판매 배율 방지
        }

        public int CalculateSalePrice(RecoverableValue recoverable) // 회수품의 현재 실제 판매 금액 계산
        {
            if (recoverable == null || recoverable.IsSold) // 유효한 미판매 회수품 여부 확인
            {
                return 0; // 판매 대상이 아니면 금액 없음 반환
            }

            return Mathf.Max(0, Mathf.FloorToInt(recoverable.Value * saleMultiplier)); // 확정 가격에 현재 판매 배율을 적용하여 정수 판매가 계산
        }

        public void AddFunds(int amount) // 회수품 판매 등으로 공동 자금 증가
        {
            if (amount <= 0) // 실제 증가 금액 존재 여부 확인
            {
                return; // 0 이하 금액은 무시
            }

            sharedFunds += amount; // 공동 자금에 판매 수익 반영
        }

        public bool TrySpend(int amount) // 채무 상환 등으로 공동 자금 사용 시도
        {
            if (amount <= 0 || sharedFunds < amount) // 유효 금액과 보유 자금 충족 여부 확인
            {
                return false; // 자금 부족 또는 잘못된 금액이면 사용 실패
            }

            sharedFunds -= amount; // 공동 자금에서 요청 금액 차감
            return true; // 자금 사용 성공 반환
        }

        private void OnValidate() // 인스펙터 경제 값 검증
        {
            sharedFunds = Mathf.Max(0, sharedFunds); // 공동 자금 음수 방지
            saleMultiplier = Mathf.Max(0f, saleMultiplier); // 판매 배율 음수 방지
        }
    }
}
