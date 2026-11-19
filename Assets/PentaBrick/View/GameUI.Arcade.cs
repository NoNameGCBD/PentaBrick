using System;
using System.Collections.Generic;
using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    public sealed partial class GameUI
    {
        private static readonly string[] BlueprintNames = { "TOP", "FRONT", "BACK", "LEFT", "RIGHT" };
        private string utilityPopup;
        private Texture2D panelSheen;
        private readonly List<VisualElement> confetti = new List<VisualElement>();

        private static void Stretch(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = element.style.right = element.style.top = element.style.bottom = 0;
        }

        private static void UseFont(VisualElement element, string name)
        {
            var font = Resources.Load<Font>("Fonts/" + name);
            if (font != null) element.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        // A translucent gradient surface: supported by both UI Toolkit and WebGL.
        private void Glass(VisualElement element)
        {
            element.style.backgroundColor = new Color(0.055f, 0.09f, 0.16f, 0.92f);
            if (panelSheen == null)
            {
                panelSheen = new Texture2D(1, 64, TextureFormat.RGBA32, false);
                panelSheen.name = "HUD glass sheen";
                panelSheen.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 64; y++) panelSheen.SetPixel(0, y, new Color(0.6f, 0.78f, 1, 0.035f + y / 63f * 0.065f));
                panelSheen.Apply();
            }
            element.style.backgroundImage = new StyleBackground(panelSheen);
            Round(element, 22);
            Border(element, new Color(0.57f, 0.73f, 0.91f, 0.28f), 1);
            element.style.borderTopColor = new Color(0.67f, 0.85f, 1, 0.48f);
            element.style.borderBottomWidth = 1;
            element.style.borderBottomColor = new Color(0.4f, 0.56f, 0.72f, 0.2f);
        }

        private void ReleaseArcadeTextures()
        {
            if (panelSheen != null) Destroy(panelSheen);
        }

        private static void Bevel(VisualElement element, Color color)
        {
            Border(element, Color.Lerp(color, Color.white, 0.2f), 1);
            element.style.borderTopColor = Color.Lerp(color, Color.white, 0.48f);
            element.style.borderBottomColor = Color.Lerp(color, Color.black, 0.18f);
            element.style.borderBottomWidth = 1;
            if (!(element is Button)) return;
            element.style.overflow = Overflow.Visible;
            // 자식 요소를 추가하면 글자 버튼의 자동 너비 계산이 바뀌므로 직접 그린다.
            element.generateVisualContent += context =>
            {
                float w = element.layout.width, h = element.layout.height;
                if (w <= 0 || h <= 0) return;
                var painter = context.painter2D;
                painter.lineWidth = 1.5f;
                float radius = Mathf.Min(w, h) * 0.25f;
                // 얇고 투명한 곡선을 겹쳐 아래쪽으로 퍼지는 그림자를 만든다.
                for (int i = 7; i >= 1; i--)
                {
                    float spread = i * 0.85f;
                    float left = -spread, right = w + spread;
                    float bottom = h + 1.5f + spread;
                    painter.strokeColor = new Color(0, 0, 0, 0.07f * Mathf.Pow(1 - i / 8f, 2));
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(left, bottom - radius));
                    painter.QuadraticCurveTo(new Vector2(left, bottom), new Vector2(left + radius, bottom));
                    painter.LineTo(new Vector2(right - radius, bottom));
                    painter.QuadraticCurveTo(new Vector2(right, bottom), new Vector2(right, bottom - radius));
                    painter.Stroke();
                }
            };
        }

        private static void GlowRing(VisualElement element, Color color)
        {
            var halo = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(halo);
            halo.style.left = halo.style.right = halo.style.top = halo.style.bottom = -5;
            Round(halo, 16);
            Border(halo, new Color(color.r, color.g, color.b, 0.24f), 3);
            element.Add(halo);
        }

        private static void KeyBadge(VisualElement button, string key)
        {
            var badge = new Label(key) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("key-badge");
            badge.style.position = Position.Absolute;
            badge.style.top = -5;
            badge.style.left = -3;
            badge.style.backgroundColor = (Color)new Color32(0x0c, 0x14, 0x24, 255);
            badge.style.color = TextColor;
            badge.style.fontSize = 9;
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            Round(badge, 4);
            UseFont(badge, "SpaceGrotesk-Static");
            button.Add(badge);
        }

        private static void AddCornerBadge(VisualElement element, string text, Color color)
        {
            var badge = new Label(text) { pickingMode = PickingMode.Ignore };
            badge.style.position = Position.Absolute;
            badge.style.right = -4;
            badge.style.top = -6;
            badge.style.backgroundColor = color;
            badge.style.color = PageColor;
            badge.style.fontSize = 8;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            Round(badge, 5);
            element.Add(badge);
        }

        private static void TileHighlight(VisualElement square, int cell)
        {
            var shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.style.position = Position.Absolute;
            shine.style.left = shine.style.right = 1;
            shine.style.top = 1;
            shine.style.height = Mathf.Max(1, cell * 0.12f);
            shine.style.backgroundColor = new Color(1, 1, 1, 0.26f);
            Round(shine, 2);
            square.Add(shine);
            square.style.borderBottomColor = new Color(0, 0, 0, 0.35f);
            square.style.borderBottomWidth = cell >= 20 ? 3 : 2;
        }

        private VisualElement DockGroup(VisualElement parent, string caption, bool fullRow = false)
        {
            var group = new VisualElement { pickingMode = PickingMode.Ignore };
            group.style.alignItems = Align.Center;
            group.style.marginLeft = group.style.marginRight = portrait ? 4 : 7;
            group.style.marginTop = group.style.marginBottom = 4;
            if (fullRow) group.style.width = Length.Percent(100);
            // 글자 제목 대신 얇은 구분선으로 묶음을 나눈다.
            if (!portrait && parent.childCount > 0)
            {
                group.style.borderLeftWidth = 1;
                group.style.borderLeftColor = new Color(0.57f, 0.73f, 0.91f, 0.16f);
                group.style.paddingLeft = 10;
            }
            parent.Add(group);
            var buttons = Row(group);
            buttons.pickingMode = PickingMode.Ignore;
            buttons.style.justifyContent = Justify.Center;
            return buttons;
        }

        private void BuildArcadeTopHud()
        {
            var hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            hud.style.position = Position.Absolute;
            hud.style.top = 18;
            hud.style.left = hud.style.right = portrait ? 10 : 18;
            hud.style.alignItems = Align.Center;
            root.Add(hud);
            var capsule = Row(hud);
            capsule.name = "top-capsule";
            capsule.style.alignItems = Align.Center;
            capsule.style.justifyContent = Justify.Center;
            capsule.style.flexWrap = Wrap.Wrap;
            capsule.style.paddingLeft = capsule.style.paddingRight = portrait ? 10 : 18;
            capsule.style.paddingTop = capsule.style.paddingBottom = portrait ? 10 : 9;
            if (portrait) capsule.style.width = Length.Percent(100);
            Glass(capsule);
            var clock = new VisualElement();
            capsule.Add(clock);
            timeLabel = Text(clock, "00:00", portrait ? 15 : 20, true);
            timeLabel.name = "timer";
            UseFont(timeLabel, "SpaceGrotesk-Static");
            var score = Text(clock, $"★ {state.Score}", 10, true, new Color32(0xff, 0xd8, 0x70, 255));
            score.name = "score";
            var layers = Row(capsule);
            layers.name = "layer-controls";
            layers.style.alignItems = Align.Center;
            layers.style.marginLeft = portrait ? 0 : 20;
            if (portrait)
            {
                layers.style.width = Length.Percent(100);
                layers.style.justifyContent = Justify.Center;
                layers.style.marginTop = 10;
            }
            for (int layer = -1; layer < state.Puzzle.Height; layer++)
            {
                int selectedLayer = layer;
                bool selected = editor.VisibleLayer == layer;
                var button = Btn(layers, layer < 0 ? "전체" : $"{layer + 1}층", () => { editor.SetVisibleLayer(selectedLayer); dirty = true; }, selected);
                button.name = layer < 0 ? "layer-all" : "layer-" + (layer + 1);
                button.tooltip = layer < 0 ? "전체 모형 보기" : $"{layer + 1}층만 보기 · 다른 층 숨김";
                button.style.height = portrait ? 36 : 32;
                button.style.minWidth = 42;
                Round(button, 9);
                if (selected) { button.AddToClassList("selected"); button.style.color = PageColor; button.style.backgroundColor = SelectColor; }
            }
            var pause = new VisualElement();
            pause.style.position = Position.Absolute;
            pause.style.left = 0;
            pause.style.top = portrait ? 126 : 8;
            hud.Add(pause);
            IconButton(pause, "pause", Icon.Pause, "일시정지 (Esc)", OpenMenu);
            var utility = Row(hud);
            utility.style.position = Position.Absolute;
            utility.style.right = 0;
            utility.style.top = portrait ? 126 : 8;
            utility.style.flexDirection = portrait ? FlexDirection.Row : FlexDirection.Column;
            UtilityButton(utility, "hud-new", Icon.Restart, "새 문제 생성", () => OpenUtility("new"));
            UtilityButton(utility, "hud-share", Icon.Link, "문제 공유", () => OpenUtility("share"));
            UtilityButton(utility, "hud-help", Icon.Help, "게임 방법", () => OpenUtility("help"));
        }

        private void UtilityButton(VisualElement parent, string name, Icon icon, string tip, Action action)
        {
            var button = IconButton(parent, name, icon, tip, action);
            button.style.marginBottom = 9;
        }

        private void BuildBlueprints()
        {
            var holder = new VisualElement { name = "blueprint-holder", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = portrait ? 10 : 22;
            holder.style.top = portrait ? 208 : 112;
            if (portrait) holder.style.right = 10;
            root.Add(holder);
            var panel = new VisualElement { name = "blueprint-panel" };
            panel.style.paddingLeft = panel.style.paddingRight = portrait ? 9 : 12;
            panel.style.paddingTop = panel.style.paddingBottom = 12;
            Glass(panel);
            holder.Add(panel);
            var mine = state.ShowMine ? Projection.ProjectAll(state.Model) : null;
            void Card(VisualElement row, ViewDir dir) => BuildViewCard(row, dir, mine == null ? null : mine[(int)dir]);
            void Spacer(VisualElement row)
            {
                var blank = new VisualElement { pickingMode = PickingMode.Ignore };
                blank.style.width = CardWidth + 4;
                row.Add(blank);
            }
            if (portrait)
            {
                var row = Row(panel);
                row.style.justifyContent = Justify.Center;
                BlockCountTile(row);
                for (int i = 0; i < 5; i++) Card(row, (ViewDir)i);
            }
            else
            {
                // 전개도 왼쪽 위 빈자리에 블록 수를 크게 둔다.
                var back = Row(panel); BlockCountTile(back); Card(back, ViewDir.Back); Spacer(back);
                var middle = Row(panel); Card(middle, ViewDir.Left); Card(middle, ViewDir.Top); Card(middle, ViewDir.Right);
                var front = Row(panel); Spacer(front); Card(front, ViewDir.Front); Spacer(front);
            }
        }

        private int CardWidth => portrait ? 54 : 64;
        private int CardHeight => portrait ? 60 : 68;

        /// <summary>문제에서 써야 하는 블록 수(크게)와 지금까지 놓은 수. 평면도 카드와 같은 크기의 칸.</summary>
        private void BlockCountTile(VisualElement row)
        {
            int used = state.UsedTotal, total = state.RequiredTotal;
            var tile = new VisualElement { name = "block-count", pickingMode = PickingMode.Ignore };
            tile.style.width = CardWidth;
            tile.style.height = CardHeight;
            tile.style.flexShrink = 0;
            tile.style.marginLeft = tile.style.marginRight = 2;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.backgroundColor = new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0.12f);
            Border(tile, new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0.45f), 1);
            Round(tile, 8);
            row.Add(tile);
            foreach (var label in new[]
            {
                Text(tile, "블록", 8, true, SelectColor),
                Text(tile, total.ToString(), portrait ? 20 : 24, true, TextColor),
                Text(tile, $"{used} / {total}", 9, true, used == total ? GoodColor : DimColor),
            })
            {
                label.style.marginTop = label.style.marginBottom = 0;
                label.style.paddingTop = label.style.paddingBottom = 0;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            var number = (Label)tile[1];
            number.name = "block-total";
            UseFont(number, "SpaceGrotesk-Static");
            var counter = (Label)tile[2];
            counter.name = "block-counter";
            UseFont(counter, "SpaceGrotesk-Static");
        }

        private readonly Label[] gridDirectionLabels = new Label[4];
        private static readonly Vector3[] GridEdges = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };

        private void BuildGridDirections()
        {
            var holder = new VisualElement { name = "grid-directions", pickingMode = PickingMode.Ignore };
            Stretch(holder);
            center.Add(holder);
            string[] names = { "front", "back", "left", "right" };
            string[] captions = { "FRONT", "BACK", "LEFT", "RIGHT" };
            for (int i = 0; i < 4; i++)
            {
                var label = new Label(captions[i]) { name = "grid-" + names[i], pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.paddingLeft = label.style.paddingRight = 7;
                label.style.height = 22;
                label.style.fontSize = 10;
                label.style.letterSpacing = 0.5f;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                label.style.color = i == 0 ? SelectColor : TextColor;
                label.style.backgroundColor = new Color(0.04f, 0.07f, 0.13f, 0.78f);
                Border(label, new Color(0.45f, 0.65f, 0.8f, 0.24f), 1);
                Round(label, 8);
                holder.Add(label);
                gridDirectionLabels[i] = label;
            }
        }

        private void UpdateGridDirections()
        {
            var camera = Camera.main;
            float width = root.worldBound.width, height = root.worldBound.height;
            if (camera == null || width <= 0 || height <= 0) return;
            Vector3 middle = camera.WorldToViewportPoint(Vector3.zero);
            float edge = state.Puzzle.Size / 2f + 0.25f;
            for (int i = 0; i < gridDirectionLabels.Length; i++)
            {
                var label = gridDirectionLabels[i];
                if (label?.panel == null) continue;
                // 모형의 앞은 월드 -Z이며, 회전해도 방향의 기준은 격자에 고정된다.
                Vector3 point = camera.WorldToViewportPoint(GridEdges[i] * edge);
                bool visible = point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                Vector2 outward = new Vector2((point.x - middle.x) * width, -(point.y - middle.y) * height).normalized * 16;
                label.style.left = point.x * width + outward.x;
                label.style.top = (1 - point.y) * height + outward.y;
            }
        }

        public void RequestBoardReset()
        {
            if (state.Phase == Phase.Playing && state.UsedTotal > 0)
                LeavePuzzle("놓은 블록을 모두 치울까요?", state.Reset, "실행 취소로 되돌릴 수 있습니다.");
        }

        private void OpenUtility(string popup)
        {
            utilityPopup = popup;
            codeMessage = "";
            RefreshInput();
            dirty = true;
        }

        private void CloseUtility()
        {
            utilityPopup = null;
            RefreshInput();
            dirty = true;
        }

        private void BuildUtilityPopup()
        {
            var box = Modal(420);
            box.name = "utility-" + utilityPopup;
            var head = Row(box);
            head.style.alignItems = Align.Center;
            Text(head, utilityPopup == "new" ? "새로운 도전" : utilityPopup == "share" ? "같은 퍼즐, 함께 도전" : "다섯 시선을 하나로", 21, true).style.flexGrow = 1;
            IconButton(head, "utility-close", Icon.Erase, "닫기 (Esc)", CloseUtility, 36);
            if (utilityPopup == "new")
            {
                Text(box, "현재 난이도로 새로운 문제를 만듭니다. 놓은 블록과 시간은 초기화됩니다.", 13, false, DimColor).style.marginTop = 16;
                var button = TrayButton(box, "utility-new-confirm", "새 문제 시작", () => { CloseUtility(); state.NewPuzzle(); }, SelectColor, PageColor);
                button.style.marginTop = 20;
            }
            else if (utilityPopup == "share")
            {
                Text(box, state.Puzzle.Id, 24, true, SelectColor).style.marginTop = 20;
                if (state.Mode != null) TrayButton(box, "utility-copy", "문제 코드 / 링크 복사", CopyShare, SelectColor, PageColor);
                else Text(box, "연습 샘플은 모든 플레이어에게 같은 순서로 제공됩니다.", 13, false, DimColor).style.marginTop = 10;
                if (codeMessage != "") Text(box, codeMessage, 11, false, GoodColor).style.marginTop = 12;
            }
            else
            {
                Text(box, "01  관찰하기", 15, true, SelectColor).style.marginTop = 18;
                Text(box, "다섯 청사진을 보고, 각 방향에서 처음 보이는 블록의 색을 맞추세요.", 13);
                Text(box, "02  쌓아보기", 15, true, SelectColor).style.marginTop = 14;
                Text(box, "색을 고르고 작업대를 클릭하세요. Q 놓기 · E 지우기(위에 쌓인 블록까지) · R 초기화. 격자 위 상한선 높이까지 쌓을 수 있습니다. 드래그로 회전하고 휠로 확대합니다.", 13);
                Text(box, "03  정답 확인", 15, true, SelectColor).style.marginTop = 14;
                Text(box, "평면도 옆의 블록 수를 정확히 쓰고 다섯 방향을 만족시키세요. 색별 개수는 자유입니다. 틀려도 감점 없이 다시 도전하거나 답을 확인할 수 있습니다. 모순된 문제라면 ‘불가능’을 선언하세요.", 13);
                var tutorialButton = TrayButton(box, "utility-tutorial", "직접 배우는 튜토리얼", () => { CloseUtility(); LeavePuzzle("튜토리얼을 시작할까요?", StartTutorial); }, SelectColor, PageColor);
                tutorialButton.style.marginTop = 18;
            }
        }

        private void ResultStat(VisualElement parent, string label, string value)
        {
            var stat = new VisualElement();
            stat.style.alignItems = Align.Center;
            stat.style.minWidth = portrait ? 90 : 105;
            parent.Add(stat);
            Text(stat, label, 9, true, DimColor).style.letterSpacing = 1;
            var number = Text(stat, value, 21, true, TextColor);
            UseFont(number, "SpaceGrotesk-Static");
        }

        private void BuildClearEmblem(VisualElement parent)
        {
            var blocks = Row(parent);
            blocks.style.marginBottom = 14;
            for (int i = 0; i < 5; i++)
            {
                var block = new VisualElement { pickingMode = PickingMode.Ignore };
                block.style.width = block.style.height = i == 2 ? 34 : 24;
                block.style.marginLeft = block.style.marginRight = 4;
                block.style.backgroundColor = (Color)ColorValues[i + 1];
                Round(block, 6); Bevel(block, ColorValues[i + 1]);
                blocks.Add(block);
            }
        }

        private void BuildConfetti(VisualElement overlay)
        {
            for (int i = 0; i < 28; i++)
            {
                var particle = new VisualElement { pickingMode = PickingMode.Ignore };
                particle.style.position = Position.Absolute;
                particle.style.left = Length.Percent(12 + (i * 37 % 76));
                particle.style.top = Length.Percent(15 + (i * 13 % 60));
                particle.style.width = i % 2 == 0 ? 7 : 5;
                particle.style.height = i % 2 == 0 ? 5 : 12;
                particle.style.backgroundColor = (Color)ColorValues[i % 6 + 1];
                particle.style.rotate = new Rotate(Angle.Degrees(i * 31));
                Round(particle, 2);
                overlay.Add(particle);
                confetti.Add(particle);
            }
        }

        private void AnimateConfetti(float age)
        {
            for (int i = 0; i < confetti.Count; i++)
            {
                confetti[i].style.translate = new Translate(Mathf.Sin(age * 2 + i) * 18, Mathf.Repeat(age * (18 + i % 7), 160));
                confetti[i].style.rotate = new Rotate(Angle.Degrees(i * 31 + age * 55));
            }
        }
    }
}
