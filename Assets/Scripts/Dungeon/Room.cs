using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 던전 방(그리드 셀 하나). 4방향 벽을 가지며, 인접 방과 연결되는 쪽 벽을 연다(비활성화).
    /// 방 프리팹에는 LightRoom(트리거+밝기)도 함께 넣어 밝기 계산에 자동 참여. (기획서 PART 6.1)
    /// </summary>
    public class Room : MonoBehaviour
    {
        public enum Dir { N = 0, E = 1, S = 2, W = 3 }  // N=+Z, E=+X, S=-Z, W=-X

        [SerializeField] GameObject wallN;
        [SerializeField] GameObject wallE;
        [SerializeField] GameObject wallS;
        [SerializeField] GameObject wallW;

        /// <summary>해당 방향 벽을 열어(비활성화) 통로를 만든다.</summary>
        public void OpenSide(Dir d)
        {
            var w = WallOf(d);
            if (w != null) w.SetActive(false);
        }

        GameObject WallOf(Dir d)
        {
            switch (d)
            {
                case Dir.N: return wallN;
                case Dir.E: return wallE;
                case Dir.S: return wallS;
                default:    return wallW;
            }
        }

        public static Dir Opposite(Dir d)
        {
            switch (d)
            {
                case Dir.N: return Dir.S;
                case Dir.S: return Dir.N;
                case Dir.E: return Dir.W;
                default:    return Dir.E;
            }
        }
    }
}
