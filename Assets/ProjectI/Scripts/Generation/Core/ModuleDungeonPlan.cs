using System.Collections.Generic; // 목록 사용

namespace ProjectI.Generation // 배치 규칙 네임스페이스 (유니티 비의존)
{
    public sealed class PlacedSocket // 배치된 모듈의 출입구 하나 (월드 격자 기준)
    {
        public int ModuleIndex; // 소속 모듈 번호
        public int SocketIndex; // 모듈 안에서의 출입구 번호
        public CellPoint Cell; // 출입구 안쪽 칸 (월드)
        public int Floor; // 출입구가 있는 층
        public GridDirection Facing; // 모듈 바깥 방향 (월드)
        public SocketKind Kind; // 종류
        public int ConnectedModule = -1; // 연결된 모듈 (없으면 -1)
        public int ConnectedSocket = -1; // 연결된 출입구 (없으면 -1)
        public PassageFill Fill = PassageFill.Open; // 이 출입구에 놓이는 것 (연결된 두 출입구는 같은 값)
        public bool IsExteriorDoor => Fill == PassageFill.Exterior; // 외부 씬 문으로 예약됨
        public bool IsBreakable => Fill == PassageFill.Breakable; // 부술 수 있는 벽으로 연결됨
        public bool IsSealed; // 연결되지 않아 벽으로 막힌 출입구

        public bool IsOpen => ConnectedModule < 0 && !IsExteriorDoor && !IsSealed; // 아직 비어 있는 출입구
        public CellPoint OutsideCell => Cell.Step(Facing); // 바깥쪽 칸
        public WorldCell WorldCell => new WorldCell(Cell, Floor); // 층까지 포함한 칸
        public WorldCell OutsideWorldCell => new WorldCell(OutsideCell, Floor); // 바깥쪽 칸 (같은 층)
    }

    public sealed class PlacedModule // 배치된 모듈 하나
    {
        public int Index; // 번호
        public ModuleDefinition Definition; // 모듈 종류
        public int QuarterTurns; // 90° 단위 회전
        public CellPoint Origin; // 월드 격자 원점
        public int Floor; // 모듈 바닥이 놓인 층
        public int Depth; // 시작 방에서의 거리
        public readonly List<CellPoint> Cells = new List<CellPoint>(); // 차지하는 월드 칸
        public readonly List<PlacedSocket> Sockets = new List<PlacedSocket>(); // 출입구

        public ModuleRole Role => Definition.Role; // 역할
    }

    public sealed class ModuleDungeonPlan // 소켓 배치 결과
    {
        public readonly List<PlacedModule> Modules = new List<PlacedModule>(); // 배치된 모듈
        public int EntranceIndex = -1; // 시작 방
        public int MinFloor; // 가장 아래층
        public int MaxFloor; // 가장 위층
        public int FloorCount => (MaxFloor - MinFloor) + 1; // 층 수
        public int BossIndex = -1; // 보스방
        public readonly List<int> SecretIndices = new List<int>(); // 비밀방
        public readonly List<PlacedSocket> ExteriorSockets = new List<PlacedSocket>(); // 외부 씬 문으로 쓸 출입구
        public int Attempt; // 성공한 시도 번호

        public PlacedModule Module(int index) => Modules[index]; // 모듈 조회

        public int CountOfRole(ModuleRole role) // 역할별 모듈 수
        {
            int count = 0; // 집계

            foreach (PlacedModule module in Modules) // 순회
            {
                if (module.Role == role) // 일치
                {
                    count++; // 집계
                }
            }

            return count; // 결과
        }

        public IEnumerable<PlacedSocket> AllSockets() // 전체 출입구
        {
            foreach (PlacedModule module in Modules) // 모듈 순회
            {
                foreach (PlacedSocket socket in module.Sockets) // 출입구 순회
                {
                    yield return socket; // 반환
                }
            }
        }
    }

    public sealed class ModulePlanConfig // 소켓 배치 규칙
    {
        public int FloorsAbove = 2; // 시작 층 위로 만들 층 수
        public int FloorsBelow = 2; // 시작 층 아래로 만들 층 수
        public float VerticalChance = 0.22f; // 이어 붙일 때 세로형 방을 고를 확률
        public int MinModulesPerFloor = 4; // 층마다 최소 모듈 수
        public int TargetModules = 26; // 목표 모듈 수
        public int MinModules = 18; // 최소 모듈 수
        public int ExteriorDoorCount = 2; // 외부 씬 서브문 수 (외부 씬 배치 수를 읽어 넣음)
        public bool EnableBoss = true; // 보스방 사용
        public int SecretCount = 1; // 비밀방 수
        public int MaxAttempts = 40; // 재시도 횟수
        public int GridRadius = 90; // 배치 가능한 격자 반경 (칸)
        public int MinBossDepth = 4; // 보스방 최소 깊이
        public int MinSecretDepth = 2; // 비밀방 최소 깊이
        public float CorridorAlternateBias = 0.7f; // 방 다음에는 복도를 고르는 성향
    }
}
