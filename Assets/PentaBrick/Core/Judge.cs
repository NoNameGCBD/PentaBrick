namespace PentaBrick.Core
{
    /// <summary>판정 결과.</summary>
    public sealed class JudgeResult
    {
        /// <summary>최종 정답 여부.</summary>
        public bool Correct { get; }

        /// <summary>
        /// 방향별 일치 여부(인덱스 = (int)ViewDir). 결과 화면에서 틀린 평면도를 보여줄 때 쓴다.
        /// 불가능 선언의 결과에는 없다(null).
        /// </summary>
        public bool[] ViewMatches { get; }

        /// <summary>색별 블록 수가 문제와 정확히 같은지.</summary>
        public bool CountsMatch { get; }

        /// <summary>점수 변동. Judge.Check만 했을 때는 0.</summary>
        public int Delta { get; }

        public JudgeResult(bool correct, bool[] viewMatches, bool countsMatch, int delta)
        {
            Correct = correct;
            ViewMatches = viewMatches;
            CountsMatch = countsMatch;
            Delta = delta;
        }
    }

    /// <summary>
    /// 정답 판정과 점수. (명세서 2.2, 규칙 4·5)
    ///
    /// 정답 모형과 직접 비교하지 않는다. 같은 평면도를 만드는 모형이 여러 개일 수 있으므로
    /// "다섯 평면도가 모두 일치" + "색별 블록 수를 정확히 사용" 두 조건만 검사한다.
    /// </summary>
    public static class Judge
    {
        public const int SubmitPoints = 1;     // 규칙 4: 정답 +1, 오답 -1
        public const int ImpossiblePoints = 2; // 규칙 5: 맞으면 +2, 틀리면 -2

        /// <summary>모형을 문제와 대조한다(점수 변동 없음). 크기가 다른 모형은 오답이다.</summary>
        public static JudgeResult Check(Model model, Puzzle puzzle) => Check(model, puzzle, 0);

        /// <summary>모형 제출(규칙 4).</summary>
        public static JudgeResult ScoreSubmit(Model model, Puzzle puzzle) => Check(model, puzzle, SubmitPoints);

        /// <summary>불가능 선언(규칙 5). 문제에 저장된 Solvable 값으로 판단한다.</summary>
        public static JudgeResult ScoreImpossible(Puzzle puzzle)
        {
            bool correct = !puzzle.Solvable;
            return new JudgeResult(correct, null, false, correct ? ImpossiblePoints : -ImpossiblePoints);
        }

        private static JudgeResult Check(Model model, Puzzle puzzle, int points)
        {
            var mine = Projection.ProjectAll(model);
            var viewMatches = new bool[Projection.ViewCount];
            bool allViews = true;
            for (int i = 0; i < Projection.ViewCount; i++)
            {
                viewMatches[i] = Projection.ViewsEqual(mine[i], puzzle.Views[i]);
                allViews &= viewMatches[i];
            }

            var used = model.CountColors();
            bool countsMatch = true;
            for (int color = 0; color < used.Length; color++)
                if (used[color] != puzzle.Counts[color]) countsMatch = false;

            bool correct = allViews && countsMatch;
            return new JudgeResult(correct, viewMatches, countsMatch, correct ? points : -points);
        }
    }
}
