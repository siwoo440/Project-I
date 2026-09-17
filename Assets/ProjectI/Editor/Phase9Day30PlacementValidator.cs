using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 목록 참조
using ProjectI.Generation; // 배치 규칙 참조
using UnityEditor; // 에디터 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase9Day30PlacementValidator // 실제 모듈 프리팹으로 소켓 배치를 여러 시드 돌려 검사합니다
    {
        private const string LibraryPath = "Assets/ProjectI/Prefabs/Dungeon/ModuleLibrary/DungeonModuleLibrary.asset"; // 모듈 목록
        private const int SeedCount = 600; // 검사할 시드 수

        [MenuItem("Project I/Day 30/Validate Placement")] // 메뉴
        public static void Validate() // 배치 검사
        {
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 목록

            if (library == null || library.Modules.Count == 0) // 없음
            {
                Debug.LogError("[Project I] 모듈 목록이 없습니다. Build Module Set 을 먼저 실행하세요."); // 오류
                return; // 종료
            }

            List<ModuleDefinition> definitions = library.BuildDefinitions(); // 정의
            ModulePlanConfig config = new ModulePlanConfig { TargetModules = 26, MinModules = 18, ExteriorDoorCount = 2, EnableBoss = true, SecretCount = 1 }; // 규칙
            List<string> problems = new List<string>(); // 문제
            int success = 0; // 성공 수
            int totalModules = 0; // 모듈 합
            int totalDoors = 0; // 문 합
            int totalOpen = 0; // 뚫린 통로 합
            int totalLocked = 0; // 잠긴 문 합
            int totalFloors = 0; // 층 수 합
            int totalVertical = 0; // 세로형 모듈 합
            int totalZones = 0; // 배전 구역 합

            for (int seed = 0; seed < SeedCount; seed++) // 시드 순회
            {
                ModuleDungeonPlan plan = SocketDungeonPlanner.Plan(definitions, config, (seed * 5417) + 13); // 배치

                if (plan == null) // 실패
                {
                    problems.Add($"seed={seed} 배치 실패"); // 등록
                    continue; // 다음
                }

                success++; // 성공
                totalModules += plan.Modules.Count; // 집계
                totalFloors += plan.FloorCount; // 집계
                totalVertical += plan.CountOfRole(ModuleRole.Vertical); // 집계
                totalZones += plan.Zones.Count; // 집계
                string problem = Inspect(plan, config, seed); // 검사

                if (!string.IsNullOrEmpty(problem)) // 문제
                {
                    problems.Add(problem); // 등록
                }

                foreach (PlacedSocket socket in plan.AllSockets()) // 출입구 순회
                {
                    if (socket.ConnectedModule < socket.ModuleIndex) // 짝은 한 번만
                    {
                        continue; // 다음
                    }

                    totalDoors += socket.Fill == PassageFill.Door ? 1 : 0; // 문
                    totalLocked += socket.Fill == PassageFill.LockedDoor ? 1 : 0; // 잠긴 문
                    totalOpen += socket.ConnectedModule >= 0 && socket.Fill == PassageFill.Open ? 1 : 0; // 뚫린 통로
                }
            }

            if (problems.Count > 0) // 문제 있음
            {
                int show = Mathf.Min(10, problems.Count); // 표시 수
                Debug.LogError($"[Project I] 30일차 배치 검사 실패 {problems.Count}건 / 성공 {success}/{SeedCount}\n{string.Join("\n", problems.GetRange(0, show))}"); // 오류
                return; // 종료
            }

            float average = success == 0 ? 0f : (float)totalModules / success; // 평균 모듈 수
            float averageFloors = success == 0 ? 0f : (float)totalFloors / success; // 평균 층 수
            Debug.Log($"[Project I] 30일차 배치 검사 통과 / 시드 {SeedCount}개 전부 성공 / 평균 모듈 {average:F1}개 · 평균 층 {averageFloors:F1}개 · 세로형 {totalVertical}개 · 전력 구역 {totalZones}개 / 연결당 뚫린 통로 {totalOpen} · 여닫이문 {totalDoors} · 잠긴 문 {totalLocked}"); // 완료
        }

        public static void ValidateFromCommandLine() // 배치모드 실행용
        {
            Validate(); // 검사
        }

        private static string Inspect(ModuleDungeonPlan plan, ModulePlanConfig config, int seed) // 배치 하나를 규칙과 대조
        {
            Dictionary<WorldCell, int> occupied = new Dictionary<WorldCell, int>(); // 칸 점유 (층 포함)

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (CellPoint cell in module.Cells) // 칸 순회
                {
                    for (int level = 0; level < module.Definition.FloorSpan; level++) // 차지하는 층 순회
                    {
                        WorldCell world = new WorldCell(cell, module.Floor + level); // 층까지 포함한 칸

                        if (occupied.TryGetValue(world, out int other)) // 겹침
                        {
                            return $"seed={seed} 칸 겹침 {world} / 모듈 {other} 와 {module.Index}"; // 오류
                        }

                        occupied[world] = module.Index; // 등록
                    }
                }
            }

            if (plan.Modules.Count < config.MinModules) // 규모 미달
            {
                return $"seed={seed} 모듈 {plan.Modules.Count}개 < 최소 {config.MinModules}"; // 오류
            }

            if (plan.ExteriorSockets.Count != config.ExteriorDoorCount + 1) // 외부 문 수
            {
                return $"seed={seed} 외부 문 {plan.ExteriorSockets.Count}개 (정문 1 + 서브문 {config.ExteriorDoorCount} 이어야 함)"; // 오류
            }

            if (config.EnableBoss && plan.BossIndex < 0) // 보스방 없음
            {
                return $"seed={seed} 보스방 없음"; // 오류
            }

            if (plan.SecretIndices.Count != config.SecretCount) // 비밀방 수
            {
                return $"seed={seed} 비밀방 {plan.SecretIndices.Count}개"; // 오류
            }

            string power = CheckPower(plan, config, seed); // 전력 계통 검사

            if (!string.IsNullOrEmpty(power)) // 문제
            {
                return power; // 오류
            }

            string reach = CheckReachable(plan, seed); // 도달 가능 검사

            if (!string.IsNullOrEmpty(reach)) // 문제
            {
                return reach; // 오류
            }

            return CheckConnections(plan, seed); // 연결 정합 검사
        }

        private static string CheckPower(ModuleDungeonPlan plan, ModulePlanConfig config, int seed) // 발전실·배전반·구역 배정 검사
        {
            if (!config.EnablePower) // 전력 계통 미사용
            {
                return string.Empty; // 통과
            }

            if (plan.PowerPlantIndex < 0) // 발전실 없음
            {
                return $"seed={seed} 발전실 없음"; // 오류
            }

            if (plan.Zones.Count != config.PowerZoneCount) // 구역 수
            {
                return $"seed={seed} 배전 구역 {plan.Zones.Count}개 (기대 {config.PowerZoneCount})"; // 오류
            }

            foreach (ModuleZone zone in plan.Zones) // 구역 순회
            {
                if (zone.BreakerModuleIndex < 0) // 차단기 없음
                {
                    return $"seed={seed} {zone.Id}구역에 배전반 없음"; // 오류
                }

                if (plan.Module(zone.BreakerModuleIndex).Role != ModuleRole.Breaker) // 역할 확인
                {
                    return $"seed={seed} {zone.Id}구역 배전반이 배전반 방이 아님"; // 오류
                }

                if (zone.ModuleIndices.Count == 0) // 빈 구역
                {
                    return $"seed={seed} {zone.Id}구역에 모듈이 없음"; // 오류
                }
            }

            foreach (PlacedModule module in plan.Modules) // 모든 모듈이 구역에 속해야 함
            {
                if (module.ZoneId < 0 || module.ZoneId >= plan.Zones.Count) // 미배정
                {
                    return $"seed={seed} 모듈 {module.Index} 구역 미배정 ({module.ZoneId})"; // 오류
                }
            }

            HashSet<int> open = ReachableWithoutBlockers(plan); // 잠긴 문·금 간 벽을 지나지 않고 갈 수 있는 모듈

            if (!open.Contains(plan.PowerPlantIndex)) // 발전실이 막힌 너머
            {
                return $"seed={seed} 발전실이 잠긴 문·금 간 벽 너머에만 있음"; // 오류
            }

            foreach (ModuleZone zone in plan.Zones) // 배전반도 마찬가지
            {
                if (!open.Contains(zone.BreakerModuleIndex)) // 막힌 너머
                {
                    return $"seed={seed} {zone.Id}구역 배전반이 잠긴 문·금 간 벽 너머에만 있음"; // 오류
                }
            }

            return string.Empty; // 통과
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

        private static string CheckReachable(ModuleDungeonPlan plan, int seed) // 시작 방에서 모든 모듈에 갈 수 있는지
        {
            HashSet<int> visited = new HashSet<int> { plan.EntranceIndex }; // 방문
            Queue<int> queue = new Queue<int>(); // 탐색
            queue.Enqueue(plan.EntranceIndex); // 시작

            while (queue.Count > 0) // 너비 우선 탐색
            {
                PlacedModule current = plan.Module(queue.Dequeue()); // 현재

                foreach (PlacedSocket socket in current.Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0 || !visited.Add(socket.ConnectedModule)) // 연결 없음·이미 방문
                    {
                        continue; // 다음
                    }

                    queue.Enqueue(socket.ConnectedModule); // 등록
                }
            }

            return visited.Count == plan.Modules.Count ? string.Empty : $"seed={seed} 도달 불가 모듈 {plan.Modules.Count - visited.Count}개"; // 결과
        }

        private static string CheckConnections(ModuleDungeonPlan plan, int seed) // 연결된 출입구가 서로 마주 보고 칸이 맞닿는지
        {
            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (PlacedSocket socket in module.Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0) // 연결 없음
                    {
                        if (!socket.IsSealed && !socket.IsExteriorDoor) // 막지도 않음
                        {
                            return $"seed={seed} 모듈 {module.Index} 출입구가 연결도 봉인도 안 됨"; // 오류
                        }

                        continue; // 다음
                    }

                    PlacedSocket other = plan.Module(socket.ConnectedModule).Sockets[socket.ConnectedSocket]; // 상대

                    if (other.ConnectedModule != module.Index || other.ConnectedSocket != socket.SocketIndex) // 짝이 어긋남
                    {
                        return $"seed={seed} 모듈 {module.Index} 출입구 연결이 한쪽만 이어짐"; // 오류
                    }

                    if (other.Floor != socket.Floor) // 층이 다름
                    {
                        return $"seed={seed} 모듈 {module.Index} 연결이 층을 건너뜀 ({socket.Floor} vs {other.Floor})"; // 오류
                    }

                    if (other.Facing != GridDirections.Opposite(socket.Facing)) // 마주 보지 않음
                    {
                        return $"seed={seed} 모듈 {module.Index} 출입구가 마주 보지 않음 ({socket.Facing} vs {other.Facing})"; // 오류
                    }

                    if (!socket.OutsideCell.Equals(other.Cell) || !other.OutsideCell.Equals(socket.Cell)) // 칸이 맞닿지 않음
                    {
                        return $"seed={seed} 모듈 {module.Index} 출입구 칸이 맞닿지 않음 {socket.Cell}→{socket.OutsideCell} vs {other.Cell}"; // 오류
                    }

                    if (socket.Fill != other.Fill) // 채움이 다름
                    {
                        return $"seed={seed} 모듈 {module.Index} 연결 양쪽 채움이 다름 ({socket.Fill} vs {other.Fill})"; // 오류
                    }
                }
            }

            foreach (int secretIndex in plan.SecretIndices) // 비밀방 검사
            {
                PlacedModule secret = plan.Module(secretIndex); // 비밀방
                int entrances = 0; // 입구 수

                foreach (PlacedSocket socket in secret.Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0) // 연결 없음
                    {
                        continue; // 다음
                    }

                    entrances++; // 집계

                    if (socket.Fill != PassageFill.Breakable) // 금 간 벽이 아님
                    {
                        return $"seed={seed} 비밀방 {secretIndex} 입구가 금 간 벽이 아님 ({socket.Fill})"; // 오류
                    }
                }

                if (entrances != 1) // 입구 1개
                {
                    return $"seed={seed} 비밀방 {secretIndex} 입구 {entrances}개"; // 오류
                }
            }

            if (plan.BossIndex >= 0) // 보스방 검사
            {
                int entrances = 0; // 입구 수

                foreach (PlacedSocket socket in plan.Module(plan.BossIndex).Sockets) // 출입구 순회
                {
                    entrances += socket.ConnectedModule >= 0 ? 1 : 0; // 집계
                }

                if (entrances != 1) // 입구 1개
                {
                    return $"seed={seed} 보스방 입구 {entrances}개"; // 오류
                }
            }

            return string.Empty; // 문제 없음
        }
    }
}
