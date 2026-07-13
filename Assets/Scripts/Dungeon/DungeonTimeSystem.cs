using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 던전 시간 & 제한 시간. (기획서 PART 3.3)
    /// 게임시간 6:00 → 24:00이 현실 약 18분에 흐르며 카운트다운. 24:00 도달 시 탈출 봉쇄(유기 = 실패).
    /// 시간 경과에 따른 밝기 저하(화톳불 decay)는 Brazier가 담당(PART 5.5).
    /// </summary>
    public class DungeonTimeSystem : MonoBehaviour
    {
        [Header("시간 설정")]
        [SerializeField] float realDurationSeconds = 18f * 60f; // ≈18분 (테스트 시 30~60으로 줄여보세요)
        [SerializeField] float startHour = 6f;
        [SerializeField] float endHour = 24f;
        [SerializeField] float wagonProximity = 5f; // 이 거리 안이면 '마차에 있음'으로 간주

        float elapsed;
        bool deadlineFired;
        bool failed;
        Wagon wagon;
        InventorySystem playerInv;

        public float Progress => realDurationSeconds > 0f ? Mathf.Clamp01(elapsed / realDurationSeconds) : 1f;
        public float GameHour => Mathf.Lerp(startHour, endHour, Progress);
        public float RemainingSeconds => Mathf.Max(0f, realDurationSeconds - elapsed);
        public bool IsLocked => Progress >= 1f;   // 탈출 봉쇄

        void Start()
        {
            Time.timeScale = 1f; // 이전 플레이에서 0으로 남았을 수 있어 초기화
            wagon = FindFirstObjectByType<Wagon>();
            playerInv = FindFirstObjectByType<InventorySystem>();
        }

        void Update()
        {
            if (deadlineFired) return;
            elapsed += Time.deltaTime; // timeScale=0(탈출/실패)면 자동 정지
            if (Progress >= 1f)
            {
                deadlineFired = true;
                OnDeadline();
            }
        }

        void OnDeadline()
        {
            if (wagon != null && wagon.HasLeft) return; // 이미 탈출 성공

            // 시간 초과 시점에 마차 근처에 있으면 → 탈출 성공 처리
            if (wagon != null && playerInv != null &&
                Vector3.Distance(playerInv.transform.position, wagon.transform.position) <= wagonProximity)
            {
                Debug.Log("[Time] 시간 초과 — 마차에서 탈출 성공");
                wagon.Leave(playerInv);
                return;
            }

            failed = true;
            Debug.Log("[Time] 24:00 도달 — 탈출 봉쇄(유기, 탈출 실패)");
            Time.timeScale = 0f;
        }

        void OnGUI()
        {
            int gh = Mathf.FloorToInt(GameHour);
            int gm = Mathf.FloorToInt((GameHour - gh) * 60f);
            int rs = Mathf.CeilToInt(RemainingSeconds);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width - 230, 6, 220, 22),
                $"던전 시간 {gh:00}:{gm:00}   (남은 {rs / 60:00}:{rs % 60:00})", style);

            if (failed)
            {
                var t = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                var s = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 40), "제한 시간 초과 — 탈출 실패 (유기)", t);
                GUI.Label(new Rect(0, Screen.height / 2f + 4, Screen.width, 30), "(플레이 정지 후 다시 시작하세요)", s);
            }
        }
    }
}
