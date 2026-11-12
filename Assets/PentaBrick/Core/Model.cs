using System;
using System.Collections.Generic;

namespace PentaBrick.Core
{
    /// <summary>
    /// 플레이어가 쌓는 블록 모형. 화면이나 게임 진행과 무관한 순수 자료구조다.
    ///
    /// 좌표계 (앞에서 바라보는 기준, 명세서 6.1)
    ///   x : 왼쪽(0) → 오른쪽(Size-1)
    ///   y : 바닥(0) → 위(Height-1)
    ///   z : 뒤(0)   → 앞(Size-1)
    ///
    /// 저장 방식
    ///   바닥 칸(x, z)마다 "아래→위 순서의 색 배열(스택)"을 가진다.
    ///   스택이라 중간이 빈 상태를 표현할 수 없으므로 "공중에 뜬 블록 금지" 규칙이
    ///   자료구조 차원에서 지켜진다.
    ///
    /// 불변(immutable)
    ///   Place / Remove / Paint 는 원본을 바꾸지 않고 새 모형을 돌려준다(실패하면 null).
    ///   덕분에 실행 취소가 "이전 모형 객체를 보관"하는 것만으로 끝난다.
    /// </summary>
    public sealed class Model
    {
        private const string ColorLetters = ".ROYGBP"; // Parse에서 쓰는 한 글자 색 표기. 인덱스 = 색 번호
        private static readonly int[] NoBlocks = new int[0];

        // columns[z * Size + x] = 그 칸의 색 스택. 배열은 절대 수정하지 않는다(모형끼리 공유한다).
        private readonly int[][] columns;

        /// <summary>바닥 한 변의 칸 수.</summary>
        public int Size { get; }

        /// <summary>최대로 쌓을 수 있는 높이.</summary>
        public int Height { get; }

        private Model(int size, int height, int[][] columns)
        {
            Size = size;
            Height = height;
            this.columns = columns;
        }

        /// <summary>비어 있는 모형을 만든다.</summary>
        public static Model Empty(int size, int height)
        {
            if (size < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(size), "격자 크기와 높이는 1 이상이어야 함");
            var columns = new int[size * size][];
            for (int i = 0; i < columns.Length; i++) columns[i] = NoBlocks;
            return new Model(size, height, columns);
        }

        /// <summary>칸 배열(columns[z * size + x] = 아래→위 색)로 모형을 만든다. 배열은 복사한다.</summary>
        public static Model FromColumns(int size, int height, int[][] columns)
        {
            if (columns == null || columns.Length != size * size)
                throw new ArgumentException($"칸 배열의 길이가 {size * size}가 아님", nameof(columns));
            var copy = new int[columns.Length][];
            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i] ?? NoBlocks;
                if (column.Length > height) throw new ArgumentException($"칸 {i}이 최대 높이 {height}를 넘음", nameof(columns));
                foreach (int color in column)
                    if (!Colors.IsColor(color)) throw new ArgumentException($"칸 {i}에 잘못된 색 번호 {color}", nameof(columns));
                copy[i] = (int[])column.Clone();
            }
            return new Model(size, height, copy);
        }

        /// <summary>
        /// 문자열로 모형을 만든다. 샘플 문제와 테스트를 손으로 적을 때 쓴다.
        ///
        /// rows: 뒤(z=0)에서 앞(z=Size-1) 순서의 행. 각 행은 공백으로 칸을 나누고,
        ///       칸은 아래→위 순서의 색 글자(R O Y G B P), 빈 칸은 '.'로 적는다.
        ///
        /// 예) Parse(new[] { "BB G .",     // 뒷줄: 파랑 2개 / 초록 1개 / 빈칸
        ///                   "B  . Y",     // 가운데 줄
        ///                   "BR Y ." })   // 앞줄: 파랑 위에 빨강 / 노랑 / 빈칸
        ///
        /// 글자 배열이 상(위) 평면도와 같은 방향(위쪽 행이 뒤)이라 읽기 쉽다.
        /// height를 0으로 두면 행 수와 같은 높이가 된다.
        /// </summary>
        public static Model Parse(string[] rows, int height = 0)
        {
            int size = rows.Length;
            if (height == 0) height = size;
            var columns = new int[size * size][];
            for (int z = 0; z < size; z++)
            {
                var cells = rows[z].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length != size) throw new FormatException($"행 {z}의 칸 수가 {size}개가 아님: \"{rows[z]}\"");
                for (int x = 0; x < size; x++)
                {
                    string cell = cells[x];
                    if (cell == ".")
                    {
                        columns[z * size + x] = NoBlocks;
                        continue;
                    }
                    if (cell.Length > height) throw new FormatException($"칸 ({x}, {z})이 최대 높이 {height}를 넘음");
                    var column = new int[cell.Length];
                    for (int y = 0; y < cell.Length; y++)
                    {
                        int color = ColorLetters.IndexOf(cell[y]);
                        if (color < 1) throw new FormatException($"알 수 없는 색 글자: \"{cell[y]}\"");
                        column[y] = color;
                    }
                    columns[z * size + x] = column;
                }
            }
            return new Model(size, height, columns);
        }

        /// <summary>바닥 좌표(x, z)가 격자 안에 있는지 검사한다.</summary>
        public bool InBounds(int x, int z) => x >= 0 && x < Size && z >= 0 && z < Size;

        /// <summary>칸(x, z)의 색 스택(아래→위). 격자 밖이면 빈 목록.</summary>
        public IReadOnlyList<int> Column(int x, int z) => InBounds(x, z) ? columns[z * Size + x] : NoBlocks;

        /// <summary>3차원 좌표(x, y, z)의 색. 블록이 없거나 격자 밖이면 Colors.Empty.</summary>
        public int CellAt(int x, int y, int z)
        {
            if (!InBounds(x, z)) return Colors.Empty;
            var column = columns[z * Size + x];
            return y >= 0 && y < column.Length ? column[y] : Colors.Empty;
        }

        // ---------------------------------------------------------------
        // 편집 함수 세 가지. 모두 "새 모형" 또는 "불가능하면 null"을 돌려준다.
        // ---------------------------------------------------------------

        /// <summary>칸(x, z)의 맨 위에 블록을 쌓는다. 실패(null): 격자 밖, 잘못된 색, 이미 최대 높이.</summary>
        public Model Place(int x, int z, int color)
        {
            if (!InBounds(x, z) || !Colors.IsColor(color)) return null;
            var column = columns[z * Size + x];
            if (column.Length >= Height) return null;
            var next = new int[column.Length + 1];
            column.CopyTo(next, 0);
            next[column.Length] = color;
            return WithColumn(x, z, next);
        }

        /// <summary>
        /// 블록(x, y, z)을 제거한다. 그 위에 있던 블록들은 한 칸씩 내려온다.
        /// 실패(null): 격자 밖, 그 높이에 블록이 없음.
        /// </summary>
        public Model Remove(int x, int y, int z)
        {
            if (!InBounds(x, z)) return null;
            var column = columns[z * Size + x];
            if (y < 0 || y >= column.Length) return null;
            var next = new int[column.Length - 1];
            for (int i = 0; i < next.Length; i++) next[i] = column[i < y ? i : i + 1];
            return WithColumn(x, z, next);
        }

        /// <summary>블록(x, y, z)의 색을 바꾼다. 실패(null): 격자 밖, 잘못된 색, 블록 없음, 이미 같은 색.</summary>
        public Model Paint(int x, int y, int z, int color)
        {
            if (!InBounds(x, z) || !Colors.IsColor(color)) return null;
            var column = columns[z * Size + x];
            if (y < 0 || y >= column.Length || column[y] == color) return null;
            var next = (int[])column.Clone();
            next[y] = color;
            return WithColumn(x, z, next);
        }

        /// <summary>칸 하나만 바꾼 새 모형을 만든다(다른 칸의 배열은 공유해서 비용이 작다).</summary>
        private Model WithColumn(int x, int z, int[] column)
        {
            var next = (int[][])columns.Clone();
            next[z * Size + x] = column;
            return new Model(Size, Height, next);
        }

        // ---------------------------------------------------------------
        // 집계
        // ---------------------------------------------------------------

        /// <summary>
        /// 색별 블록 개수. 배열의 인덱스가 색 번호다(길이 7, [0]은 항상 0).
        /// 문제의 Counts와 같은 모양이라 바로 비교할 수 있다.
        /// </summary>
        public int[] CountColors()
        {
            var counts = new int[Colors.Count + 1];
            foreach (var column in columns)
                foreach (int color in column) counts[color]++;
            return counts;
        }

        /// <summary>모형에 있는 블록의 총 개수.</summary>
        public int TotalBlocks
        {
            get
            {
                int total = 0;
                foreach (var column in columns) total += column.Length;
                return total;
            }
        }

        /// <summary>크기와 모든 블록이 같은지 비교한다.</summary>
        public bool SameAs(Model other)
        {
            if (other == null || other.Size != Size || other.Height != Height) return false;
            for (int i = 0; i < columns.Length; i++)
            {
                int[] a = columns[i], b = other.columns[i];
                if (a.Length != b.Length) return false;
                for (int y = 0; y < a.Length; y++) if (a[y] != b[y]) return false;
            }
            return true;
        }
    }
}
