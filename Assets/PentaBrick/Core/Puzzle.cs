namespace PentaBrick.Core
{
    /// <summary>문제 하나. (명세서 6.4)</summary>
    public sealed class Puzzle
    {
        /// <summary>식별자. 샘플은 "p1", 생성 문제는 "normal-1p0wp".</summary>
        public string Id { get; }

        public int Size { get; }
        public int Height { get; }

        /// <summary>다섯 평면도. 인덱스는 (int)ViewDir.</summary>
        public int[][,] Views { get; }

        /// <summary>
        /// 정답 모형의 색별 블록 수. 인덱스 = 색 번호, [0]은 항상 0.
        /// 플레이어에게는 보이지 않고, 0보다 큰 색이 이 문제에서 쓸 수 있는 색(팔레트)이다.
        /// </summary>
        public int[] Counts { get; }

        /// <summary>써야 하는 블록의 총수. 판정은 다섯 평면도 + 이 총수로 한다(색별 개수는 자유).</summary>
        public int Total { get; }

        /// <summary>이 문제에서 쓸 수 있는 색인지.</summary>
        public bool HasColor(int color) => Colors.IsColor(color) && Counts[color] > 0;

        /// <summary>쓸 수 있는 색을 색 번호 순서로. 숫자 키와 색 칩은 이 순서로 1번부터 매긴다.</summary>
        public int[] Palette { get; }

        /// <summary>해가 존재하면 true, 불가능 문제면 false.</summary>
        public bool Solvable { get; }

        /// <summary>정답 모형(불가능 문제는 null). "정답 모형 보기"에 쓴다.</summary>
        public Model Answer { get; }

        /// <summary>불가능 문제의 모순 설명. 가능 문제는 null.</summary>
        public string Reason { get; }

        /// <summary>생성 문제에만 있다.</summary>
        public Difficulty? Difficulty { get; }

        /// <summary>생성 문제에만 있다.</summary>
        public uint? Seed { get; }

        public Puzzle(string id, int size, int height, int[][,] views, int[] counts,
            bool solvable, Model answer, string reason = null, Difficulty? difficulty = null, uint? seed = null, int? total = null)
        {
            if (total == null) { total = 0; foreach (int count in counts) total += count; }
            Total = total.Value;
            var palette = new System.Collections.Generic.List<int>();
            for (int color = 1; color <= Colors.Count; color++) if (counts[color] > 0) palette.Add(color);
            Palette = palette.ToArray();
            Id = id;
            Size = size;
            Height = height;
            Views = views;
            Counts = counts;
            Solvable = solvable;
            Answer = answer;
            Reason = reason;
            Difficulty = difficulty;
            Seed = seed;
        }

        /// <summary>정답 모형에서 문제를 만든다. 평면도와 블록 수를 계산하므로 해가 반드시 존재한다.</summary>
        public static Puzzle FromModel(string id, Model answer, Difficulty? difficulty = null, uint? seed = null)
        {
            return new Puzzle(id, answer.Size, answer.Height, Projection.ProjectAll(answer), answer.CountColors(),
                solvable: true, answer: answer, difficulty: difficulty, seed: seed);
        }
    }
}
