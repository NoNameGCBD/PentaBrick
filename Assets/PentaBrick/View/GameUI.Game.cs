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
            // 3D 화면 위쪽 가운데. 평면도 카드를 펼쳤으면 그 아래로 내린다.
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = 16;
            holder.style.alignItems = Align.Center;
            int expandedRows = expandedView == null ? 0 : state.Puzzle.Views[(int)expandedView.Value].GetLength(0);
            holder.style.top = expandedView == null ? 12 : 8 + 28 * expandedRows + 60;
            center.Add(holder);
            var card = new VisualElement { name = "tutorial-card" };
            card.style.width = Length.Percent(100);
            card.style.maxWidth = 420;
            card.style.backgroundColor = PanelColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 14;
            Border(card, SelectColor, 2);
            holder.Add(card);

            var head = Row(card);
            head.style.alignItems = Align.Center;
            Text(head, $"튜토리얼 {tutorial.Index + 1} / {tutorial.Count}", 11, true, SelectColor).style.marginRight = 10;
            Text(head, step.Title, 16, true).style.flexGrow = 1;
            Btn(head, "그만하기", () => Show(UiScreen.Main)).name = "tutorial-stop";
            Text(card, step.Text, 13).style.marginTop = 6;

            var buttons = Row(card);
            buttons.style.marginTop = 10;
            if (tutorial.NeedsRetry(state))
            {
                Text(buttons, "아쉽네요. 다시 해 보세요.  ", 12, true, BadColor);
                Btn(buttons, "다시 하기", () => { tutorial.Retry(state); dirty = true; }).name = "tutorial-retry";
            }
            else if (tutorial.IsLast)
            {
                var play = Btn(buttons, "게임 시작하기", () => Show(UiScreen.DifficultySelect));
                play.name = "tutorial-play";
                play.style.backgroundColor = SelectColor;
                play.style.color = Color.white;
            }
            else if (step.IsManual) Btn(buttons, "다음 →", () => { tutorial.Next(state); dirty = true; }).name = "tutorial-next";
            else Text(buttons, "▶ 직접 해 보세요", 12, true, DimColor);
        }

        private void BuildGame()
        {
            if (state.Puzzle != builtPuzzle)
            {
                builtPuzzle = state.Puzzle;
                codeMessage = ""; // 이전 문제의 "복사했습니다" 문장을 남기지 않는다
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
            if (!state.ShowingAnswer) BuildTray(); // 정답 모형을 볼 때는 3D 화면을 넓게
            if (state.Phase == Phase.Result) BuildResultPopup();
        }

        /// <summary>위쪽 HUD: 일시정지, 평면도 카드 다섯 장, 점수와 시간. 빈 곳은 3D 화면 클릭이 통과한다.</summary>
        private void BuildHud()
        {
            var hud = Row(root);
            hud.name = "hud";
            hud.style.backgroundColor = PageColor; // 3D 카메라가 그리지 않는 띠라 불투명해야 지난 프레임 글자가 남지 않는다
            hud.style.paddingBottom = 8;
            hud.pickingMode = PickingMode.Ignore;
            hud.style.alignItems = Align.FlexStart;
            hud.style.flexWrap = Wrap.Wrap; // 세로 화면: 첫 줄 일시정지·시간, 둘째 줄 평면도 카드
            hud.style.paddingLeft = hud.style.paddingRight = hud.style.paddingTop = 10;

            var left = Row(hud);
            left.pickingMode = PickingMode.Ignore;
            left.style.width = portrait ? Length.Percent(50) : 140;
            IconButton(left, "pause", Icon.Pause, "일시정지 (Esc)", OpenMenu);

            var cards = Row(hud);
            cards.pickingMode = PickingMode.Ignore;
            cards.style.flexGrow = 1;
            cards.style.justifyContent = Justify.Center;
            var mine = Projection.ProjectAll(state.Model);
            for (int i = 0; i < ViewNames.Length; i++) BuildViewCard(cards, (ViewDir)i, mine[i]);

            var right = Row(hud);
            right.pickingMode = PickingMode.Ignore;
            right.style.width = portrait ? Length.Percent(50) : 140;
            right.style.justifyContent = Justify.FlexEnd;
            right.style.alignItems = Align.Center;
            right.style.height = 44;
            var score = Text(right, $"★ {state.Score}", 15, true);
            score.name = "score";
            score.style.marginRight = 12;
            timeLabel = Text(right, "", 15);
            timeLabel.name = "timer";
            if (portrait)
            {
                right.SendToBack(); // 순서: 일시정지 | 점수·시간 (첫 줄), 카드 (둘째 줄)
                left.SendToBack();
                cards.style.width = Length.Percent(100);
                cards.style.marginTop = 6;
            }
        }

        /// <summary>작은 평면도 카드. 누르면 그 방향으로 시점을 돌리고 크게 펼친다.</summary>
        private void BuildViewCard(VisualElement parent, ViewDir dir, int[,] mine)
        {
            var target = state.Puzzle.Views[(int)dir];
            bool match = state.ShowMine && Projection.ViewsEqual(mine, target); // 일치 여부는 힌트라 연습용 설정을 켰을 때만
            bool selected = expandedView == dir;
            bool wrong = state.Phase == Phase.Result && state.Result.ViewMatches != null && !state.Result.ViewMatches[(int)dir];
            var card = new Button(() => ToggleView(dir)) { name = "view-" + dir.ToString().ToLowerInvariant(), text = "", tooltip = ViewHints[(int)dir] };
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.minWidth = 48;
            card.style.marginLeft = card.style.marginRight = 4;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 4;
            card.style.backgroundColor = CardColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            Border(card, wrong ? BadColor : selected ? SelectColor : match ? GoodColor : LineColor, 2);
            if (match) card.AddToClassList("match");
            if (wrong) card.AddToClassList("wrong");
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

        /// <summary>아래 트레이: 도구 | 색 칩 | 실행 취소·다시 실행·기본 시점 | 불가능·제출.</summary>
        private void BuildTray()
        {
            bool playing = state.Phase == Phase.Playing;
            var puzzle = state.Puzzle;
            var tray = Row(root);
            tray.name = "tray";
            tray.style.backgroundColor = PanelColor;
            tray.style.justifyContent = Justify.Center;
            tray.style.alignItems = Align.Center;
            tray.style.flexWrap = Wrap.Wrap;
            tray.style.paddingTop = tray.style.paddingBottom = 8;
            tray.style.paddingLeft = tray.style.paddingRight = 8;
            tray.style.borderTopWidth = 1;
            tray.style.borderTopColor = LineColor;

            VisualElement chips = null;
            if (portrait) chips = Group(tray); // 세로 화면: 색 칩을 첫 줄에
            var tools = Group(tray);
            ToolButton(tools, "tool-place", Icon.Place, "놓기 (Q)", Tool.Place);
            ToolButton(tools, "tool-paint", Icon.Paint, "칠하기 (W)", Tool.Paint);
            ToolButton(tools, "tool-erase", Icon.Erase, "지우기 (E / 우클릭)", Tool.Erase);

            if (chips == null) chips = Group(tray);
            for (int color = 1; color <= Colors.Count; color++)
                if (puzzle.Counts[color] > 0) ColorChip(chips, color);

            var edit = Group(tray);
            IconButton(edit, "undo", Icon.Undo, "실행 취소 (Ctrl+Z)", state.Undo).SetEnabled(state.CanUndo);
            IconButton(edit, "redo", Icon.Redo, "다시 실행 (Ctrl+Shift+Z)", state.Redo).SetEnabled(state.CanRedo);
            IconButton(edit, "camera", Icon.Camera, "기본 시점", ResetView);

            var judge = Group(tray);
            var impossible = TrayButton(judge, "impossible", "불가능", state.DeclareImpossible, CardColor, TextColor);
            impossible.tooltip = "만들 수 없는 문제라고 선언 (맞으면 +2, 틀리면 −2)";
            impossible.SetEnabled(playing);
            var submit = TrayButton(judge, "submit", "제출", state.Submit, GoodColor, Color.black);
            submit.tooltip = "블록을 모두 쓴 모형을 제출 (정답 +1, 오답 −1)";
            submit.style.minWidth = 96;
            submit.SetEnabled(state.CanSubmit);
        }

        /// <summary>트레이 안의 묶음. 묶음 사이를 띄우고, 화면이 좁으면 묶음 단위로 줄을 바꾼다.</summary>
        private static VisualElement Group(VisualElement tray)
        {
            var group = Row(tray);
            group.style.alignItems = Align.Center;
            group.style.marginLeft = group.style.marginRight = 10;
            group.style.marginTop = group.style.marginBottom = 3;
            return group;
        }

        private void ToolButton(VisualElement parent, string name, Icon icon, string tooltip, Tool tool)
        {
            var button = IconButton(parent, name, icon, tooltip, () => state.SetTool(tool));
            if (state.Tool != tool) return;
            button.AddToClassList("selected");
            Border(button, SelectColor, 2);
            button.style.backgroundColor = new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0.25f);
        }

        /// <summary>색 칩: 동그란 색 버튼, 오른쪽 아래에 남은 개수.</summary>
        private void ColorChip(VisualElement parent, int color)
        {
            int remaining = state.Remaining(color);
            var chip = new Button(() => state.SetColor(color)) { name = "color-" + color, text = "", tooltip = $"{ColorNames[color]} ({color})" };
            chip.style.width = chip.style.height = 44;
            chip.style.flexShrink = 0; // 줄이 좁아도 44px 터치 크기를 지킨다
            chip.style.marginLeft = chip.style.marginRight = 4;
            chip.style.paddingLeft = chip.style.paddingRight = chip.style.paddingTop = chip.style.paddingBottom = 0;
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 22;
            chip.style.backgroundColor = (Color)ColorValues[color];
            bool selected = state.Color == color;
            Border(chip, selected ? Color.white : PanelColor, 3);
            if (selected) chip.AddToClassList("selected");
            if (remaining == 0) chip.style.opacity = 0.35f;
            parent.Add(chip);

            var badge = new Label(remaining.ToString()) { pickingMode = PickingMode.Ignore };
            badge.style.position = Position.Absolute;
            badge.style.right = -6;
            badge.style.bottom = -6;
            badge.style.minWidth = 20;
            badge.style.fontSize = 11;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.color = TextColor;
            badge.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 10;
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            chip.Add(badge);
        }

        private static Button TrayButton(VisualElement parent, string name, string text, Action onClick, Color background, Color textColor)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.style.height = 44;
            button.style.paddingLeft = button.style.paddingRight = 16;
            button.style.flexShrink = 0; // 줄이 좁아도 44px 터치 크기를 지킨다
            button.style.marginLeft = button.style.marginRight = 4;
            button.style.fontSize = 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = textColor;
            button.style.backgroundColor = background;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 12;
            Border(button, LineColor, 1);
            parent.Add(button);
            return button;
        }

        /// <summary>
        /// 결과 팝업: 큰 제목과 점수, 다음 문제. 틀린 평면도는 위쪽 카드에 빨간 테두리로 보인다.
        /// 정답 모형을 보는 중이거나 튜토리얼 중이면(안내 카드를 가리지 않게) 아래쪽 작은 띠로 접는다.
        /// </summary>
        private void BuildResultPopup()
        {
            var result = state.Result;
            var puzzle = state.Puzzle;
            bool good = result.Correct;
            string title = !state.DeclaredImpossible
                ? (good ? "정답!" : "오답")
                : (good ? "맞습니다, 불가능한 문제" : "틀렸습니다, 만들 수 있는 문제");
            var notes = new List<string>();
            if (state.TimeBonus > 0) notes.Add("시간 보너스 +1");
            if (state.IsCustom) notes.Add("점수 미반영 (튜토리얼)");
            else if (state.Unscored) notes.Add("점수 미반영 (내 모형의 평면도 표시를 켠 문제)");
            if (state.DeclaredImpossible && good) notes.Add(puzzle.Reason);
            if (!state.DeclaredImpossible && !good && !puzzle.Solvable) notes.Add("사실 불가능한 문제였습니다. " + puzzle.Reason);
            int seconds = (int)(state.ElapsedMs / 1000);
            string points = $"{(state.LastDelta > 0 ? "+" : state.LastDelta < 0 ? "−" : "")}{Math.Abs(state.LastDelta)}";

            if (state.ShowingAnswer || tutorial != null)
            {
                var holder = new VisualElement { pickingMode = PickingMode.Ignore };
                holder.style.position = Position.Absolute;
                holder.style.left = holder.style.right = 0;
                holder.style.bottom = 12;
                holder.style.alignItems = Align.Center;
                center.Add(holder);
                var strip = Row(holder);
                strip.name = "result";
                strip.style.alignItems = Align.Center;
                strip.style.backgroundColor = PanelColor;
                strip.style.paddingLeft = strip.style.paddingRight = 12;
                strip.style.paddingTop = strip.style.paddingBottom = 8;
                strip.style.borderTopLeftRadius = strip.style.borderTopRightRadius = strip.style.borderBottomLeftRadius = strip.style.borderBottomRightRadius = 12;
                Border(strip, good ? GoodColor : BadColor, 2);
                Text(strip, title, 15, true, good ? GoodColor : BadColor).style.marginRight = 10;
                Text(strip, points, 15, true).style.marginRight = 12;
                ResultButtons(strip);
                return;
            }

            var box = Modal(360);
            resultPopupShown = true;
            modalOverlay.pickingMode = PickingMode.Ignore; // 상자 밖은 통과: 평면도 카드와 카메라 회전은 그대로 쓴다
            modalOverlay.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            box.name = "result";
            box.style.alignItems = Align.Center;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 16;
            Border(box, good ? GoodColor : BadColor, 2);
            Text(box, title, 22, true, good ? GoodColor : BadColor);
            Text(box, points, 40, true).style.marginTop = 4;
            foreach (string note in notes)
            {
                var label = Text(box, note, 12, false, DimColor);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.marginTop = 4;
            }
            Text(box, $"{seconds / 60:00}:{seconds % 60:00}", 11, false, DimColor).style.marginTop = 6;
            var buttons = Row(box);
            buttons.style.marginTop = 14;
            buttons.style.alignItems = Align.Center;
            ResultButtons(buttons);
            if (codeMessage != "") Text(box, codeMessage, 11, false, DimColor).style.marginTop = 8;
        }

        /// <summary>결과의 버튼: 다음 문제(튜토리얼 제외), 정답 보기, 코드 복사, 메인.</summary>
        private void ResultButtons(VisualElement parent)
        {
            if (tutorial == null)
            {
                var next = TrayButton(parent, "result-next", state.Mode == null ? "다음 샘플" : "다음 문제", state.NewPuzzle, GoodColor, Color.black);
                next.style.marginRight = 8;
            }
            if (state.Puzzle.Answer != null && !state.ShowingAnswer) IconButton(parent, "result-answer", Icon.Eye, "정답 모형 보기", state.ShowAnswer);
            if (state.Mode != null) IconButton(parent, "result-copy", Icon.Link, "문제 코드 복사", CopyShare);
            IconButton(parent, "result-home", Icon.Home, "메인으로", () => Show(UiScreen.Main));
        }
    }
}
