using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 목록 참조
using ProjectI.Generation; // 배치 규칙 참조
using UnityEditor; // 에디터 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase10Day31CatacombPreset // 지하묘지 프리셋(20~24방)을 대량 시드로 검사합니다
    {
        private const string LibraryPath = "Assets/ProjectI/Prefabs/Dungeon/ModuleLibrary/DungeonModuleLibrary.asset"; // 모듈 목록
        private const int SeedCount = 1000; // 검사할 시드 수
        private const int MinRooms = 20; // 최소 방 수
        private const int MaxRooms = 24; // 최대 방 수

        public static ModulePlanConfig CatacombConfig() // 지하묘지 프리셋
        {
            return new ModulePlanConfig
            {
                TargetRoomCount = 22, // 목표 방 수 (복도·세로형 방·특수 방 제외)
                MinModules = 24, // 최소 모듈 수
                TargetModules = 60, // 방 수 기준을 쓰므로 여유 있게
                ExteriorDoorCount = 2, // 외부 서브문
                EnableBoss = true, // 보스방
                SecretCount = 1, // 비밀방
                EnablePower = true, // 전력 계통
                PowerZoneCount = 3, // 배전 구역
                GridRadius = 120, // 넓은 배치 범위
            }; // 반환
        }

        [MenuItem("Project I/Day 31/Validate Catacomb Preset")] // 메뉴
        public static void Validate() // 대량 시드 검사
        {
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 목록

            if (library == null || library.Modules.Count == 0) // 없음
            {
                Debug.LogError("[Project I] 모듈 목록이 없습니다. Build Module Set 을 먼저 실행하세요."); // 오류
                return; // 종료
            }

            List<ModuleDefinition> definitions = library.BuildDefinitions(); // 정의
            ModulePlanConfig config = CatacombConfig(); // 프리셋
            List<string> problems = new List<string>(); // 문제
            int success = 0; // 성공
            int totalRooms = 0; // 방 합
            int totalModules = 0; // 모듈 합
            int totalFloors = 0; // 층 합
            int minRoomsSeen = int.MaxValue; // 최소 방 수
            int maxRoomsSeen = 0; // 최대 방 수

            for (int seed = 0; seed < SeedCount; seed++) // 시드 순회
            {
                ModuleDungeonPlan plan = SocketDungeonPlanner.Plan(definitions, config, (seed * 2654435761u).GetHashCode()); // 배치

                if (plan == null) // 실패
                {
                    problems.Add($"seed={seed} 배치 실패"); // 등록
                    continue; // 다음
                }

                success++; // 성공
                int rooms = plan.CountOfRole(ModuleRole.Room); // 방 수
                totalRooms += rooms; // 집계
                totalModules += plan.Modules.Count; // 집계
                totalFloors += plan.FloorCount; // 집계
                minRoomsSeen = rooms < minRoomsSeen ? rooms : minRoomsSeen; // 최소
                maxRoomsSeen = rooms > maxRoomsSeen ? rooms : maxRoomsSeen; // 최대

                if (rooms < MinRooms || rooms > MaxRooms) // 방 수 범위
                {
                    problems.Add($"seed={seed} 방 {rooms}개 (기대 {MinRooms}~{MaxRooms})"); // 등록
                    continue; // 다음
                }

                string problem = Inspect(plan, config, seed); // 규칙 검사

                if (!string.IsNullOrEmpty(problem)) // 문제
                {
                    problems.Add(problem); // 등록
                }
            }

            if (problems.Count > 0) // 실패
            {
                int show = Mathf.Min(10, problems.Count); // 표시 수
                Debug.LogError($"[Project I] 31일차 지하묘지 프리셋 검사 실패 {problems.Count}건 / 성공 {success}/{SeedCount} / 방 {minRoomsSeen}~{maxRoomsSeen}\n{string.Join("\n", problems.GetRange(0, show))}"); // 오류
                return; // 종료
            }

            Debug.Log($"[Project I] 31일차 지하묘지 프리셋 검사 통과 / 시드 {SeedCount}개 전부 성공 / 방 {minRoomsSeen}~{maxRoomsSeen}개 (평균 {(float)totalRooms / success:F1}) · 모듈 평균 {(float)totalModules / success:F1}개 · 층 평균 {(float)totalFloors / success:F1}개"); // 완료
        }

        public static void ValidateFromCommandLine() // 배치모드 실행용
        {
            Validate(); // 검사
        }

        private static string Inspect(ModuleDungeonPlan plan, ModulePlanConfig config, int seed) // 배치 하나를 규칙과 대조
        {
            Dictionary<WorldCell, int> occupied = new Dictionary<WorldCell, int>(); // 점유

            foreach (PlacedModule module in plan.Modules) // 모듈 순회
            {
                foreach (CellPoint cell in module.Cells) // 칸 순회
                {
                    for (int level = 0; level < module.Definition.FloorSpan; level++) // 층 순회
                    {
                        WorldCell world = new WorldCell(cell, module.Floor + level); // 칸

                        if (occupied.ContainsKey(world)) // 겹침
                        {
                            return $"seed={seed} 칸 겹침 {world}"; // 오류
                        }

                        occupied[world] = module.Index; // 등록
                    }
                }
            }

            if (plan.BossIndex < 0 || plan.SecretIndices.Count != config.SecretCount || plan.PowerPlantIndex < 0) // 특수 방
            {
                return $"seed={seed} 특수 방 누락 (보스 {plan.BossIndex} · 비밀 {plan.SecretIndices.Count} · 발전실 {plan.PowerPlantIndex})"; // 오류
            }

            if (plan.ExteriorSockets.Count != config.ExteriorDoorCount + 1) // 외부 문
            {
                return $"seed={seed} 외부 문 {plan.ExteriorSockets.Count}개"; // 오류
            }

            if (plan.Zones.Count != config.PowerZoneCount) // 배전 구역
            {
                return $"seed={seed} 배전 구역 {plan.Zones.Count}개"; // 오류
            }

            HashSet<int> reachable = Reachable(plan, false); // 전체 도달

            if (reachable.Count != plan.Modules.Count) // 도달 불가
            {
                return $"seed={seed} 도달 불가 모듈 {plan.Modules.Count - reachable.Count}개"; // 오류
            }

            HashSet<int> open = Reachable(plan, true); // 잠긴 문·금 간 벽을 빼고

            if (!open.Contains(plan.PowerPlantIndex)) // 발전실
            {
                return $"seed={seed} 발전실이 막힌 너머에만 있음"; // 오류
            }

            foreach (ModuleZone zone in plan.Zones) // 배전반
            {
                if (!open.Contains(zone.BreakerModuleIndex)) // 막힌 너머
                {
                    return $"seed={seed} {zone.Id}구역 배전반이 막힌 너머에만 있음"; // 오류
                }
            }

            foreach (int secretIndex in plan.SecretIndices) // 비밀방은 금 간 벽으로만
            {
                foreach (PlacedSocket socket in plan.Module(secretIndex).Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule >= 0 && socket.Fill != PassageFill.Breakable) // 다른 연결
                    {
                        return $"seed={seed} 비밀방 입구가 금 간 벽이 아님"; // 오류
                    }
                }
            }

            return string.Empty; // 통과
        }

        private static HashSet<int> Reachable(ModuleDungeonPlan plan, bool blockLocked) // 시작 방에서 갈 수 있는 모듈
        {
            HashSet<int> visited = new HashSet<int> { plan.EntranceIndex }; // 방문
            Queue<int> queue = new Queue<int>(); // 탐색
            queue.Enqueue(plan.EntranceIndex); // 시작

            while (queue.Count > 0) // 너비 우선 탐색
            {
                foreach (PlacedSocket socket in plan.Module(queue.Dequeue()).Sockets) // 출입구 순회
                {
                    if (socket.ConnectedModule < 0) // 연결 없음
                    {
                        continue; // 다음
                    }

                    if (blockLocked && (socket.Fill == PassageFill.LockedDoor || socket.Fill == PassageFill.Breakable)) // 막힌 연결
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
    }
}
