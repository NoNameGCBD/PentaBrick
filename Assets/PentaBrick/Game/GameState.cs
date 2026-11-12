using System;
using System.Collections.Generic;
using System.Diagnostics;
using PentaBrick.Core;

namespace PentaBrick.Game
{
    public enum Tool { Place, Paint, Erase }

    public enum Phase { Playing, Result }

    /// <summary>
    /// 게임 진행 상태와 액션. (명세서 3장, 6.5 / 웹판 src/game/state.js)
    /// 화면은 속성을 읽기만 하고, 바꿀 때는 메서드를 부른다. 바뀌면 Changed가 불린다.
    /// </summary>
    public sealed class GameState
    {
        private readonly Func<double> nowMs;
        private readonly Stack<Model> past = new Stack<Model>();
        private readonly Stack<Model> future = new Stack<Model>();
        private double startedAt;
        private double resultElapsedMs;

        /// <summary>null이면 샘플 모드, 아니면 그 난이도의 생성 모드.</summary>
        public Difficulty? Mode { get; private set; }
        public int PuzzleIndex { get; private set; }
        public Puzzle Puzzle { get; private set; }
        public Model Model { get; private set; }
        public int Color { get; private set; }
        public Tool Tool { get; private set; }
        public int Score { get; private set; }
        public Phase Phase { get; private set; }
        /// <summary>결과 화면에서만 있다. 불가능 선언이면 ViewMatches가 null.</summary>
        public JudgeResult Result { get; private set; }
        public bool DeclaredImpossible { get; private set; }
        private bool showMineSetting;
        /// <summary>"내 모형의 평면도 표시". 연습(샘플) 모드에서만 켜진다.</summary>
        public bool ShowMine => showMineSetting && Mode == null;
        /// <summary>설정에 저장된 값(난이도 모드에서는 꺼져 보여도 켜져 있을 수 있다).</summary>
        public bool ShowMineSetting => showMineSetting;
        /// <summary>튜토리얼처럼 샘플·생성 문제가 아닌 문제를 푸는 중. 점수에 반영하지 않는다.</summary>
        public bool IsCustom { get; private set; }
        /// <summary>이 문제에서 "내 모형의 평면도 표시"를 켠 적이 있어 점수에 반영하지 않는다.</summary>
        public bool Unscored { get; private set; }
        /// <summary>결과 화면에서: 이번 문제의 시간 보너스(0 또는 1).</summary>
        public int TimeBonus { get; private set; }
        /// <summary>결과 화면에서: 이번 문제로 실제로 바뀐 점수(연습이면 0).</summary>
        public int LastDelta { get; private set; }
        public bool ShowingAnswer { get; private set; }
        /// <summary>문제를 새로 시작할 때마다 1씩 는다(같은 문제를 다시 열어도). 효과음이 새 문제를 알아보는 데 쓴다.</summary>
        public int PuzzleSerial { get; private set; }

        public event Action Changed;

        /// <param name="nowMs">현재 시각(ms). 테스트에서 시간을 조작할 때만 넘긴다.</param>
        public GameState(Func<double> nowMs = null)
        {
            if (nowMs == null)
            {
                var watch = Stopwatch.StartNew();
                nowMs = () => watch.Elapsed.TotalMilliseconds;
            }
            this.nowMs = nowMs;
        }

        // --- 파생 값 ---

        public int Remaining(int color) => Puzzle.Counts[color] - Model.CountColors()[color];
        public int UsedTotal => Model.TotalBlocks;
        public int RequiredTotal { get { int sum = 0; foreach (int count in Puzzle.Counts) sum += count; return sum; } }
        public bool CanUndo => Phase == Phase.Playing && past.Count > 0;
        public bool CanRedo => Phase == Phase.Playing && future.Count > 0;
        /// <summary>블록을 정확히 다 썼을 때만 제출할 수 있다(실수로 인한 감점 방지).</summary>
        public bool CanSubmit => Phase == Phase.Playing && UsedTotal == RequiredTotal;
        /// <summary>경과 시간. 결과 화면에서는 제출 시점에 멈춘다.</summary>
        public double ElapsedMs => Phase == Phase.Result ? resultElapsedMs : nowMs() - startedAt;

        // --- 문제 ---

        /// <summary>게임 시작. 점수를 0으로 하고 샘플 1번 또는 그 난이도(seed가 있으면 그 문제)를 낸다.</summary>
        public void Start(Difficulty? mode = null, uint? seed = null)
        {
            Score = 0;
            Load(mode, 0, seed);
        }

        public void ResetScore()
        {
            Score = 0;
            Changed?.Invoke();
        }

        /// <summary>모드를 바꾸면 그 모드의 새 문제가 바로 나온다. 점수는 유지한다.</summary>
        public void SetMode(Difficulty? mode) => Load(mode, 0, null);

        /// <summary>새 문제. 샘플 모드에서는 다음 샘플(마지막이면 처음으로).</summary>
        public void NewPuzzle() => Load(Mode, (PuzzleIndex + 1) % SamplePuzzles.All.Count, null);

        /// <summary>문제 코드로 받은 특정 생성 문제를 연다. 점수는 유지한다.</summary>
        public void OpenPuzzle(Difficulty mode, uint seed) => Load(mode, 0, seed);

        /// <summary>튜토리얼 문제를 연다. 연습 모드로 취급하고 점수에 반영하지 않는다.</summary>
        public void OpenCustom(Puzzle puzzle)
        {
            Mode = null;
            PuzzleIndex = 0;
            Puzzle = puzzle;
            IsCustom = true;
            BeginPuzzle();
        }

        private void Load(Difficulty? mode, int sampleIndex, uint? seed)
        {
            Mode = mode;
            PuzzleIndex = mode == null ? sampleIndex : 0;
            Puzzle = mode == null
                ? SamplePuzzles.All[PuzzleIndex]
                : Generator.Generate(mode.Value, seed ?? Generator.RandomSeed());
            IsCustom = false;
            BeginPuzzle();
        }

        /// <summary>현재 Puzzle을 빈 모형으로 시작한다: 기록 초기화, 타이머 시작.</summary>
        private void BeginPuzzle()
        {
            Model = Model.Empty(Puzzle.Size, Puzzle.Height);
            past.Clear();
            future.Clear();
            Color = Array.FindIndex(Puzzle.Counts, count => count > 0); // 개수가 있는 첫 색
            Tool = Tool.Place;
            Phase = Phase.Playing;
            Result = null;
            DeclaredImpossible = false;
            ShowingAnswer = false;
            Unscored = ShowMine || IsCustom;
            TimeBonus = LastDelta = 0;
            startedAt = nowMs();
            PuzzleSerial++;
            Changed?.Invoke();
        }

        // --- 편집 ---

        public void Place(int x, int z)
        {
            if (Remaining(Color) > 0) Commit(Model.Place(x, z, Color));
        }

        public void Paint(int x, int y, int z)
        {
            if (Remaining(Color) > 0) Commit(Model.Paint(x, y, z, Color));
        }

        public void Erase(int x, int y, int z) => Commit(Model.Remove(x, y, z));

        /// <summary>모형을 비운다. 실행 취소로 되돌릴 수 있다.</summary>
        public void Reset()
        {
            if (UsedTotal > 0) Commit(Model.Empty(Puzzle.Size, Puzzle.Height));
        }

        public void Undo()
        {
            if (!CanUndo) return;
            future.Push(Model);
            Model = past.Pop();
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo) return;
            past.Push(Model);
            Model = future.Pop();
            Changed?.Invoke();
        }

        private void Commit(Model next)
        {
            if (next == null || Phase != Phase.Playing) return;
            past.Push(Model);
            future.Clear();
            Model = next;
            Changed?.Invoke();
        }

        // --- 선택 ---

        /// <summary>문제에 없는 색은 선택할 수 없다.</summary>
        public void SetColor(int color)
        {
            if (!Colors.IsColor(color) || Puzzle.Counts[color] == 0) return;
            Color = color;
            Changed?.Invoke();
        }

        public void SetTool(Tool tool)
        {
            Tool = tool;
            Changed?.Invoke();
        }

        public void SetShowMine(bool value)
        {
            showMineSetting = value;
            if (ShowMine && Phase == Phase.Playing) Unscored = true;
            Changed?.Invoke();
        }

        // --- 제출 ---

        /// <summary>규칙 4: 모형 제출.</summary>
        public void Submit()
        {
            if (CanSubmit) Finish(Judge.ScoreSubmit(Model, Puzzle), false);
        }

        /// <summary>규칙 5: 불가능 선언.</summary>
        public void DeclareImpossible()
        {
            if (Phase == Phase.Playing) Finish(Judge.ScoreImpossible(Puzzle), true);
        }

        private void Finish(JudgeResult result, bool declaredImpossible)
        {
            resultElapsedMs = nowMs() - startedAt;
            Result = result;
            DeclaredImpossible = declaredImpossible;
            // 시간 보너스: 생성 문제에서 정답 제출을 기준 시간 안에 했을 때만(불가능 선언은 빨리 찍는 것을 보상하게 되므로 제외).
            bool fast = Mode != null && resultElapsedMs <= DifficultySettings.Get(Mode.Value).BonusSeconds * 1000.0;
            TimeBonus = !declaredImpossible && result.Correct && fast ? 1 : 0;
            LastDelta = Unscored ? 0 : result.Delta + TimeBonus;
            Score += LastDelta;
            Phase = Phase.Result;
            Changed?.Invoke();
        }

        /// <summary>결과 화면에서 정답 모형을 보여준다(불가능 문제는 정답이 없다).</summary>
        public void ShowAnswer()
        {
            if (Phase != Phase.Result || Puzzle.Answer == null) return;
            Model = Puzzle.Answer;
            ShowingAnswer = true;
            Changed?.Invoke();
        }
    }
}
