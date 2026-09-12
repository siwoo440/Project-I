using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 생성기 참조
using ProjectI.Generation; // 배치 규칙 참조
using UnityEditor; // 에디터 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase9Day30DungeonPreview // 모듈 던전을 실제로 생성해 월드 좌표까지 맞는지 확인합니다
    {
        private const string LibraryPath = "Assets/ProjectI/Prefabs/Dungeon/Library/DungeonModuleLibrary.asset"; // 모듈 목록
        private const string PreviewRootName = "Day30_ModuleDungeonPreview"; // 미리보기 루트
        private const int PreviewSeeds = 12; // 실제 생성으로 확인할 시드 수
        private const float SocketTolerance = 0.01f; // 출입구가 맞닿았다고 볼 오차 (m)

        [MenuItem("Project I/Day 30/Build Dungeon Preview")] // 메뉴
        public static void BuildPreview() // 한 시드를 씬에 남겨 눈으로 확인
        {
            ModuleDungeonGenerator generator = CreateGenerator(); // 생성기

            if (generator == null) // 실패
            {
                return; // 종료
            }

            if (!generator.Generate(20250912)) // 생성
            {
                Debug.LogError($"[Project I] 미리보기 생성 실패 / {generator.FailureReason}"); // 오류
                return; // 종료
            }

            string problem = Inspect(generator); // 검사
            Selection.activeGameObject = generator.gameObject; // 선택

            if (!string.IsNullOrEmpty(problem)) // 문제
            {
                Debug.LogError($"[Project I] 미리보기 검사 실패 / {problem}"); // 오류
                return; // 종료
            }

            Debug.Log($"[Project I] 30일차 미리보기 생성 완료 / 모듈 {generator.SpawnedModules.Count}개 · 문 {generator.SpawnedDoors.Count}개 · 금 간 벽 {generator.BreakableWalls.Count}개 · 외부 문 {generator.ExteriorDoors.Count}개 / 씬의 {PreviewRootName} 을 확인하세요"); // 완료
        }

        [MenuItem("Project I/Day 30/Validate Dungeon Build")] // 메뉴
        public static void ValidateBuild() // 여러 시드를 실제로 생성해 월드 좌표 검사
        {
            ModuleDungeonGenerator generator = CreateGenerator(); // 생성기

            if (generator == null) // 실패
            {
                return; // 종료
            }

            List<string> problems = new List<string>(); // 문제
            int totalModules = 0; // 모듈 합
            int totalDoors = 0; // 문 합
            int totalPlugs = 0; // 막은 출입구 합
            int totalVertical = 0; // 세로형 모듈 합

            for (int index = 0; index < PreviewSeeds; index++) // 시드 순회
            {
                int seed = (index * 104729) + 7; // 시드

                if (!generator.Generate(seed)) // 생성
                {
                    problems.Add($"seed={seed} 생성 실패 / {generator.FailureReason}"); // 등록
                    continue; // 다음
                }

                totalModules += generator.SpawnedModules.Count; // 집계
                totalDoors += generator.SpawnedDoors.Count; // 집계
                totalPlugs += generator.SpawnedPlugs.Count; // 집계
                totalVertical += generator.Plan.CountOfRole(ModuleRole.Vertical); // 집계
                string problem = Inspect(generator); // 검사

                if (!string.IsNullOrEmpty(problem)) // 문제
                {
                    problems.Add($"seed={seed} {problem}"); // 등록
                }
            }

            generator.Clear(); // 정리
            Object.DestroyImmediate(generator.gameObject); // 임시 오브젝트 제거

            if (problems.Count > 0) // 문제
            {
                Debug.LogError($"[Project I] 30일차 실제 생성 검사 실패 {problems.Count}건\n{string.Join("\n", problems.GetRange(0, Mathf.Min(8, problems.Count)))}"); // 오류
                return; // 종료
            }

            Debug.Log($"[Project I] 30일차 실제 생성 검사 통과 / 시드 {PreviewSeeds}개 / 평균 모듈 {(float)totalModules / PreviewSeeds:F1}개 · 평균 세로형 {(float)totalVertical / PreviewSeeds:F1}개 · 평균 문 {(float)totalDoors / PreviewSeeds:F1}개 · 평균 막은 출입구 {(float)totalPlugs / PreviewSeeds:F1}개 / 출입구 월드 오차 {SocketTolerance}m 이내"); // 완료
        }

        public static void ValidateFromCommandLine() // 배치모드 실행용
        {
            ValidateBuild(); // 검사
        }

        private static ModuleDungeonGenerator CreateGenerator() // 미리보기용 생성기 오브젝트
        {
            DungeonModuleLibrary library = AssetDatabase.LoadAssetAtPath<DungeonModuleLibrary>(LibraryPath); // 목록

            if (library == null) // 없음
            {
                Debug.LogError("[Project I] 모듈 목록이 없습니다. Build Module Set 을 먼저 실행하세요."); // 오류
                return null; // 실패
            }

            GameObject existing = GameObject.Find(PreviewRootName); // 기존 미리보기

            if (existing != null) // 있으면 제거
            {
                Object.DestroyImmediate(existing); // 제거
            }

            GameObject root = new GameObject(PreviewRootName); // 루트
            ModuleDungeonGenerator generator = root.AddComponent<ModuleDungeonGenerator>(); // 생성기
            generator.ConfigureLibrary(library, root.transform); // 모듈 목록 연결
            return generator; // 반환
        }

        private static string Inspect(ModuleDungeonGenerator generator) // 생성 결과를 월드 좌표 기준으로 검사
        {
            ModuleDungeonPlan plan = generator.Plan; // 배치

            if (plan == null || generator.SpawnedModules.Count != plan.Modules.Count) // 모듈 수 불일치
            {
                return $"모듈 수 불일치 (배치 {plan?.Modules.Count} vs 생성 {generator.SpawnedModules.Count})"; // 오류
            }

            for (int index = 0; index < plan.Modules.Count; index++) // 모듈 순회
            {
                PlacedModule placed = plan.Modules[index]; // 배치
                DungeonModule instance = generator.SpawnedModules[index]; // 오브젝트

                if (instance.ModuleId != placed.Definition.Id) // 종류 불일치
                {
                    return $"모듈 {index} 종류 불일치 ({instance.ModuleId} vs {placed.Definition.Id})"; // 오류
                }

                for (int socketIndex = 0; socketIndex < placed.Sockets.Count; socketIndex++) // 출입구 순회
                {
                    PlacedSocket socket = placed.Sockets[socketIndex]; // 배치 출입구
                    DungeonSocket socketObject = instance.SocketAt(socketIndex); // 오브젝트

                    if (socketObject == null) // 없음
                    {
                        return $"모듈 {index} 출입구 {socketIndex} 오브젝트 없음"; // 오류
                    }

                    string cellProblem = CheckSocketCell(generator, socket, socketObject, index, socketIndex); // 칸 대조

                    if (!string.IsNullOrEmpty(cellProblem)) // 문제
                    {
                        return cellProblem; // 오류
                    }

                    if (socket.ConnectedModule < 0 || socket.ConnectedModule < index) // 연결 없음·짝을 이미 검사
                    {
                        continue; // 다음
                    }

                    DungeonSocket otherObject = generator.SpawnedModules[socket.ConnectedModule].SocketAt(socket.ConnectedSocket); // 상대 출입구
                    float distance = Vector3.Distance(socketObject.DoorAnchor.position, otherObject.DoorAnchor.position); // 거리

                    if (distance > SocketTolerance) // 어긋남
                    {
                        return $"모듈 {index}↔{socket.ConnectedModule} 출입구가 {distance:F3}m 어긋남"; // 오류
                    }

                    float facing = Vector3.Dot(socketObject.transform.forward, otherObject.transform.forward); // 방향
                    if (facing > -0.99f) // 마주 보지 않음
                    {
                        return $"모듈 {index}↔{socket.ConnectedModule} 출입구가 마주 보지 않음 (dot={facing:F2})"; // 오류
                    }
                }
            }

            string overlap = CheckCellOverlap(generator); // 월드 칸 겹침 검사

            if (!string.IsNullOrEmpty(overlap)) // 문제
            {
                return overlap; // 오류
            }

            string sealedProblem = CheckSealedSockets(generator); // 뚫린 채 남은 출입구 검사

            if (!string.IsNullOrEmpty(sealedProblem)) // 문제
            {
                return sealedProblem; // 오류
            }

            return CheckLightLeak(generator); // 실제로 밖이 보이는 틈이 있는지 광선으로 확인
        }

        private static string CheckSocketCell(ModuleDungeonGenerator generator, PlacedSocket socket, DungeonSocket socketObject, int moduleIndex, int socketIndex) // 배치가 계산한 칸과 실제 오브젝트 위치가 같은지
        {
            Vector3 local = generator.GeneratedRoot.InverseTransformPoint(socketObject.DoorAnchor.position); // 루트 로컬
            Vector3 expected = generator.CellToLocal(socket.Cell, socket.Floor) + (OutwardVector(socket.Facing) * (ModuleDoorway.CellSize * 0.5f)); // 기대 위치 (칸 한가운데에서 바깥 면까지)
            float distance = Vector3.Distance(local, expected); // 거리 (층 높이 포함)

            return distance <= SocketTolerance ? string.Empty : $"모듈 {moduleIndex} 출입구 {socketIndex} 위치가 배치와 {distance:F3}m 다름 (칸 {socket.Cell} {socket.Facing} {WorldCell.FloorName(socket.Floor)})"; // 결과
        }

        private static string CheckLightLeak(ModuleDungeonGenerator generator) // 방 안에서 사방으로 광선을 쏴 하나라도 밖으로 빠져나가면 틈이 있는 것
        {
            Physics.SyncTransforms(); // 생성한 충돌체를 물리에 반영
            const int RayCount = 160; // 지점마다 쏠 광선 수
            const float MaxDistance = 400f; // 이 거리까지 아무것도 못 맞으면 밖으로 나간 것
            Vector3[] directions = FibonacciDirections(RayCount); // 고르게 퍼진 방향

            foreach (PlacedModule module in generator.Plan.Modules) // 모듈 순회
            {
                int samples = Mathf.Min(3, module.Cells.Count); // 모듈당 확인 지점 수

                for (int sample = 0; sample < samples; sample++) // 지점 순회
                {
                    CellPoint cell = module.Cells[(sample * module.Cells.Count) / samples]; // 고르게 고른 칸
                    Vector3 origin = generator.GeneratedRoot.TransformPoint(generator.CellToLocal(cell, module.Floor)) + (Vector3.up * 1.6f); // 눈높이

                    foreach (Vector3 direction in directions) // 방향 순회
                    {
                        if (!Physics.Raycast(origin, direction, MaxDistance, ~0, QueryTriggerInteraction.Ignore)) // 아무것도 못 맞음
                        {
                            return $"모듈 {module.Index}({module.Definition.Id}) 칸 {cell} {WorldCell.FloorName(module.Floor)} 에서 {direction} 방향으로 밖이 보입니다"; // 오류
                        }
                    }
                }
            }

            return string.Empty; // 문제 없음
        }

        private static Vector3[] FibonacciDirections(int count) // 구 표면에 고르게 퍼진 방향
        {
            Vector3[] directions = new Vector3[count]; // 결과
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f)); // 황금각

            for (int index = 0; index < count; index++) // 순회
            {
                float y = 1f - ((index / (float)(count - 1)) * 2f); // 위아래
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - (y * y))); // 반지름
                float theta = golden * index; // 각도
                directions[index] = new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius); // 방향
            }

            return directions; // 반환
        }

        private static string CheckSealedSockets(ModuleDungeonGenerator generator) // 연결되지 않은 출입구가 전부 벽으로 막혔는지 (밖이 보이는 틈 방지)
        {
            int expected = 0; // 막아야 할 출입구 수

            foreach (PlacedSocket socket in generator.Plan.AllSockets()) // 출입구 순회
            {
                if (socket.ConnectedModule < 0 && socket.Fill != PassageFill.Exterior) // 연결도 외부 문도 아님
                {
                    expected++; // 집계
                }
            }

            if (generator.SpawnedPlugs.Count != expected) // 막은 수가 모자람
            {
                return $"연결되지 않은 출입구 {expected}개 중 {generator.SpawnedPlugs.Count}개만 막힘 (밖이 보이는 틈이 생깁니다)"; // 오류
            }

            foreach (GameObject plug in generator.SpawnedPlugs) // 막음 벽 순회
            {
                if (plug == null || plug.GetComponentInChildren<Renderer>(true) == null) // 실체 없음
                {
                    return "막음 벽이 비어 있습니다"; // 오류
                }
            }

            return string.Empty; // 문제 없음
        }

        private static string CheckCellOverlap(ModuleDungeonGenerator generator) // 실제로 생성된 모듈끼리 칸이 겹치지 않는지
        {
            Dictionary<WorldCell, int> occupied = new Dictionary<WorldCell, int>(); // 점유 (층 포함)

            foreach (PlacedModule module in generator.Plan.Modules) // 모듈 순회
            {
                foreach (CellPoint cell in module.Cells) // 칸 순회
                {
                    for (int level = 0; level < module.Definition.FloorSpan; level++) // 차지하는 층 순회
                    {
                        WorldCell world = new WorldCell(cell, module.Floor + level); // 층까지 포함한 칸

                        if (occupied.TryGetValue(world, out int other)) // 겹침
                        {
                            return $"칸 {world} 을 모듈 {other} 와 {module.Index} 가 함께 차지"; // 오류
                        }

                        occupied[world] = module.Index; // 등록
                    }
                }
            }

            return string.Empty; // 문제 없음
        }

        private static Vector3 OutwardVector(GridDirection direction) // 방향 → 월드 방향
        {
            switch (direction) // 방향별
            {
                case GridDirection.North: return Vector3.forward; // +Z
                case GridDirection.East: return Vector3.right; // +X
                case GridDirection.South: return Vector3.back; // -Z
                default: return Vector3.left; // -X
            }
        }
    }
}
