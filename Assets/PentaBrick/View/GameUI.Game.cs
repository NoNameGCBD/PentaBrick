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
            holder.style.top = (portrait ? 340 : 126) + (expandedView == null ? 0 : 28 * expandedRows + 64);
            center.Add(holder);
            var card = new VisualElement { name = "tutorial-card" };
            card.style.width = Length.Percent(100);
            card.style.maxWidth = 420;
            card.style.backgroundColor = PanelColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 14;
            Glass(card);
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
                codeMessage = "";
                expandedView = null;
            }
            if (state.Revealed && editor.VisibleLayer >= 0) editor.SetVisibleLayer(-1);
            center = new VisualElement { name = "scene-surface", pickingMode = PickingMode.Ignore };
            Stretch(center);
            root.Add(center);
            BuildGridDirections();
            BuildHud();
            BuildBlueprints();
            if (expandedView != null) BuildExpandedView(expandedView.Value);
            if (tutorial != null) BuildTutorialCard();
            if (!state.Revealed) BuildTray();
            BuildNotice();
            if (state.Phase == Phase.Result) BuildResultPopup();
        }

        /// <summary>위쪽 HUD: 일시정지, 평면도 카드 다섯 장, 점수와 시간. 빈 곳은 3D 화면 클릭이 통과한다.</summary>
        private void BuildHud() => BuildArcadeTopHud();

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
            card.style.width = card.style.minWidth = CardWidth;
            card.style.height = CardHeight;
            card.style.flexShrink = 0;
            card.AddToClassList("blueprint-card");
            card.style.marginLeft = card.style.marginRight = 2;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 5;
            card.style.backgroundColor = CardColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            Border(card, wrong ? BadColor : selected ? SelectColor : match ? GoodColor : LineColor, 2);
            if (match) { card.AddToClassList("match"); GlowRing(card, GoodColor); }
            if (wrong) card.AddToClassList("wrong");
            if (selected) card.AddToClassList("selected");
            parent.Add(card);
            Text(card, BlueprintNames[(int)dir], 8, true, selected ? SelectColor : DimColor).style.marginBottom = 3;
            if (match) AddCornerBadge(card, "OK", GoodColor);
            int size = Mathf.Max(target.GetLength(0), target.GetLength(1));
            Grid(card, null, target, Mathf.Max(6, (portrait ? 34 : 40) / size));
        }

        /// <summary>펼친 평면도 카드: HUD 바로 아래 가운데에 크게. 내 모형 표시를 켰으면 나란히.</summary>
        private void BuildExpandedView(ViewDir dir)
        {
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = 0;
            holder.style.top = portrait ? 300 : 126;
            holder.style.alignItems = Align.Center;
            center.Add(holder);

            var card = new VisualElement { name = "view-expanded" };
            card.style.flexDirection = FlexDirection.Row;
            card.style.backgroundColor = PanelColor;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;
            Border(card, SelectColor, 2);
            holder.Add(card);
            Grid(card, $"{BlueprintNames[(int)dir]} · 목표", state.Puzzle.Views[(int)dir], 22);
            if (state.ShowMine) Grid(card, "내 모형", Projection.Project(state.Model, dir), 22);
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
                    Round(square, cell >= 20 ? 4 : 2);
                    if (view[r, c] != Colors.Empty) TileHighlight(square, cell);
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
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = portrait ? 10 : 22;
            holder.style.bottom = portrait ? 14 : 22;
            holder.style.alignItems = Align.Center;
            root.Add(holder);
            var tray = Row(holder);
            tray.name = "tray";
            tray.style.justifyContent = Justify.Center;
            tray.style.alignItems = Align.Center;
            tray.style.flexWrap = Wrap.Wrap;
            tray.style.maxWidth = Length.Percent(100);
            if (portrait) tray.style.width = Length.Percent(100); // 내용 폭에 맞추면 줄바꿈 높이를 잘못 계산한다(Yoga)
            tray.style.paddingLeft = tray.style.paddingRight = portrait ? 6 : 12;
            tray.style.paddingTop = portrait ? 10 : 12;
            tray.style.paddingBottom = 14;
            Glass(tray);
            VisualElement chips = portrait ? DockGroup(tray, "블록 인벤토리", true) : null;
            var tools = DockGroup(tray, "BUILD TOOLS");
            ToolButton(tools, "tool-place", Icon.Place, "놓기 (Q)", Tool.Place);
            ToolButton(tools, "tool-erase", Icon.Erase, "지우기: 위에 쌓인 블록까지 (E / 우클릭)", Tool.Erase);
            if (chips == null) chips = DockGroup(tray, "BLOCK INVENTORY");
            for (int slot = 1; slot <= state.Puzzle.Palette.Length; slot++) ColorChip(chips, state.Puzzle.Palette[slot - 1], slot);
            var edit = DockGroup(tray, "EDIT & VIEW");
            IconButton(edit, "undo", Icon.Undo, "실행 취소 (Ctrl+Z)", state.Undo).SetEnabled(state.CanUndo);
            IconButton(edit, "redo", Icon.Redo, "다시 실행 (Ctrl+Shift+Z)", state.Redo).SetEnabled(state.CanRedo);
            var reset = IconButton(edit, "reset-board", Icon.Restart, "전체 초기화 (R)", RequestBoardReset);
            KeyBadge(reset, "R");
            reset.SetEnabled(playing && state.UsedTotal > 0);
            IconButton(edit, "camera", Icon.Camera, "기본 시점", ResetView);
            var judge = DockGroup(tray, "READY TO SOLVE?", portrait); // 세로: 제출 줄을 따로 둬서 줄바꿈 높이가 정확하다
            var impossible = TrayButton(judge, "impossible", "불가능", state.DeclareImpossible, CardColor, TextColor);
            impossible.tooltip = "만들 수 없는 문제라고 선언 (맞으면 +2, 틀리면 −2, 다시 도전 없음)";
            impossible.SetEnabled(playing);
            var submit = TrayButton(judge, "submit", "정답 확인", state.Submit, new Color32(0x8b, 0xe9, 0x9a, 255), PageColor);
            submit.AddToClassList("arcade-submit");
            submit.tooltip = "지금 모형을 제출 (정답 +1, 오답은 감점 없이 다시 도전)";
            submit.style.minWidth = portrait ? 124 : 144;
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
            KeyBadge(button, tool == Tool.Place ? "Q" : "E");
            if (state.Tool != tool) return;
            button.AddToClassList("selected");
            Border(button, SelectColor, 2);
            GlowRing(button, SelectColor);
            button.style.backgroundColor = new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0.25f);
        }

        /// <summary>색 칩: 동그란 색 버튼. 색별 개수 제한은 없다.</summary>
        private void ColorChip(VisualElement parent, int color, int slot)
        {
            var chip = new Button(() => state.SetColor(color)) { name = "color-" + color, text = "", tooltip = $"{ColorNames[color]} ({slot})" };
            chip.style.width = chip.style.height = 44;
            chip.style.flexShrink = 0; // 줄이 좁아도 44px 터치 크기를 지킨다
            chip.style.marginLeft = chip.style.marginRight = 4;
            chip.style.paddingLeft = chip.style.paddingRight = chip.style.paddingTop = chip.style.paddingBottom = 0;
            Round(chip, 12);
            chip.AddToClassList("tactile");
            Bevel(chip, ColorValues[color]);
            KeyBadge(chip, slot.ToString()); // 숫자 키는 색 번호가 아니라 이 문제의 색 순서
            chip.style.backgroundColor = (Color)ColorValues[color];
            bool selected = state.Color == color;
            Border(chip, selected ? Color.white : PanelColor, 3);
            if (selected) { chip.AddToClassList("selected"); chip.AddToClassList("slot-selected"); GlowRing(chip, ColorValues[color]); }
            parent.Add(chip);
        }

        private Button TrayButton(VisualElement parent, string name, string text, Action onClick, Color background, Color textColor)
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
            button.AddToClassList("tactile");
            Bevel(button, background);
            button.style.backgroundImage = new StyleBackground(panelSheen);
            button.style.unityBackgroundImageTintColor = Color.white;
            parent.Add(button);
            return button;
        }

        /// <summary>
        /// 결과 팝업. 오답 제출이면 [다시 도전]이 기본이고, [답 확인하기]로 문제를 마치고 답을 볼 수 있다.
        /// 틀린 평면도는 카드에 빨간 테두리로 보인다.
        /// 답 확인 중이거나 튜토리얼 중이면(모형·안내 카드를 가리지 않게) 아래쪽 작은 띠로 접는다.
        /// </summary>
        private void BuildResultPopup()
        {
            var result = state.Result;
            var puzzle = state.Puzzle;
            bool good = result.Correct;
            bool retry = state.CanRetry;
            string title = !state.DeclaredImpossible
                ? (good ? "정답!" : "아직 달라요")
                : (good ? "맞습니다, 불가능한 문제" : "틀렸습니다, 만들 수 있는 문제");
            var notes = new List<string>();
            if (state.TimeBonus > 0) notes.Add("시간 보너스 +1");
            if (state.IsCustom) notes.Add("점수 미반영 (튜토리얼)");
            else if (state.Unscored && !retry) notes.Add("점수 미반영 (내 모형의 평면도 표시를 켠 문제)");
            if (retry)
            {
                if (!result.CountsMatch) notes.Add($"블록을 정확히 {state.RequiredTotal}개 써야 합니다 (지금 {state.UsedTotal}개)");
                if (result.ViewMatches != null && Array.IndexOf(result.ViewMatches, false) >= 0) notes.Add("빨간 테두리의 평면도가 다릅니다");
                notes.Add("감점 없이 이어서 다시 도전할 수 있습니다");
            }
            if (state.DeclaredImpossible && good) notes.Add(puzzle.Reason);
            int seconds = (int)(state.ElapsedMs / 1000);
            string points = $"{(state.LastDelta > 0 ? "+" : state.LastDelta < 0 ? "−" : "")}{Math.Abs(state.LastDelta)}";

            if (state.Revealed)
            {
                BuildRevealStrip();
                return;
            }
            if (tutorial != null)
            {
                var strip = ResultStrip(good ? GoodColor : BadColor);
                Text(strip, title, 15, true, good ? GoodColor : BadColor).style.marginRight = 10;
                IconButton(strip, "result-home", Icon.Home, "메인으로", () => Show(UiScreen.Main));
                return;
            }

            var box = Modal(420);
            resultPopupShown = true;
            modalOverlay.pickingMode = PickingMode.Ignore; // 상자 밖은 통과: 평면도 카드와 카메라 회전은 그대로 쓴다
            modalOverlay.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            box.name = "result";
            box.style.alignItems = Align.Center;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 16;
            Color accent = good ? GoodColor : retry ? SelectColor : BadColor;
            Border(box, accent, 2);
            if (good)
            {
                BuildClearEmblem(box);
                var clear = Text(box, "STAGE CLEAR!", portrait ? 32 : 38, true, new Color32(0xff, 0xd8, 0x70, 255));
                UseFont(clear, "Fredoka-Static");
                clear.AddToClassList("game-title");
                BuildConfetti(modalOverlay);
            }
            else Text(box, "TRY AGAIN", 30, true, accent).style.letterSpacing = 2;
            Text(box, title, good ? 16 : 20, true, accent);
            if (!retry) Text(box, points, 40, true).style.marginTop = 4;
            foreach (string note in notes)
            {
                var label = Text(box, note, 12, false, DimColor);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.marginTop = 4;
            }
            if (good)
            {
                var stats = Row(box);
                stats.style.marginTop = 14;
                ResultStat(stats, "TIME", $"{seconds / 60:00}:{seconds % 60:00}");
                ResultStat(stats, "BLOCKS", state.UsedTotal.ToString());
                ResultStat(stats, "HEIGHT", state.Puzzle.Height.ToString());
            }
            var buttons = Row(box);
            buttons.style.marginTop = 16;
            buttons.style.alignItems = Align.Center;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.justifyContent = Justify.Center;
            if (retry)
            {
                TrayButton(buttons, "result-retry", "다시 도전", state.Retry, SelectColor, PageColor).style.minWidth = 120;
                TrayButton(buttons, "result-reveal", "답 확인하기", state.Reveal, CardColor, TextColor);
            }
            else
            {
                TrayButton(buttons, "result-next", state.Mode == null ? "다음 샘플" : "다음 문제", state.NewPuzzle, GoodColor, Color.black).style.marginRight = 8;
                if (state.CanReveal) TrayButton(buttons, "result-reveal", "답 확인하기", state.Reveal, CardColor, TextColor).style.marginRight = 8;
                if (state.Mode != null) IconButton(buttons, "result-copy", Icon.Link, "문제 코드 복사", CopyShare);
                IconButton(buttons, "result-home", Icon.Home, "메인으로", () => Show(UiScreen.Main));
            }
            if (codeMessage != "") Text(box, codeMessage, 11, false, DimColor).style.marginTop = 8;
        }

        /// <summary>화면 아래쪽의 작은 결과 띠.</summary>
        private VisualElement ResultStrip(Color border)
        {
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = portrait ? 10 : 0;
            holder.style.bottom = 12;
            holder.style.alignItems = Align.Center;
            center.Add(holder);
            var strip = Row(holder);
            strip.name = "result";
            strip.style.alignItems = Align.Center;
            strip.style.justifyContent = Justify.Center;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.paddingLeft = strip.style.paddingRight = 12;
            strip.style.paddingTop = strip.style.paddingBottom = 8;
            Glass(strip);
            Round(strip, 14);
            Border(strip, border, 2);
            return strip;
        }

        /// <summary>답 확인 중: 정답 모형과 내가 배치한 모형을 번갈아 보고, 다음 문제로 넘어간다.</summary>
        private void BuildRevealStrip()
        {
            var strip = ResultStrip(SelectColor);
            var toggle = Row(strip);
            toggle.style.alignItems = Align.Center;
            toggle.style.marginTop = toggle.style.marginBottom = 4;
            if (state.Puzzle.Answer != null)
            {
                var answer = TrayButton(toggle, "result-show-answer", "정답 모형 보기", state.ShowAnswer,
                    state.ShowingAnswer ? SelectColor : CardColor, state.ShowingAnswer ? PageColor : TextColor);
                if (state.ShowingAnswer) answer.AddToClassList("selected");
            }
            else
            {
                var reason = Text(toggle, "만들 수 없는 문제였습니다. " + state.Puzzle.Reason, 12, false, DimColor);
                reason.style.whiteSpace = WhiteSpace.Normal;
                reason.style.maxWidth = portrait ? 340 : 420;
                reason.style.marginRight = 8;
            }
            var mine = TrayButton(toggle, "result-show-mine", "내가 배치한 모형 보기", state.ShowMyModel,
                !state.ShowingAnswer ? SelectColor : CardColor, !state.ShowingAnswer ? PageColor : TextColor);
            if (!state.ShowingAnswer) mine.AddToClassList("selected");
            var actions = Row(strip);
            actions.style.alignItems = Align.Center;
            actions.style.marginLeft = 8;
            actions.style.marginTop = actions.style.marginBottom = 4;
            TrayButton(actions, "result-next", state.Mode == null ? "다음 샘플" : "다음 문제", state.NewPuzzle, GoodColor, Color.black).style.marginRight = 6;
            if (state.Mode != null) IconButton(actions, "result-copy", Icon.Link, "문제 코드 복사", CopyShare);
            IconButton(actions, "result-home", Icon.Home, "메인으로", () => Show(UiScreen.Main));
        }

        private string notice;
        private float noticeUntil;

        /// <summary>잠깐 보이는 경고 문구(높이 제한, 블록 수 초과 등).</summary>
        public void Notice(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 2.2f;
            dirty = true;
        }

        private void BuildNotice()
        {
            if (notice == null) return;
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.right = 0;
            holder.style.bottom = portrait ? 200 : 104;
            holder.style.alignItems = Align.Center;
            center.Add(holder);
            var label = new Label(notice) { name = "notice", pickingMode = PickingMode.Ignore };
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = (Color)new Color32(0xff, 0xd8, 0x70, 255);
            label.style.backgroundColor = new Color(0.04f, 0.07f, 0.13f, 0.92f);
            label.style.paddingLeft = label.style.paddingRight = 14;
            label.style.paddingTop = label.style.paddingBottom = 7;
            Border(label, new Color(1f, 0.85f, 0.44f, 0.55f), 1);
            Round(label, 10);
            holder.Add(label);
        }

        private void UpdateNotice()
        {
            if (notice != null && Time.unscaledTime > noticeUntil)
            {
                notice = null;
                dirty = true;
            }
        }
    }
}
