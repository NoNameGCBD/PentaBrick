using NUnit.Framework;
using PentaBrick.Core;
using PentaBrick.Game;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class TutorialTests
    {
        private GameState game;
        private Tutorial tutorial;

        [SetUp]
        public void SetUp()
        {
            game = new GameState(() => 0);
            game.Start();
            tutorial = new Tutorial();
            tutorial.Begin(game);
        }

        private void Build(Model answer)
        {
            for (int z = 0; z < answer.Size; z++)
                for (int x = 0; x < answer.Size; x++)
                    foreach (int color in answer.Column(x, z))
                    {
                        game.SetColor(color);
                        game.Place(x, z);
                    }
        }

        [Test]
        public void 튜토리얼_문제는_하나는_풀_수_있고_하나는_불가능하다()
        {
            Assert.IsTrue(Judge.Check(Tutorial.BuildPuzzle.Answer, Tutorial.BuildPuzzle).Correct);
            Assert.AreEqual(SolveResult.Solvable, Solver.Solve(Tutorial.BuildPuzzle, out Model _));
            Assert.IsFalse(Tutorial.ImpossiblePuzzle.Solvable);
            Assert.AreEqual(SolveResult.Unsolvable, Solver.Solve(Tutorial.ImpossiblePuzzle, out Model _));
        }

        [Test]
        public void 처음부터_끝까지_진행하고_점수는_바뀌지_않는다()
        {
            Assert.AreEqual(0, tutorial.Index);
            Assert.IsTrue(game.IsCustom);
            Assert.AreSame(Tutorial.BuildPuzzle, game.Puzzle);
            Assert.IsTrue(game.Unscored);

            tutorial.Next(game); // 목표 → 평면도 읽기
            tutorial.Next(game); // → 블록 놓기
            Assert.IsFalse(tutorial.Current.IsManual);
            tutorial.Next(game); // 행동이 필요한 단계는 "다음"으로 넘어가지 않는다
            Assert.AreEqual(2, tutorial.Index);
            Assert.IsFalse(tutorial.Update(game));

            game.SetColor(B);
            game.Place(0, 0);
            Assert.IsTrue(tutorial.Update(game), "블록을 놓으면 쌓기 단계로");
            game.Place(0, 0);
            Assert.IsTrue(tutorial.Update(game), "두 개를 쌓으면 다음 단계로");
            Assert.IsTrue(tutorial.Current.IsManual);
            tutorial.Next(game); // → 모형 완성하기

            game.Reset();
            Build(game.Puzzle.Answer);
            game.Submit();
            Assert.IsTrue(tutorial.Update(game));
            tutorial.Next(game); // 점수 → 불가능한 문제
            Assert.AreSame(Tutorial.ImpossiblePuzzle, game.Puzzle);

            game.DeclareImpossible();
            Assert.IsTrue(tutorial.Update(game));
            tutorial.Next(game); // → 준비 완료
            Assert.IsTrue(tutorial.IsLast);
            Assert.AreEqual(0, game.Score);
        }

        /// <summary>제목에 part가 들어간 단계까지 진행한다. 행동 단계는 조건을 채워서 넘긴다.</summary>
        private void GoTo(string part)
        {
            for (int guard = 0; !tutorial.Current.Title.Contains(part); guard++)
            {
                Assert.Less(guard, 20, part + " 단계를 찾지 못함");
                if (tutorial.Current.IsManual) { tutorial.Next(game); continue; }
                game.SetColor(B);
                game.Place(0, 0); // 놓기
                tutorial.Update(game);
                game.Place(0, 0); // 쌓기
                tutorial.Update(game);
            }
        }

        [Test]
        public void 틀리게_제출하면_다시_하기로_같은_문제를_새로_연다()
        {
            GoTo("완성");
            game.Reset();
            // 정답(BB R / . G)과 같은 블록을 엉뚱한 자리에 쌓는다.
            game.SetColor(B); game.Place(1, 1); game.Place(1, 1);
            game.SetColor(R); game.Place(0, 0);
            game.SetColor(G); game.Place(1, 0);
            game.Submit();
            Assert.AreEqual(Phase.Result, game.Phase);
            Assert.IsFalse(game.Result.Correct);
            Assert.IsTrue(tutorial.NeedsRetry(game));
            Assert.IsFalse(tutorial.Update(game));

            tutorial.Retry(game);
            Assert.AreEqual(Phase.Playing, game.Phase);
            Assert.AreEqual(0, game.UsedTotal);
            Assert.AreSame(Tutorial.BuildPuzzle, game.Puzzle);
            Assert.IsFalse(tutorial.NeedsRetry(game));
        }

        [Test]
        public void 튜토리얼_문제가_아닌_문제로_바뀌면_Owns가_false()
        {
            Assert.IsTrue(tutorial.Owns(game.Puzzle));
            game.NewPuzzle();
            Assert.IsFalse(tutorial.Owns(game.Puzzle));
            Assert.IsFalse(game.IsCustom);
        }
    }
}
