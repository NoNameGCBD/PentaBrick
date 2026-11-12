namespace PentaBrick.Core
{
    /// <summary>평면도의 방향. 화면에 보이는 순서(상·전·후·좌·우)와 같다.</summary>
    public enum ViewDir
    {
        Top = 0,
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4,
    }

    /// <summary>
    /// 모형을 다섯 방향에서 바라봤을 때 보이는 색 격자(평면도)를 만든다.
    /// 문제 출제(정답 모형 → 평면도)와 판정(플레이어 모형 → 평면도) 양쪽에서 쓰인다.
    ///
    /// 평면도 자료형: view[row, col]  (명세서 6.3)
    ///   row : 위→아래 (옆면은 높은 층이 위, 상 평면도는 뒤쪽 줄이 위)
    ///   col : "그 방향에 서서 봤을 때"의 왼쪽→오른쪽
    ///   값  : 시선이 처음 만나는 블록의 색, 아무것도 없으면 Colors.Empty
    ///
    /// 원리는 하나다. 칸마다 시선 방향으로 블록을 "가까운 것부터" 훑어서 처음 나오는
    /// 색을 적는다. 네 옆면의 차이는 (열, 깊이) → (x, z) 좌표 변환식뿐이다.
    /// </summary>
    public static class Projection
    {
        /// <summary>방향의 개수(5).</summary>
        public const int ViewCount = 5;

        /// <summary>한 방향의 평면도. 상은 Size×Size, 옆면은 Height×Size.</summary>
        public static int[,] Project(Model model, ViewDir dir)
        {
            int n = model.Size, h = model.Height;

            if (dir == ViewDir.Top)
            {
                // 각 칸의 맨 위 블록. 위쪽 행이 뒤(z=0).
                var top = new int[n, n];
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        var column = model.Column(x, z);
                        top[z, x] = column.Count > 0 ? column[column.Count - 1] : Colors.Empty;
                    }
                return top;
            }

            var view = new int[h, n];
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row; // 평면도의 위쪽 행이 높은 층
                for (int col = 0; col < n; col++)
                    for (int depth = 0; depth < n; depth++) // depth 0이 보는 사람에게 가장 가깝다
                    {
                        int x, z;
                        switch (dir)
                        {
                            case ViewDir.Front: x = col; z = n - 1 - depth; break;         // 왼쪽 열이 x=0
                            case ViewDir.Back: x = n - 1 - col; z = depth; break;          // 좌우 반전
                            case ViewDir.Left: x = depth; z = col; break;                  // 왼쪽 열이 뒤(z=0)
                            default: x = n - 1 - depth; z = n - 1 - col; break;            // Right: 왼쪽 열이 앞
                        }
                        int color = model.CellAt(x, y, z);
                        if (color == Colors.Empty) continue;
                        view[row, col] = color; // 처음 만나는 블록이 보인다
                        break;
                    }
            }
            return view;
        }

        /// <summary>다섯 평면도를 한 번에 계산한다. 배열 인덱스는 (int)ViewDir.</summary>
        public static int[][,] ProjectAll(Model model)
        {
            var views = new int[ViewCount][,];
            for (int i = 0; i < ViewCount; i++) views[i] = Project(model, (ViewDir)i);
            return views;
        }

        /// <summary>평면도 두 개가 칸 단위로 완전히 같은지 비교한다. 크기가 다르면 false.</summary>
        public static bool ViewsEqual(int[,] a, int[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int row = 0; row < a.GetLength(0); row++)
                for (int col = 0; col < a.GetLength(1); col++)
                    if (a[row, col] != b[row, col]) return false;
            return true;
        }
    }
}
