using System;
using System.Collections.Generic;
using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    /// <summary>화면 흐름: 메인 → (게임 시작) → 난이도 선택 → 게임.</summary>
    public enum UiScreen { Intro, Main, DifficultySelect, Game }

    /// <summary>
    /// 화면 UI. 게임 화면은 상단 바, 평면도 띠(상단), 3D 화면(가운데), 조작 패널(오른쪽), 시점 버튼.
    /// 웹판처럼 상태가 바뀔 때마다 전체를 다시 만든다. 시간 표시만 매 프레임 갱신한다.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        private static readonly string[] ColorNames = { "", "빨강", "주황", "노랑", "초록", "파랑", "보라" };
        private static readonly Color32[] ColorValues =
        {
            new Color32(0x1c, 0x1f, 0x26, 255), // 빈칸
            new Color32(0xe5, 0x48, 0x4d, 255), new Color32(0xf0, 0x8c, 0x2e, 255), new Color32(0xf2, 0xc9, 0x4c, 255),
            new Color32(0x3f, 0xb5, 0x6b, 255), new Color32(0x3b, 0x82, 0xf6, 255), new Color32(0x9b, 0x5d, 0xe5, 255),
        };
        private static readonly string[] ViewNames = { "상", "전", "후", "좌", "우" };
        private static readonly string[] ViewHints =
        {
            "위에서 · 아래쪽이 앞", "앞에서", "뒤에서 · 좌우 반전", "왼쪽에서 · 오른쪽이 앞", "오른쪽에서 · 왼쪽이 앞",
        };

        private static readonly Color PanelColor = new Color32(0x24, 0x28, 0x31, 245);
        private static readonly Color CardColor = new Color32(0x2e, 0x33, 0x3e, 255);
        private static readonly Color LineColor = new Color32(0x4a, 0x50, 0x5e, 255);
        private static readonly Color TextColor = new Color32(0xe8, 0xea, 0xee, 255);
        private static readonly Color DimColor = new Color32(0x9a, 0xa0, 0xad, 255);
        private static readonly Color GoodColor = new Color32(0x3f, 0xb5, 0x6b, 255);
        private static readonly Color BadColor = new Color32(0xe5, 0x48, 0x4d, 255);
        private static readonly Color SelectColor = new Color32(0x3b, 0x82, 0xf6, 255);

        private BlockEditor editor;
        private GameState state;
        private VisualElement root;
        private VisualElement center; // 3D 화면이 보이는 가운데 영역
        private Label timeLabel;
        private bool dirty = true;
        private string codeInput = "";
        private string codeMessage = "";
        private string confirmMessage; // null이 아니면 확인 창을 띄운다
        private Action confirmAction;
        private bool menuOpen;     // Esc 메뉴
        private bool settingsOpen; // Esc 메뉴 안의 설정
        private const string VolumeKey = "volume";
        public const string TutorialSeenKey = "tutorialSeen"; // 튜토리얼을 하거나 건너뛰었으면 첫 접속 안내를 다시 띄우지 않는다
        private Tutorial tutorial;      // null이 아니면 튜토리얼 진행 중
        private bool showMineBeforeTutorial;
        private bool askTutorial;       // 첫 접속 안내 창

        /// <summary>튜토리얼 진행 중인지.</summary>
        public bool InTutorial => tutorial != null;
        /// <summary>첫 접속 안내 창이 떠 있는지.</summary>
        public bool IsAskingTutorial => askTutorial;

        public UiScreen CurrentScreen { get; private set; } = UiScreen.Main;

        private void Start()
        {
            editor = GetComponent<BlockEditor>();
            state = editor.State;
            state.Changed += () => dirty = true; // 버튼 콜백 도중에 화면을 갈아엎지 않도록 다음 Update에서 다시 만든다

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("PentaBrickTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 800);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;

            root = document.rootVisualElement;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;
            root.style.color = TextColor;
            root.style.fontSize = 13;
            // shortcut: 한글 글꼴을 OS에서 빌려 쓴다(에디터·PC 빌드 전용), WebGL 빌드 전에 글꼴 파일을 프로젝트에 넣을 것
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR" }, 14);
            root.style.unityFontDefinition = FontDefinition.FromFont(font);
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

            // 공유 주소(?mode=&seed=)로 들어왔으면 메뉴를 건너뛰고 그 문제로 바로 들어간다.
            if (Generator.TryParseUrl(Application.absoluteURL, out _, out _)) Show(UiScreen.Game);
            else
            {
                Show(UiScreen.Intro); // 인트로 → (아무 버튼) → 메인. 첫 접속 안내는 메인에서 뜬다
                askTutorial = PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0;
            }
        }

        private static void MarkTutorialSeen()
        {
            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            PlayerPrefs.Save(); // WebGL은 Save를 불러야 브라우저 저장소에 기록된다
        }

        /// <summary>튜토리얼을 처음부터 시작한다. 연습용 평면도 표시를 켜고, 끝나면 원래대로 돌려놓는다.</summary>
        public void StartTutorial()
        {
            MarkTutorialSeen();
            askTutorial = false;
            if (tutorial == null) showMineBeforeTutorial = state.ShowMineSetting;
            tutorial = new Tutorial();
            state.SetShowMine(true);
            tutorial.Begin(state);
            Show(UiScreen.Game);
        }

        /// <summary>첫 접속 안내에서 "건너뛰기".</summary>
        public void SkipTutorial()
        {
            MarkTutorialSeen();
            askTutorial = false;
            dirty = true;
        }

        private void EndTutorial()
        {
            if (tutorial == null) return;
            tutorial = null;
            state.SetShowMine(showMineBeforeTutorial);
            dirty = true;
        }

        /// <summary>풀던 문제를 버리게 되는지: 풀이 중이고 블록을 놓았거나 편집 기록이 있다.</summary>
        public bool HasProgress => state.Phase == Phase.Playing && (state.UsedTotal > 0 || state.CanUndo);

        /// <summary>확인 창이 떠 있는지.</summary>
        public bool IsConfirming => confirmMessage != null;

        /// <summary>Esc 메뉴가 떠 있는지.</summary>
        public bool IsMenuOpen => menuOpen;

        /// <summary>게임 화면에서 창이 하나도 안 떠 있을 때만 3D 편집과 단축키를 받는다.</summary>
        private void RefreshInput() => editor.enabled = CurrentScreen == UiScreen.Game && !menuOpen && !IsConfirming;

        /// <summary>Esc: 떠 있는 창을 하나 닫고(확인 창 → 설정 → 메뉴), 아무것도 없으면 메뉴를 연다.</summary>
        public void OnEscape()
        {
            if (CurrentScreen != UiScreen.Game) return;
            if (IsConfirming) CloseConfirm(false);
            else if (settingsOpen) settingsOpen = false;
            else
            {
                menuOpen = !menuOpen;
                codeMessage = "";
            }
            RefreshInput();
            dirty = true;
        }

        private void OpenMenu()
        {
            menuOpen = true;
            codeMessage = "";
            RefreshInput();
            dirty = true;
        }

        /// <summary>풀던 문제가 있으면 확인 창을 띄우고, 없으면 바로 실행한다.</summary>
        private void LeavePuzzle(string message, Action leave)
        {
            menuOpen = settingsOpen = false;
            dirty = true;
            if (!HasProgress)
            {
                leave();
                return;
            }
            confirmMessage = message;
            confirmAction = leave;
            RefreshInput();
        }

        private void CloseConfirm(bool accept)
        {
            var action = confirmAction;
            confirmMessage = null;
            confirmAction = null;
            RefreshInput();
            dirty = true;
            if (accept) action();
        }

        // --- 인트로 ---
        private float introStart;
        private readonly List<VisualElement> introBlocks = new List<VisualElement>();
        private Label introTitle, introSubtitle, introPress;
        private const float BlockDrop = 0.5f, BlockGap = 0.12f; // 블록 하나가 떨어지는 시간, 다음 블록까지의 간격(초)
        private float IntroTime => Time.unscaledTime - introStart;

        /// <summary>인트로에서 아무 버튼이나 누르면 메인 화면으로.</summary>
        public void EnterMain()
        {
            if (CurrentScreen == UiScreen.Intro) Show(UiScreen.Main);
        }

        /// <summary>인트로 화면: 여섯 색 블록이 차례로 떨어지고 제목이 나타난 뒤 "PRESS ANY BUTTON"이 깜박인다.</summary>
        private void BuildIntro()
        {
            var page = Page();
            var blocks = Row(page);
            blocks.style.marginBottom = 28;
            introBlocks.Clear();
            for (int color = 1; color <= Colors.Count; color++)
            {
                var block = new VisualElement();
                block.style.width = block.style.height = 46;
                block.style.marginLeft = block.style.marginRight = 5;
                block.style.backgroundColor = (Color)ColorValues[color];
                block.style.borderTopLeftRadius = block.style.borderTopRightRadius =
                    block.style.borderBottomLeftRadius = block.style.borderBottomRightRadius = 6;
                block.style.opacity = 0;
                blocks.Add(block);
                introBlocks.Add(block);
            }
            introTitle = Text(page, "PentaBrick", 64, true);
            introTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            introSubtitle = Text(page, "다섯 방향의 평면도로 쌓는 공간 추론 퍼즐", 16, false, DimColor);
            introSubtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            introSubtitle.style.marginTop = 6;

            introPress = new Label("PRESS ANY BUTTON");
            introPress.style.position = Position.Absolute;
            introPress.style.left = introPress.style.right = 0;
            introPress.style.bottom = Length.Percent(20); // 화면 중하단
            introPress.style.unityTextAlign = TextAnchor.MiddleCenter;
            introPress.style.fontSize = 18;
            introPress.style.letterSpacing = 4;
            introPress.style.color = TextColor;
            page.Add(introPress);
            UpdateIntro();
        }

        /// <summary>매 프레임 인트로 애니메이션을 진행하고, 아무 입력이나 들어오면 메인으로 넘어간다.</summary>
        private void UpdateIntro()
        {
            float t = IntroTime;
            for (int i = 0; i < introBlocks.Count; i++)
            {
                float p = Mathf.Clamp01((t - i * BlockGap) / BlockDrop);
                introBlocks[i].style.opacity = p > 0 ? 1 : 0;
                introBlocks[i].style.translate = new Translate(0, -260 * (1 - Bounce(p)));
            }
            float titleStart = introBlocks.Count * BlockGap + BlockDrop * 0.6f;
            float title = Mathf.Clamp01((t - titleStart) / 0.6f);
            introTitle.style.opacity = title;
            introTitle.style.translate = new Translate(0, 16 * (1 - title));
            introSubtitle.style.opacity = Mathf.Clamp01((t - titleStart - 0.3f) / 0.6f);
            float pressStart = titleStart + 0.9f;
            // 나타난 뒤에는 1.2초 주기로 깜박인다.
            introPress.style.opacity = t < pressStart ? 0 : 0.2f + 0.8f * (0.5f + 0.5f * Mathf.Cos((t - pressStart) * Mathf.PI * 2 / 1.2f));

            if (AnyInputPressed()) EnterMain();
        }

        /// <summary>떨어져서 튀어 오르는 움직임(0 → 1).</summary>
        private static float Bounce(float p)
        {
            const float n = 7.5625f, d = 2.75f;
            if (p < 1 / d) return n * p * p;
            if (p < 2 / d) { p -= 1.5f / d; return n * p * p + 0.75f; }
            if (p < 2.5f / d) { p -= 2.25f / d; return n * p * p + 0.9375f; }
            p -= 2.625f / d;
            return n * p * p + 0.984375f;
        }

        private static bool AnyInputPressed()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var touch = Touchscreen.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                || (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                || (gamepad != null && (gamepad.startButton.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame));
        }

        public void Show(UiScreen screen)
        {
            if (screen == UiScreen.Intro) introStart = Time.unscaledTime;
            if (screen != UiScreen.Game) EndTutorial();
            CurrentScreen = screen;
            menuOpen = settingsOpen = false;
            RefreshInput();
            codeMessage = "";
            dirty = true;
        }

        /// <summary>난이도 선택 화면에서 고른 모드로 게임을 시작한다. null이면 샘플(연습). 점수는 이어진다.</summary>
        public void Play(Difficulty? mode)
        {
            state.SetMode(mode);
            Show(UiScreen.Game);
        }

        /// <summary>화면 좌표(Input System 기준, 왼쪽 아래가 원점)가 패널이나 버튼 위인지.</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return root.panel.Pick(panelPos) != null;
        }

        /// <summary>글자 입력 칸에 커서가 있는지.</summary>
        public bool IsTyping => root?.panel?.focusController?.focusedElement is VisualElement focused
            && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);

        private void OpenCode()
        {
            if (Generator.TryParseId(codeInput, out Difficulty difficulty, out uint seed))
            {
                codeInput = "";
                state.OpenPuzzle(difficulty, seed);
                Show(UiScreen.Game);
            }
            else
            {
                codeMessage = "코드를 읽을 수 없습니다. 예: normal-1p0wp";
                dirty = true;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) OnEscape();
            if (tutorial != null)
            {
                if (!tutorial.Owns(state.Puzzle)) EndTutorial(); // 메뉴에서 다른 문제로 넘어갔다
                else if (tutorial.Update(state)) dirty = true;    // 행동 단계를 마쳤다
            }
            if (dirty)
            {
                dirty = false;
                Build();
            }
            if (CurrentScreen == UiScreen.Intro) UpdateIntro();
            if (CurrentScreen != UiScreen.Game) return;
            int seconds = (int)(state.ElapsedMs / 1000);
            timeLabel.text = $"시간 {seconds / 60:00}:{seconds % 60:00}";
            if (state.Mode != null)
            {
                int bonus = DifficultySettings.Get(state.Mode.Value).BonusSeconds;
                timeLabel.text += $" / 보너스 {bonus / 60:00}:{bonus % 60:00}";
            }

            // 3D 카메라가 가운데 영역에만 그리게 한다(모형이 패널에 가려지지 않게).
            Rect full = root.worldBound, area = center.worldBound;
            if (full.width > 0 && area.width > 0 && Camera.main != null)
                Camera.main.rect = new Rect(area.x / full.width, 1 - area.yMax / full.height, area.width / full.width, area.height / full.height);
        }

        // ---------------------------------------------------------------
        // 화면 구성
        // ---------------------------------------------------------------

        private void Build()
        {
            root.Clear();
            if (CurrentScreen == UiScreen.Intro) BuildIntro();
            else if (CurrentScreen == UiScreen.Main) BuildMain();
            else if (CurrentScreen == UiScreen.DifficultySelect) BuildDifficultySelect();
            else BuildGame();
            if (menuOpen) BuildMenu();
            if (IsConfirming) BuildConfirm();
            if (askTutorial && CurrentScreen == UiScreen.Main) BuildTutorialPrompt();
        }

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

        /// <summary>화면 전체를 어둡게 덮고 가운데에 창을 띄운다. 창의 내용을 담을 상자를 돌려준다.</summary>
        private VisualElement Modal(int width)
        {
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            root.Add(overlay);

            var box = new VisualElement();
            box.style.width = width;
            box.style.backgroundColor = PanelColor;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 20;
            Border(box, LineColor, 1);
            overlay.Add(box);
            return box;
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
                // WebGL에서는 같은 문제로 바로 들어오는 주소를, 그 밖에서는 문제 코드를 복사한다.
                string page = Application.absoluteURL;
                bool web = !string.IsNullOrEmpty(page);
                string share = web
                    ? $"{page.Split('?')[0]}?mode={DifficultySettings.Get(state.Mode.Value).Key}&seed={SeedCode.Encode(state.Puzzle.Seed.Value)}"
                    : state.Puzzle.Id;
                MenuButton(box, web ? "링크 복사" : "문제 코드 복사", () =>
                {
                    GUIUtility.systemCopyBuffer = share;
                    codeMessage = "복사했습니다: " + share;
                });
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

        private void BuildGame()
        {
            var puzzle = state.Puzzle;

            // 상단 바 (5.2)
            var top = Row(root);
            top.style.backgroundColor = PanelColor;
            top.style.paddingLeft = top.style.paddingRight = 14;
            top.style.height = 40;
            top.style.alignItems = Align.Center;
            Text(top, "PentaBrick", 16, true).style.flexGrow = 1;
            string size = $"({puzzle.Size}×{puzzle.Size}×{puzzle.Height})";
            Text(top, state.IsCustom ? $"튜토리얼 {size}" : state.Mode == null
                ? $"문제 샘플 {state.PuzzleIndex + 1} / {SamplePuzzles.All.Count} {size}"
                : $"문제 {DifficultySettings.Get(state.Mode.Value).Label} · {SeedCode.Encode(puzzle.Seed.Value)} {size}").style.marginRight = 24;
            Text(top, $"점수 {state.Score}", 13, true).style.marginRight = 24;
            timeLabel = Text(top, "");
            timeLabel.style.marginRight = 24;
            Btn(top, "메뉴 (Esc)", OpenMenu);

            BuildViews();

            var body = Row(root);
            body.style.flexGrow = 1;
            body.style.flexShrink = 1; // 남은 높이만 차지한다(오른쪽 패널이 길면 패널 안에서 스크롤)
            body.style.flexBasis = 0;
            body.pickingMode = PickingMode.Ignore;

            center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.style.flexGrow = 1;
            center.style.justifyContent = Justify.FlexEnd;
            center.style.alignItems = Align.Center;
            body.Add(center);
            var viewButtons = Row(center);
            viewButtons.style.marginBottom = 12;
            Text(viewButtons, "시점", 12).style.marginRight = 6;
            Btn(viewButtons, "기본", () => editor.SetView(null));
            for (int i = 0; i < ViewNames.Length; i++)
            {
                var dir = (ViewDir)i;
                Btn(viewButtons, ViewNames[i], () => editor.SetView(dir));
            }
            if (tutorial != null) BuildTutorialCard();

            BuildControls(Panel(body, 290));
        }

        /// <summary>평면도 띠 (5.3, 상단 배치): 다섯 카드를 가로로, 각 카드에 목표와 내 모형의 평면도를 나란히.</summary>
        private void BuildViews()
        {
            var panel = Row(root);
            panel.style.backgroundColor = PanelColor;
            panel.style.paddingLeft = panel.style.paddingRight = 8;
            panel.style.paddingBottom = 8;
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = LineColor;
            var mine = Projection.ProjectAll(state.Model);
            for (int i = 0; i < ViewNames.Length; i++)
            {
                bool match = Projection.ViewsEqual(mine[i], state.Puzzle.Views[i]);
                var card = new VisualElement();
                card.style.backgroundColor = CardColor;
                card.style.flexGrow = 1;
                card.style.flexBasis = 0;
                card.style.marginTop = 8;
                card.style.marginLeft = card.style.marginRight = 4;
                card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 8;
                Border(card, state.ShowMine && match ? GoodColor : LineColor, 2);
                panel.Add(card);

                var title = Row(card);
                title.style.alignItems = Align.Center;
                title.style.marginBottom = 6;
                Text(title, ViewNames[i], 15, true).style.marginRight = 8;
                var hint = Text(title, ViewHints[i], 10, false, DimColor);
                hint.style.flexGrow = 1;
                hint.style.flexShrink = 1;
                if (state.ShowMine) Text(title, match ? "일치" : "불일치", 11, true, match ? GoodColor : DimColor);

                var grids = Row(card);
                Grid(grids, "목표", state.Puzzle.Views[i]);
                if (state.ShowMine) Grid(grids, "내 모형", mine[i]);
            }
        }

        private void Grid(VisualElement parent, string caption, int[,] view)
        {
            var box = new VisualElement();
            box.style.marginRight = 14;
            parent.Add(box);
            Text(box, caption, 10, false, DimColor);
            int cell = Mathf.Min(16, 75 / Mathf.Max(view.GetLength(0), view.GetLength(1))); // 5×5도 카드 안에 들어가게
            for (int r = 0; r < view.GetLength(0); r++)
            {
                var row = Row(box);
                for (int c = 0; c < view.GetLength(1); c++)
                {
                    var square = new VisualElement();
                    square.style.width = square.style.height = cell;
                    square.style.backgroundColor = (Color)ColorValues[view[r, c]];
                    Border(square, PanelColor, 1);
                    row.Add(square);
                }
            }
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

        // ---------------------------------------------------------------
        // 작은 도우미
        // ---------------------------------------------------------------

        /// <summary>메뉴 화면의 바탕: 화면 전체를 덮고 내용을 가운데 정렬한다.</summary>
        private VisualElement Page()
        {
            var page = new VisualElement();
            page.style.flexGrow = 1;
            page.style.backgroundColor = (Color)new Color32(0x1a, 0x1d, 0x24, 255);
            page.style.alignItems = Align.Center;
            page.style.justifyContent = Justify.Center;
            root.Add(page);
            return page;
        }

        private static Button BigButton(VisualElement parent, string title, string detail, Action onClick)
        {
            var button = new Button(onClick) { text = detail == "" ? title : $"{title}\n<size=12>{detail}</size>" };
            button.style.width = 360;
            button.style.height = detail == "" ? 56 : 64;
            button.style.fontSize = 20;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.marginBottom = 10;
            button.style.color = TextColor;
            button.style.backgroundColor = CardColor;
            Border(button, LineColor, 1);
            parent.Add(button);
            return button;
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;
            parent.Add(row);
            return row;
        }

        private static ScrollView Panel(VisualElement parent, int width)
        {
            var panel = new ScrollView(); // 내용이 길면 스크롤
            panel.style.width = width;
            panel.style.flexShrink = 0;
            panel.style.backgroundColor = PanelColor;
            panel.contentContainer.style.paddingLeft = panel.contentContainer.style.paddingRight = 12;
            panel.contentContainer.style.paddingBottom = 12;
            parent.Add(panel);
            return panel;
        }

        private static void Header(VisualElement parent, string text)
        {
            var label = Text(parent, text, 12, true, DimColor);
            label.style.marginTop = 12;
            label.style.marginBottom = 4;
        }

        private static Label Text(VisualElement parent, string text, int size = 13, bool bold = false, Color? color = null)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (color != null) label.style.color = color.Value;
            parent.Add(label);
            return label;
        }

        private static Button Btn(VisualElement parent, string text, Action onClick, bool selected = false)
        {
            var button = new Button(onClick) { text = text };
            button.style.height = 28;
            button.style.color = TextColor;
            button.style.backgroundColor = CardColor;
            Border(button, selected ? SelectColor : LineColor, selected ? 2 : 1);
            parent.Add(button);
            return button;
        }

        private static void Border(VisualElement element, Color color, int width)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor = element.style.borderTopColor = element.style.borderBottomColor = color;
        }
    }
}
