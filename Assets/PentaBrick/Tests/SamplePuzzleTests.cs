using System.Linq;
using NUnit.Framework;
using PentaBrick.Core;

namespace PentaBrick.Tests
{
    public class SamplePuzzleTests
    {
        [Test]
        public void 샘플은_웹_프로토타입과_같다()
        {
            // 번호, 크기, 블록 수, 가능 여부. 웹 프로토타입(src/core/puzzles.js)의 실제 값이다.
            // 명세서 4.5의 표에는 p3 9개, p4 16개, p6 20개로 적혀 있지만 실제 모형은 8, 18, 22개다.
            var expected = new[]
            {
                ("p1", 3, 7, true), ("p2", 3, 12, true), ("p3", 3, 8, false),
                ("p4", 4, 18, true), ("p5", 3, 6, false), ("p6", 4, 22, true),
            };
            var actual = SamplePuzzles.All
                .Select(p => (p.Id, p.Size, p.Counts.Sum(), p.Solvable)).ToArray();
            CollectionAssert.AreEqual(expected, actual);
            Assert.IsTrue(SamplePuzzles.All.All(p => p.Height == p.Size));
        }

        [Test]
        public void 가능한_샘플_문제는_모두_정답_모형이_판정을_통과한다()
        {
            foreach (var puzzle in SamplePuzzles.All.Where(p => p.Solvable))
                Assert.IsTrue(Judge.Check(puzzle.Answer, puzzle).Correct, $"{puzzle.Id}의 정답 모형이 통과하지 못함");
        }

        [Test]
        public void 전수_탐색_3x3_샘플_문제의_가능_불가능_표시가_실제와_같다()
        {
            foreach (var puzzle in SamplePuzzles.All.Where(p => p.Size == 3))
                Assert.AreEqual(puzzle.Solvable, TestUtil.HasSolution(puzzle), puzzle.Id);
        }

        [Test]
        public void 불가능_샘플에는_이유가_있고_정답_모형이_없다()
        {
            var impossible = SamplePuzzles.All.Where(p => !p.Solvable).ToList();
            Assert.AreEqual(2, impossible.Count);
            foreach (var puzzle in impossible)
            {
                Assert.IsFalse(string.IsNullOrEmpty(puzzle.Reason), puzzle.Id);
                Assert.IsNull(puzzle.Answer, puzzle.Id);
            }
        }
    }
}
