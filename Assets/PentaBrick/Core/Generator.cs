using System;
using System.Text;

namespace PentaBrick.Core
{
    /// <summary>자동 생성 문제의 난이도.</summary>
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>난이도별 생성 설정. (명세서 4.2)</summary>
    public sealed class DifficultySettings
    {
        /// <summary>문제 id와 주소에 쓰는 이름: easy / normal / hard.</summary>
        public string Key { get; }

        /// <summary>화면에 보이는 이름: 쉬움 / 보통 / 어려움.</summary>
        public string Label { get; }

        public int Size { get; }
        public int Height { get; }

        /// <summary>문제에 등장하는 색 가짓수(6개 중 무작위로 고름).</summary>
        public int Colors { get; }

        public int MinBlocks { get; }
        public int MaxBlocks { get; }

        /// <summary>바닥 칸 하나에 기둥이 생길 확률.</summary>
        public double Fill { get; }

        /// <summary>이 시간(초) 안에 정답을 제출하면 시간 보너스 +1점.</summary>
        public int BonusSeconds { get; }

        private DifficultySettings(string key, string label, int size, int height, int colors, int minBlocks, int maxBlocks, double fill, int bonusSeconds)
        {
            BonusSeconds = bonusSeconds;
            Key = key;
            Label = label;
            Size = size;
            Height = height;
            Colors = colors;
            MinBlocks = minBlocks;
            MaxBlocks = maxBlocks;
            Fill = fill;
        }

        private static readonly DifficultySettings[] All =
        {
            new DifficultySettings("easy", "쉬움", size: 3, height: 3, colors: 3, minBlocks: 6, maxBlocks: 10, fill: 0.6, bonusSeconds: 60),
            new DifficultySettings("normal", "보통", size: 4, height: 3, colors: 4, minBlocks: 12, maxBlocks: 20, fill: 0.65, bonusSeconds: 180),
            new DifficultySettings("hard", "어려움", size: 5, height: 3, colors: 6, minBlocks: 22, maxBlocks: 36, fill: 0.65, bonusSeconds: 360),
        };

        public static DifficultySettings Get(Difficulty difficulty)
        {
            int index = (int)difficulty;
            if (index < 0 || index >= All.Length)
                throw new ArgumentOutOfRangeException(nameof(difficulty), $"알 수 없는 난이도: {difficulty}");
            return All[index];
        }
    }

    /// <summary>
    /// 32비트 seed로 초기화하는 작은 난수 생성기(mulberry32).
    /// 웹 프로토타입과 비트 단위로 같은 수열을 내야 같은 seed가 같은 문제를 만든다.
    /// </summary>
    public sealed class Mulberry32
    {
        private uint state;

        public Mulberry32(uint seed)
        {
            state = seed;
        }

        /// <summary>0 이상 1 미만의 수.</summary>
        public double Next()
        {
            unchecked // 32비트에서 넘치는 부분은 버린다(JS의 Math.imul, >>> 0과 같은 동작)
            {
                state += 0x6d2b79f5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    /// <summary>seed ↔ 36진수 짧은 코드. 화면 표시와 공유에 쓴다. 예: 2847193 ↔ "1p0wp"</summary>
    public static class SeedCode
    {
        private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        public static string Encode(uint seed)
        {
            if (seed == 0) return "0";
            var code = new StringBuilder();
            for (uint rest = seed; rest > 0; rest /= 36) code.Insert(0, Digits[(int)(rest % 36)]);
            return code.ToString();
        }

        /// <summary>
        /// 코드를 seed로 되돌린다. 사람이 직접 입력할 수 있으므로 대문자와 앞뒤 공백은 허용한다.
        /// 빈 문자열, 다른 문자, 32비트를 넘는 값은 false.
        /// </summary>
        public static bool TryDecode(string code, out uint seed)
        {
            seed = 0;
            if (code == null) return false;
            code = code.Trim().ToLowerInvariant();
            if (code.Length == 0) return false;
            ulong value = 0;
            foreach (char c in code)
            {
                int digit = Digits.IndexOf(c);
                if (digit < 0) return false;
                value = value * 36 + (ulong)digit;
                if (value > uint.MaxValue) return false; // 여기서 끊으므로 ulong은 넘치지 않는다
            }
            seed = (uint)value;
            return true;
        }
    }

    /// <summary>
    /// 난이도별 문제 자동 생성. (명세서 4.3)
    ///
    /// 원리: 정답 모형을 먼저 무작위로 만들고 그 평면도를 문제로 낸다. 해가 반드시 존재한다.
    /// 재현성: 같은 난이도 + 같은 seed → 항상 같은 문제(웹 프로토타입과도 같다).
    ///         그래서 난수를 뽑는 순서를 바꾸면 안 된다.
    /// </summary>
    public static class Generator
    {
        private const int MaxAttempts = 1000; // 조건에 맞는 모형이 나올 때까지 최대 반복 횟수
        private static readonly Random SeedSource = new Random();

        /// <summary>새 문제를 만들 때 쓸 무작위 seed.</summary>
        public static uint RandomSeed()
        {
            var bytes = new byte[4];
            lock (SeedSource) SeedSource.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private const int TamperAttempts = 30;
        private static readonly string[] ViewNames = { "상(위)", "전(앞)", "후(뒤)", "좌(왼쪽)", "우(오른쪽)" };

        /// <summary>
        /// 난이도와 seed로 문제를 만든다. Id는 "normal-1p0wp" 형태.
        /// 10~20%의 확률로 불가능한 문제가 나온다(같은 seed면 항상 같은 결과).
        /// </summary>
        public static Puzzle Generate(Difficulty difficulty, uint seed)
        {
            var puzzle = GenerateSolvable(difficulty, seed);
            // 모형을 만드는 난수와 섞이지 않게 별도의 수열을 쓴다(가능한 문제는 웹 프로토타입과 그대로 같다).
            var rng = new Mulberry32(seed ^ 0x9e3779b9u);
            double ratio = 0.10 + 0.10 * rng.Next();
            if (rng.Next() >= ratio) return puzzle;

            for (int attempt = 0; attempt < TamperAttempts; attempt++)
            {
                var broken = Tamper(puzzle, rng);
                // 고친 뒤에도 풀리는 문제이거나 한도 안에 증명하지 못하면 다른 곳을 고쳐 본다.
                if (broken != null && Solver.Solve(broken, out _) == SolveResult.Unsolvable) return broken;
            }
            return puzzle;
        }

        /// <summary>
        /// 가능한 문제의 평면도 한 칸이나 블록 수를 고친 문제를 만든다(해가 없는지는 아직 모른다).
        /// 고칠 곳을 못 찾으면 null.
        /// </summary>
        private static Puzzle Tamper(Puzzle puzzle, Mulberry32 rng)
        {
            var palette = new System.Collections.Generic.List<int>();
            for (int color = 1; color <= Colors.Count; color++) if (puzzle.Counts[color] > 0) palette.Add(color);
            int Pick(int count) => (int)Math.Floor(rng.Next() * count);

            var views = new int[puzzle.Views.Length][,];
            for (int i = 0; i < views.Length; i++) views[i] = (int[,])puzzle.Views[i].Clone();
            var counts = (int[])puzzle.Counts.Clone();
            string reason;

            if (Pick(3) == 0)
            {
                // 블록 수 모순: 한 색을 줄이고 다른 색을 늘린다.
                int fewer = palette[Pick(palette.Count)], more = palette[Pick(palette.Count)];
                if (fewer == more) return null;
                counts[fewer]--;
                counts[more]++;
                reason = $"원래 풀 수 있는 문제에서 {Colors.Names[fewer]} 블록을 1개 줄이고 {Colors.Names[more]} 블록을 1개 늘린 문제입니다. "
                    + "다섯 평면도를 모두 만족하면서 이 개수를 정확히 쓰는 모형은 없습니다.";
            }
            else
            {
                // 색 가림 모순 / 실루엣 모순: 평면도에서 블록이 보이는 칸 하나를 다른 색이나 빈칸으로 바꾼다.
                int v = Pick(views.Length);
                var view = views[v];
                int row = Pick(view.GetLength(0)), col = Pick(view.GetLength(1));
                int before = view[row, col];
                if (before == Colors.Empty) return null;
                int after = Pick(4) == 0 ? Colors.Empty : palette[Pick(palette.Count)];
                if (after == before) return null;
                view[row, col] = after;
                reason = $"원래 풀 수 있는 문제의 {ViewNames[v]} 평면도에서 위에서 {row + 1}번째 줄, 왼쪽에서 {col + 1}번째 칸을 "
                    + $"{Colors.Names[before]}에서 {Colors.Names[after]}{(after == Colors.Purple ? "로" : "으로")} 바꾼 문제입니다. "
                    + "이 칸은 다른 평면도나 블록 수와 동시에 만족할 수 없습니다.";
            }

            return new Puzzle(puzzle.Id, puzzle.Size, puzzle.Height, views, counts,
                solvable: false, answer: null, reason: reason, difficulty: puzzle.Difficulty, seed: puzzle.Seed);
        }

        /// <summary>항상 가능한 문제를 만든다(웹 프로토타입의 generatePuzzle과 같은 결과).</summary>
        public static Puzzle GenerateSolvable(Difficulty difficulty, uint seed)
        {
            var options = DifficultySettings.Get(difficulty);
            var rng = new Mulberry32(seed);
            // 같은 seed의 난수 수열을 이어서 쓰므로, 몇 번 다시 만들더라도 결과는 항상 같다.
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var model = GenerateModel(rng, options, out int[] palette);
                if (IsAcceptable(model, palette, options))
                    return Puzzle.FromModel($"{options.Key}-{SeedCode.Encode(seed)}", model, difficulty, seed);
            }
            throw new InvalidOperationException($"문제 생성 실패({options.Key}, seed {seed}): 조건을 만족하는 모형을 찾지 못함");
        }

        /// <summary>
        /// 문제 코드("normal-1p0wp")를 난이도와 seed로 되돌린다. 사람이 입력하므로 대문자와 공백은 허용한다.
        /// </summary>
        public static bool TryParseId(string id, out Difficulty difficulty, out uint seed)
        {
            difficulty = default;
            seed = 0;
            if (id == null) return false;
            string[] parts = id.Trim().Split('-');
            return parts.Length == 2 && TryParseDifficulty(parts[0], out difficulty) && SeedCode.TryDecode(parts[1], out seed);
        }

        /// <summary>
        /// 주소의 "?mode=normal&amp;seed=1p0wp"에서 문제를 읽는다(웹 프로토타입과 같은 형식, 명세서 4.4).
        /// </summary>
        public static bool TryParseUrl(string url, out Difficulty difficulty, out uint seed)
        {
            difficulty = default;
            seed = 0;
            int start = url == null ? -1 : url.IndexOf('?');
            if (start < 0) return false;
            string mode = null, code = null;
            foreach (string pair in url.Substring(start + 1).Split('#')[0].Split('&'))
            {
                string[] keyValue = pair.Split('=');
                if (keyValue.Length != 2) continue;
                if (keyValue[0] == "mode") mode = keyValue[1];
                if (keyValue[0] == "seed") code = keyValue[1];
            }
            return TryParseDifficulty(mode, out difficulty) && SeedCode.TryDecode(code, out seed);
        }

        private static bool TryParseDifficulty(string key, out Difficulty difficulty)
        {
            difficulty = default;
            if (key == null) return false;
            key = key.Trim().ToLowerInvariant();
            foreach (Difficulty candidate in Enum.GetValues(typeof(Difficulty)))
            {
                if (DifficultySettings.Get(candidate).Key != key) continue;
                difficulty = candidate;
                return true;
            }
            return false;
        }

        /// <summary>여섯 색을 섞어서 앞의 count개를 고른다(피셔-예이츠 셔플).</summary>
        private static int[] PickColors(Mulberry32 rng, int count)
        {
            var colors = new int[Colors.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = i + 1;
            for (int i = colors.Length - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(rng.Next() * (i + 1));
                int swap = colors[i];
                colors[i] = colors[j];
                colors[j] = swap;
            }
            var picked = new int[count];
            Array.Copy(colors, picked, count);
            return picked;
        }

        /// <summary>
        /// 무작위 모형 하나를 만든다(아직 조건 검사 전).
        /// 칸마다 Fill 확률로 기둥을 세우고, 높이는 낮은 쪽이 더 자주 나오도록 rng^1.5로 치우치게 한다.
        /// </summary>
        private static Model GenerateModel(Mulberry32 rng, DifficultySettings options, out int[] palette)
        {
            palette = PickColors(rng, options.Colors);
            var columns = new int[options.Size * options.Size][];
            for (int i = 0; i < columns.Length; i++)
            {
                if (rng.Next() >= options.Fill)
                {
                    columns[i] = new int[0];
                    continue;
                }
                int stackHeight = 1 + (int)Math.Floor(Math.Pow(rng.Next(), 1.5) * options.Height);
                var column = new int[stackHeight];
                for (int y = 0; y < stackHeight; y++) column[y] = palette[(int)Math.Floor(rng.Next() * palette.Length)];
                columns[i] = column;
            }
            return Model.FromColumns(options.Size, options.Height, columns);
        }

        /// <summary>
        /// 생성된 모형이 문제로 쓸 만한지 검사한다.
        ///   1. 총 블록 수가 난이도 범위 안
        ///   2. 고른 색을 모두 한 번 이상 사용
        ///   3. 높이 2 이상 기둥이 하나는 있음(한 층짜리 평면 문제 제외)
        /// </summary>
        private static bool IsAcceptable(Model model, int[] palette, DifficultySettings options)
        {
            int blocks = model.TotalBlocks;
            if (blocks < options.MinBlocks || blocks > options.MaxBlocks) return false;

            var counts = model.CountColors();
            foreach (int color in palette) if (counts[color] == 0) return false;

            if (options.Height > 1)
            {
                bool tall = false;
                for (int z = 0; z < model.Size && !tall; z++)
                    for (int x = 0; x < model.Size && !tall; x++)
                        tall = model.Column(x, z).Count >= 2;
                if (!tall) return false;
            }
            return true;
        }
    }
}
