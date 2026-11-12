using System;
using PentaBrick.Core;

namespace PentaBrick.Game
{
    /// <summary>튜토리얼의 한 단계.</summary>
    public sealed class TutorialStep
    {
        public string Title { get; }
        public string Text { get; }
        /// <summary>이 단계에서 풀 문제.</summary>
        public Puzzle Puzzle { get; }
        /// <summary>단계를 마치는 조건. null이면 "다음" 버튼으로 넘어간다.</summary>
        public Func<GameState, bool> Done { get; }
        public bool IsManual => Done == null;

        public TutorialStep(string title, string text, Puzzle puzzle, Func<GameState, bool> done = null)
        {
            Title = title;
            Text = text;
            Puzzle = puzzle;
            Done = done;
        }
    }

    /// <summary>
    /// 게임 규칙(명세서 2장)을 순서대로 익히는 튜토리얼. 2×2 크기의 작은 문제 두 개로 진행한다.
    /// 설명 단계는 "다음"으로, 행동 단계는 그 행동을 하면 자동으로 넘어간다.
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>직접 만들어 볼 문제: 뒤-왼쪽에 파랑 2개, 뒤-오른쪽에 빨강, 앞-오른쪽에 초록.</summary>
        public static readonly Puzzle BuildPuzzle = Puzzle.FromModel("tutorial-1", Model.Parse(new[] { "BB R", ".  G" }));

        /// <summary>
        /// 불가능한 문제: 초록 블록 하나뿐인데 전(앞) 평면도 왼쪽 아래 칸이 빨강이다.
        /// </summary>
        public static readonly Puzzle ImpossiblePuzzle = MakeImpossible();

        private static Puzzle MakeImpossible()
        {
            var model = Model.Parse(new[] { "G .", ". ." });
            var views = Projection.ProjectAll(model);
            views[(int)ViewDir.Front][1, 0] = Colors.Red; // [row 1 = 바닥층, col 0 = 왼쪽]
            return new Puzzle("tutorial-2", model.Size, model.Height, views, model.CountColors(), solvable: false, answer: null,
                reason: "전(앞) 평면도의 왼쪽 아래 칸은 빨강인데, 주어진 블록은 초록 1개뿐이라 빨강이 보일 수 없습니다.");
        }

        private static readonly TutorialStep[] AllSteps =
        {
            new TutorialStep("목표",
                "PentaBrick은 다섯 방향의 평면도를 보고 똑같은 블록 모형을 만드는 퍼즐입니다. 작은 2×2 문제로 규칙을 익혀 봅시다.",
                BuildPuzzle),
            new TutorialStep("평면도 읽기",
                "위쪽 카드 다섯 장은 정답 모형을 상(위)·전(앞)·후(뒤)·좌(왼쪽)·우(오른쪽)에서 본 모습입니다. " +
                "각 칸에는 그 방향에서 가장 먼저 보이는 블록의 색이 나옵니다. 옆의 '내 모형'은 지금 만든 모형의 평면도입니다(연습 모드 전용).",
                BuildPuzzle),
            new TutorialStep("블록 놓기",
                "오른쪽에서 색을 고르고(숫자 키 1~6) 가운데 바닥 칸을 클릭해 블록을 하나 놓아 보세요.",
                BuildPuzzle, game => game.UsedTotal >= 1),
            new TutorialStep("쌓기",
                "블록이 있는 칸을 클릭하면 그 위에 쌓입니다. 블록을 공중에 띄울 수는 없습니다. 한 칸에 블록 두 개를 쌓아 보세요.",
                BuildPuzzle, HasStack),
            new TutorialStep("보는 방향 바꾸기와 고치기",
                "드래그하면 회전, 휠로 확대·축소, 아래 시점 버튼으로 각 방향에서 볼 수 있습니다. " +
                "우클릭이나 지우기 도구(E)로 블록을 지우고, 칠하기 도구(W)로 색을 바꾸며, Ctrl(⌘)+Z로 되돌립니다.",
                BuildPuzzle),
            new TutorialStep("모형 완성하기",
                "다섯 평면도가 모두 '일치'가 되도록 만든 뒤 '정답 제출'을 누르세요. 주어진 블록은 남김없이 정확히 다 써야 제출할 수 있습니다.",
                BuildPuzzle, game => game.Phase == Phase.Result && !game.DeclaredImpossible && game.Result.Correct),
            new TutorialStep("점수",
                "정답 제출은 맞으면 +1점, 틀리면 −1점입니다. 난이도 모드에서는 기준 시간 안에 맞히면 시간 보너스 +1점이 더해집니다. " +
                "튜토리얼과 '내 모형의 평면도 표시'를 켠 연습 문제는 점수에 반영되지 않습니다.",
                BuildPuzzle),
            new TutorialStep("불가능한 문제",
                "어떤 문제는 평면도와 블록 수가 서로 맞지 않아 만들 수 없습니다. 전(앞) 평면도의 왼쪽 아래 칸을 보세요. " +
                "빨강이 보여야 하지만 주어진 블록은 초록 하나뿐입니다. 이럴 때는 '불가능'을 누릅니다.",
                ImpossiblePuzzle, game => game.Phase == Phase.Result && game.DeclaredImpossible && game.Result.Correct),
            new TutorialStep("불가능 선언",
                "불가능 선언은 맞으면 +2점, 틀리면 −2점입니다. 확신이 들 때만 누르세요. 난이도 모드 문제 중 일부(10~20%)는 불가능한 문제입니다.",
                ImpossiblePuzzle),
            new TutorialStep("준비 완료",
                "이제 난이도를 골라 시작해 보세요. 게임 중 Esc를 누르면 메뉴에서 튜토리얼을 다시 볼 수 있습니다.",
                ImpossiblePuzzle),
        };

        private static bool HasStack(GameState game)
        {
            for (int z = 0; z < game.Model.Size; z++)
                for (int x = 0; x < game.Model.Size; x++)
                    if (game.Model.Column(x, z).Count >= 2) return true;
            return false;
        }

        public int Index { get; private set; }
        public int Count => AllSteps.Length;
        public TutorialStep Current => AllSteps[Index];
        public bool IsLast => Index == AllSteps.Length - 1;

        /// <summary>첫 단계부터 시작한다.</summary>
        public void Begin(GameState game)
        {
            Index = 0;
            game.OpenCustom(Current.Puzzle);
        }

        /// <summary>설명 단계에서 "다음". 행동 단계에서는 아무 일도 없다.</summary>
        public void Next(GameState game)
        {
            if (!Current.IsManual || IsLast) return;
            Index++;
            Enter(game);
        }

        /// <summary>행동 단계의 조건을 채웠으면 다음 단계로 넘어가고 true.</summary>
        public bool Update(GameState game)
        {
            if (Current.IsManual || !Current.Done(game)) return false;
            Index++;
            Enter(game);
            return true;
        }

        /// <summary>행동 단계에서 틀린 결과가 나왔는지(다시 하기가 필요한지).</summary>
        public bool NeedsRetry(GameState game) => !Current.IsManual && game.Phase == Phase.Result && !Current.Done(game);

        /// <summary>현재 단계의 문제를 처음부터 다시 연다.</summary>
        public void Retry(GameState game) => game.OpenCustom(Current.Puzzle);

        /// <summary>이 튜토리얼의 문제인지. 다른 문제로 바뀌면 튜토리얼을 끝낸다.</summary>
        public bool Owns(Puzzle puzzle) => puzzle == BuildPuzzle || puzzle == ImpossiblePuzzle;

        /// <summary>새 단계의 문제가 지금 문제와 다르면 연다.</summary>
        private void Enter(GameState game)
        {
            if (game.Puzzle != Current.Puzzle) game.OpenCustom(Current.Puzzle);
        }
    }
}
