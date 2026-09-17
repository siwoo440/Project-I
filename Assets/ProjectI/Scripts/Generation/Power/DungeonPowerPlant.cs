using System.Collections; // 기동 연출 Coroutine 사용
using ProjectI.Interaction; // 상호작용 규약 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonPowerPlant : MonoBehaviour, IInteractable // 발전기 — 던전 전체 전력을 기동·정지
    {
        [SerializeField] private Transform flywheel; // 돌아가는 부품
        [SerializeField] private Renderer indicator; // 표시등
        [SerializeField] private float spinSpeed = 220f; // 회전 속도
        [SerializeField] private float startupDuration = 1.2f; // 기동에 걸리는 시간
        private DungeonPowerGrid grid; // 전력망
        private Material indicatorMaterial; // 표시등 재질 인스턴스
        private bool isBusy; // 기동·정지 중

        public DungeonPowerGrid Grid => grid; // 전력망 공개
        public bool IsRunning => grid != null && grid.PlantRunning; // 가동 여부 공개
        public string Prompt => grid == null ? "발전기 — 연결되지 않음" : isBusy ? "발전기 작동 중" : IsRunning ? "발전기 정지" : "발전기 기동"; // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // 누르기
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(Transform wheel, Renderer indicatorRenderer) // 구성 (프리팹 제작기에서 호출)
        {
            flywheel = wheel; // 부품
            indicator = indicatorRenderer; // 표시등
        }

        public void Bind(DungeonPowerGrid target) // 전력망 연결 (생성기에서 호출)
        {
            grid = target; // 전력망
            Refresh(); // 표시 반영
        }

        public bool CanInteract(PlayerInteractor interactor) // 조작 가능 여부
        {
            return grid != null && !isBusy; // 연결되어 있고 작동 중이 아닐 때
        }

        public void Interact(PlayerInteractor interactor) // 기동·정지
        {
            if (grid == null || isBusy) // 조작 불가
            {
                return; // 종료
            }

            if (IsRunning) // 가동 중이면 즉시 정지
            {
                grid.SetPlant(false); // 정지
                Refresh(); // 표시
                return; // 종료
            }

            StartCoroutine(StartupRoutine()); // 기동 연출
        }

        public void SetRunningImmediate(bool running) // 즉시 상태 지정 (검증·초기화용)
        {
            isBusy = false; // 연출 없음

            if (grid != null) // 확인
            {
                grid.SetPlant(running); // 상태
            }

            Refresh(); // 표시
        }

        private IEnumerator StartupRoutine() // 잠시 뒤 전력이 들어옴
        {
            isBusy = true; // 작동 중
            yield return new WaitForSeconds(startupDuration); // 대기
            grid.SetPlant(true); // 기동
            isBusy = false; // 종료
            Refresh(); // 표시
        }

        private void Update() // 가동 중에는 부품이 돈다
        {
            if (flywheel != null && IsRunning) // 가동 확인
            {
                flywheel.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self); // 회전
            }
        }

        private void Refresh() // 표시등 갱신
        {
            if (indicator == null) // 없음
            {
                return; // 종료
            }

            indicatorMaterial = indicatorMaterial != null ? indicatorMaterial : indicator.material; // 인스턴스 재질
            indicatorMaterial.color = IsRunning ? new Color(0.35f, 1f, 0.45f) : new Color(0.9f, 0.25f, 0.25f); // 녹색·빨강
        }
    }
}
