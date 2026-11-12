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

        /// <summary>Esc 메뉴와 그 안의 설정.</summary>
        private void BuildMenu()
        {
            var box = Modal(320);
            if (settingsOpen)
            {
                Text(box, "설정", 18, true).style.marginBottom = 12;
                var volume = new Slider("소리 크기", 0f, 1f) { value = AudioListener.volume };
                volume.RegisterValueChangedCallback(change =>
                {
                    AudioListener.volume = change.newValue;
                    PlayerPrefs.SetFloat(VolumeKey, change.newValue);
                });
                box.Add(volume);
                var showMine = new Toggle("내 모형의 평면도 표시 (연습 모드 전용)") { value = state.ShowMine };
                showMine.RegisterValueChangedCallback(change => state.SetShowMine(change.newValue));
                showMine.SetEnabled(state.Mode == null);
                showMine.style.marginTop = 8;
                box.Add(showMine);
                MenuButton(box, $"점수 초기화 (현재 {state.Score}점)", state.ResetScore).style.marginTop = 16;
                MenuButton(box, "← 뒤로", () => settingsOpen = false);
                return;
            }

            Text(box, "메뉴", 18, true).style.marginBottom = 12;
            MenuButton(box, "계속하기", () => menuOpen = false);
            MenuButton(box, state.Mode == null ? "다음 샘플" : "새 문제",
                () => LeavePuzzle("이 문제를 버리고 새 문제로 넘어가시겠습니까?", state.NewPuzzle));
            if (state.Mode != null)
            {
                MenuButton(box, string.IsNullOrEmpty(Application.absoluteURL) ? "문제 코드 복사" : "링크 복사", CopyShare);
                if (codeMessage != "") Text(box, codeMessage, 11, false, DimColor).style.marginBottom = 6;
            }
            MenuButton(box, "난이도 변경", () => LeavePuzzle("난이도 선택 화면으로 나가시겠습니까?", () => Show(UiScreen.DifficultySelect)));
            MenuButton(box, "튜토리얼", () => LeavePuzzle("풀던 문제를 버리고 튜토리얼을 시작하시겠습니까?", StartTutorial));
            MenuButton(box, "설정", () => settingsOpen = true);
            MenuButton(box, "메인 메뉴", () => LeavePuzzle("메인 메뉴로 나가시겠습니까?", () => Show(UiScreen.Main)));

            Text(box, "조작법", 12, true, DimColor).style.marginTop = 14;
            Text(box, "좌클릭: 선택한 도구 · 우클릭: 지우기 · 드래그: 회전 · 휠: 확대/축소\n" +
                "1~6: 색 · Q/W/E: 놓기/칠하기/지우기 · Ctrl(⌘)+Z: 실행 취소 · Esc: 메뉴", 11, false, DimColor);
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
            Text(box, "놓은 블록과 경과 시간이 사라집니다. 점수는 바뀌지 않습니다.", 12, false, DimColor).style.marginTop = 6;

            var buttons = Row(box);
            buttons.style.marginTop = 16;
            buttons.style.justifyContent = Justify.FlexEnd;
            Btn(buttons, "계속 풀기", () => CloseConfirm(false));
            var leave = Btn(buttons, "나가기", () => CloseConfirm(true));
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
            Btn(buttons, "취소", () => { quitOpen = false; dirty = true; });
            var quit = Btn(buttons, "종료", Quit);
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

        /// <summary>메인 화면: 제목, 규칙 요약, 게임 시작.</summary>
        private void BuildMain()
        {
            var page = Page();
            Text(page, "PentaBrick", 56, true).style.unityTextAlign = TextAnchor.MiddleCenter;
            var subtitle = Text(page, "다섯 방향의 평면도를 보고 똑같은 블록 모형을 쌓는 공간 추론 퍼즐", 16, false, DimColor);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginBottom = 36;

            var start = BigButton(page, "게임 시작", "", () => Show(UiScreen.DifficultySelect));
            start.style.backgroundColor = SelectColor;
            BigButton(page, "튜토리얼", "", StartTutorial);

            var rules = Text(page,
                "평면도와 블록 수를 모두 맞춘 모형을 만들면 정답 제출 (정답 +1 / 오답 −1)\n" +
                "만들 수 없는 문제라고 판단하면 불가능 선언 (맞으면 +2 / 틀리면 −2)",
                13, false, DimColor);
            rules.style.unityTextAlign = TextAnchor.MiddleCenter;
            rules.style.marginTop = 36;
            if (state.Score != 0) Text(page, $"현재 점수 {state.Score}", 14, true).style.marginTop = 12;
        }

        /// <summary>난이도 선택 화면: 샘플(연습)과 세 난이도, 문제 코드로 열기.</summary>
        private void BuildDifficultySelect()
        {
            var page = Page();
            Text(page, "난이도 선택", 32, true).style.marginBottom = 24;

            BigButton(page, "샘플 (연습)", $"손으로 만든 문제 {SamplePuzzles.All.Count}개를 차례로 풉니다", () => Play(null));
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                var d = difficulty;
                var s = DifficultySettings.Get(d);
                BigButton(page, s.Label, $"{s.Size}×{s.Size}×{s.Height} · {s.Colors}색 · 블록 {s.MinBlocks}~{s.MaxBlocks}개", () => Play(d));
            }

            var codeRow = Row(page);
            codeRow.style.marginTop = 24;
            codeRow.style.width = 360;
            var codeField = new TextField { value = codeInput };
            codeField.style.flexGrow = 1;
            codeField.textEdition.placeholder = "문제 코드로 열기 (예: normal-1p0wp)";
            codeField.RegisterValueChangedCallback(change => codeInput = change.newValue);
            codeRow.Add(codeField);
            Btn(codeRow, "열기", OpenCode);
            if (codeMessage != "") Text(page, codeMessage, 12, false, BadColor);

            Btn(page, "← 메인으로", () => Show(UiScreen.Main)).style.marginTop = 24;
        }
    }
}
