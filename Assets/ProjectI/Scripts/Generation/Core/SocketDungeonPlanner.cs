using System.Collections.Generic; // 목록 사용
using Random = System.Random; // 결정적 난수

namespace ProjectI.Generation // 배치 규칙 네임스페이스 (유니티 비의존)
{
    public static class SocketDungeonPlanner // 모듈의 출입구(소켓)끼리 맞춰 붙여 던전을 배치 (격자 성장 방식이 아님)
    {
        public static ModuleDungeonPlan Plan(IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, int seed) // 배치 (실패하면 null)
        {
            if (modules == null || modules.Count == 0 || config == null) // 입력 확인
            {
                return null; // 실패
            }

            for (int attempt = 0; attempt < config.MaxAttempts; attempt++) // 재시도
            {
                ModuleDungeonPlan plan = TryPlan(modules, config, new Random(unchecked(seed + (attempt * 7919)))); // 한 번 시도

                if (plan != null) // 성공
                {
                    plan.Attempt = attempt; // 시도 번호
                    return plan; // 반환
                }
            }

            return null; // 실패
        }

        private static ModuleDungeonPlan TryPlan(IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random) // 한 번 배치 시도
        {
            ModuleDungeonPlan plan = new ModuleDungeonPlan(); // 결과
            Dictionary<WorldCell, int> occupied = new Dictionary<WorldCell, int>(); // 월드 칸(층 포함) → 모듈 번호
            ModuleDefinition entrance = PickByRole(modules, ModuleRole.Entrance, random); // 시작 방

            if (entrance == null || !TryPlace(plan, occupied, config, entrance, 0, new CellPoint(0, 0), 0, 0)) // 시작 방 배치
            {
                return null; // 실패
            }

            plan.EntranceIndex = 0; // 시작 방 번호

            if (!ReserveExteriorDoors(plan, config, random)) // 외부 씬 문 예약 (정문 1개 + 서브문 N개)
            {
                return null; // 실패
            }

            GrowBody(plan, occupied, modules, config, random); // 본체 성장

            if (plan.Modules.Count < config.MinModules) // 최소 규모 미달
            {
                return null; // 실패
            }

            if (config.EnableBoss && !PlaceSpecial(plan, occupied, modules, config, random, ModuleRole.Boss, config.MinBossDepth, PassageFill.Door)) // 보스방
            {
                return null; // 실패
            }

            for (int index = 0; index < config.SecretCount; index++) // 비밀방
            {
                if (!PlaceSpecial(plan, occupied, modules, config, random, ModuleRole.Secret, config.MinSecretDepth, PassageFill.Breakable)) // 금 간 벽 너머
                {
                    return null; // 실패
                }
            }

            RecordFloorRange(plan); // 층 범위 기록
            AddLoops(plan, random); // 마주 본 빈 출입구끼리 이어 순환로 생성
            AssignFills(plan, config, random); // 문·잠긴 문·뚫린 통로 결정
            SealOpenSockets(plan); // 남은 출입구는 벽으로 막음
            return plan; // 성공
        }

        private static void GrowBody(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random) // 목표 수까지 모듈을 이어 붙임
        {
            int guard = config.TargetModules * 40; // 무한 반복 방지

            while (plan.Modules.Count < config.TargetModules && guard-- > 0) // 목표까지
            {
                PlacedSocket socket = PickFrontier(plan, random); // 이어 붙일 출입구

                if (socket == null) // 더 붙일 곳 없음
                {
                    return; // 종료
                }

                PlacedModule owner = plan.Module(socket.ModuleIndex); // 소속 모듈
                bool preferCorridor = owner.Role != ModuleRole.Corridor && random.NextDouble() < config.CorridorAlternateBias; // 방 다음에는 복도
                bool wantVertical = owner.Role != ModuleRole.Vertical && random.NextDouble() < config.VerticalChance; // 가끔 층을 옮기는 세로형 방

                if (wantVertical && TryAttachVertical(plan, occupied, modules, config, random, socket)) // 세로형 방 먼저 시도
                {
                    continue; // 다음
                }

                if (!TryAttachAny(plan, occupied, modules, config, random, socket, preferCorridor)) // 붙이기 시도
                {
                    socket.IsSealed = true; // 실패한 출입구는 막음
                }
            }
        }

        private static bool TryAttachVertical(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random, PlacedSocket socket) // 층을 옮기는 세로형 방을 붙임
        {
            List<ModuleDefinition> candidates = new List<ModuleDefinition>(); // 후보

            foreach (ModuleDefinition definition in modules) // 모듈 순회
            {
                if (definition.Role == ModuleRole.Vertical) // 세로형
                {
                    candidates.Add(definition); // 후보
                }
            }

            if (candidates.Count == 0) // 후보 없음
            {
                return false; // 실패
            }

            foreach (ModuleDefinition candidate in Shuffle(candidates, random)) // 후보 순회
            {
                if (TryAttach(plan, occupied, config, random, socket, candidate, PassageFill.Open)) // 붙이기
                {
                    return true; // 성공
                }
            }

            return false; // 실패
        }

        private static void RecordFloorRange(ModuleDungeonPlan plan) // 실제로 쓰인 층 범위 기록
        {
            int min = 0; // 최저
            int max = 0; // 최고

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                min = module.Floor < min ? module.Floor : min; // 최저
                int top = module.Floor + (module.Definition.FloorSpan - 1); // 이 모듈의 최상층
                max = top > max ? top : max; // 최고
            }

            plan.MinFloor = min; // 기록
            plan.MaxFloor = max; // 기록
        }

        private static bool TryAttachAny(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random, PlacedSocket socket, bool preferCorridor) // 후보 모듈을 차례로 시도
        {
            List<ModuleDefinition> candidates = Shuffle(BodyCandidates(modules, preferCorridor), random); // 후보 (가중치 반영 후 섞기)

            foreach (ModuleDefinition candidate in candidates) // 후보 순회
            {
                if (TryAttach(plan, occupied, config, random, socket, candidate, PassageFill.Open)) // 붙이기
                {
                    return true; // 성공
                }
            }

            return false; // 실패
        }

        private static bool IsFloorUsable(ModuleDungeonPlan plan, int floor) // 이 층에 모듈이 있는지
        {
            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                if (module.Floor == floor) // 일치
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static bool TryAttach(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, ModulePlanConfig config, Random random, PlacedSocket socket, ModuleDefinition candidate, PassageFill fill) // 지정 모듈을 지정 출입구에 붙임
        {
            List<int> socketOrder = new List<int>(); // 후보 모듈의 출입구 순서

            for (int index = 0; index < candidate.Sockets.Count; index++) // 출입구 순회
            {
                SocketKind kind = candidate.Sockets[index].Kind; // 종류
                bool usable = fill == PassageFill.Breakable ? kind == SocketKind.Breakable : kind == SocketKind.Door || kind == SocketKind.VerticalUp; // 연결에 쓸 수 있는 종류인지 (세로형 방의 위층 출구 포함)

                if (usable) // 사용 가능
                {
                    socketOrder.Add(index); // 후보
                }
            }

            ShuffleInts(socketOrder, random); // 순서 섞기
            GridDirection required = GridDirections.Opposite(socket.Facing); // 상대 출입구가 향해야 하는 방향
            CellPoint targetCell = socket.OutsideCell; // 상대 출입구의 안쪽 칸
            int targetFloor = socket.Floor; // 상대 출입구가 있어야 하는 층

            foreach (int socketIndex in socketOrder) // 출입구 순회
            {
                SocketDefinition local = candidate.Sockets[socketIndex]; // 모듈 로컬 출입구

                for (int turns = 0; turns < 4; turns++) // 네 방향 회전
                {
                    SocketDefinition rotated = local.Rotated(turns); // 회전 적용

                    if (rotated.Facing != required) // 방향 불일치
                    {
                        continue; // 다음 회전
                    }

                    CellPoint origin = new CellPoint(targetCell.X - rotated.Cell.X, targetCell.Y - rotated.Cell.Y); // 월드 원점
                    int floor = targetFloor - rotated.FloorOffset; // 모듈 바닥이 놓일 층 (세로형 방을 위층 출구로 붙이면 한 층 아래에 놓임)

                    if (floor < -config.FloorsBelow || (floor + candidate.FloorSpan) - 1 > config.FloorsAbove) // 허용 층 범위 밖
                    {
                        continue; // 다음 회전
                    }

                    if (!TryPlace(plan, occupied, config, candidate, turns, origin, floor, plan.Module(socket.ModuleIndex).Depth + 1)) // 배치 시도
                    {
                        continue; // 다음 회전
                    }

                    PlacedModule placed = plan.Modules[plan.Modules.Count - 1]; // 방금 배치한 모듈
                    Connect(socket, placed.Sockets[socketIndex], fill); // 출입구 연결
                    return true; // 성공
                }
            }

            return false; // 실패
        }

        private static bool TryPlace(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, ModulePlanConfig config, ModuleDefinition definition, int turns, CellPoint origin, int floor, int depth) // 겹침·범위를 확인하고 배치
        {
            List<CellPoint> worldCells = new List<CellPoint>(definition.Cells.Count); // 월드 칸

            foreach (CellPoint cell in definition.Cells) // 칸 순회
            {
                CellPoint rotated = cell.Rotated(turns); // 회전
                CellPoint world = new CellPoint(rotated.X + origin.X, rotated.Y + origin.Y); // 이동

                if (world.X < -config.GridRadius || world.X > config.GridRadius || world.Y < -config.GridRadius || world.Y > config.GridRadius) // 범위 밖
                {
                    return false; // 실패
                }

                for (int level = 0; level < definition.FloorSpan; level++) // 차지하는 층 순회 (세로형 방은 두 층)
                {
                    if (occupied.ContainsKey(new WorldCell(world, floor + level))) // 이미 다른 모듈이 차지
                    {
                        return false; // 실패
                    }
                }

                worldCells.Add(world); // 등록
            }

            PlacedModule module = new PlacedModule { Index = plan.Modules.Count, Definition = definition, QuarterTurns = turns, Origin = origin, Floor = floor, Depth = depth }; // 모듈

            foreach (CellPoint world in worldCells) // 칸 확정
            {
                for (int level = 0; level < definition.FloorSpan; level++) // 층 순회
                {
                    occupied[new WorldCell(world, floor + level)] = module.Index; // 점유
                }

                module.Cells.Add(world); // 등록
            }

            for (int index = 0; index < definition.Sockets.Count; index++) // 출입구 확정
            {
                SocketDefinition rotated = definition.Sockets[index].Rotated(turns); // 회전
                module.Sockets.Add(new PlacedSocket
                {
                    ModuleIndex = module.Index, // 소속
                    SocketIndex = index, // 번호
                    Cell = new CellPoint(rotated.Cell.X + origin.X, rotated.Cell.Y + origin.Y), // 월드 칸
                    Floor = floor + rotated.FloorOffset, // 출입구가 있는 층
                    Facing = rotated.Facing, // 월드 방향
                    Kind = rotated.Kind, // 종류
                }); // 등록
            }

            plan.Modules.Add(module); // 배치 확정

            if (definition.Role == ModuleRole.Boss) // 보스방
            {
                plan.BossIndex = module.Index; // 기록
            }
            else if (definition.Role == ModuleRole.Secret) // 비밀방
            {
                plan.SecretIndices.Add(module.Index); // 기록
            }

            return true; // 성공
        }

        private static bool PlaceSpecial(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random, ModuleRole role, int minDepth, PassageFill fill) // 보스방·비밀방을 깊은 곳에 배치
        {
            List<ModuleDefinition> candidates = new List<ModuleDefinition>(); // 후보

            foreach (ModuleDefinition definition in modules) // 모듈 순회
            {
                if (definition.Role == role) // 역할 일치
                {
                    candidates.Add(definition); // 후보
                }
            }

            if (candidates.Count == 0) // 후보 없음
            {
                return false; // 실패
            }

            List<PlacedSocket> frontier = OpenSockets(plan); // 빈 출입구
            frontier.Sort((left, right) => plan.Module(right.ModuleIndex).Depth.CompareTo(plan.Module(left.ModuleIndex).Depth)); // 깊은 곳 우선

            foreach (PlacedSocket socket in frontier) // 출입구 순회
            {
                if (plan.Module(socket.ModuleIndex).Depth < minDepth) // 너무 얕음
                {
                    continue; // 다음
                }

                if (plan.Module(socket.ModuleIndex).Role == ModuleRole.Boss || plan.Module(socket.ModuleIndex).Role == ModuleRole.Secret) // 특수 방끼리 붙이지 않음
                {
                    continue; // 다음
                }

                foreach (ModuleDefinition candidate in Shuffle(candidates, random)) // 후보 순회
                {
                    if (TryAttach(plan, occupied, config, random, socket, candidate, fill)) // 붙이기
                    {
                        return true; // 성공
                    }
                }
            }

            return false; // 실패
        }

        private static bool ReserveExteriorDoors(ModuleDungeonPlan plan, ModulePlanConfig config, Random random) // 시작 방의 외부 전용 출입구를 정문·서브문으로 예약
        {
            PlacedModule entrance = plan.Module(plan.EntranceIndex); // 시작 방
            List<PlacedSocket> exterior = new List<PlacedSocket>(); // 외부 전용 출입구

            foreach (PlacedSocket socket in entrance.Sockets) // 출입구 순회
            {
                if (socket.Kind == SocketKind.Exterior) // 외부 전용
                {
                    exterior.Add(socket); // 후보
                }
            }

            int required = config.ExteriorDoorCount + 1; // 서브문 + 정문

            if (exterior.Count < required) // 부족
            {
                return false; // 실패
            }

            ShuffleSockets(exterior, random); // 순서 섞기

            for (int index = 0; index < exterior.Count; index++) // 예약
            {
                if (index < required) // 사용
                {
                    exterior[index].Fill = PassageFill.Exterior; // 외부 문
                    plan.ExteriorSockets.Add(exterior[index]); // 등록
                }
                else // 남는 외부 전용 출입구
                {
                    exterior[index].IsSealed = true; // 벽으로 막음
                }
            }

            return true; // 성공
        }

        private static void AddLoops(ModuleDungeonPlan plan, Random random) // 마주 본 빈 출입구끼리 연결해 막다른 길을 줄임
        {
            Dictionary<WorldCell, PlacedSocket> byOutside = new Dictionary<WorldCell, PlacedSocket>(); // 바깥 칸 → 출입구

            foreach (PlacedSocket socket in OpenSockets(plan)) // 빈 출입구 순회
            {
                byOutside[socket.OutsideWorldCell] = socket; // 등록 (같은 칸이면 마지막 것)
            }

            foreach (PlacedSocket socket in OpenSockets(plan)) // 빈 출입구 순회
            {
                if (!socket.IsOpen) // 이미 연결됨
                {
                    continue; // 다음
                }

                if (!byOutside.TryGetValue(socket.WorldCell, out PlacedSocket other)) // 내 안쪽 칸을 바깥 칸으로 삼는 출입구 찾기
                {
                    continue; // 다음
                }

                if (!other.IsOpen || other.ModuleIndex == socket.ModuleIndex || other.Facing != GridDirections.Opposite(socket.Facing)) // 짝이 아님
                {
                    continue; // 다음
                }

                if (random.NextDouble() > 0.65) // 일부만 연결 (전부 이으면 구조가 단조로워짐)
                {
                    continue; // 다음
                }

                Connect(socket, other, PassageFill.Open); // 순환로 연결
            }
        }

        private static void AssignFills(ModuleDungeonPlan plan, ModulePlanConfig config, Random random) // 연결된 출입구에 문·잠긴 문·뚫린 통로 배정
        {
            bool lockedPlaced = false; // 잠긴 문은 던전에 하나

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (PlacedSocket socket in module.Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0 || socket.ConnectedModule < socket.ModuleIndex) // 연결 없음·이미 처리한 짝
                    {
                        continue; // 다음
                    }

                    if (socket.Fill == PassageFill.Breakable || socket.Fill == PassageFill.Exterior) // 이미 정해짐
                    {
                        continue; // 다음
                    }

                    PlacedSocket other = plan.Module(socket.ConnectedModule).Sockets[socket.ConnectedSocket]; // 상대 출입구
                    PassageFill fill = PassageFill.Open; // 기본은 뚫린 통로
                    bool toBoss = plan.Module(socket.ConnectedModule).Role == ModuleRole.Boss || module.Role == ModuleRole.Boss; // 보스방 입구

                    if (toBoss) // 보스방은 항상 문
                    {
                        fill = PassageFill.Door; // 여닫이문
                    }
                    else if (!lockedPlaced && plan.Module(socket.ModuleIndex).Depth >= config.MinBossDepth && random.NextDouble() < 0.35) // 깊은 곳에 잠긴 문 하나
                    {
                        fill = PassageFill.LockedDoor; // 잠긴 문
                        lockedPlaced = true; // 기록
                    }
                    else if (random.NextDouble() < 0.45) // 나머지는 일부만 문
                    {
                        fill = PassageFill.Door; // 여닫이문
                    }

                    socket.Fill = fill; // 적용
                    other.Fill = fill; // 짝도 같은 값
                }
            }
        }

        private static void SealOpenSockets(ModuleDungeonPlan plan) // 연결되지 않은 출입구는 벽으로 막음
        {
            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (PlacedSocket socket in module.Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0 && !socket.IsExteriorDoor) // 비어 있음
                    {
                        socket.IsSealed = true; // 막음
                    }
                }
            }
        }

        private static void Connect(PlacedSocket a, PlacedSocket b, PassageFill fill) // 두 출입구를 서로 연결
        {
            a.ConnectedModule = b.ModuleIndex; // 상대 모듈
            a.ConnectedSocket = b.SocketIndex; // 상대 출입구
            b.ConnectedModule = a.ModuleIndex; // 상대 모듈
            b.ConnectedSocket = a.SocketIndex; // 상대 출입구
            a.Fill = fill; // 채움
            b.Fill = fill; // 채움
        }

        private static PlacedSocket PickFrontier(ModuleDungeonPlan plan, Random random) // 다음에 이어 붙일 빈 출입구 (깊은 쪽을 조금 더 선호)
        {
            List<PlacedSocket> open = OpenSockets(plan); // 빈 출입구

            if (open.Count == 0) // 없음
            {
                return null; // 종료
            }

            PlacedSocket best = null; // 선택
            int bestScore = int.MinValue; // 점수

            foreach (PlacedSocket socket in open) // 순회
            {
                int score = (plan.Module(socket.ModuleIndex).Depth * 2) + random.Next(0, 7); // 깊이 + 흔들기
                best = score > bestScore ? socket : best; // 갱신
                bestScore = score > bestScore ? score : bestScore; // 갱신
            }

            return best; // 반환
        }

        private static List<PlacedSocket> OpenSockets(ModuleDungeonPlan plan) // 아직 비어 있는 출입구 모음
        {
            List<PlacedSocket> open = new List<PlacedSocket>(); // 결과

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (PlacedSocket socket in module.Sockets) // 출입구 순회
                {
                    if (socket.IsOpen && (socket.Kind == SocketKind.Door || socket.Kind == SocketKind.VerticalUp)) // 일반 연결용 빈 출입구 (세로형 방의 위층 출구 포함)
                    {
                        open.Add(socket); // 등록
                    }
                }
            }

            return open; // 반환
        }

        private static List<ModuleDefinition> BodyCandidates(IReadOnlyList<ModuleDefinition> modules, bool preferCorridor) // 본체에 쓸 후보 (가중치만큼 중복해 넣음)
        {
            List<ModuleDefinition> candidates = new List<ModuleDefinition>(); // 결과

            foreach (ModuleDefinition definition in modules) // 모듈 순회
            {
                if (definition.Role != ModuleRole.Room && definition.Role != ModuleRole.Corridor) // 본체 대상 아님
                {
                    continue; // 다음
                }

                int weight = definition.Weight; // 가중치
                bool corridor = definition.Role == ModuleRole.Corridor; // 복도 여부
                weight = preferCorridor == corridor ? weight * 3 : weight; // 선호에 맞으면 가중

                for (int index = 0; index < weight; index++) // 가중치만큼
                {
                    candidates.Add(definition); // 등록
                }
            }

            return candidates; // 반환
        }

        private static ModuleDefinition PickByRole(IReadOnlyList<ModuleDefinition> modules, ModuleRole role, Random random) // 역할로 하나 뽑기
        {
            List<ModuleDefinition> matches = new List<ModuleDefinition>(); // 후보

            foreach (ModuleDefinition definition in modules) // 순회
            {
                if (definition.Role == role) // 일치
                {
                    matches.Add(definition); // 후보
                }
            }

            return matches.Count == 0 ? null : matches[random.Next(matches.Count)]; // 반환
        }

        private static List<ModuleDefinition> Shuffle(List<ModuleDefinition> source, Random random) // 순서 섞기
        {
            for (int index = source.Count - 1; index > 0; index--) // 뒤에서부터
            {
                int swap = random.Next(index + 1); // 교환 대상
                ModuleDefinition temp = source[index]; // 임시
                source[index] = source[swap]; // 교환
                source[swap] = temp; // 교환
            }

            return source; // 반환
        }

        private static void ShuffleInts(List<int> source, Random random) // 순서 섞기
        {
            for (int index = source.Count - 1; index > 0; index--) // 뒤에서부터
            {
                int swap = random.Next(index + 1); // 교환 대상
                int temp = source[index]; // 임시
                source[index] = source[swap]; // 교환
                source[swap] = temp; // 교환
            }
        }

        private static void ShuffleSockets(List<PlacedSocket> source, Random random) // 순서 섞기
        {
            for (int index = source.Count - 1; index > 0; index--) // 뒤에서부터
            {
                int swap = random.Next(index + 1); // 교환 대상
                PlacedSocket temp = source[index]; // 임시
                source[index] = source[swap]; // 교환
                source[swap] = temp; // 교환
            }
        }
    }
}
