namespace ProjectI.Generation // 배치 규칙 네임스페이스 (유니티 비의존)
{
    public enum PassageFill // 연결된 출입구에 무엇이 놓이는지
    {
        Open, // 그냥 뚫려 있음 (문 없음)
        Door, // 여닫이문 (열고 닫을 수 있음)
        LockedDoor, // 잠긴 문 (열쇠 필요)
        Breakable, // 부술 수 있는 벽 (비밀방)
        Exterior, // 외부 씬과 잇는 순간이동 문
    }

    public static class ModuleDoorway // 모든 모듈이 공유하는 출입구 규격
    {
        public const float CellSize = 1f; // 모듈 격자 한 칸 (m)
        public const float Width = 2.4f; // 출입구 폭 (m) — 모든 모듈·문이 이 값을 씁니다
        public const float Height = 2.8f; // 출입구 높이 (m)
        public const float FrameDepth = 0.3f; // 문틀 두께 (m)
        public const float LeafThickness = 0.12f; // 문짝 두께 (m)
        public const float LeafClearance = 0.04f; // 문짝과 문틀 사이 여유 (m)

        public static float LeafWidth => Width - (LeafClearance * 2f); // 문짝 폭
        public static float LeafHeight => Height - LeafClearance; // 문짝 높이

        public static bool BlocksMovement(PassageFill fill) // 통행을 막는 채움인지
        {
            return fill == PassageFill.LockedDoor || fill == PassageFill.Breakable; // 잠긴 문·금 간 벽만 막음 (여닫이문은 열 수 있음)
        }

        public static bool HasLeaf(PassageFill fill) // 여닫이 문짝이 있는지
        {
            return fill == PassageFill.Door || fill == PassageFill.LockedDoor; // 문짝 필요
        }
    }
}
