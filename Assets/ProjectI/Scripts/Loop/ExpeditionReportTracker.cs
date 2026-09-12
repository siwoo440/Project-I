using System.Collections.Generic; // 아이템 ID 집합 기능 참조
using ProjectI.Economy; // 회수품 가격 참조
using ProjectI.Items; // WorldItem·인벤토리·식별자 참조
using ProjectI.Persistence; // 일차 원정 단계 표시용 저장 서비스 참조
using ProjectI.Wagon; // 마차 적재칸 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    [DisallowMultipleComponent] // Persistent 시스템에 중복 부착 방지
    public sealed class ExpeditionReportTracker : MonoBehaviour // 출발 시 가져간 물건과 귀환 시 가져온 물건을 비교해 원정 결과를 기록
    {
        [SerializeField] private float reportDisplaySeconds = 12f; // 귀환 결과 화면 표시 시간
        private readonly HashSet<string> broughtItemIds = new HashSet<string>(); // 출발 시 마차·인벤토리에 있던 아이템 InstanceId
        private bool expeditionActive; // 원정 결과 추적 중 여부
        private float reportVisibleUntil; // 결과 표시 종료 시각
        private GUIStyle panelStyle; // 결과 패널 글꼴 스타일

        public ExpeditionReport LastReport { get; private set; } // 최근 원정 결과 공개

        public void BeginExpedition(Transform wagonRoot) // 던전 출발 시 가져가는 물건 기록
        {
            broughtItemIds.Clear(); // 이전 원정 기록 초기화

            foreach (WorldItem item in CollectPartyItems(wagonRoot)) // 마차 적재·플레이어 소지 아이템 순회
            {
                string id = GetInstanceId(item); // 개별 ID 조회

                if (!string.IsNullOrEmpty(id)) // 유효 ID 확인
                {
                    broughtItemIds.Add(id); // 가져간 물건으로 등록
                }
            }

            expeditionActive = true; // 결과 추적 시작
        }

        public void MarkFailed(int lostCount, int lostValue) // 원정 실패로 잃은 물건을 결과에 기록
        {
            if (LastReport == null) // 결과 없음 확인
            {
                LastReport = new ExpeditionReport(); // 빈 결과 생성
            }

            LastReport.Failed = true; // 실패 표시
            LastReport.LostOnFailureCount = lostCount; // 잃은 물건 수
            LastReport.LostOnFailureValue = lostValue; // 잃은 가치
        }

        public void CompleteExpedition(Transform wagonRoot) // 던전 출발(귀환) 직전 원정 결과 계산
        {
            if (!expeditionActive) // 추적 중인 원정이 있는지 확인
            {
                return; // 게임 시작 직후 등 원정 기록이 없으면 생략
            }

            ExpeditionReport report = new ExpeditionReport(); // 새 결과 생성
            HashSet<string> returnedIds = new HashSet<string>(); // 귀환하는 물건 ID 집합

            foreach (WorldItem item in CollectPartyItems(wagonRoot)) // 귀환하는 마차 적재·소지 아이템 순회
            {
                string id = GetInstanceId(item); // 개별 ID 조회
                returnedIds.Add(id ?? string.Empty); // 귀환 집합 등록
                report.ReturnedCount++; // 귀환 개수 집계

                if (!broughtItemIds.Contains(id ?? string.Empty)) // 이번 원정에서 새로 얻은 물건인지 확인
                {
                    report.NewLootCount++; // 신규 회수품 개수 집계
                    RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 가격 데이터 조회
                    report.NewLootValue += recoverable == null || recoverable.IsSold ? 0 : recoverable.Value; // 신규 회수품 가치 합산
                }
            }

            foreach (string id in broughtItemIds) // 가져갔던 물건 순회
            {
                if (!returnedIds.Contains(id)) // 귀환 목록에 없는지 확인
                {
                    report.LostBroughtCount++; // 던전에 두고 온 장비 개수 집계
                }
            }

            report.Day = DailySnapshotService.Instance == null ? 0 : DailySnapshotService.Instance.CurrentDay; // 원정 일차 기록
            LastReport = report; // 최근 결과 저장
            expeditionActive = false; // 추적 종료
            reportVisibleUntil = Time.unscaledTime + reportDisplaySeconds; // 결과 표시 시작
            Debug.Log($"[Project I] 원정 결과 / Day={report.Day} / 귀환 {report.ReturnedCount}개 / 신규 회수품 {report.NewLootCount}개(가치 {report.NewLootValue}) / 두고 온 장비 {report.LostBroughtCount}개", this); // 결과 로그
        }

        private static List<WorldItem> CollectPartyItems(Transform wagonRoot) // 마차 적재칸 내부와 플레이어 소지 아이템 수집
        {
            List<WorldItem> results = new List<WorldItem>(); // 결과 목록
            WagonCargoArea cargoArea = wagonRoot == null ? null : wagonRoot.GetComponentInChildren<WagonCargoArea>(true); // 적재칸 조회
            BoxCollider cargoTrigger = cargoArea == null ? null : cargoArea.GetComponent<BoxCollider>(); // 적재칸 판정 박스 조회
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 플레이어 인벤토리 조회
            WorldItem[] items = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 WorldItem 조회

            foreach (WorldItem item in items) // 활성 아이템 순회
            {
                if (item == null) // 파괴 항목 확인
                {
                    continue; // 다음 항목 검사
                }

                bool carried = inventory != null && item.transform.IsChildOf(inventory.transform); // 플레이어 손·인벤토리 소지 여부
                bool inCargo = !item.IsStored && !item.IsHeld && ContainsPoint(cargoTrigger, item.transform.position); // 적재칸 내부 여부

                if (carried || inCargo) // 원정대가 가지고 있는 물건인지 확인
                {
                    results.Add(item); // 결과 등록
                }
            }

            return results; // 수집 결과 반환
        }

        private static bool ContainsPoint(BoxCollider trigger, Vector3 worldPoint) // 회전된 적재칸 박스 내부 점 판정
        {
            if (trigger == null) // 판정 박스 누락 확인
            {
                return false; // 내부 아님
            }

            Vector3 local = trigger.transform.InverseTransformPoint(worldPoint) - trigger.center; // 로컬 좌표 변환
            Vector3 half = trigger.size * 0.5f; // 박스 반크기
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z; // 세 축 모두 내부인지 반환
        }

        private static string GetInstanceId(WorldItem item) // 아이템 개별 ID 조회
        {
            WorldItemIdentity identity = item == null ? null : item.GetComponent<WorldItemIdentity>(); // 식별자 조회
            return identity == null ? null : identity.InstanceId; // 개별 ID 반환
        }

        private void OnGUI() // 일차 상태와 최근 원정 결과를 테스트 화면에 표시
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스 조회

            if (service == null || !service.IsInitialized) // 일차 시스템 준비 여부 확인
            {
                return; // 표시 생략
            }

            panelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, wordWrap = true }; // 패널 스타일 생성
            string status = $"{service.CurrentDay}일차 · {GetPhaseText(service.DayPhase)}"; // 일차 상태 문구

            if (Time.unscaledTime < reportVisibleUntil && LastReport != null) // 귀환 결과 표시 시간인지 확인
            {
                status += $"\n원정 결과: 귀환 {LastReport.ReturnedCount}개 / 신규 회수품 {LastReport.NewLootCount}개 (가치 {LastReport.NewLootValue}) / 두고 온 장비 {LastReport.LostBroughtCount}개"; // 결과 문구 추가
            }

            float width = Mathf.Min(620f, Screen.width - 40f); // 패널 너비 계산
            GUI.Box(new Rect((Screen.width - width) * 0.5f, 12f, width, status.Contains("\n") ? 58f : 32f), status, panelStyle); // 화면 상단 중앙 표시
        }

        private static string GetPhaseText(ExpeditionDayPhase phase) // 원정 단계 표시 문구
        {
            switch (phase) // 단계별 문구 선택
            {
                case ExpeditionDayPhase.OnExpedition: return "원정 중"; // 원정 중 문구
                case ExpeditionDayPhase.Returned: return "귀환 완료 — 판매·보관 후 일차 마감 장부에서 하루를 마감하세요"; // 귀환 문구
                default: return "사무소 준비 — 마차 창고 안의 종으로 출발"; // 준비 문구
            }
        }
    }

    public sealed class ExpeditionReport // 한 번의 원정 결과 요약
    {
        public int Day; // 원정 일차
        public int ReturnedCount; // 귀환한 물건 수
        public int NewLootCount; // 새로 가져온 물건 수
        public int NewLootValue; // 새로 가져온 회수품 가치 합계
        public int LostBroughtCount; // 가져갔다가 두고 온 물건 수
        public bool Failed; // 원정 실패(전원 사망) 여부
        public int LostOnFailureCount; // 실패로 잃은 물건 수
        public int LostOnFailureValue; // 실패로 잃은 회수품 가치 합계
    }
}
