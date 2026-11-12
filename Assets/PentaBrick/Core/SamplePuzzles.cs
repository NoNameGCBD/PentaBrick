using System;
using System.Collections.Generic;

namespace PentaBrick.Core
{
    /// <summary>
    /// 손으로 만든 샘플 문제 6개(가능 4개, 불가능 2개). (명세서 4.5)
    ///
    /// 불가능 문제는 가능한 문제를 먼저 만든 뒤 평면도 한 칸이나 블록 수를 살짝 바꿔 모순을 넣는다.
    /// 정말 해가 없는지는 테스트의 전수 탐색(TestUtil.HasSolution)으로 확인한다.
    /// </summary>
    public static class SamplePuzzles
    {
        /// <summary>p1~p6 순서.</summary>
        public static IReadOnlyList<Puzzle> All { get; } = new[]
        {
            // p1: 첫 문제. 7개 블록, 높이 2까지.
            Puzzle.FromModel("p1", Model.Parse(new[]
            {
                "GG .  .",
                "G  R  .",
                "B  R  Y",
            })),

            // p2: 12개 블록, 높이 3 기둥과 가려지는 블록이 있다.
            Puzzle.FromModel("p2", Model.Parse(new[]
            {
                "RBR BB .",
                "YY  G  B",
                "Y   .  GP",
            })),

            // p3 (불가능, 색 가림 모순): 전(앞) 평면도 왼쪽 열의 빨강을 파랑으로 바꿨다.
            // 상 평면도에서 앞줄 왼쪽 칸의 꼭대기가 빨강인데, 앞줄은 앞에서 가려질 수 없으므로
            // 전 평면도 왼쪽 열에 빨강이 있어야 한다. 없으니 해가 없다.
            Impossible("p3",
                Model.Parse(new[]
                {
                    "BB G .",
                    "B  . Y",
                    "BR Y .",
                }),
                "상(위) 평면도에서 맨 앞줄 왼쪽 칸의 꼭대기는 빨강입니다. 맨 앞줄은 앞에서 볼 때 가려질 수 없으므로 전(앞) 평면도의 왼쪽 열에도 빨강이 보여야 하는데, 보이지 않습니다.",
                (views, counts) => views[(int)ViewDir.Front][1, 0] = Colors.Blue), // [row 1 = 높이 y=1, col 0 = x=0]

            // p4: 4×4×4, 18개 블록.
            Puzzle.FromModel("p4", Model.Parse(new[]
            {
                "PP P   .  G",
                "B  BRB O  G",
                ".  Y   OO .",
                "R  Y   .  GGG",
            })),

            // p5 (불가능, 블록 수 모순): 초록 하나를 보라로 바꿨다.
            // 모든 블록이 어느 방향에서든 보이는 모형이라 색 배치가 완전히 정해진다.
            // 평면도상 초록이 2개 필요한데 1개뿐이고, 보라는 숨길 자리가 없다.
            Impossible("p5",
                Model.Parse(new[]
                {
                    "R .  B",
                    ". GG .",
                    "Y .  O",
                }),
                "평면도대로라면 가운데 기둥은 초록 2개여야 하는데 초록 블록은 1개뿐입니다. 남는 보라 블록은 어느 평면도에도 보이지 않아야 하지만 숨길 자리가 없습니다.",
                (views, counts) =>
                {
                    counts[Colors.Green] -= 1;
                    counts[Colors.Purple] += 1;
                }),

            // p6: 4×4×4, 안쪽에 가려진 블록이 있어 정답이 여러 개다.
            Puzzle.FromModel("p6", Model.Parse(new[]
            {
                "OO O   O  .",
                "B  BPB BB Y",
                "B  GRG GG Y",
                ".  G   G  YR",
            })),
        };

        /// <summary>가능한 모형(baseModel)의 평면도나 블록 수를 tamper로 고쳐 불가능 문제를 만든다.</summary>
        private static Puzzle Impossible(string id, Model baseModel, string reason, Action<int[][,], int[]> tamper)
        {
            var views = Projection.ProjectAll(baseModel);
            var counts = baseModel.CountColors();
            tamper(views, counts);
            return new Puzzle(id, baseModel.Size, baseModel.Height, views, counts,
                solvable: false, answer: null, reason: reason);
        }
    }
}
