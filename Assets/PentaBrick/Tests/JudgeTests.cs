using System.Linq;
using NUnit.Framework;
using PentaBrick.Core;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class JudgeTests
    {
        // 3×3×3 정육면체. 가운데 기둥의 아래 두 칸은 어느 방향에서도 보이지 않는다.
        private static Model Cube(string center) => M($"RRR RRR RRR/RRR {center}R RRR/RRR RRR RRR");

        [Test]
        public void 정답_모형은_정답으로_판정된다()
        {
            var answer = M("GG . ./G R ./B R Y");
            var result = Judge.Check(answer, Puzzle.FromModel("t", answer));
            Assert.IsTrue(result.Correct && result.CountsMatch);
            Assert.IsTrue(result.ViewMatches.All(match => match));
            Assert.AreEqual(0, result.Delta);
        }

        [Test]
        public void 평면도와_블록_수가_같으면_원본과_달라도_정답이다()
        {
            var puzzle = Puzzle.FromModel("t", Cube("BG"));
            var other = Cube("GB");
            Assert.IsFalse(other.SameAs(puzzle.Answer));
            Assert.IsTrue(Judge.Check(other, puzzle).Correct);
        }

        [Test]
        public void 평면도가_같아도_블록_수가_다르면_오답이다()
        {
            var puzzle = Puzzle.FromModel("t", Cube("BG"));
            var result = Judge.Check(Cube("BB"), puzzle);
            Assert.IsTrue(result.ViewMatches.All(match => match), "평면도는 모두 일치해야 함");
            Assert.IsFalse(result.CountsMatch);
            Assert.IsFalse(result.Correct);
        }

        [Test]
        public void 평면도가_다르면_어느_평면도가_틀렸는지_알려준다()
        {
            var puzzle = Puzzle.FromModel("t", M("R ./. ."));
            var result = Judge.Check(M(". R/. ."), puzzle);
            Assert.IsFalse(result.Correct);
            Assert.IsTrue(result.CountsMatch);
            // 순서: Top, Front, Back, Left, Right
            CollectionAssert.AreEqual(new[] { false, false, false, true, true }, result.ViewMatches);
        }

        [Test]
        public void 점수_제출은_1점_불가능은_2점()
        {
            var solvable = SamplePuzzles.All.First(p => p.Solvable);
            var impossible = SamplePuzzles.All.First(p => !p.Solvable);
            Assert.AreEqual(1, Judge.ScoreSubmit(solvable.Answer, solvable).Delta);
            Assert.AreEqual(-1, Judge.ScoreSubmit(Model.Empty(solvable.Size, solvable.Height), solvable).Delta);
            Assert.AreEqual(2, Judge.ScoreImpossible(impossible).Delta);
            Assert.IsTrue(Judge.ScoreImpossible(impossible).Correct);
            Assert.AreEqual(-2, Judge.ScoreImpossible(solvable).Delta);
            Assert.IsFalse(Judge.ScoreImpossible(solvable).Correct);
        }

        [Test]
        public void 문제와_크기가_다른_모형은_예외_없이_오답이다()
        {
            var puzzle = Puzzle.FromModel("t", M("R . ./. . ./. . ."));
            var result = Judge.ScoreSubmit(M("R ./. ."), puzzle);
            Assert.IsFalse(result.Correct);
            Assert.AreEqual(-1, result.Delta);
        }

        [Test]
        public void FromModel은_평면도와_블록_수를_채운다()
        {
            var puzzle = Puzzle.FromModel("t", M("RR B/P RG", 3));
            Assert.AreEqual("t", puzzle.Id);
            Assert.AreEqual(2, puzzle.Size);
            Assert.AreEqual(3, puzzle.Height);
            Assert.IsTrue(puzzle.Solvable);
            Assert.IsNull(puzzle.Reason);
            Assert.IsNull(puzzle.Difficulty);
            Assert.IsNull(puzzle.Seed);
            CollectionAssert.AreEqual(new[] { 0, 3, 0, 0, 1, 1, 1 }, puzzle.Counts);
            Assert.AreEqual(5, puzzle.Views.Length);
        }
    }
}
