using System.Collections.Generic; // 목록 기능 참조
using NUnit.Framework; // 테스트 프레임워크 참조

namespace ProjectI.Generation.Tests // 절차적 던전 생성 테스트 네임스페이스
{
    public sealed class DungeonLayoutGeneratorTests // 27일차 실내 던전 그래프 생성 규칙 자동 검사
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
