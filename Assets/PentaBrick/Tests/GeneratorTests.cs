using System;
using System.Linq;
using NUnit.Framework;
using PentaBrick.Core;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class GeneratorTests
    {
        [Test]
        public void 난수_생성기는_seed가_같으면_같은_수열을_낸다()
        {
            Mulberry32 a = new Mulberry32(42), b = new Mulberry32(42), c = new Mulberry32(43);
            double[] seqA = { a.Next(), a.Next(), a.Next() };
            double[] seqB = { b.Next(), b.Next(), b.Next() };
            double[] seqC = { c.Next(), c.Next(), c.Next() };
            CollectionAssert.AreEqual(seqA, seqB);
            CollectionAssert.AreNotEqual(seqA, seqC);
            Assert.IsTrue(seqA.All(v => v >= 0 && v < 1));
        }

        [Test]
        public void 난수_생성기는_웹_프로토타입과_같은_값을_낸다()
        {
            var rng = new Mulberry32(42);
            var values = new[] { rng.Next(), rng.Next(), rng.Next() }.Select(v => (uint)(v * 4294967296.0));
            CollectionAssert.AreEqual(new uint[] { 2581720956, 1925393290, 3661312704 }, values);
        }

        [Test]
        public void seed_코드는_왕복_변환되고_잘못된_코드는_거부된다()
        {
            foreach (uint seed in new uint[] { 0, 1, 2847193, 0xffffffff })
            {
                Assert.IsTrue(SeedCode.TryDecode(SeedCode.Encode(seed), out uint decoded));
                Assert.AreEqual(seed, decoded);
            }
            Assert.AreEqual("0", SeedCode.Encode(0));
            Assert.AreEqual("1p0wp", SeedCode.Encode(2847193));
            Assert.AreEqual("1z141z3", SeedCode.Encode(4294967295));

            Assert.IsFalse(Decodes("1z141z4"), "2^32");
            Assert.IsFalse(Decodes("zzzzzzzzz"));
            Assert.IsFalse(Decodes("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzz"), "64비트도 넘는 길이");
            Assert.IsFalse(Decodes("abc-"));
            Assert.IsFalse(Decodes("가나다"));
            Assert.IsFalse(Decodes(""));
            Assert.IsFalse(Decodes("   "));
            Assert.IsFalse(Decodes(null));
        }

        // TestUtil의 빈칸 상수 이름이 _ 라서 이 파일에서는 "out _"를 쓸 수 없다.
        private static bool Decodes(string code) => SeedCode.TryDecode(code, out uint ignored);

        [Test]
        public void seed_코드는_대문자와_앞뒤_공백을_허용한다()
        {
            Assert.IsTrue(SeedCode.TryDecode(" 1P0WP\n", out uint seed));
            Assert.AreEqual(2847193u, seed);
        }

        [Test]
        public void 같은_난이도와_seed면_같은_문제_seed가_다르면_다른_문제()
        {
            Puzzle a = Generator.GenerateSolvable(Difficulty.Normal, 7), b = Generator.GenerateSolvable(Difficulty.Normal, 7),
                c = Generator.GenerateSolvable(Difficulty.Normal, 8);
            Assert.IsTrue(a.Answer.SameAs(b.Answer));
            CollectionAssert.AreEqual(a.Counts, b.Counts);
            Assert.IsFalse(a.Answer.SameAs(c.Answer));
            Assert.AreEqual("normal-7", a.Id);
            Assert.AreEqual(Difficulty.Normal, a.Difficulty);
            Assert.AreEqual(7u, a.Seed);
        }

        // 웹 프로토타입(src/core/generator.js)이 같은 난이도·seed로 만든 정답 모형
        [TestCase(Difficulty.Easy, 1u, "OYR . ./YO . O/R OY .")]
        [TestCase(Difficulty.Easy, 2847193u, "B BB ./. BG BG/. BBY .")]
        [TestCase(Difficulty.Normal, 7u, ". . GB GG/BP B R R/. . B BB/. . . BPB")]
        [TestCase(Difficulty.Normal, 2847193u, "Y BB G B/. Y . ./. . . BB/Y R YR BB")]
        [TestCase(Difficulty.Hard, 0u, "BG P . BR PY/. R . G BY/Y Y GGR . ./GG PG . PP R/O . PYR . .")]
        [TestCase(Difficulty.Hard, 1u, "OR . R Y RB/. P . Y R/P . . YP ./OOR O OR BBP G/O OYB P O .")]
        [TestCase(Difficulty.Hard, 4294967295u, "GB . . R PB/P . . . Y/B Y G BO ./. O . BP ./. G OYG . OBR")]
        public void 웹_프로토타입과_같은_문제를_만든다(Difficulty difficulty, uint seed, string expected)
        {
            var puzzle = Generator.GenerateSolvable(difficulty, seed);
            Assert.IsTrue(puzzle.Answer.SameAs(M(expected, 3)), $"{difficulty} seed {seed}");
        }

        [Test]
        public void 난이도_설정은_명세서_표와_같다()
        {
            void Check(Difficulty d, string key, string label, int size, int colors, int min, int max, double fill)
            {
                var s = DifficultySettings.Get(d);
                Assert.AreEqual((key, label, size, 3, colors, min, max, fill),
                    (s.Key, s.Label, s.Size, s.Height, s.Colors, s.MinBlocks, s.MaxBlocks, s.Fill));
            }
            Check(Difficulty.Easy, "easy", "쉬움", 3, 3, 6, 10, 0.6);
            Check(Difficulty.Normal, "normal", "보통", 4, 4, 12, 20, 0.65);
            Check(Difficulty.Hard, "hard", "어려움", 5, 6, 22, 36, 0.65);
        }

        [Test]
        public void 생성된_문제는_모든_난이도에서_조건을_만족하고_정답이_판정을_통과한다()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                var options = DifficultySettings.Get(difficulty);
                for (uint seed = 1; seed <= 150; seed++)
                {
                    var puzzle = Generator.GenerateSolvable(difficulty, seed);
                    string where = $"{difficulty} seed {seed}";
                    Assert.IsTrue(puzzle.Solvable && puzzle.Answer != null, where);
                    Assert.AreEqual((options.Size, options.Height), (puzzle.Size, puzzle.Height), where);
                    int blocks = puzzle.Counts.Sum();
                    Assert.IsTrue(blocks >= options.MinBlocks && blocks <= options.MaxBlocks, $"{where}: 블록 {blocks}개");
                    Assert.AreEqual(options.Colors, puzzle.Counts.Count(count => count > 0), where);
                    bool tall = false;
                    for (int z = 0; z < puzzle.Size; z++)
                        for (int x = 0; x < puzzle.Size; x++)
                        {
                            int height = puzzle.Answer.Column(x, z).Count;
                            Assert.LessOrEqual(height, options.Height, where);
                            tall |= height >= 2;
                        }
                    Assert.IsTrue(tall, $"{where}: 한 층짜리");
                    Assert.IsTrue(Judge.Check(puzzle.Answer, puzzle).Correct, where);
                }
            }
        }

        [Test]
        public void 문제_코드를_난이도와_seed로_되돌린다()
        {
            var puzzle = Generator.Generate(Difficulty.Hard, 2847193);
            Assert.IsTrue(Generator.TryParseId(puzzle.Id, out Difficulty difficulty, out uint seed));
            Assert.AreEqual((Difficulty.Hard, 2847193u), (difficulty, seed));

            Assert.IsTrue(Generator.TryParseId("  Normal-1P0WP ", out difficulty, out seed));
            Assert.AreEqual((Difficulty.Normal, 2847193u), (difficulty, seed));

            foreach (string bad in new[] { null, "", "p1", "normal", "normal-", "-7", "impossible-7", "normal-7-7", "normal-zzzzzzzzz" })
                Assert.IsFalse(Generator.TryParseId(bad, out difficulty, out seed), bad);
        }

        [Test]
        public void 주소에서_문제를_읽는다()
        {
            Assert.IsTrue(Generator.TryParseUrl("https://example.com/pentabrick/?mode=normal&seed=1p0wp", out Difficulty difficulty, out uint seed));
            Assert.AreEqual((Difficulty.Normal, 2847193u), (difficulty, seed));
            Assert.IsTrue(Generator.TryParseUrl("https://example.com/?x=1&seed=7&mode=easy#top", out difficulty, out seed));
            Assert.AreEqual((Difficulty.Easy, 7u), (difficulty, seed));

            foreach (string bad in new[] { null, "", "https://example.com/", "https://example.com/?mode=sample", "https://example.com/?mode=easy", "https://example.com/?seed=7", "https://example.com/?mode=easy&seed=!!" })
                Assert.IsFalse(Generator.TryParseUrl(bad, out difficulty, out seed), bad);
        }

        [Test]
        public void 알_수_없는_난이도는_오류()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Generator.Generate((Difficulty)99, 1));
        }

        [Test]
        public void RandomSeed는_매번_다른_값을_낸다()
        {
            var seeds = Enumerable.Range(0, 20).Select(i => Generator.RandomSeed()).Distinct();
            Assert.Greater(seeds.Count(), 1);
        }
    }
}
