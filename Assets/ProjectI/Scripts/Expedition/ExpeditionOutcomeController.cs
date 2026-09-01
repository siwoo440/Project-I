using System.Collections.Generic; // 귀환·손실 아이템 결과 목록 기능 참조
using ProjectI.Items; // 기존 WorldItem·PlayerInventory 기능 참조
using ProjectI.Player; // 생존·사망 플레이어 상태 기능 참조
using ProjectI.Wagon; // 마차 확보 회수품·공동 보관함 기능 참조
using UnityEngine; // 씬 오브젝트 검색과 비활성화 기능 참조

namespace ProjectI.Expedition // 원정 결과 기능 네임스페이스
{
    public sealed class ExpeditionOutcomeController : MonoBehaviour // Day22 정상·부분·실패 귀환과 미회수품 손실 판정
    {
        private readonly List<WorldItem> returnedItems = new List<WorldItem>(); // 현재 결과에서 귀환 처리된 아이템 목록
        private readonly List<WorldItem> lostItems = new List<WorldItem>(); // 현재 결과에서 손실 처리된 아이템 목록

        public ExpeditionResult CurrentResult { get; private set; } = ExpeditionResult.None; // 현재 확정된 원정 결과 공개
        public bool HasResolved { get; private set; } // 현재 원정 결과 처리 완료 여부 공개
        public int LastReturnedCount => returnedItems.Count; // 최근 귀환 아이템 개수 공개
        public int LastLostCount => lostItems.Count; // 최근 손실 아이템 개수 공개
        public IReadOnlyList<WorldItem> ReturnedItems => returnedItems; // Day23 감정·판매 연결용 귀환 아이템 목록 공개
        public IReadOnlyList<WorldItem> LostItems => lostItems; // 진단용 손실 아이템 목록 공개

        public bool ResolveFromCurrentPlayers() // 현재 플레이어 생존 상태에서 정상·부분·실패 결과 자동 결정
        {
            PlayerDeathController[] players = Object.FindObjectsByType<PlayerDeathController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 Player 사망 상태 전체 조회

            if (players.Length == 0) // 원정 참가 플레이어가 없는지 확인
            {
                Debug.LogWarning("[Project I] 원정 결과를 판정할 PlayerDeathController가 없습니다.", this); // 구성 누락 경고 출력
                return false; // 결과 확정 실패 반환
            }

            int aliveCount = 0; // 생존 플레이어 수 초기화

            foreach (PlayerDeathController player in players) // 참가 플레이어 상태 순회
            {
                if (player != null && !player.IsDead) // 유효 생존 플레이어 여부 확인
                {
                    aliveCount++; // 생존 인원 증가
                }
            }

            ExpeditionResult result = aliveCount <= 0 ? ExpeditionResult.Failed : aliveCount == players.Length ? ExpeditionResult.NormalReturn : ExpeditionResult.PartialReturn; // 생존 인원에 따라 기본 결과 계산
            return Resolve(result); // 계산된 원정 결과와 물품 손실 판정 실행
        }

        public bool Resolve(ExpeditionResult result) // 지정 원정 결과 기준 귀환·손실 아이템 분류
        {
            if (result == ExpeditionResult.None) // 미확정 결과 요청 여부 확인
            {
                return false; // 실제 원정 종료 처리 거부
            }

            returnedItems.Clear(); // 이전 귀환 결과 목록 초기화
            lostItems.Clear(); // 이전 손실 결과 목록 초기화
            CurrentResult = result; // 현재 원정 결과 저장
            HasResolved = true; // 결과 확정 상태 기록
            WorldItem[] items = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬의 기존 WorldItem 전체 조회

            foreach (WorldItem item in items) // 원정 아이템 전체 순회
            {
                if (item == null) // 파괴된 아이템 참조 확인
                {
                    continue; // 다음 아이템 검사
                }

                bool returned = result != ExpeditionResult.Failed && IsReturnedItem(item); // 실패가 아니면서 마차 확보·보관·생존자 소지인지 판정

                if (returned) // 귀환 처리 대상 여부 확인
                {
                    returnedItems.Add(item); // Day23에서 사용할 귀환 목록에 유지
                    continue; // 손실 처리 건너뜀
                }

                lostItems.Add(item); // 미회수 또는 전원 사망 손실 목록에 등록
                item.gameObject.SetActive(false); // 현재 원정 월드에서 손실 아이템 비활성화
            }

            Debug.Log($"[Project I] 원정 결과 {CurrentResult} / 귀환 {returnedItems.Count}개 / 손실 {lostItems.Count}개", this); // 개발용 결과 로그 출력
            return true; // 결과 처리 성공 반환
        }

        public void ResetForTesting() // Play Mode 반복 테스트용 원정 결과 초기화
        {
            foreach (WorldItem item in lostItems) // 이전 손실 목록 순회
            {
                if (item != null) // 파괴되지 않은 아이템 확인
                {
                    item.gameObject.SetActive(true); // 테스트 목적으로 비활성화한 월드 아이템 재활성화
                }
            }

            returnedItems.Clear(); // 귀환 목록 초기화
            lostItems.Clear(); // 손실 목록 초기화
            CurrentResult = ExpeditionResult.None; // 원정 결과 미확정 상태 복구
            HasResolved = false; // 결과 처리 상태 해제
        }

        private static bool IsReturnedItem(WorldItem item) // 정상·부분 귀환에서 아이템 확보 여부 판정
        {
            WagonCargoItemState cargoState = item.GetComponent<WagonCargoItemState>(); // Day21 마차 회수품 확보 상태 조회

            if (cargoState != null && cargoState.IsSecured) // 마차 CargoArea에 실제 확보된 회수품인지 확인
            {
                return true; // 확보 회수품 귀환 처리
            }

            if (item.GetComponentInParent<WagonSharedStorage>() != null) // Day21 공동 보관함 내부 아이템인지 확인
            {
                return true; // 마차 공동 보관 장비 귀환 처리
            }

            PlayerDeathController[] players = Object.FindObjectsByType<PlayerDeathController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 생존 플레이어 소지품 판정용 플레이어 조회

            foreach (PlayerDeathController player in players) // 플레이어 전체 순회
            {
                if (player == null || player.IsDead) // 유효 생존 플레이어 여부 확인
                {
                    continue; // 죽은 플레이어는 소지품 보호 대상에서 제외
                }

                if (item.transform == player.transform || item.transform.IsChildOf(player.transform)) // 현재 아이템이 생존 플레이어의 손·인벤토리 아래에 있는지 확인
                {
                    return true; // 생존자 귀환 소지품으로 유지
                }
            }

            return false; // 마차나 생존자에게 회수되지 않은 현장 아이템으로 판정
        }
    }
}
