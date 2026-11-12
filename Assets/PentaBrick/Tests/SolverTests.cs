using System;
using System.Linq;
using NUnit.Framework;
using PentaBrick.Core;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class SolverTests
    {
        [Test]
        public void 샘플_문제의_가능_불가능을_모두_맞히고_찾은_해는_판정을_통과한다()
        {
            foreach (var puzzle in SamplePuzzles.All)
            {
                var result = Solver.Solve(puzzle, out Model solution);
                Assert.AreEqual(puzzle.Solvable ? SolveResult.Solvable : SolveResult.Unsolvable, result, puzzle.Id);
                if (puzzle.Solvable) Assert.IsTrue(Judge.Check(solution, puzzle).Correct, puzzle.Id);
                else Assert.IsNull(solution, puzzle.Id);
            }
        }

        [Test]
        public void 빈_문제는_빈_모형이_해다()
        {
            var puzzle = Puzzle.FromModel("t", Model.Empty(3, 3));
            Assert.AreEqual(SolveResult.Solvable, Solver.Solve(puzzle, out Model solution));
            Assert.AreEqual(0, solution.TotalBlocks);
        }

        [Test]
        public void 안_보이는_블록이_있어도_해를_찾는다()
        {
            // 3×3×3 정육면체: 가운데 기둥의 아래 두 칸은 어느 방향에서도 안 보인다.
            var puzzle = Puzzle.FromModel("t", M("RRR RRR RRR/RRR BGR RRR/RRR RRR RRR"));
            Assert.AreEqual(SolveResult.Solvable, Solver.Solve(puzzle, out Model solution));
            Assert.IsTrue(Judge.Check(solution, puzzle).Correct);
        }

        [Test]
        public void 탐색_한도를_넘으면_Unknown()
        {
            var puzzle = Generator.GenerateSolvable(Difficulty.Hard, 1);
            Assert.AreEqual(SolveResult.Unknown, Solver.Solve(puzzle, out Model _, nodeLimit: 3));
        }

        [Test]
        public void 생성된_가능한_문제는_모든_난이도에서_해를_찾는다()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
                for (uint seed = 1; seed <= 150; seed++)
                {
                    var puzzle = Generator.GenerateSolvable(difficulty, seed);
                    Assert.AreEqual(SolveResult.Solvable, Solver.Solve(puzzle, out Model solution), $"{difficulty} {seed}");
                    Assert.IsTrue(Judge.Check(solution, puzzle).Correct, $"{difficulty} {seed}");
                }
        }

        [Test]
        public void 쉬움_문제에서_솔버와_전수_탐색의_결론이_같다()
        {
            // 불가능 문제가 섞인 쉬움(3×3) 문제 400개를 느리지만 단순한 전수 탐색과 대조한다.
            int impossible = 0;
            for (uint seed = 1; seed <= 400; seed++)
            {
                var puzzle = Generator.Generate(Difficulty.Easy, seed);
                bool brute = HasSolution(puzzle);
                Assert.AreEqual(puzzle.Solvable, brute, $"seed {seed}: 문제 표시와 전수 탐색");
                Assert.AreEqual(brute ? SolveResult.Solvable : SolveResult.Unsolvable, Solver.Solve(puzzle, out Model _), $"seed {seed}");
                if (!brute) impossible++;
            }
            Assert.Greater(impossible, 20);
        }

        [Test]
        public void 생성_모드의_불가능_문제는_10에서_20퍼센트이고_이유가_있다()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                int impossible = 0;
                const int total = 400;
                for (uint seed = 1; seed <= total; seed++)
                {
                    var puzzle = Generator.Generate(difficulty, seed);
                    var same = Generator.Generate(difficulty, seed);
                    Assert.AreEqual(puzzle.Solvable, same.Solvable, "같은 seed면 같은 결과");
                    Assert.AreEqual(puzzle.Reason, same.Reason);
                    Assert.AreEqual((difficulty, seed), (puzzle.Difficulty.Value, puzzle.Seed.Value));
                    if (puzzle.Solvable)
                    {
                        Assert.IsTrue(puzzle.Answer.SameAs(Generator.GenerateSolvable(difficulty, seed).Answer));
                        continue;
                    }
                    impossible++;
                    Assert.IsNull(puzzle.Answer);
                    StringAssert.Contains("원래 풀 수 있는 문제", puzzle.Reason);
                    Assert.AreEqual(SolveResult.Unsolvable, Solver.Solve(puzzle, out Model _));
                    Assert.AreEqual(Generator.GenerateSolvable(difficulty, seed).Counts.Sum(), puzzle.Counts.Sum(), "총 블록 수는 그대로");
                }
                double ratio = impossible / (double)total;
                Assert.IsTrue(ratio >= 0.10 && ratio <= 0.20, $"{difficulty}: 불가능 {impossible}/{total}");
            }
        }
    }
}
