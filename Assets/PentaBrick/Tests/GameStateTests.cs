using NUnit.Framework;
using PentaBrick.Core;
using PentaBrick.Game;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class GameStateTests
    {
        private double clock;
        private GameState game;

        [SetUp]
        public void SetUp()
        {
            clock = 1000;
            game = new GameState(() => clock);
            game.Start();
        }

        /// <summary>정답 모형과 똑같이 블록을 놓는다.</summary>
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
        public void 시작하면_샘플_1번이_빈_모형으로_나온다()
        {
            Assert.IsNull(game.Mode);
            Assert.AreEqual("p1", game.Puzzle.Id);
            Assert.AreEqual(0, game.Model.TotalBlocks);
            Assert.AreEqual(Phase.Playing, game.Phase);
            Assert.AreEqual(Tool.Place, game.Tool);
            Assert.AreEqual(R, game.Color, "개수가 있는 첫 색");
            Assert.AreEqual(7, game.RequiredTotal);
            Assert.IsFalse(game.ShowMine, "연습용 표시는 기본으로 꺼져 있다(켜면 점수 미반영)");
        }

        [Test]
        public void ResetTimer는_풀이_중에만_시간을_다시_잰다()
        {
            clock += 3000;
            game.ResetTimer();
            Assert.AreEqual(0, game.ElapsedMs);
            clock += 1000;
            game.DeclareImpossible();
            game.ResetTimer(); // 결과 화면: 멈춘 시간 그대로
            Assert.AreEqual(1000, game.ElapsedMs);
        }

        [Test]
        public void 놓기는_남은_개수를_줄이고_다_쓰면_거부한다()
        {
            game.SetColor(B); // p1의 파랑은 1개
            Assert.AreEqual(1, game.Remaining(B));
            game.Place(0, 0);
            Assert.AreEqual(0, game.Remaining(B));
            game.Place(1, 1);
            Assert.AreEqual(1, game.UsedTotal);
        }

        [Test]
        public void 칠하기와_지우기()
        {
            game.SetColor(G);
            game.Place(0, 0);
            game.Place(0, 0);
            game.SetColor(R);
            game.Paint(0, 0, 0);
            CollectionAssert.AreEqual(new[] { R, G }, game.Model.Column(0, 0));
            game.Erase(0, 0, 0);
            CollectionAssert.AreEqual(new[] { G }, game.Model.Column(0, 0));

            game.SetColor(B);
            game.Place(1, 1);
            game.Paint(0, 0, 0); // 파랑은 이미 다 썼다
            CollectionAssert.AreEqual(new[] { G }, game.Model.Column(0, 0));
        }

        [Test]
        public void 문제에_없는_색은_선택할_수_없다()
        {
            game.SetColor(P); // p1에는 보라가 없다
            Assert.AreEqual(R, game.Color);
            game.SetColor(99);
            game.SetColor(-1);
            Assert.AreEqual(R, game.Color);
        }

        [Test]
        public void 실행_취소와_다시_실행()
        {
            game.Undo(); // 기록이 없으면 아무 일도 없다
            game.Place(0, 0);
            game.Place(1, 0);
            game.Undo();
            Assert.AreEqual(1, game.UsedTotal);
            Assert.IsTrue(game.CanUndo && game.CanRedo);
            game.Redo();
            Assert.AreEqual(2, game.UsedTotal);
            game.Undo();
            game.Place(2, 2); // 새 편집을 하면 다시 실행 기록은 사라진다
            Assert.IsFalse(game.CanRedo);

            game.Reset();
            Assert.AreEqual(0, game.UsedTotal);
            game.Undo(); // 초기화도 되돌릴 수 있다
            Assert.AreEqual(2, game.UsedTotal);
        }

        [Test]
        public void 블록을_다_써야_제출할_수_있고_정답이면_1점()
        {
            game.Place(0, 0);
            Assert.IsFalse(game.CanSubmit);
            game.Submit();
            Assert.AreEqual(Phase.Playing, game.Phase);
            game.Undo();

            Build(game.Puzzle.Answer);
            clock += 5000;
            Assert.IsTrue(game.CanSubmit);
            game.Submit();
            Assert.AreEqual(Phase.Result, game.Phase);
            Assert.IsTrue(game.Result.Correct);
            Assert.IsFalse(game.DeclaredImpossible);
            Assert.AreEqual(1, game.Score);

            clock += 9999; // 결과 화면에서는 시간이 멈춘다
            Assert.AreEqual(5000, game.ElapsedMs);
        }

        [Test]
        public void 결과_화면에서는_편집과_재제출이_잠긴다()
        {
            game.DeclareImpossible();
            Assert.AreEqual(-2, game.Score);
            game.Place(0, 0);
            game.Undo();
            game.Reset();
            game.Submit();
            game.DeclareImpossible();
            Assert.AreEqual(0, game.UsedTotal);
            Assert.AreEqual(-2, game.Score);
        }

        [Test]
        public void 틀린_모형을_제출하면_감점()
        {
            Build(game.Puzzle.Answer);
            game.SetColor(Y);
            game.Erase(2, 0, 2);
            game.Place(2, 0); // 노랑을 다른 칸으로
            game.Submit();
            Assert.IsFalse(game.Result.Correct);
            Assert.IsTrue(game.Result.CountsMatch);
            Assert.AreEqual(-1, game.Score);
        }

        [Test]
        public void 불가능_선언과_정답_모형_보기()
        {
            game.ShowAnswer(); // 풀이 중에는 볼 수 없다
            Assert.IsFalse(game.ShowingAnswer);

            game.DeclareImpossible(); // p1은 가능한 문제
            Assert.IsTrue(game.DeclaredImpossible);
            Assert.IsFalse(game.Result.Correct);
            game.ShowAnswer();
            Assert.IsTrue(game.ShowingAnswer);
            Assert.IsTrue(game.Model.SameAs(game.Puzzle.Answer));

            game.NewPuzzle();
            game.NewPuzzle(); // p3: 불가능한 문제
            Assert.AreEqual("p3", game.Puzzle.Id);
            Assert.IsFalse(game.ShowingAnswer);
            game.DeclareImpossible();
            Assert.IsTrue(game.Result.Correct);
            Assert.AreEqual(0, game.Score); // -2 + 2
            game.ShowAnswer(); // 정답이 없다
            Assert.IsFalse(game.ShowingAnswer);
        }

        [Test]
        public void 샘플은_마지막_다음에_처음으로_돌아간다()
        {
            for (int i = 0; i < SamplePuzzles.All.Count; i++) game.NewPuzzle();
            Assert.AreEqual("p1", game.Puzzle.Id);
            Assert.AreEqual(0, game.PuzzleIndex);
        }

        [Test]
        public void 모드를_바꿔도_점수는_유지되고_Start는_점수를_지운다()
        {
            game.DeclareImpossible();
            game.SetMode(Difficulty.Easy);
            Assert.AreEqual(Difficulty.Easy, game.Mode);
            Assert.AreEqual(Difficulty.Easy, game.Puzzle.Difficulty);
            Assert.AreEqual(Phase.Playing, game.Phase);
            Assert.AreEqual(-2, game.Score);

            game.NewPuzzle();
            Assert.AreEqual(Difficulty.Easy, game.Puzzle.Difficulty);
            game.SetMode(null);
            Assert.AreEqual("p1", game.Puzzle.Id);

            game.Start(Difficulty.Normal, 7);
            Assert.AreEqual("normal-7", game.Puzzle.Id);
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void 문제_코드로_열면_그_문제가_나오고_점수는_유지된다()
        {
            game.DeclareImpossible();
            game.OpenPuzzle(Difficulty.Hard, 1);
            Assert.AreEqual("hard-1", game.Puzzle.Id);
            Assert.AreEqual(Difficulty.Hard, game.Mode);
            Assert.AreEqual(Phase.Playing, game.Phase);
            Assert.AreEqual(-2, game.Score);
            game.NewPuzzle(); // 그 뒤의 새 문제도 같은 난이도
            Assert.AreEqual(Difficulty.Hard, game.Puzzle.Difficulty);
        }

        /// <summary>풀 수 있는 생성 문제를 연다(불가능 문제가 섞여 있어 seed를 골라야 한다).</summary>
        private void OpenSolvable(Difficulty difficulty)
        {
            for (uint seed = 1; ; seed++)
                if (Generator.Generate(difficulty, seed).Solvable)
                {
                    game.OpenPuzzle(difficulty, seed);
                    return;
                }
        }

        [Test]
        public void 기준_시간_안에_맞히면_시간_보너스_1점()
        {
            OpenSolvable(Difficulty.Easy);
            Assert.AreEqual(60, DifficultySettings.Get(Difficulty.Easy).BonusSeconds);
            Build(game.Puzzle.Answer);
            clock += 59_000;
            game.Submit();
            Assert.AreEqual(1, game.TimeBonus);
            Assert.AreEqual(2, game.Score);
            Assert.AreEqual(2, game.LastDelta);
        }

        [Test]
        public void 기준_시간을_넘기면_보너스가_없다()
        {
            OpenSolvable(Difficulty.Normal);
            Assert.AreEqual(180, DifficultySettings.Get(Difficulty.Normal).BonusSeconds);
            Build(game.Puzzle.Answer);
            clock += 180_001;
            game.Submit();
            Assert.AreEqual(0, game.TimeBonus);
            Assert.AreEqual(1, game.Score);
        }

        [Test]
        public void 오답_불가능_선언_샘플에는_시간_보너스가_없다()
        {
            Assert.AreEqual(360, DifficultySettings.Get(Difficulty.Hard).BonusSeconds);
            Build(game.Puzzle.Answer); // 샘플 p1, 바로 제출
            game.Submit();
            Assert.AreEqual(0, game.TimeBonus, "샘플(연습)");
            Assert.AreEqual(1, game.Score);

            OpenSolvable(Difficulty.Easy);
            game.DeclareImpossible(); // 가능한 문제를 불가능 선언
            Assert.AreEqual(0, game.TimeBonus);
            Assert.AreEqual(-1, game.Score);

            OpenSolvable(Difficulty.Easy);
            var answer = game.Puzzle.Answer;
            for (int z = 0; z < answer.Size; z++) // 같은 수의 블록을 모두 한 색 순서로 엉뚱하게 쌓는다
                for (int x = 0; x < answer.Size; x++)
                    foreach (int color in answer.Column(x, z))
                    {
                        game.SetColor(color);
                        game.Place(answer.Size - 1 - x, z);
                    }
            game.Submit();
            if (!game.Result.Correct) Assert.AreEqual(0, game.TimeBonus, "오답");
        }

        [Test]
        public void 평면도_표시는_연습_모드에서만_켜지고_켠_문제는_점수에_반영하지_않는다()
        {
            // 연습(샘플) 모드: 켜지고, 한 번이라도 켠 문제는 점수 미반영
            game.SetShowMine(true);
            Assert.IsTrue(game.ShowMine);
            game.SetShowMine(false); // 다시 꺼도 이 문제는 미반영
            Assert.IsTrue(game.Unscored);
            Build(game.Puzzle.Answer);
            game.Submit();
            Assert.IsTrue(game.Result.Correct);
            Assert.AreEqual(0, game.LastDelta);
            Assert.AreEqual(0, game.Score);

            // 난이도 모드: 설정을 켜도 표시되지 않고 점수는 정상 반영
            game.SetShowMine(true);
            OpenSolvable(Difficulty.Easy);
            Assert.IsFalse(game.ShowMine);
            Assert.IsFalse(game.Unscored);
            game.SetShowMine(true);
            Assert.IsFalse(game.ShowMine);
            Assert.IsFalse(game.Unscored);
            game.DeclareImpossible();
            Assert.AreEqual(-2, game.Score);

            // 연습 모드로 돌아오면 켜 둔 설정이 다시 적용된다
            game.SetMode(null);
            Assert.IsTrue(game.ShowMine);
            Assert.IsTrue(game.Unscored);
        }

        [Test]
        public void 점수를_초기화할_수_있고_다시_시작하면_0점이다()
        {
            game.DeclareImpossible();
            Assert.AreEqual(-2, game.Score);
            game.ResetScore();
            Assert.AreEqual(0, game.Score);
            game.NewPuzzle();
            game.DeclareImpossible();
            game.Start();
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void 상태가_바뀌면_Changed가_불린다()
        {
            int calls = 0;
            game.Changed += () => calls++;
            game.Place(0, 0);
            game.SetTool(Tool.Erase);
            game.SetShowMine(true);
            Assert.AreEqual(3, calls);
            game.Erase(2, 2, 2); // 블록이 없어 아무 일도 없다
            Assert.AreEqual(3, calls);
        }
    }
}
