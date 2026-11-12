using System.Collections.Generic;

namespace PentaBrick.Core
{
    public enum SolveResult
    {
        Solvable,
        Unsolvable,
        /// <summary>탐색 한도 안에 결론을 내지 못함.</summary>
        Unknown,
    }

    /// <summary>
    /// 문제에 해가 있는지 탐색한다. 불가능 문제를 자동으로 만들 때 "정말 해가 없음"을 증명하는 데 쓴다.
    ///
    /// 원리: 칸마다 기둥 높이만 정하면 어느 블록이 어느 방향에서 보이는지가 모두 정해진다.
    ///   보이는 블록의 색은 평면도가 정해 주고(두 평면도가 다른 색을 요구하면 모순),
    ///   어느 방향에서도 안 보이는 블록은 남은 색을 아무렇게나 채우면 된다.
    ///   그래서 색은 탐색하지 않고 높이만 탐색한다.
    ///
    /// 탐색 순서: 앞줄부터 뒷줄로, 줄 안에서는 오른쪽부터 왼쪽으로.
    ///   이 순서면 전·우 평면도에서 보이는지를 기둥을 놓는 순간 알 수 있어 일찍 가지를 친다.
    ///   좌 평면도는 줄이 끝날 때, 후 평면도는 마지막 줄에서 검사한다.
    /// </summary>
    public static class Solver
    {
        public static SolveResult Solve(Puzzle puzzle, out Model solution, int nodeLimit = 300000)
        {
            var search = new Search(puzzle, nodeLimit);
            bool found = search.Place(0);
            solution = found ? search.BuildSolution() : null;
            if (found) return SolveResult.Solvable;
            return search.OutOfBudget ? SolveResult.Unknown : SolveResult.Unsolvable;
        }

        private sealed class Search
        {
            private readonly int n, h, total;
            private readonly int[,] top, front, back, left, right;
            private readonly int[] counts;
            private readonly int[] heights;              // [z * n + x]
            private readonly int[] forced;               // [(z * n + x) * h + y] 평면도가 정해 준 색, 0이면 아직 자유
            private readonly int[] forcedCount = new int[Colors.Count + 1];
            private readonly Stack<int> trail = new Stack<int>(); // forced에 값을 넣은 순서(되돌리기용)
            private int cells;
            private int budget;

            public bool OutOfBudget => budget < 0;

            public Search(Puzzle puzzle, int nodeLimit)
            {
                n = puzzle.Size;
                h = puzzle.Height;
                top = puzzle.Views[(int)ViewDir.Top];
                front = puzzle.Views[(int)ViewDir.Front];
                back = puzzle.Views[(int)ViewDir.Back];
                left = puzzle.Views[(int)ViewDir.Left];
                right = puzzle.Views[(int)ViewDir.Right];
                counts = puzzle.Counts;
                foreach (int count in counts) total += count;
                heights = new int[n * n];
                forced = new int[n * n * h];
                budget = nodeLimit;
            }

            // 옆 평면도에서 (열, 높이 y)의 값. 행은 위가 높은 층이다.
            private int FrontAt(int x, int y) => front[h - 1 - y, x];
            private int BackAt(int x, int y) => back[h - 1 - y, n - 1 - x];
            private int LeftAt(int z, int y) => left[h - 1 - y, z];
            private int RightAt(int z, int y) => right[h - 1 - y, n - 1 - z];

            /// <summary>k번째 칸부터 끝까지 기둥을 세울 수 있으면 true.</summary>
            public bool Place(int k)
            {
                if (k == n * n) return cells == total;
                int z = n - 1 - k / n, x = n - 1 - k % n;
                bool empty = top[z, x] == Colors.Empty;

                for (int height = empty ? 0 : 1; height <= (empty ? 0 : h); height++)
                {
                    if (--budget < 0) return false;
                    int mark = trail.Count;
                    heights[z * n + x] = height;
                    cells += height;

                    if (cells <= total && Check(x, z, height) && Place(k + 1)) return true;

                    cells -= height;
                    heights[z * n + x] = 0;
                    while (trail.Count > mark)
                    {
                        int cell = trail.Pop();
                        forcedCount[forced[cell]]--;
                        forced[cell] = 0;
                    }
                }
                return false;
            }

            /// <summary>방금 세운 기둥이 평면도와 어긋나지 않는지 검사하고, 보이는 블록의 색을 확정한다.</summary>
            private bool Check(int x, int z, int height)
            {
                if (height > 0 && !Force(x, height - 1, z, top[z, x])) return false;

                for (int y = 0; y < height; y++)
                {
                    // 앞쪽(z가 큰 쪽)·오른쪽(x가 큰 쪽) 기둥은 이미 다 세웠으므로 가려지는지 바로 안다.
                    bool frontVisible = true, rightVisible = true;
                    for (int near = z + 1; near < n; near++) if (heights[near * n + x] > y) frontVisible = false;
                    for (int near = x + 1; near < n; near++) if (heights[z * n + near] > y) rightVisible = false;
                    if (frontVisible && !Force(x, y, z, FrontAt(x, y))) return false;
                    if (rightVisible && !Force(x, y, z, RightAt(z, y))) return false;
                }

                if (z == 0) // x열을 앞뒤로 다 세웠다: 전 평면도의 빈칸과 후 평면도를 검사
                    for (int y = 0; y < h; y++)
                    {
                        int nearest = -1;
                        for (int i = n - 1; i >= 0; i--) if (heights[i * n + x] > y) nearest = i; // 뒤에서 가장 가까운 기둥
                        if (nearest < 0 ? FrontAt(x, y) != 0 || BackAt(x, y) != 0 : !Force(x, y, nearest, BackAt(x, y))) return false;
                    }

                if (x == 0) // z줄을 좌우로 다 세웠다: 우 평면도의 빈칸과 좌 평면도를 검사
                    for (int y = 0; y < h; y++)
                    {
                        int nearest = -1;
                        for (int i = n - 1; i >= 0; i--) if (heights[z * n + i] > y) nearest = i; // 왼쪽에서 가장 가까운 기둥
                        if (nearest < 0 ? RightAt(z, y) != 0 || LeftAt(z, y) != 0 : !Force(nearest, y, z, LeftAt(z, y))) return false;
                    }

                return true;
            }

            /// <summary>블록 (x, y, z)의 색을 평면도가 요구하는 색으로 확정한다. 모순이면 false.</summary>
            private bool Force(int x, int y, int z, int color)
            {
                if (color == Colors.Empty) return false; // 블록이 보여야 할 자리가 평면도에서는 빈칸
                int cell = (z * n + x) * h + y;
                if (forced[cell] == color) return true;
                if (forced[cell] != 0) return false;                  // 다른 평면도가 다른 색을 요구
                if (forcedCount[color] >= counts[color]) return false; // 그 색 블록이 모자람
                forced[cell] = color;
                forcedCount[color]++;
                trail.Push(cell);
                return true;
            }

            /// <summary>찾은 높이와 확정된 색으로 모형을 만든다. 안 보이는 블록은 남은 색으로 채운다.</summary>
            public Model BuildSolution()
            {
                var spare = new Stack<int>();
                for (int color = 1; color <= Colors.Count; color++)
                    for (int i = forcedCount[color]; i < counts[color]; i++) spare.Push(color);

                var columns = new int[n * n][];
                for (int i = 0; i < columns.Length; i++)
                {
                    columns[i] = new int[heights[i]];
                    for (int y = 0; y < heights[i]; y++)
                        columns[i][y] = forced[i * h + y] != 0 ? forced[i * h + y] : spare.Pop();
                }
                return Model.FromColumns(n, h, columns);
            }
        }
    }
}
