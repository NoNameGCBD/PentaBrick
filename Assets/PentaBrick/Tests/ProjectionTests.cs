using NUnit.Framework;
using PentaBrick.Core;
using static PentaBrick.Tests.TestUtil;

namespace PentaBrick.Tests
{
    public class ProjectionTests
    {
        private static int[] Rw(params int[] cells) => cells;

        [Test]
        public void 빈_모형의_평면도는_모두_비어_있다()
        {
            var views = Projection.ProjectAll(Model.Empty(3, 3));
            Assert.AreEqual(5, views.Length);
            foreach (var view in views)
            {
                Assert.AreEqual(3, view.GetLength(0));
                Assert.AreEqual(3, view.GetLength(1));
                foreach (int cell in view) Assert.AreEqual(Colors.Empty, cell);
            }
        }

        [Test]
        public void 방향_확인_뒤_왼쪽_구석의_블록_하나()
        {
            var model = M("R . ./. . ./. . .");
            AssertView(Projection.Project(model, ViewDir.Top), Rw(R, _, _), Rw(_, _, _), Rw(_, _, _));
            AssertView(Projection.Project(model, ViewDir.Front), Rw(_, _, _), Rw(_, _, _), Rw(R, _, _));
            AssertView(Projection.Project(model, ViewDir.Back), Rw(_, _, _), Rw(_, _, _), Rw(_, _, R));
            AssertView(Projection.Project(model, ViewDir.Left), Rw(_, _, _), Rw(_, _, _), Rw(R, _, _));
            AssertView(Projection.Project(model, ViewDir.Right), Rw(_, _, _), Rw(_, _, _), Rw(_, _, R));
        }

        [Test]
        public void 방향_확인_앞_오른쪽_구석의_블록_하나()
        {
            var model = M(". . ./. . ./. . G");
            AssertView(Projection.Project(model, ViewDir.Top), Rw(_, _, _), Rw(_, _, _), Rw(_, _, G));
            CollectionAssert.AreEqual(Rw(_, _, G), Row(Projection.Project(model, ViewDir.Front), 2));
            CollectionAssert.AreEqual(Rw(G, _, _), Row(Projection.Project(model, ViewDir.Back), 2));
            CollectionAssert.AreEqual(Rw(_, _, G), Row(Projection.Project(model, ViewDir.Left), 2));
            CollectionAssert.AreEqual(Rw(G, _, _), Row(Projection.Project(model, ViewDir.Right), 2));
        }

        [Test]
        public void 전후_가까운_블록이_먼_블록을_가린다()
        {
            // x=1 열: 뒤(z=0)에 RR, 앞(z=2)에 B
            var model = M(". RR ./. . ./. B .");
            AssertView(Projection.Project(model, ViewDir.Front), Rw(_, _, _), Rw(_, R, _), Rw(_, B, _));
            AssertView(Projection.Project(model, ViewDir.Back), Rw(_, _, _), Rw(_, R, _), Rw(_, R, _));
        }

        [Test]
        public void 좌우_가까운_블록이_먼_블록을_가린다()
        {
            // z=1 행: 왼쪽(x=0)에 Y, 오른쪽(x=2)에 PP
            var model = M(". . ./Y . PP/. . .");
            AssertView(Projection.Project(model, ViewDir.Left), Rw(_, _, _), Rw(_, P, _), Rw(_, Y, _));
            AssertView(Projection.Project(model, ViewDir.Right), Rw(_, _, _), Rw(_, P, _), Rw(_, P, _));
        }

        [Test]
        public void 상_각_칸의_맨_위_블록_색이_보인다()
        {
            var model = M("RB G ./. OYP ./B . R");
            AssertView(Projection.Project(model, ViewDir.Top), Rw(B, G, _), Rw(_, P, _), Rw(B, _, R));
        }

        [Test]
        public void 가로와_높이가_다른_격자도_처리한다()
        {
            var model = M("RGB ./. Y", 4);
            AssertView(Projection.Project(model, ViewDir.Front), Rw(_, _), Rw(B, _), Rw(G, _), Rw(R, Y));
            AssertView(Projection.Project(model, ViewDir.Right), Rw(_, _), Rw(_, B), Rw(_, G), Rw(Y, R));
        }

        [Test]
        public void ViewsEqual은_크기가_다르면_false()
        {
            Assert.IsFalse(Projection.ViewsEqual(new int[2, 3], new int[3, 2]));
            Assert.IsTrue(Projection.ViewsEqual(new int[2, 3], new int[2, 3]));
        }
    }
}
