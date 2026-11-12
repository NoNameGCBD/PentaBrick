using System;
using System.Collections.Generic;
using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    /// <summary>메인·난이도 선택·메뉴·확인 창.</summary>
    public sealed partial class GameUI
    {
        /// <summary>첫 접속 때 튜토리얼을 할지 묻는 창.</summary>
        private void BuildTutorialPrompt()
        {
            var box = Modal(400);
            Text(box, "PentaBrick에 오신 것을 환영합니다", 18, true);
            Text(box, "처음이라면 튜토리얼에서 평면도 읽는 법, 블록 쌓기, 정답 제출과 불가능 선언을 3~5분 동안 익혀 보세요.", 13).style.marginTop = 8;
            Text(box, "튜토리얼은 나중에 메인 화면이나 게임 중 Esc 메뉴에서 다시 볼 수 있습니다.", 12, false, DimColor).style.marginTop = 6;
            var buttons = Row(box);
            buttons.style.marginTop = 16;
            buttons.style.justifyContent = Justify.FlexEnd;
            Btn(buttons, "건너뛰기", SkipTutorial);
            var start = Btn(buttons, "튜토리얼 시작", StartTutorial);
            start.style.backgroundColor = SelectColor;
            start.style.color = Color.white;
        }

        /// <summary>일시정지 창: 제목은 문제 이름, 큰 버튼 두 개, 아이콘 줄, 조작법.</summary>
        private void BuildMenu()
        {
            var box = Modal(340);
            box.name = "pause-menu";
            box.style.alignItems = Align.Center;
            Text(box, PuzzleName(), 18, true).style.marginBottom = 14;
            var resume = MenuButton(box, "계속하기", () => menuOpen = false);
            resume.name = "pause-resume";
            resume.style.backgroundColor = GoodColor;
            resume.style.color = Color.black;
            MenuButton(box, state.Mode == null ? "다음 샘플" : "새 문제",
                () => LeavePuzzle("이 문제를 버리고 새 문제로 넘어가시겠습니까?", state.NewPuzzle)).name = "pause-next";

            var icons = Row(box);
            icons.style.marginTop = 8;
            IconButton(icons, "pause-restart", Icon.Restart, "처음부터 다시",
                () => LeavePuzzle("처음부터 다시 하시겠습니까?", state.Reset, "놓은 블록을 모두 치웁니다. 실행 취소로 되돌릴 수 있습니다."))
                .SetEnabled(state.Phase == Phase.Playing && state.UsedTotal > 0);
            IconButton(icons, "pause-settings", Icon.Settings, "설정", OpenSettings);
            if (state.Mode != null) IconButton(icons, "pause-copy", Icon.Link, string.IsNullOrEmpty(Application.absoluteURL) ? "문제 코드 복사" : "링크 복사", CopyShare);
            IconButton(icons, "pause-tutorial", Icon.Help, "튜토리얼", () => LeavePuzzle("풀던 문제를 버리고 튜토리얼을 시작하시겠습니까?", StartTutorial));
            IconButton(icons, "pause-home", Icon.Home, "메인으로", () => LeavePuzzle("메인 메뉴로 나가시겠습니까?", () => Show(UiScreen.Main)));
            if (codeMessage != "") Text(box, codeMessage, 11, false, DimColor).style.marginTop = 6;

            var help = Text(box, "좌클릭: 선택한 도구 · 우클릭: 지우기 · 드래그: 회전 · 휠: 확대/축소\n" +
                "1~6: 색 · Q/W/E: 놓기/칠하기/지우기 · Ctrl(⌘)+Z: 실행 취소", 10, false, DimColor);
            help.style.marginTop = 14;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        /// <summary>일시정지 창 제목: 샘플 번호, 난이도와 문제 코드, 또는 튜토리얼.</summary>
        private string PuzzleName() => state.IsCustom ? "튜토리얼"
            : state.Mode == null ? $"샘플 {state.PuzzleIndex + 1} / {SamplePuzzles.All.Count}"
            : $"{DifficultySettings.Get(state.Mode.Value).Label} · {SeedCode.Encode(state.Puzzle.Seed.Value)}";

        private void OpenSettings()
        {
            settingsOpen = true;
            RefreshInput();
            dirty = true;
        }

        /// <summary>설정 창: 소리 크기, 내 모형 평면도 표시(연습 전용), 점수 초기화. 일시정지에서 열면 닫을 때 일시정지로 돌아간다.</summary>
        private void BuildSettings()
        {
            var box = Modal(340);
            box.name = "settings";
            var head = Row(box);
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 12;
            Text(head, "설정", 18, true).style.flexGrow = 1;
            IconButton(head, "settings-close", Icon.Erase, "닫기 (Esc)", () => { settingsOpen = false; RefreshInput(); dirty = true; }, 36);

            var volume = new Slider("소리 크기", 0f, 1f) { name = "settings-volume", value = AudioListener.volume };
            volume.RegisterValueChangedCallback(change =>
            {
                AudioListener.volume = change.newValue;
                PlayerPrefs.SetFloat(VolumeKey, change.newValue);
            });
            box.Add(volume);
            var showMine = new Toggle("내 모형의 평면도 표시 (연습 모드 전용)") { name = "settings-showmine", value = state.ShowMine };
            showMine.RegisterValueChangedCallback(change => state.SetShowMine(change.newValue));
            showMine.SetEnabled(state.Mode == null);
            showMine.style.marginTop = 8;
            box.Add(showMine);
            var reset = MenuButton(box, $"점수 초기화 (현재 {state.Score}점)", state.ResetScore);
            reset.name = "settings-reset";
            reset.style.marginTop = 16;
        }

        /// <summary>WebGL에서는 같은 문제로 바로 들어오는 주소를, 그 밖에서는 문제 코드를 복사한다.</summary>
        private void CopyShare()
        {
            string page = Application.absoluteURL;
            string share = !string.IsNullOrEmpty(page)
                ? $"{page.Split('?')[0]}?mode={DifficultySettings.Get(state.Mode.Value).Key}&seed={SeedCode.Encode(state.Puzzle.Seed.Value)}"
                : state.Puzzle.Id;
            GUIUtility.systemCopyBuffer = share;
            codeMessage = "복사했습니다: " + share;
            dirty = true;
        }

        /// <summary>메뉴 창의 가로로 꽉 찬 버튼. 누른 뒤 메뉴를 다시 그린다.</summary>
        private Button MenuButton(VisualElement parent, string text, Action onClick)
        {
            var button = Btn(parent, text, () =>
            {
                onClick();
                RefreshInput();
                dirty = true;
            });
            button.style.height = 34;
            button.style.alignSelf = Align.Stretch;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 10;
            button.style.marginLeft = button.style.marginRight = 0;
            button.style.marginBottom = 6;
            return button;
        }

        /// <summary>풀던 문제를 버리기 전에 묻는 확인 창.</summary>
        private void BuildConfirm()
        {
            var box = Modal(380);
            Text(box, "풀던 문제가 있습니다", 16, true);
            Text(box, confirmMessage, 13).style.marginTop = 8;
            Text(box, confirmDetail, 12, false, DimColor).style.marginTop = 6;

            var buttons = Row(box);
            buttons.style.marginTop = 16;
            buttons.style.justifyContent = Justify.FlexEnd;
            Btn(buttons, "계속 풀기", () => CloseConfirm(false)).name = "confirm-stay";
            var leave = Btn(buttons, "나가기", () => CloseConfirm(true));
            leave.name = "confirm-leave";
            leave.style.backgroundColor = BadColor;
            leave.style.color = Color.white;
        }

        /// <summary>메인 화면에서 Esc를 누르면 뜨는 게임 종료 확인 창.</summary>
        private void BuildQuitConfirm()
        {
            var box = Modal(340);
            Text(box, "게임을 종료하시겠습니까?", 16, true);
            var buttons = Row(box);
            buttons.style.marginTop = 16;
            buttons.style.justifyContent = Justify.FlexEnd;
            Btn(buttons, "취소", () => { quitOpen = false; dirty = true; }).name = "quit-cancel";
            var quit = Btn(buttons, "종료", Quit);
            quit.name = "quit-ok";
            quit.style.backgroundColor = BadColor;
            quit.style.color = Color.white;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit이 무시되므로 플레이 모드를 끝낸다
#else
            Application.Quit(); // WebGL에서는 아무 일도 일어나지 않는다
#endif
        }

        /// <summary>메인 화면: 제목, 플레이, 튜토리얼. 설정은 오른쪽 위.</summary>
        private void BuildMain()
        {
            var page = Page();
            IconButton(Corner(page, true), "main-settings", Icon.Settings, "설정", OpenSettings);
            Text(page, "PentaBrick", 56, true).style.marginBottom = 40;
            var play = BigButton(page, "플레이", "", () => Show(UiScreen.DifficultySelect));
            play.name = "main-play";
            play.style.backgroundColor = GoodColor;
            play.style.color = Color.black;
            BigButton(page, "튜토리얼", "", StartTutorial).name = "main-tutorial";
            if (state.Score != 0) Text(page, $"★ {state.Score}", 15, true, DimColor).style.marginTop = 24;
        }

        /// <summary>모드 선택: 타일 네 개. 뒤로는 왼쪽 위, 문제 코드는 오른쪽 위 작은 창.</summary>
        private void BuildDifficultySelect()
        {
            var page = Page();
            IconButton(Corner(page, false), "mode-back", Icon.Back, "메인으로 (Esc)", () => Show(UiScreen.Main));
            IconButton(Corner(page, true), "mode-code", Icon.Code, "문제 코드로 열기", () => { codeOpen = true; codeMessage = ""; dirty = true; });
            Text(page, "모드 선택", 28, true).style.marginBottom = 28;

            var tiles = Row(page);
            tiles.style.flexWrap = Wrap.Wrap;
            tiles.style.justifyContent = Justify.Center;
            ModeTile(tiles, "mode-sample", "연습", $"샘플 {SamplePuzzles.All.Count}", () => Play(null));
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                var d = difficulty;
                var s = DifficultySettings.Get(d);
                ModeTile(tiles, "mode-" + s.Key, s.Label, $"{s.Size}×{s.Size}", () => Play(d));
            }
        }

        private static void ModeTile(VisualElement parent, string name, string title, string detail, Action onClick)
        {
            var tile = new Button(onClick) { name = name, text = $"{title}\n<size=13><color=#9aa0ad>{detail}</color></size>" };
            tile.style.width = 140;
            tile.style.height = 160;
            tile.style.marginLeft = tile.style.marginRight = tile.style.marginTop = tile.style.marginBottom = 8;
            tile.style.fontSize = 22;
            tile.style.unityFontStyleAndWeight = FontStyle.Bold;
            tile.style.color = TextColor;
            tile.style.backgroundColor = CardColor;
            tile.style.borderTopLeftRadius = tile.style.borderTopRightRadius = tile.style.borderBottomLeftRadius = tile.style.borderBottomRightRadius = 18;
            Border(tile, LineColor, 1);
            parent.Add(tile);
        }

        /// <summary>문제 코드 창: 입력 칸, 열기, 오류 문장.</summary>
        private void BuildCodePopup()
        {
            var box = Modal(360);
            var head = Row(box);
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 12;
            Text(head, "문제 코드로 열기", 16, true).style.flexGrow = 1;
            IconButton(head, "code-close", Icon.Erase, "닫기 (Esc)", () => { codeOpen = false; dirty = true; }, 36);
            var row = Row(box);
            var field = new TextField { name = "code-field", value = codeInput };
            field.style.flexGrow = 1;
            field.textEdition.placeholder = "예: normal-1p0wp";
            field.RegisterValueChangedCallback(change => codeInput = change.newValue);
            row.Add(field);
            var open = Btn(row, "열기", OpenCode);
            open.name = "code-open";
            if (codeMessage != "") Text(box, codeMessage, 12, false, BadColor).style.marginTop = 8;
        }

        /// <summary>메뉴 화면의 왼쪽(false)·오른쪽(true) 위 모서리 자리.</summary>
        private static VisualElement Corner(VisualElement page, bool right)
        {
            var corner = new VisualElement();
            corner.style.position = Position.Absolute;
            corner.style.top = 16;
            if (right) corner.style.right = 16; else corner.style.left = 16;
            page.Add(corner);
            return corner;
        }
    }
}
