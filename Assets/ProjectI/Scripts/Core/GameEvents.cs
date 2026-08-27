using System; // 이벤트 대리자 참조

namespace ProjectI.Core // 프로젝트 공통 네임스페이스
{
    public static class GameEvents // 공통 게임 이벤트 모음
    {
        public static event Action<GameState> GameStateChanged; // 게임 상태 변경 이벤트
        public static event Action<string> SceneChanged; // 씬 변경 이벤트

        public static void PublishGameStateChanged(GameState state) // 게임 상태 이벤트 발행
        {
            GameStateChanged?.Invoke(state); // 구독자에게 상태 전달
        }

        public static void PublishSceneChanged(string sceneName) // 씬 변경 이벤트 발행
        {
            SceneChanged?.Invoke(sceneName); // 구독자에게 씬 이름 전달
        }

        public static void Clear() // 이벤트 구독 초기화
        {
            GameStateChanged = null; // 상태 이벤트 초기화
            SceneChanged = null; // 씬 이벤트 초기화
        }
    }
}
