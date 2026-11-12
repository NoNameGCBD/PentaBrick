using NUnit.Framework;
using PentaBrick.Core;
using PentaBrick.Game;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class SoundCueTests
    {
        private GameState game;

        [SetUp]
        public void SetUp()
        {
            game = new GameState(() => 0);
            game.Start();
        }

        /// <summary>action 전후 상태로 고른 소리.</summary>
        private Sound After(System.Action action)
        {
            var before = SoundCue.Snapshot.Of(game);
            action();
            return SoundCue.Pick(before, game);
        }

        [Test]
        public void 놓기_지우기_칠하기()
        {
            game.SetColor(G);
            Assert.AreEqual(Sound.Place, After(() => game.Place(0, 0)));
            game.SetColor(R);
            Assert.AreEqual(Sound.Paint, After(() => game.Paint(0, 0, 0)));
            Assert.AreEqual(Sound.Erase, After(() => game.Erase(0, 0, 0)));
        }

        [Test]
        public void 되돌리기와_다시하기는_바뀐_방향의_소리()
        {
            game.Place(0, 0);
            Assert.AreEqual(Sound.Erase, After(game.Undo));
            Assert.AreEqual(Sound.Place, After(game.Redo));
            Assert.AreEqual(Sound.Erase, After(game.Reset));
        }

        [Test]
        public void 거부된_동작과_선택은_소리가_없다()
        {
            game.SetColor(B); // p1의 파랑은 1개
            game.Place(0, 0);
            Assert.AreEqual(Sound.None, After(() => game.Place(1, 1)));
            Assert.AreEqual(Sound.None, After(() => game.SetColor(R)));
            Assert.AreEqual(Sound.None, After(() => game.SetTool(Tool.Erase)));
        }

        [Test]
        public void 새_문제는_블록이_사라져도_소리가_없다()
        {
            game.Place(0, 0);
            Assert.AreEqual(Sound.None, After(game.NewPuzzle));
            game.Place(0, 0);
            Assert.AreEqual(Sound.None, After(() => game.Start()), "같은 샘플을 다시 시작해도");
        }

        [Test]
        public void 제출하면_정답_오답_정답보기는_소리가_없다()
        {
            Assert.AreEqual(Sound.Wrong, After(game.DeclareImpossible)); // p1은 풀 수 있는 문제
            Assert.AreEqual(Sound.None, After(game.ShowAnswer));
            Assert.AreEqual(Sound.None, After(game.NewPuzzle));

            var answer = game.Puzzle.Answer;
            for (int z = 0; z < answer.Size; z++)
                for (int x = 0; x < answer.Size; x++)
                    foreach (int color in answer.Column(x, z)) { game.SetColor(color); game.Place(x, z); }
            Assert.AreEqual(Sound.Correct, After(game.Submit));
        }
    }
}
