using System.Collections.Generic; // 목록 기능 참조
using NUnit.Framework; // 테스트 프레임워크 참조

namespace ProjectI.Generation.Tests // 절차적 던전 생성 테스트 네임스페이스
{
    public sealed class DungeonLayoutGeneratorTests // 27·28일차 실내 던전 그래프 생성 규칙 자동 검사
    {
        private const int SeedCount = 1000; // 서브문 수별 검사 시드 개수

        private static DungeonGenerationConfig CreateConfig(int subDoorCount) // 테스트 던전과 같은 규칙
        {
            return new DungeonGenerationConfig { SubDoorCount = subDoorCount }; // 기본값 + 서브문 수
        }

        [TestCase(0)] // 서브문 없는 외부 씬
        [TestCase(1)] // 서브문 1개
        [TestCase(2)] // 테스트 던전 (2개)
        [TestCase(3)] // 서브문 3개
        [TestCase(4)] // 설계 문서 최대 4개
        public void AllSeedsProduceValidLayouts(int subDoorCount) // 시드 1,000개 모두 규칙 통과
        {
            DungeonGenerationConfig config = CreateConfig(subDoorCount); // 규칙
            List<string> failures = new List<string>(); // 실패 기록

            for (int seed = 0; seed < SeedCount; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, seed * 7919 + subDoorCount); // 생성

                if (layout == null) // 생성 실패
                {
                    failures.Add($"seed {seed}: 생성 실패"); // 기록
                    continue; // 다음 시드
                }

                List<string> errors = DungeonLayoutValidator.Validate(layout, config); // 규칙 검사

                if (errors.Count > 0) // 규칙 위반
                {
                    failures.Add($"seed {seed}: {string.Join(" / ", errors)}"); // 기록
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.GetRange(0, System.Math.Min(10, failures.Count)))); // 실패 없음
        }

        [TestCase(0, 0)] // 1층만
        [TestCase(1, 0)] // 1~2층
        [TestCase(0, 2)] // 1층 + 지하 2층
        [TestCase(2, 2)] // 기본값 (3F ~ B2)
        [TestCase(3, 3)] // 7층
        public void AllRequestedFloorsAreBuiltAndValid(int floorsAbove, int floorsBelow) // 요청한 층이 모두 생성되고 규칙을 지킴
        {
            List<string> failures = new List<string>(); // 실패 기록

            for (int seed = 0; seed < 200; seed++) // 시드 순회
            {
                DungeonGenerationConfig config = new DungeonGenerationConfig { SubDoorCount = 2, FloorsAbove = floorsAbove, FloorsBelow = floorsBelow }; // 규칙
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 104729) + 5); // 생성

                if (layout == null) // 생성 실패
                {
                    failures.Add($"seed {seed}: 생성 실패"); // 기록
                    continue; // 다음 시드
                }

                List<string> errors = DungeonLayoutValidator.Validate(layout, config); // 규칙 검사

                if (errors.Count > 0) // 규칙 위반
                {
                    failures.Add($"seed {seed}: {string.Join(" / ", errors)}"); // 기록
                    continue; // 다음 시드
                }

                Assert.That(layout.MinFloor, Is.EqualTo(-floorsBelow), $"seed {seed} 최저 층"); // 지하층 수
                Assert.That(layout.MaxFloor, Is.EqualTo(floorsAbove), $"seed {seed} 최고 층"); // 위층 수
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.GetRange(0, System.Math.Min(10, failures.Count)))); // 실패 없음
        }

        [Test]
        public void EveryFloorPairIsLinkedByExactlyOneVerticalRoomChain() // 인접한 두 층은 세로형 방으로만 이어짐
        {
            DungeonGenerationConfig config = CreateConfig(2); // 기본 규칙 (3F ~ B2)

            for (int seed = 0; seed < 200; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 31337) + 11); // 생성
                Assert.That(layout, Is.Not.Null, $"seed {seed}"); // 생성 성공
                HashSet<int> linked = new HashSet<int>(); // 연결된 층 쌍 (아래층 번호)

                foreach (RoomNode room in layout.VerticalRooms) // 세로형 방 순회
                {
                    linked.Add(room.LowerFloor); // 층 쌍 기록
                    Assert.That(room.UpperFloor, Is.EqualTo(room.LowerFloor + 1), $"seed {seed} 세로형 방 {room.Id}"); // 바로 위층만 연결
                    Assert.That(room.Vertical, Is.Not.EqualTo(VerticalKind.None), $"seed {seed} 세로형 방 {room.Id}"); // 종류 지정
                    Assert.That(layout.DegreeOf(room.Id), Is.EqualTo(2), $"seed {seed} 세로형 방 {room.Id}"); // 문 2개
                    Assert.That(room.LowerWall, Is.Not.EqualTo(room.UpperWall), $"seed {seed} 세로형 방 {room.Id}"); // 아래·위 문 벽 다름
                    Assert.That(layout.HasDoorOnWall(room.Id, room.LowerWall, room.LowerFloor), Is.True, $"seed {seed} 세로형 방 {room.Id} 아래 문"); // 아래층 문
                    Assert.That(layout.HasDoorOnWall(room.Id, room.UpperWall, room.UpperFloor), Is.True, $"seed {seed} 세로형 방 {room.Id} 위 문"); // 위층 문
                }

                for (int lower = config.FloorsBelow * -1; lower < config.FloorsAbove; lower++) // 인접 층 쌍
                {
                    Assert.That(linked.Contains(lower), Is.True, $"seed {seed} {GridPoint.FloorName(lower)}↔{GridPoint.FloorName(lower + 1)} 세로형 방 없음"); // 연결 존재
                }

                foreach (DoorEdge door in layout.Doors) // 문 순회
                {
                    RoomNode a = layout.Room(door.A); // 방 A
                    RoomNode b = layout.Room(door.B); // 방 B

                    if (!a.IsVertical && !b.IsVertical) // 일반 방끼리
                    {
                        Assert.That(a.Cell.Floor, Is.EqualTo(b.Cell.Floor), $"seed {seed} 층이 다른 방 직결 {door.A}-{door.B}"); // 같은 층만
                    }
                }
            }
        }

        [TestCase(VerticalKind.Stairwell)] // 계단통만
        [TestCase(VerticalKind.Ladder)] // 사다리 방만
        [TestCase(VerticalKind.Shaft)] // 수직 통로만
        public void ForcedVerticalKindAppliesToEveryVerticalRoom(VerticalKind forced) // 테스트용 종류 고정이 모든 세로형 방에 적용
        {
            DungeonGenerationConfig config = new DungeonGenerationConfig { SubDoorCount = 2, ForcedVerticalKind = forced }; // 고정 규칙

            for (int seed = 0; seed < 120; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 7919) + 13); // 생성
                Assert.That(layout, Is.Not.Null, $"forced={forced} seed={seed}"); // 생성 성공
                Assert.That(DungeonLayoutValidator.Validate(layout, config), Is.Empty, $"forced={forced} seed={seed}"); // 규칙 통과
                int count = 0; // 세로형 방 수

                foreach (RoomNode room in layout.VerticalRooms) // 세로형 방 순회
                {
                    count++; // 집계
                    Assert.That(room.Vertical, Is.EqualTo(forced), $"forced={forced} seed={seed} room={room.Id}"); // 고정 종류
                }

                Assert.That(count, Is.GreaterThanOrEqualTo(4), $"forced={forced} seed={seed}"); // 기본 층 구성은 세로형 방 4개
            }
        }

        [Test]
        public void QuickStairwellLayoutHasExactlyOneStairwell() // 빠른 테스트 구성(1F + 2F)은 계단통 1개만 생성
        {
            DungeonGenerationConfig config = new DungeonGenerationConfig { SubDoorCount = 2, FloorsAbove = 1, FloorsBelow = 0, ForcedVerticalKind = VerticalKind.Stairwell }; // 빠른 구성

            for (int seed = 0; seed < 120; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 104729) + 21); // 생성
                Assert.That(layout, Is.Not.Null, $"seed={seed}"); // 생성 성공
                Assert.That(DungeonLayoutValidator.Validate(layout, config), Is.Empty, $"seed={seed}"); // 규칙 통과
                List<RoomNode> verticals = new List<RoomNode>(layout.VerticalRooms); // 세로형 방
                Assert.That(verticals.Count, Is.EqualTo(1), $"seed={seed}"); // 1개만
                Assert.That(verticals[0].Vertical, Is.EqualTo(VerticalKind.Stairwell), $"seed={seed}"); // 계단통
                Assert.That(layout.MaxFloor, Is.EqualTo(1)); // 2층까지
                Assert.That(layout.MinFloor, Is.EqualTo(0)); // 지하 없음
            }
        }

        [Test]
        public void BossRoomIsThreeByThreeOnDeepestFloorWithOneEntrance() // 보스방은 최심층 3x3 · 입구 1개
        {
            DungeonGenerationConfig config = CreateConfig(2); // 기본 규칙

            for (int seed = 0; seed < 150; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 9176) + 31); // 생성
                Assert.That(layout, Is.Not.Null, $"seed={seed}"); // 생성 성공
                Assert.That(layout.BossRoomId, Is.GreaterThanOrEqualTo(0), $"seed={seed} 보스방 없음"); // 보스방 존재
                RoomNode boss = layout.Room(layout.BossRoomId); // 보스방
                Assert.That(boss.Shape, Is.EqualTo(RoomShape.Boss), $"seed={seed}"); // 3x3 모양
                Assert.That(boss.Cells.Count, Is.EqualTo(9), $"seed={seed}"); // 9칸
                Assert.That(layout.DegreeOf(boss.Id), Is.EqualTo(1), $"seed={seed} 입구 수"); // 입구 1개
                Assert.That(boss.Cell.Floor, Is.EqualTo(-config.FloorsBelow), $"seed={seed} 최심층"); // 최심층
                Assert.That(boss.Kind, Is.Not.EqualTo(RoomKind.Corridor), $"seed={seed}"); // 복도 아님

                foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문 순회
                {
                    Assert.That(sub.RoomId, Is.Not.EqualTo(boss.Id), $"seed={seed} 보스방 서브문"); // 보스방에는 서브문 없음
                }
            }
        }

        [Test]
        public void SecretRoomsAreOnlyReachableThroughBreakableWalls() // 비밀방은 부술 수 있는 벽으로만 연결
        {
            DungeonGenerationConfig config = CreateConfig(2); // 기본 규칙

            for (int seed = 0; seed < 150; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 7717) + 5); // 생성
                Assert.That(layout, Is.Not.Null, $"seed={seed}"); // 생성 성공
                Assert.That(layout.SecretRoomIds.Count, Is.EqualTo(config.SecretRoomCount), $"seed={seed}"); // 개수

                foreach (int secretId in layout.SecretRoomIds) // 비밀방 순회
                {
                    RoomNode secret = layout.Room(secretId); // 비밀방
                    Assert.That(secret.Role, Is.EqualTo(RoomRole.Secret), $"seed={seed}"); // 역할
                    Assert.That(layout.DegreeOf(secretId), Is.EqualTo(1), $"seed={seed} 입구 수"); // 입구 1개

                    foreach (DoorEdge door in layout.DoorsOf(secretId)) // 연결 문
                    {
                        Assert.That(door.IsBreakable, Is.True, $"seed={seed} 부술 수 있는 벽 아님"); // 파괴 벽
                        Assert.That(door.IsLocked, Is.False, $"seed={seed} 잠긴 문과 중복"); // 잠금 아님
                    }
                }

                foreach (DoorEdge door in layout.Doors) // 파괴 벽은 비밀방 전용
                {
                    if (door.IsBreakable) // 파괴 벽
                    {
                        Assert.That(layout.Room(door.A).IsSecret || layout.Room(door.B).IsSecret, Is.True, $"seed={seed}"); // 비밀방 연결
                    }
                }
            }
        }

        [Test]
        public void MultiCellRoomsAreConnectedAndNeverOverlap() // 여러 칸 방은 칸이 이어지고 서로 겹치지 않음
        {
            DungeonGenerationConfig config = CreateConfig(2); // 기본 규칙
            int multiCellSeen = 0; // 다칸 방 수

            for (int seed = 0; seed < 150; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 3571) + 17); // 생성
                Assert.That(layout, Is.Not.Null, $"seed={seed}"); // 생성 성공
                HashSet<GridPoint> cells = new HashSet<GridPoint>(); // 전체 칸

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    Assert.That(room.Cells.Count, Is.GreaterThan(0), $"seed={seed} 방 {room.Id}"); // 칸 존재
                    Assert.That(room.Cells[0], Is.EqualTo(room.Cell), $"seed={seed} 방 {room.Id}"); // 기준 칸

                    if (!room.IsVertical) // 일반 방
                    {
                        Assert.That(room.Cells.Count, Is.EqualTo(RoomShapes.CellCount(room.Shape)), $"seed={seed} 방 {room.Id}"); // 모양과 칸 수
                        multiCellSeen += room.IsMultiCell ? 1 : 0; // 집계
                    }

                    foreach (GridPoint cell in room.Cells) // 칸 순회
                    {
                        Assert.That(cells.Add(cell), Is.True, $"seed={seed} 칸 겹침 {cell}"); // 중복 없음
                    }
                }

                foreach (DoorEdge door in layout.Doors) // 문 칸 정합
                {
                    Assert.That(layout.Room(door.A).OccupiesCell(door.CellA), Is.True, $"seed={seed} 문 {door.A}-{door.B}"); // A 칸
                    Assert.That(layout.Room(door.B).OccupiesCell(door.CellB), Is.True, $"seed={seed} 문 {door.A}-{door.B}"); // B 칸
                    Assert.That(System.Math.Abs(door.CellA.X - door.CellB.X) + System.Math.Abs(door.CellA.Y - door.CellB.Y), Is.EqualTo(1), $"seed={seed} 문 {door.A}-{door.B}"); // 맞닿은 칸
                }
            }

            Assert.That(multiCellSeen, Is.GreaterThan(100), "여러 칸 방이 거의 생성되지 않음"); // 실제로 다양한 모양이 나오는지
        }

        [Test]
        public void RoomCellsNeverOverlapAcrossFloors() // 세로형 방이 차지하는 두 칸을 포함해 칸이 겹치지 않음
        {
            DungeonGenerationConfig config = CreateConfig(2); // 기본 규칙

            for (int seed = 0; seed < 200; seed++) // 시드 순회
            {
                DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 65599) + 3); // 생성
                Assert.That(layout, Is.Not.Null, $"seed {seed}"); // 생성 성공
                HashSet<GridPoint> cells = new HashSet<GridPoint>(); // 칸 집합

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    Assert.That(cells.Add(room.Cell), Is.True, $"seed {seed} 칸 겹침 {room.Cell}"); // 아래층 칸
                    Assert.That(room.Cell.Floor, Is.EqualTo(room.LowerFloor)); // 기준 층 일치

                    if (room.IsVertical) // 세로형 방
                    {
                        Assert.That(cells.Add(room.Cell.Above), Is.True, $"seed {seed} 세로형 위층 칸 겹침 {room.Cell.Above}"); // 위층 칸
                    }
                }
            }
        }

        [Test]
        public void SubDoorsOnlyAppearOnMainFloor() // 서브문은 외부와 이어지는 1층에만 생성
        {
            for (int subDoorCount = 1; subDoorCount <= 4; subDoorCount++) // 서브문 수별
            {
                DungeonGenerationConfig config = CreateConfig(subDoorCount); // 규칙

                for (int seed = 0; seed < 120; seed++) // 시드
                {
                    DungeonLayout layout = DungeonLayoutGenerator.Generate(config, (seed * 7717) + subDoorCount); // 생성
                    Assert.That(layout, Is.Not.Null, $"subDoors={subDoorCount} seed={seed}"); // 생성 성공

                    foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문 순회
                    {
                        Assert.That(layout.Room(sub.RoomId).Cell.Floor, Is.EqualTo(0), $"subDoors={subDoorCount} seed={seed}"); // 1층만
                        Assert.That(layout.Room(sub.RoomId).IsVertical, Is.False, $"subDoors={subDoorCount} seed={seed}"); // 세로형 방 제외
                    }
                }
            }
        }

        [Test]
        public void SubDoorCountAlwaysMatchesExteriorAndMapsOneToOne() // 실내 서브문 수 = 외부 서브문 수, 번호 1:1, 방 겹침 없음
        {
            for (int subDoorCount = 0; subDoorCount <= 4; subDoorCount++) // 서브문 수별
            {
                DungeonGenerationConfig config = CreateConfig(subDoorCount); // 규칙

                for (int seed = 0; seed < 200; seed++) // 시드
                {
                    DungeonLayout layout = DungeonLayoutGenerator.Generate(config, seed); // 생성
                    Assert.That(layout, Is.Not.Null, $"subDoors={subDoorCount} seed={seed}"); // 성공
                    Assert.That(layout.SubDoors.Count, Is.EqualTo(subDoorCount)); // 개수 동일
                    HashSet<int> indices = new HashSet<int>(); // 번호
                    HashSet<int> rooms = new HashSet<int>(); // 방

                    foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문 순회
                    {
                        Assert.That(indices.Add(sub.Index), Is.True); // 번호 중복 없음
                        Assert.That(rooms.Add(sub.RoomId), Is.True); // 방 겹침 없음
                        Assert.That(sub.RoomId, Is.Not.EqualTo(layout.StartRoomId)); // 시작 방 아님
                        Assert.That(DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId).Cell, layout.Room(layout.StartRoomId).Cell), Is.GreaterThanOrEqualTo(2)); // 정문 방 옆 칸 아님

                        foreach (SubDoorPlacement other in layout.SubDoors) // 다른 서브문
                        {
                            if (other != sub) // 자기 자신 제외
                            {
                                Assert.That(DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId).Cell, layout.Room(other.RoomId).Cell), Is.GreaterThanOrEqualTo(2), $"subDoors={subDoorCount} seed={seed}"); // 옆 칸 겹침 없음
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void SameSeedProducesSameLayout() // 같은 시드(같은 날)는 같은 구조
        {
            DungeonGenerationConfig config = CreateConfig(2); // 테스트 던전 규칙

            for (int seed = 0; seed < 100; seed++) // 시드
            {
                string first = DungeonLayoutGenerator.Generate(config, seed).Signature(); // 첫 생성
                string second = DungeonLayoutGenerator.Generate(config, seed).Signature(); // 재생성
                Assert.That(second, Is.EqualTo(first), $"seed {seed}"); // 동일
            }
        }

        [Test]
        public void DifferentDaysProduceDifferentLayouts() // 다른 날은 대부분 다른 구조
        {
            DungeonGenerationConfig config = CreateConfig(2); // 테스트 던전 규칙
            HashSet<string> signatures = new HashSet<string>(); // 서명 모음

            for (int day = 1; day <= 30; day++) // 30일
            {
                signatures.Add(DungeonLayoutGenerator.Generate(config, DungeonSeed.For(12345, day, "02_TestDungeon")).Signature()); // 일차별 서명
            }

            Assert.That(signatures.Count, Is.GreaterThanOrEqualTo(28)); // 거의 모두 다름
        }

        [Test]
        public void SeedIsStableForSameCampaignDayRegion() // 시드 계산 결정성
        {
            Assert.That(DungeonSeed.For(777, 3, "02_TestDungeon"), Is.EqualTo(DungeonSeed.For(777, 3, "02_TestDungeon"))); // 동일
            Assert.That(DungeonSeed.For(777, 3, "02_TestDungeon"), Is.Not.EqualTo(DungeonSeed.For(777, 4, "02_TestDungeon"))); // 일차 다르면 다름
            Assert.That(DungeonSeed.For(777, 3, "02_TestDungeon"), Is.Not.EqualTo(DungeonSeed.For(777, 3, "03_OtherDungeon"))); // 지역 다르면 다름
        }
    }
}
