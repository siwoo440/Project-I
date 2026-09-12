namespace ProjectI.Generation // 절차적 던전 생성 핵심 네임스페이스
{
    public sealed class DungeonGenerationConfig // 생성 규칙 값 (씬 컴포넌트가 채워서 전달)
    {
        public int MinRooms = 14; // 1층 최소 방 수
        public int MaxRooms = 20; // 1층 최대 방 수
        public int MinOtherFloorRooms = 6; // 1층 외 각 층 최소 방 수
        public int MaxOtherFloorRooms = 11; // 1층 외 각 층 최대 방 수
        public int FloorsAbove = 2; // 메인 방 위로 생성할 층 수 (2 → 2F·3F)
        public int FloorsBelow = 2; // 메인 방 아래로 생성할 층 수 (2 → B1·B2)
        public int GridMinX = -3; // 격자 최소 X
        public int GridMaxX = 3; // 격자 최대 X
        public int GridMaxY = 5; // 격자 최대 Y (시작 방은 Y=0, 메인문은 시작 방 남쪽 벽)
        public float MainPathRatio = 0.45f; // 목표 방 수 대비 주 경로 비율
        public float LoopChance = 0.2f; // 인접 방 순환 연결 확률
        public float MaxDeadEndRatio = 0.2f; // 막다른 방 최대 비율
        public float CorridorChance = 0.45f; // 직선 통과 방을 복도로 만들 확률
        public int MaxStraightCorridors = 3; // 직선 복도 최대 연속 수
        public float StairwellRatio = 0.5f; // 세로형 방이 계단통일 확률 (나머지는 사다리 계열)
        public float ShaftRatio = 0.4f; // 사다리 계열 중 좁은 수직 통로 비율
        public VerticalKind ForcedVerticalKind = VerticalKind.None; // None이 아니면 모든 세로형 방을 이 종류로 고정 (테스트용)
        public int SubDoorCount; // 실내 서브문 수 (외부 씬의 서브문 수와 같아야 함)
        public int MinSubDoorSpacing = 2; // 서브문 방끼리·시작 방과의 최소 문 거리
        public float SubDoorMaxDepthRatio = 0.7f; // 서브문은 전체 깊이의 앞·중간 구간에만 배치
        public bool EnableLockedDoor = true; // 잠긴 문과 열쇠 생성 여부
        public int MinLootRooms = 4; // 최소 회수품 방 수
        public int MaxLootRooms = 10; // 최대 회수품 방 수
        public int MaxAttempts = 80; // 시드당 최대 재생성 시도

        public int GridCellCount => (GridMaxX - GridMinX + 1) * (GridMaxY + 1); // 한 층에서 사용 가능한 격자 칸 수
        public int FloorCount => FloorsAbove + FloorsBelow + 1; // 전체 층 수

        public int[] FloorOrder() // 생성 순서 (1층 → 위로 한 층씩 → 아래로 한 층씩)
        {
            int[] order = new int[FloorCount]; // 결과
            int index = 0; // 기록 위치
            order[index++] = 0; // 메인 방 층

            for (int floor = 1; floor <= FloorsAbove; floor++) // 위층
            {
                order[index++] = floor; // 아래 층에서 이어 올림
            }

            for (int floor = 1; floor <= FloorsBelow; floor++) // 지하층
            {
                order[index++] = -floor; // 위 층에서 이어 내림
            }

            return order; // 반환
        }
    }

    public static class DungeonSeed // 캠페인·일차·지역 기반 결정적 시드
    {
        public static int For(int campaignSeed, int day, string regionKey) // 같은 캠페인·일차·지역이면 같은 시드
        {
            unchecked
            {
                uint hash = 2166136261; // FNV-1a 초기값
                hash = Mix(hash, campaignSeed); // 캠페인 시드
                hash = Mix(hash, day); // 일차

                foreach (char character in regionKey ?? string.Empty) // 지역 키
                {
                    hash = (hash ^ character) * 16777619; // 문자 누적
                }

                return (int)(hash & 0x7FFFFFFF); // 양수 시드
            }
        }

        private static uint Mix(uint hash, int value) // 정수 4바이트 누적
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8) // 바이트 순회
                {
                    hash = (hash ^ (uint)((value >> shift) & 0xFF)) * 16777619; // FNV-1a
                }

                return hash; // 결과
            }
        }
    }
}
