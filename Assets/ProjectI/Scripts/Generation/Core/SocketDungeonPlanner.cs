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

            if (!ReserveMainDoor(plan, random)) // 시작 방에는 정문만 두고 나머지 외부 전용 출입구는 막음
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

            if (config.EnablePower && !PlacePowerRooms(plan, occupied, modules, config, random)) // 발전실·배전반
            {
                return null; // 실패
            }

            if (!PlaceSubDoors(plan, config, random)) // 서브문 (시작 방에서 충분히 떨어진 곳에만)
            {
                return null; // 실패
            }

            RecordFloorRange(plan); // 층 범위 기록
            AddLoops(plan, random); // 마주 본 빈 출입구끼리 이어 순환로 생성
            AssignFills(plan, config, random); // 문·잠긴 문·뚫린 통로 결정
            SealOpenSockets(plan); // 남은 출입구는 벽으로 막음
            return plan; // 성공
        }

        private static void GrowBody(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random) // 목표 수까지 모듈을 이어 붙임
        {
            int budget = config.TargetRoomCount * 3 > config.TargetModules ? config.TargetRoomCount * 3 : config.TargetModules; // 성장 예산
            int guard = budget * 40; // 무한 반복 방지

            while (!GrowthDone(plan, config) && guard-- > 0) // 목표까지
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

        private static bool GrowthDone(ModuleDungeonPlan plan, ModulePlanConfig config) // 성장을 멈출 조건
        {
            if (config.TargetRoomCount > 0) // 방 수 기준
            {
                return plan.CountOfRole(ModuleRole.Room) >= config.TargetRoomCount; // 방 수 도달
            }

            return plan.Modules.Count >= config.TargetModules; // 모듈 수 도달
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

        private static bool PlacePowerRooms(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random) // 발전실 1개와 구역마다 배전반 1개를 배치하고 구역을 나눔
        {
            if (!PlaceRoleRoom(plan, occupied, modules, config, random, ModuleRole.PowerPlant, config.MinPowerPlantDepth, out int plantIndex)) // 발전실
            {
                return false; // 실패
            }

            plan.PowerPlantIndex = plantIndex; // 기록
            int maxDepth = 1; // 최대 깊이

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                maxDepth = module.Depth > maxDepth ? module.Depth : maxDepth; // 갱신
            }

            int zoneCount = config.PowerZoneCount < 1 ? 1 : config.PowerZoneCount; // 구역 수

            for (int index = 0; index < zoneCount; index++) // 구역별 깊이 구간 계산
            {
                ModuleZone zone = new ModuleZone { Id = index }; // 구역
                zone.MinDepth = (maxDepth * index) / zoneCount; // 시작 깊이
                zone.MaxDepth = index == zoneCount - 1 ? int.MaxValue : ((maxDepth * (index + 1)) / zoneCount) - 1; // 끝 깊이
                plan.Zones.Add(zone); // 등록
            }

            foreach (ModuleZone zone in plan.Zones) // 구역마다 배전반 방 하나
            {
                if (!PlaceBreakerForZone(plan, occupied, modules, config, random, zone)) // 배치
                {
                    return false; // 실패
                }
            }

            AssignZones(plan); // 모든 모듈을 구역에 배정
            return true; // 성공
        }

        private static bool PlaceBreakerForZone(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random, ModuleZone zone) // 구역 깊이 구간 안의 방에 배전반을 붙임
        {
            List<ModuleDefinition> candidates = RoleCandidates(modules, ModuleRole.Breaker); // 후보

            if (candidates.Count == 0) // 후보 없음
            {
                return false; // 실패
            }

            List<PlacedSocket> frontier = OpenSockets(plan); // 빈 출입구

            foreach (PlacedSocket socket in frontier) // 구간 안쪽 우선
            {
                PlacedModule owner = plan.Module(socket.ModuleIndex); // 소속 모듈

                if (owner.Depth < zone.MinDepth || owner.Depth > zone.MaxDepth || IsSpecial(owner.Role)) // 구간 밖·특수 방
                {
                    continue; // 다음
                }

                foreach (ModuleDefinition candidate in Shuffle(new List<ModuleDefinition>(candidates), random)) // 후보 순회
                {
                    if (TryAttach(plan, occupied, config, random, socket, candidate, PassageFill.Open)) // 붙이기
                    {
                        PlacedModule placed = plan.Modules[plan.Modules.Count - 1]; // 방금 배치한 배전반 방
                        placed.ZoneId = zone.Id; // 구역 고정
                        zone.BreakerModuleIndex = placed.Index; // 기록
                        return true; // 성공
                    }
                }
            }

            foreach (PlacedSocket socket in frontier) // 구간을 못 맞추면 아무 곳에나 (구역 자체는 유지)
            {
                PlacedModule owner = plan.Module(socket.ModuleIndex); // 소속 모듈

                if (IsSpecial(owner.Role)) // 특수 방
                {
                    continue; // 다음
                }

                foreach (ModuleDefinition candidate in Shuffle(new List<ModuleDefinition>(candidates), random)) // 후보 순회
                {
                    if (TryAttach(plan, occupied, config, random, socket, candidate, PassageFill.Open)) // 붙이기
                    {
                        PlacedModule placed = plan.Modules[plan.Modules.Count - 1]; // 배전반 방
                        placed.ZoneId = zone.Id; // 구역 고정
                        zone.BreakerModuleIndex = placed.Index; // 기록
                        return true; // 성공
                    }
                }
            }

            return false; // 실패
        }

        private static bool PlaceRoleRoom(ModuleDungeonPlan plan, Dictionary<WorldCell, int> occupied, IReadOnlyList<ModuleDefinition> modules, ModulePlanConfig config, Random random, ModuleRole role, int minDepth, out int placedIndex) // 역할 방 하나를 깊이 조건에 맞춰 배치
        {
            placedIndex = -1; // 기본값
            List<ModuleDefinition> candidates = RoleCandidates(modules, role); // 후보

            if (candidates.Count == 0) // 후보 없음
            {
                return false; // 실패
            }

            List<PlacedSocket> frontier = OpenSockets(plan); // 빈 출입구

            foreach (PlacedSocket socket in frontier) // 출입구 순회
            {
                PlacedModule owner = plan.Module(socket.ModuleIndex); // 소속 모듈

                if (owner.Depth < minDepth || IsSpecial(owner.Role)) // 너무 얕음·특수 방
                {
                    continue; // 다음
                }

                foreach (ModuleDefinition candidate in Shuffle(new List<ModuleDefinition>(candidates), random)) // 후보 순회
                {
                    if (TryAttach(plan, occupied, config, random, socket, candidate, PassageFill.Open)) // 붙이기
                    {
                        placedIndex = plan.Modules.Count - 1; // 기록
                        return true; // 성공
                    }
                }
            }

            return false; // 실패
        }

        private static void AssignZones(ModuleDungeonPlan plan) // 깊이에 따라 모든 모듈을 배전 구역에 배정
        {
            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                if (module.ZoneId >= 0) // 이미 고정된 배전반 방
                {
                    continue; // 다음
                }

                foreach (ModuleZone zone in plan.Zones) // 구역 순회
                {
                    if (module.Depth >= zone.MinDepth && module.Depth <= zone.MaxDepth) // 구간 일치
                    {
                        module.ZoneId = zone.Id; // 배정
                        break; // 종료
                    }
                }

                if (module.ZoneId < 0 && plan.Zones.Count > 0) // 못 찾으면 마지막 구역
                {
                    module.ZoneId = plan.Zones[plan.Zones.Count - 1].Id; // 배정
                }
            }

            foreach (PlacedModule module in plan.Modules) // 구역별 목록 채우기
            {
                if (module.ZoneId >= 0 && module.ZoneId < plan.Zones.Count) // 유효
                {
                    plan.Zones[module.ZoneId].ModuleIndices.Add(module.Index); // 등록
                }
            }
        }

        private static List<ModuleDefinition> RoleCandidates(IReadOnlyList<ModuleDefinition> modules, ModuleRole role) // 역할별 후보
        {
            List<ModuleDefinition> candidates = new List<ModuleDefinition>(); // 결과

            foreach (ModuleDefinition definition in modules) // 모듈 순회
            {
                if (definition.Role == role) // 일치
                {
                    candidates.Add(definition); // 후보
                }
            }

            return candidates; // 반환
        }

        private static bool IsSpecial(ModuleRole role) // 다른 것을 이어 붙이면 안 되는 방
        {
            return role == ModuleRole.Boss || role == ModuleRole.Secret || role == ModuleRole.PowerPlant || role == ModuleRole.Breaker; // 판정
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

                if (IsSpecial(plan.Module(socket.ModuleIndex).Role)) // 특수 방끼리 붙이지 않음
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

        private static bool ReserveMainDoor(ModuleDungeonPlan plan, Random random) // 시작 방의 외부 전용 출입구 하나를 정문으로 쓰고 나머지는 막음
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

            if (exterior.Count == 0) // 정문 자리가 없음
            {
                return false; // 실패
            }

            ShuffleSockets(exterior, random); // 순서 섞기
            exterior[0].Fill = PassageFill.Exterior; // 정문
            plan.ExteriorSockets.Add(exterior[0]); // 등록

            for (int index = 1; index < exterior.Count; index++) // 남는 외부 전용 출입구
            {
                exterior[index].IsSealed = true; // 벽으로 막음
            }

            return true; // 성공
        }

        private static bool PlaceSubDoors(ModuleDungeonPlan plan, ModulePlanConfig config, Random random) // 시작 방에서 충분히 떨어진 방의 빈 출입구를 서브문으로 예약
        {
            List<PlacedSocket> candidates = new List<PlacedSocket>(); // 후보

            foreach (PlacedSocket socket in OpenSockets(plan)) // 빈 출입구 순회
            {
                PlacedModule owner = plan.Module(socket.ModuleIndex); // 소속 모듈

                if (owner.Depth < config.MinSubDoorDepth || IsSpecial(owner.Role) || owner.Role == ModuleRole.Vertical) // 너무 가깝거나 특수·세로형 방
                {
                    continue; // 다음
                }

                candidates.Add(socket); // 후보
            }

            candidates.Sort((left, right) => plan.Module(right.ModuleIndex).Depth.CompareTo(plan.Module(left.ModuleIndex).Depth)); // 깊은 곳 우선
            List<PlacedSocket> chosen = new List<PlacedSocket>(); // 선택

            foreach (PlacedSocket socket in candidates) // 후보 순회
            {
                if (chosen.Count >= config.ExteriorDoorCount) // 다 채움
                {
                    break; // 종료
                }

                bool tooClose = false; // 다른 서브문과 너무 가까운지

                foreach (PlacedSocket other in chosen) // 이미 고른 것과 비교
                {
                    int gap = plan.Module(socket.ModuleIndex).Depth - plan.Module(other.ModuleIndex).Depth; // 깊이 차
                    gap = gap < 0 ? -gap : gap; // 절댓값
                    tooClose |= socket.ModuleIndex == other.ModuleIndex || gap < config.MinSubDoorGap; // 같은 방·가까운 깊이
                }

                if (!tooClose) // 충분히 떨어짐
                {
                    chosen.Add(socket); // 선택
                }
            }

            if (chosen.Count < config.ExteriorDoorCount) // 간격 조건을 못 맞추면 깊이 조건만 지켜 채움
            {
                foreach (PlacedSocket socket in candidates) // 후보 순회
                {
                    if (chosen.Count >= config.ExteriorDoorCount) // 다 채움
                    {
                        break; // 종료
                    }

                    if (!chosen.Contains(socket)) // 중복 아님
                    {
                        chosen.Add(socket); // 선택
                    }
                }
            }

            if (chosen.Count < config.ExteriorDoorCount) // 자리가 모자람
            {
                return false; // 실패
            }

            foreach (PlacedSocket socket in chosen) // 서브문 예약
            {
                socket.Fill = PassageFill.Exterior; // 외부 문
                plan.ExteriorSockets.Add(socket); // 등록
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
                    else if (!lockedPlaced && plan.Module(socket.ModuleIndex).Depth >= config.MinBossDepth && random.NextDouble() < 0.35 && CanLock(plan, socket, other)) // 깊은 곳에 잠긴 문 하나 (전력 설비를 막지 않는 곳에만)
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

        private static bool CanLock(ModuleDungeonPlan plan, PlacedSocket socket, PlacedSocket other) // 이 연결을 잠가도 발전실·배전반에 갈 수 있는지
        {
            PassageFill previousA = socket.Fill; // 원래 값
            PassageFill previousB = other.Fill; // 원래 값
            socket.Fill = PassageFill.LockedDoor; // 임시 적용
            other.Fill = PassageFill.LockedDoor; // 임시 적용
            HashSet<int> reachable = ReachableWithoutBlockers(plan); // 막힌 연결을 빼고 도달 가능한 모듈
            bool ok = plan.PowerPlantIndex < 0 || reachable.Contains(plan.PowerPlantIndex); // 발전실

            foreach (ModuleZone zone in plan.Zones) // 배전반
            {
                ok &= zone.BreakerModuleIndex < 0 || reachable.Contains(zone.BreakerModuleIndex); // 확인
            }

            socket.Fill = previousA; // 되돌리기
            other.Fill = previousB; // 되돌리기
            return ok; // 판정
        }

        private static HashSet<int> ReachableWithoutBlockers(ModuleDungeonPlan plan) // 잠긴 문·금 간 벽을 지나지 않고 갈 수 있는 모듈
        {
            HashSet<int> visited = new HashSet<int> { plan.EntranceIndex }; // 방문
            Queue<int> queue = new Queue<int>(); // 탐색
            queue.Enqueue(plan.EntranceIndex); // 시작

            while (queue.Count > 0) // 너비 우선 탐색
            {
                foreach (PlacedSocket socket in plan.Module(queue.Dequeue()).Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0 || socket.Fill == PassageFill.LockedDoor || socket.Fill == PassageFill.Breakable) // 막힌 연결
                    {
                        continue; // 다음
                    }

                    if (visited.Add(socket.ConnectedModule)) // 처음 방문
                    {
                        queue.Enqueue(socket.ConnectedModule); // 등록
                    }
                }
            }

            return visited; // 반환
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
