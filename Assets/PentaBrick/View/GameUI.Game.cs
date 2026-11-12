using System;
using System.Collections.Generic;
using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    /// <summary>게임 화면.</summary>
    public sealed partial class GameUI
    {
        /// <summary>게임 화면 위쪽에 겹쳐 보이는 튜토리얼 안내 카드.</summary>
        private void BuildTutorialCard()
        {
            var step = tutorial.Current;
            var card = new VisualElement();
            card.style.position = Position.Absolute;
            card.style.top = 12;
            card.style.left = card.style.right = 16;
            card.style.backgroundColor = PanelColor;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 14;
            Border(card, SelectColor, 2);
            center.Add(card);

            var head = Row(card);
            head.style.alignItems = Align.Center;
            Text(head, $"튜토리얼 {tutorial.Index + 1} / {tutorial.Count}", 11, true, SelectColor).style.marginRight = 10;
            Text(head, step.Title, 16, true).style.flexGrow = 1;
            Btn(head, "그만하기", () => Show(UiScreen.Main));
            Text(card, step.Text, 13).style.marginTop = 6;

            var buttons = Row(card);
            buttons.style.marginTop = 10;
            if (tutorial.NeedsRetry(state))
            {
                Text(buttons, "아쉽네요. 다시 해 보세요.  ", 12, true, BadColor);
                Btn(buttons, "다시 하기", () => { tutorial.Retry(state); dirty = true; });
            }
            else if (tutorial.IsLast)
            {
                var play = Btn(buttons, "게임 시작하기", () => Show(UiScreen.DifficultySelect));
                play.style.backgroundColor = SelectColor;
                play.style.color = Color.white;
            }
            else if (step.IsManual) Btn(buttons, "다음 →", () => { tutorial.Next(state); dirty = true; });
            else Text(buttons, "▶ 직접 해 보세요", 12, true, DimColor);
        }

        private void BuildGame()
        {
            if (state.Puzzle != builtPuzzle)
            {
                builtPuzzle = state.Puzzle;
                expandedView = null; // BlockEditor도 새 문제에서 기본 시점으로 돌아간다
            }

            BuildHud();

            var body = Row(root);
            body.style.flexGrow = 1;
            body.style.flexShrink = 1; // 남은 높이만 차지한다(오른쪽 패널이 길면 패널 안에서 스크롤)
            body.style.flexBasis = 0;
            body.pickingMode = PickingMode.Ignore;

            center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.style.flexGrow = 1;
            body.Add(center);
            if (expandedView != null) BuildExpandedView(expandedView.Value);
            if (tutorial != null) BuildTutorialCard();

            BuildControls(Panel(body, 290));
        }

        /// <summary>위쪽 HUD: 일시정지, 평면도 카드 다섯 장, 점수와 시간. 빈 곳은 3D 화면 클릭이 통과한다.</summary>
        private void BuildHud()
        {
            var hud = Row(root);
            hud.pickingMode = PickingMode.Ignore;
            hud.style.alignItems = Align.FlexStart;
            hud.style.paddingLeft = hud.style.paddingRight = hud.style.paddingTop = 10;

            var left = Row(hud);
            left.pickingMode = PickingMode.Ignore;
            left.style.width = 140;
            IconButton(left, "pause", Icon.Pause, "일시정지 (Esc)", OpenMenu);

            var cards = Row(hud);
            cards.pickingMode = PickingMode.Ignore;
            cards.style.flexGrow = 1;
            cards.style.justifyContent = Justify.Center;
            var mine = Projection.ProjectAll(state.Model);
            for (int i = 0; i < ViewNames.Length; i++) BuildViewCard(cards, (ViewDir)i, mine[i]);

            var right = Row(hud);
            right.pickingMode = PickingMode.Ignore;
            right.style.width = 140;
            right.style.justifyContent = Justify.FlexEnd;
            right.style.alignItems = Align.Center;
            right.style.height = 44;
            var score = Text(right, $"★ {state.Score}", 15, true);
            score.name = "score";
            score.style.marginRight = 12;
            timeLabel = Text(right, "", 15);
            timeLabel.name = "timer";
        }

        /// <summary>작은 평면도 카드. 누르면 그 방향으로 시점을 돌리고 크게 펼친다.</summary>
        private void BuildViewCard(VisualElement parent, ViewDir dir, int[,] mine)
        {
            var target = state.Puzzle.Views[(int)dir];
            bool match = state.ShowMine && Projection.ViewsEqual(mine, target); // 일치 여부는 힌트라 연습용 설정을 켰을 때만
            bool selected = expandedView == dir;
            var card = new Button(() => ToggleView(dir)) { name = "view-" + dir.ToString().ToLowerInvariant(), text = "", tooltip = ViewHints[(int)dir] };
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.minWidth = 48;
            card.style.marginLeft = card.style.marginRight = 4;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 4;
            card.style.backgroundColor = CardColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            Border(card, selected ? SelectColor : match ? GoodColor : LineColor, 2);
            if (match) card.AddToClassList("match");
            if (selected) card.AddToClassList("selected");
            parent.Add(card);
            Text(card, ViewNames[(int)dir], 10, true, DimColor).style.marginBottom = 2;
            int size = Mathf.Max(target.GetLength(0), target.GetLength(1));
            Grid(card, null, target, Mathf.Max(8, 40 / size));
        }

        /// <summary>펼친 평면도 카드: HUD 바로 아래 가운데에 크게. 내 모형 표시를 켰으면 나란히.</summary>
        private void BuildExpandedView(ViewDir dir)
        {
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = 0;
            holder.style.top = 8;
            holder.style.alignItems = Align.Center;
            center.Add(holder);

            var card = new VisualElement { name = "view-expanded" };
            card.style.flexDirection = FlexDirection.Row;
            card.style.backgroundColor = PanelColor;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;
            Border(card, SelectColor, 2);
            holder.Add(card);
            Grid(card, $"{ViewNames[(int)dir]} · 목표", state.Puzzle.Views[(int)dir], 28);
            if (state.ShowMine) Grid(card, "내 모형", Projection.Project(state.Model, dir), 28);
        }

        private void Grid(VisualElement parent, string caption, int[,] view, int cell)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(box);
            if (caption != null)
            {
                Text(box, caption, 11, false, DimColor).style.marginBottom = 4;
                if (parent.childCount > 1) box.style.marginLeft = 14;
            }
            for (int r = 0; r < view.GetLength(0); r++)
            {
                var row = Row(box);
                row.pickingMode = PickingMode.Ignore;
                for (int c = 0; c < view.GetLength(1); c++)
                {
                    var square = new VisualElement { pickingMode = PickingMode.Ignore };
                    square.style.width = square.style.height = cell;
                    square.style.backgroundColor = (Color)ColorValues[view[r, c]];
                    Border(square, PanelColor, 1);
                    row.Add(square);
                }
            }
        }

        /// <summary>같은 카드면 닫고 기본 시점으로, 아니면 펼치고 그 방향으로.</summary>
        private void ToggleView(ViewDir dir)
        {
            if (expandedView == dir)
            {
                ResetView();
                return;
            }
            expandedView = dir;
            editor.SetView(dir);
            dirty = true;
        }

        private void ResetView()
        {
            expandedView = null;
            editor.SetView(null);
            dirty = true;
        }

        /// <summary>조작 패널 (5.5, 5.6).</summary>
        private void BuildControls(VisualElement panel)
        {
            bool playing = state.Phase == Phase.Playing;
            var puzzle = state.Puzzle;

            if (!playing) BuildResult(panel); // 결과는 스크롤하지 않아도 보이게 맨 위에

            Header(panel, $"블록   사용 {state.UsedTotal} / {state.RequiredTotal}");
            var palette = Row(panel);
            palette.style.flexWrap = Wrap.Wrap;
            for (int color = 1; color <= Colors.Count; color++)
            {
                int c = color;
                var swatch = new Button(() => state.SetColor(c)) { text = $"{c} {ColorNames[c]}\n{state.Remaining(c)}/{puzzle.Counts[c]}" };
                swatch.style.width = 80;
                swatch.style.height = 42;
                swatch.style.fontSize = 12;
                swatch.style.color = Color.black;
                swatch.style.backgroundColor = (Color)ColorValues[c];
                Border(swatch, state.Color == c ? SelectColor : PanelColor, 3);
                swatch.SetEnabled(puzzle.Counts[c] > 0); // 문제에 없는 색은 비활성
                palette.Add(swatch);
            }

            Header(panel, "도구");
            var tools = Row(panel);
            Btn(tools, "놓기 (Q)", () => state.SetTool(Tool.Place), state.Tool == Tool.Place);
            Btn(tools, "칠하기 (W)", () => state.SetTool(Tool.Paint), state.Tool == Tool.Paint);
            Btn(tools, "지우기 (E)", () => state.SetTool(Tool.Erase), state.Tool == Tool.Erase);

            Header(panel, "편집");
            var edit = Row(panel);
            Btn(edit, "실행 취소", state.Undo).SetEnabled(state.CanUndo);
            Btn(edit, "다시 실행", state.Redo).SetEnabled(state.CanRedo);
            Btn(edit, "초기화", state.Reset).SetEnabled(playing && state.UsedTotal > 0);

            Header(panel, "제출");
            var submit = Row(panel);
            Btn(submit, "정답 제출 (±1)", state.Submit).SetEnabled(state.CanSubmit);
            Btn(submit, "불가능 (±2)", state.DeclareImpossible).SetEnabled(playing);
            if (playing && !state.CanSubmit) Text(panel, "블록을 정확히 다 써야 제출할 수 있습니다.", 11, false, DimColor);
        }

        /// <summary>결과 상자 (5.6).</summary>
        private void BuildResult(VisualElement panel)
        {
            var result = state.Result;
            var puzzle = state.Puzzle;
            string title, detail;
            if (!state.DeclaredImpossible && result.Correct)
            {
                title = "정답입니다";
                detail = "다섯 평면도와 블록 수가 모두 일치합니다.";
            }
            else if (!state.DeclaredImpossible)
            {
                title = "오답입니다";
                var wrong = new List<string>();
                for (int i = 0; i < ViewNames.Length; i++) if (!result.ViewMatches[i]) wrong.Add(ViewNames[i]);
                detail = wrong.Count > 0 ? "일치하지 않는 평면도: " + string.Join(", ", wrong) : "";
                if (!result.CountsMatch) detail += (detail == "" ? "" : "\n") + "색별 블록 수 불일치";
                if (!puzzle.Solvable) detail += "\n\n사실 불가능한 문제였습니다. " + puzzle.Reason;
            }
            else if (result.Correct)
            {
                title = "맞습니다, 불가능한 문제입니다";
                detail = puzzle.Reason;
            }
            else
            {
                title = "틀렸습니다, 구현 가능한 문제입니다";
                detail = "정답 모형을 확인해 보세요.";
            }

            var box = new VisualElement();
            box.style.backgroundColor = CardColor;
            box.style.marginTop = 10;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 10;
            Border(box, result.Correct ? GoodColor : BadColor, 2);
            panel.Add(box);

            Text(box, title, 14, true, result.Correct ? GoodColor : BadColor);
            string points = state.IsCustom ? "점수 미반영 (튜토리얼)"
                : state.Unscored ? "점수 미반영 (내 모형의 평면도 표시를 켠 문제)"
                : $"{(state.LastDelta > 0 ? "+" : state.LastDelta < 0 ? "−" : "")}{Math.Abs(state.LastDelta)}점" + (state.TimeBonus > 0 ? "  (시간 보너스 +1 포함)" : "");
            Text(box, points, 13, true).style.marginTop = 2;
            Text(box, detail, 12).style.marginTop = 4;
            int seconds = (int)(state.ElapsedMs / 1000);
            Text(box, $"소요 시간 {seconds / 60:00}:{seconds % 60:00}", 11, false, DimColor).style.marginTop = 4;
            var buttons = Row(box);
            buttons.style.marginTop = 6;
            if (puzzle.Answer != null && !state.ShowingAnswer) Btn(buttons, "정답 모형 보기", state.ShowAnswer);
            if (tutorial == null) Btn(buttons, "다음 문제", state.NewPuzzle); // 튜토리얼은 안내 카드로 진행한다
        }
    }
}
