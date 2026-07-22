using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 밝기 시스템 ★핵심. (기획서 PART 5)
    /// 최종 밝기 = 현재 방 고정 밝기(화톳불) + 손에 든 광원 기여.
    /// 5단계 산출 + 상단 UI. T키로 손에 든 광원 On/Off.
    /// ※ 플레이어에 부착(현재 싱글 기준). 멀티 확장 시 구역 밝기 동기화는 이후.
    /// </summary>
    public class LightSystem : MonoBehaviour
    {
        LightRoom currentRoom;
        Transform searchRoot;   // 손에 든 광원 탐색 기준(카메라)

        [Header("디버그")] // 임시 밝기 UI 설정 구분
        [Tooltip("기존 OnGUI 밝기 표시 여부")] [SerializeField] bool showDebug = true; // 기존 OnGUI 밝기 표시 여부

        public float CurrentBrightness { get; private set; }
        public string Stage { get; private set; } = "완전한 어둠";

        void Awake()
        {
            var cam = GetComponentInChildren<Camera>();
            searchRoot = cam != null ? cam.transform : transform;
        }

        public void SetCurrentRoom(LightRoom room) { currentRoom = room; }
        public void ClearRoom(LightRoom room) { if (currentRoom == room) currentRoom = null; }

        void Update()
        {
            // T: '손에 든' 광원만 토글 (카메라 하위 = 현재 든 아이템)
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame)
            {
                var held = searchRoot != null ? searchRoot.GetComponentInChildren<LightSource>(false) : null;
                if (held != null) held.Toggle();
            }

            // 기여: 플레이어가 소지한 모든 '켜진' 광원 합 (손 + 인벤토리 보관 포함)
            float carriedC = 0f;
            foreach (var ls in GetComponentsInChildren<LightSource>(true))
                carriedC += ls.Contribution; // 꺼져 있으면 0

            float roomB = currentRoom != null ? currentRoom.FixedBrightness : 0f;
            CurrentBrightness = Mathf.Clamp(roomB + carriedC, 0f, 100f);
            Stage = StageOf(CurrentBrightness);
        }

        // 기획서 PART 5.2.1 밝기 5단계
        static string StageOf(float b)
        {
            if (b >= 81f) return "매우 밝음";
            if (b >= 61f) return "밝음";
            if (b >= 41f) return "어두움";
            if (b >= 21f) return "매우 어두움";
            return "완전한 어둠";
        }

        void OnGUI() // 기존 임시 밝기 UI 표시
        {
            if (!showDebug || !DebugUIToggleController.PlayerInfoVisible) // Inspector 설정과 F1 표시 상태 확인
            {
                return; // 기존 밝기 디버그 UI 표시 중단
            }

            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter }; // 밝기 UI 스타일 생성
            GUI.Label(new Rect(Screen.width / 2f - 120f, 6f, 240f, 22f), $"밝기: {CurrentBrightness:F0}  ({Stage})", style); // 기존 밝기 정보 표시
        }
    }
}