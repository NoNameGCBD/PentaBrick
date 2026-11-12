using System;
using NUnit.Framework;
using PentaBrick.Core;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class ModelTests
    {
        [Test]
        public void Place는_칸의_맨_위에_쌓고_원본을_바꾸지_않는다()
        {
            var empty = Model.Empty(3, 3);
            var one = empty.Place(1, 2, R);
            var two = one.Place(1, 2, B);
            CollectionAssert.AreEqual(new[] { R, B }, two.Column(1, 2));
            CollectionAssert.AreEqual(new[] { R }, one.Column(1, 2));
            Assert.AreEqual(0, empty.TotalBlocks);
        }

        [Test]
        public void Place는_최대_높이와_격자_밖과_잘못된_색을_거부한다()
        {
            var model = Model.Empty(2, 2).Place(0, 0, R).Place(0, 0, R);
            Assert.IsNull(model.Place(0, 0, R));
            Assert.IsNull(model.Place(2, 0, R));
            Assert.IsNull(model.Place(0, -1, R));
            Assert.IsNull(model.Place(1, 1, 7));
            Assert.IsNull(model.Place(1, 1, 0));
        }

        [Test]
        public void Remove는_위_블록을_아래로_내린다()
        {
            var model = M("RGB ./. .", 3);
            CollectionAssert.AreEqual(new[] { G, B }, model.Remove(0, 0, 0).Column(0, 0));
            CollectionAssert.AreEqual(new[] { R, B }, model.Remove(0, 1, 0).Column(0, 0));
            Assert.IsNull(model.Remove(0, 3, 0));
            Assert.IsNull(model.Remove(1, 0, 0));
            CollectionAssert.AreEqual(new[] { R, G, B }, model.Column(0, 0), "원본이 바뀌면 안 됨");
        }

        [Test]
        public void Paint는_한_블록의_색만_바꾼다()
        {
            var model = M("RGB ./. .", 3);
            CollectionAssert.AreEqual(new[] { R, Y, B }, model.Paint(0, 1, 0, Y).Column(0, 0));
            Assert.IsNull(model.Paint(0, 1, 0, G), "이미 같은 색");
            Assert.IsNull(model.Paint(1, 0, 0, G), "블록 없음");
            Assert.IsNull(model.Paint(0, 1, 0, 9), "잘못된 색");
        }

        [Test]
        public void CountColors는_색별_개수를_센다()
        {
            var model = M("RR B/P RG");
            CollectionAssert.AreEqual(new[] { 0, 3, 0, 0, 1, 1, 1 }, model.CountColors());
            Assert.AreEqual(6, model.TotalBlocks);
        }

        [Test]
        public void 어떤_순서로_편집해도_공중에_뜬_블록이_생기지_않는다()
        {
            var model = Model.Empty(3, 3);
            long seed = 12345;
            int Rand(int n)
            {
                seed = (seed * 1103515245 + 12345) % 2147483648;
                return (int)(seed % n);
            }
            for (int step = 0; step < 500; step++)
            {
                int x = Rand(3), z = Rand(3);
                var next = Rand(3) == 0
                    ? model.Remove(x, Rand(3), z)
                    : model.Place(x, z, 1 + Rand(Colors.Count));
                if (next != null) model = next;
                for (int cx = 0; cx < 3; cx++)
                    for (int cz = 0; cz < 3; cz++)
                    {
                        int height = model.Column(cx, cz).Count;
                        for (int y = 0; y < 3; y++)
                            Assert.AreEqual(y < height, model.CellAt(cx, y, cz) != Colors.Empty, "빈칸 위에 블록이 있음");
                    }
            }
        }

        [Test]
        public void 격자_밖_좌표는_예외_없이_빈칸이나_null로_처리한다()
        {
            var model = M("R ./. .");
            Assert.AreEqual(Colors.Empty, model.CellAt(-1, 0, 0));
            Assert.AreEqual(Colors.Empty, model.CellAt(0, -1, 0));
            Assert.AreEqual(Colors.Empty, model.CellAt(0, 5, 0));
            Assert.AreEqual(Colors.Empty, model.CellAt(0, 0, 2));
            Assert.IsNull(model.Remove(-1, 0, 0));
            Assert.IsNull(model.Remove(0, -1, 0));
            Assert.IsNull(model.Paint(5, 0, 5, G));
            Assert.IsFalse(model.InBounds(2, 0));
        }

        [Test]
        public void Parse는_잘못된_문자열에_이유를_담은_예외를_던진다()
        {
            StringAssert.Contains("칸 수", Assert.Throws<FormatException>(() => M("R ./.")).Message);
            StringAssert.Contains("X", Assert.Throws<FormatException>(() => M("X ./. .")).Message);
            StringAssert.Contains("최대 높이", Assert.Throws<FormatException>(() => M("RRR ./. .")).Message);
        }

        [Test]
        public void SameAs는_내용이_같은_모형을_같다고_본다()
        {
            Assert.IsTrue(M("RG ./. B").SameAs(Model.Empty(2, 2).Place(0, 0, R).Place(1, 1, B).Place(0, 0, G)));
            Assert.IsFalse(M("RG ./. B").SameAs(M("GR ./. B")));
            Assert.IsFalse(M("R ./. .").SameAs(M("R ./. .", 3)));
        }

        [Test]
        public void FromColumns는_받은_배열을_복사한다()
        {
            var columns = new[] { new[] { R }, new int[0], new int[0], new int[0] };
            var model = Model.FromColumns(2, 2, columns);
            columns[0][0] = B;
            Assert.AreEqual(R, model.CellAt(0, 0, 0));
        }
    }
}
