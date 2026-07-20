namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public enum GameStartMode // 메인 메뉴에서 선택한 게임 시작 방식
    {
        None, // 직접 Scene을 실행한 개발 테스트 상태
        NewGame, // 새 캠페인 시작 요청
        Continue // 기존 캠페인 이어하기 요청
    }

    public static class GameStartRequest // MainMenu에서 Village로 시작 요청 전달
    {
        static GameStartMode requestedMode = GameStartMode.None; // 현재 요청된 게임 시작 방식

        public static GameStartMode RequestedMode => requestedMode; // 현재 요청된 시작 방식 반환
        public static bool HasRequest => requestedMode != GameStartMode.None; // 시작 요청 존재 여부 반환

        public static void Request(GameStartMode mode) // 메인 메뉴에서 게임 시작 방식 등록
        {
            requestedMode = mode; // 선택한 시작 방식 저장
        }

        public static GameStartMode Consume() // Village에서 시작 요청을 가져온 뒤 초기화
        {
            GameStartMode consumedMode = requestedMode; // 현재 시작 요청 임시 저장
            requestedMode = GameStartMode.None; // 중복 처리를 방지하기 위해 요청 초기화
            return consumedMode; // 가져온 시작 방식 반환
        }

        public static void Clear() // 남아 있는 이전 시작 요청 초기화
        {
            requestedMode = GameStartMode.None; // 시작 요청을 없는 상태로 변경
        }
    }
}