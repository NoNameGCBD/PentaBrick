using System.Text;
using NUnit.Framework;
using PentaBrick.Core;

namespace PentaBrick.Tests
{
    /// <summary>테스트에서 함께 쓰는 도우미.</summary>
    internal static class TestUtil
    {
        public const int R = Colors.Red, O = Colors.Orange, Y = Colors.Yellow,
            G = Colors.Green, B = Colors.Blue, P = Colors.Purple, _ = Colors.Empty;

        /// <summary>"행/행/행" 한 줄 표기를 모형으로 바꾼다. 예: M("R . ./. . ./. . .")</summary>
        public static Model M(string rows, int height = 0) => Model.Parse(rows.Split('/'), height);

        /// <summary>평면도가 기대한 행들과 같은지 검사한다.</summary>
        public static void AssertView(int[,] actual, params int[][] expectedRows)
        {
            var expected = new int[expectedRows.Length, expectedRows[0].Length];
            for (int r = 0; r < expectedRows.Length; r++)
                for (int c = 0; c < expectedRows[r].Length; c++)
                    expected[r, c] = expectedRows[r][c];
            Assert.IsTrue(Projection.ViewsEqual(actual, expected),
                "기대값\n" + Format(expected) + "실제값\n" + Format(actual));
        }

        /// <summary>평면도의 한 행을 꺼낸다.</summary>
        public static int[] Row(int[,] view, int row)
        {
            var result = new int[view.GetLength(1)];
            for (int c = 0; c < result.Length; c++) result[c] = view[row, c];
            return result;
        }

        private static string Format(int[,] view)
        {
            var text = new StringBuilder();
            for (int r = 0; r < view.GetLength(0); r++)
            {
                for (int c = 0; c < view.GetLength(1); c++) text.Append(".ROYGBP"[view[r, c]]);
                text.Append('\n');
            }
            return text.ToString();
        }

        /// <summary>
        /// 전수 탐색으로 해가 있는지 확인한다(테스트 전용, 3×3 같은 작은 문제에만 사용).
        /// 상 평면도가 각 칸의 꼭대기 색을, 전·좌 평면도의 실루엣이 최대 높이를 정해 준다.
        /// </summary>
        public static bool HasSolution(Puzzle puzzle)
        {
            int n = puzzle.Size, h = puzzle.Height;
            var remaining = (int[])puzzle.Counts.Clone();
            var columns = new int[n * n][];
            for (int i = 0; i < columns.Length; i++) columns[i] = new int[0];
            var top = puzzle.Views[(int)ViewDir.Top];

            int Silhouette(int[,] view, int col)
            {
                for (int row = 0; row < h; row++) if (view[row, col] != Colors.Empty) return h - row;
                return 0;
            }

            bool Fill(int index)
            {
                if (index == n * n)
                {
                    foreach (int count in remaining) if (count != 0) return false;
                    return Judge.Check(Model.FromColumns(n, h, columns), puzzle).Correct;
                }
                int x = index % n, z = index / n;
                int topColor = top[z, x];
                if (topColor == Colors.Empty) return Fill(index + 1);
                int limit = System.Math.Min(
                    Silhouette(puzzle.Views[(int)ViewDir.Front], x),
                    Silhouette(puzzle.Views[(int)ViewDir.Left], z));

                // stack: 꼭대기 아래에 깔리는 블록들. 꼭대기를 얹어 보거나, 한 칸 더 깐다.
                bool Grow(int[] stack)
                {
                    if (stack.Length < limit && remaining[topColor] > 0)
                    {
                        remaining[topColor]--;
                        columns[index] = Append(stack, topColor);
                        if (Fill(index + 1)) return true;
                        remaining[topColor]++;
                    }
                    if (stack.Length + 1 < limit)
                    {
                        for (int color = 1; color <= Colors.Count; color++)
                        {
                            if (remaining[color] == 0) continue;
                            remaining[color]--;
                            if (Grow(Append(stack, color))) return true;
                            remaining[color]++;
                        }
                    }
                    return false;
                }

                bool found = Grow(new int[0]);
                if (!found) columns[index] = new int[0];
                return found;
            }

            return Fill(0);
        }

        private static int[] Append(int[] stack, int color)
        {
            var result = new int[stack.Length + 1];
            stack.CopyTo(result, 0);
            result[stack.Length] = color;
            return result;
        }
    }
}
