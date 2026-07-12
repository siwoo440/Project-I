using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 밝기 시스템 ★핵심. (기획서 PART 5)
    /// 최종 밝기 = 현재 방 고정 밝기(화톳불) + 손에 든 광원 기여.
    /// 5단계 산출 + 상단 UI. F키로 손에 든 광원 On/Off.
    /// ※ 플레이어에 부착(현재 싱글 기준). 멀티 확장 시 구역 밝기 동기화는 이후.
    /// </summary>
    public class LightSystem : MonoBehaviour
    {
        LightRoom currentRoom;
        Transform searchRoot;   // 손에 든 광원 탐색 기준(카메라)

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
            // 손에 든(활성) 광원 찾기
            LightSource carried = searchRoot != null ? searchRoot.GetComponentInChildren<LightSource>(false) : null;

            // F: 손 광원 토글
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame && carried != null)
                carried.Toggle();

            float carriedC = carried != null ? carried.Contribution : 0f;
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

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
            GUI.Label(new Rect(Screen.width / 2f - 120, 6, 240, 22), $"밝기: {CurrentBrightness:F0}  ({Stage})", style);
        }
    }
}
