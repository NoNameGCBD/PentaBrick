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
    public sealed partial class GameUI : MonoBehaviour
    {
        private static readonly string[] ColorNames = { "", "빨강", "주황", "노랑", "초록", "파랑", "보라" };
        private static readonly Color32[] ColorValues =
        {
            new Color32(0x1c, 0x1f, 0x26, 255), // 빈칸
            new Color32(0xe5, 0x48, 0x4d, 255), new Color32(0xf0, 0x8c, 0x2e, 255), new Color32(0xf2, 0xc9, 0x4c, 255),
            new Color32(0x3f, 0xb5, 0x6b, 255), new Color32(0x3b, 0x82, 0xf6, 255), new Color32(0x9b, 0x5d, 0xe5, 255),
        };
        private static readonly string[] ViewNames = { "위", "앞", "뒤", "왼쪽", "오른쪽" };
        private static readonly string[] ViewHints =
        {
            "위에서 · 아래쪽이 앞", "앞에서", "뒤에서 · 좌우 반전", "왼쪽에서 · 오른쪽이 앞", "오른쪽에서 · 왼쪽이 앞",
        };

        private static readonly Color PanelColor = new Color32(0x24, 0x28, 0x31, 245);
        private static readonly Color PageColor = new Color32(0x1a, 0x1d, 0x24, 255);
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
        private ViewDir? expandedView; // 펼친 평면도 카드(그 방향으로 시점을 돌린 상태)
        private Puzzle builtPuzzle;
        private bool dirty = true;
        private string codeInput = "";
        private string codeMessage = "";
        private string confirmMessage; // null이 아니면 확인 창을 띄운다
        private Action confirmAction;
        private string confirmDetail;
        private bool menuOpen;     // Esc 메뉴
        private bool settingsOpen; // Esc 메뉴 안의 설정
        private bool quitOpen;     // 메인 화면의 게임 종료 확인 창
        private bool codeOpen;     // 모드 선택의 문제 코드 창
        private PanelSettings panelSettings;
        private bool portrait;     // 세로 화면 배치
        private const int PortraitWidth = 480; // 세로 화면의 논리 폭(이 폭에 맞춰 확대)

        // 전환 애니메이션. Build()가 매번 화면을 새로 만들므로 시작 시각만 기억하고 Update에서 시간으로 계산한다.
        private const float ScreenFade = 0.3f, ModalPop = 0.18f; // 초
        private UiScreen? builtScreen;
        private float screenStart = -10f;
        private VisualElement screenCover; // 화면이 바뀔 때 새 화면 위에서 서서히 걷히는 막
        private string builtModal;
        private float modalStart = -10f;
        private VisualElement modalOverlay, modalBox;
        private Label tooltipLabel;          // 마우스를 올린 버튼의 이름(런타임 패널은 tooltip 속성을 스스로 보여 주지 않는다)
        private VisualElement tooltipOwner;
        private IVisualElementScheduledItem tooltipTimer;
        private const long TooltipDelayMs = 300;
        private bool resultPopupShown; // 결과 팝업(창 모양)을 그렸는지. 그 위에 일시정지가 떠도 결과 팝업은 남는다
        private VisualElement closingOverlay; // 방금 닫힌 창의 잔상. 클릭은 통과시키고 서서히 사라진다
        private float closeStart = -10f;
        public const string ClosingClass = "closing";
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
            panelSettings = settings;

            root = document.rootVisualElement;
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;
            root.style.color = TextColor;
            root.RegisterCallback<PointerOverEvent>(e => OnPointerOver(e.target as VisualElement), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOutEvent>(e => OnPointerOut(e.target as VisualElement), TrickleDown.TrickleDown);
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

        /// <summary>게임 종료 확인 창이 떠 있는지.</summary>
        public bool IsQuitOpen => quitOpen;

        /// <summary>설정 창이 떠 있는지(메인이나 일시정지에서 연다).</summary>
        public bool IsSettingsOpen => settingsOpen;

        /// <summary>모드 선택의 문제 코드 창이 떠 있는지.</summary>
        public bool IsCodeOpen => codeOpen;

        /// <summary>세로 화면 배치인지(화면 높이가 폭보다 클 때).</summary>
        public bool IsPortrait => portrait;

        /// <summary>펼친 평면도 카드의 방향. 없으면 null(기본 시점).</summary>
        public ViewDir? ExpandedView => expandedView;

        /// <summary>화면 전환 애니메이션이 진행 중인지.</summary>
        public bool IsTransitioning => Time.unscaledTime - screenStart < ScreenFade;

        /// <summary>게임 화면에서 창이 하나도 안 떠 있을 때만 3D 편집과 단축키를 받는다.</summary>
        private void RefreshInput() => editor.enabled = CurrentScreen == UiScreen.Game && !menuOpen && !settingsOpen && !IsConfirming;

        /// <summary>Esc: 메인 화면에서는 종료 확인 창을 열고 닫는다. 게임 화면에서는 떠 있는 창을 하나 닫고(확인 창 → 설정 → 메뉴), 아무것도 없으면 메뉴를 연다.</summary>
        public void OnEscape()
        {
            if (CurrentScreen == UiScreen.Main && settingsOpen)
            {
                settingsOpen = false;
                dirty = true;
                return;
            }
            if (CurrentScreen == UiScreen.Main && !askTutorial)
            {
                quitOpen = !quitOpen;
                dirty = true;
                return;
            }
            if (CurrentScreen == UiScreen.DifficultySelect)
            {
                if (codeOpen)
                {
                    codeOpen = false;
                    dirty = true;
                }
                else Show(UiScreen.Main);
                return;
            }
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
        private void LeavePuzzle(string message, Action leave, string detail = "놓은 블록과 경과 시간이 사라집니다. 점수는 바뀌지 않습니다.")
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
            confirmDetail = detail;
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
        private float introPrev; // 지난 프레임의 IntroTime. 소리 낼 순간을 지났는지 본다

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
            float t = IntroTime, prev = introPrev;
            introPrev = t;
            bool Crossed(float at) => prev < at && at <= t;
            var sfx = GetComponent<Sfx>();
            for (int i = 0; i < introBlocks.Count; i++)
            {
                if (Crossed(i * BlockGap + BlockDrop / 2.75f) && sfx != null) sfx.PlayIntroBlock(i); // Bounce가 처음 바닥에 닿는 순간
                float p = Mathf.Clamp01((t - i * BlockGap) / BlockDrop);
                introBlocks[i].style.opacity = p > 0 ? 1 : 0;
                introBlocks[i].style.translate = new Translate(0, -260 * (1 - Bounce(p)));
            }
            float titleStart = introBlocks.Count * BlockGap + BlockDrop * 0.6f;
            if (Crossed(titleStart) && sfx != null) sfx.PlayIntroTitle();
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
            if (screen == UiScreen.Intro) { introStart = Time.unscaledTime; introPrev = -1; }
            if (screen != UiScreen.Game) EndTutorial();
            CurrentScreen = screen;
            menuOpen = settingsOpen = quitOpen = codeOpen = false;
            expandedView = null;
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
            CheckOrientation();
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
            else if (screenCover != null) Animate();
            if (CurrentScreen == UiScreen.Intro) UpdateIntro();
            if (CurrentScreen != UiScreen.Game) return;
            int seconds = (int)(state.ElapsedMs / 1000);
            timeLabel.text = $"{seconds / 60:00}:{seconds % 60:00}";
            if (state.Mode != null)
            {
                int bonus = DifficultySettings.Get(state.Mode.Value).BonusSeconds; // 이 시간 안에 맞히면 보너스
                timeLabel.text += $"<color=#9aa0ad> / {bonus / 60:00}:{bonus % 60:00}</color>";
            }

            // 3D 카메라가 가운데 영역에만 그리게 한다(모형이 패널에 가려지지 않게).
            Rect full = root.worldBound, area = center.worldBound;
            if (full.width > 0 && area.width > 0 && Camera.main != null)
                Camera.main.rect = new Rect(area.x / full.width, 1 - area.yMax / full.height, area.width / full.width, area.height / full.height);
        }

        /// <summary>
        /// 가로/세로가 바뀌면 기준 폭을 바꾸고 다시 그린다. 세로는 폰 폭(480) 기준이라 버튼이 줄어들지 않고 줄을 바꾼다.
        /// 패널 크기가 아니라 실제 화면(또는 캡처 텍스처) 크기로 판단한다(패널 크기는 이 기준 폭에 따라 바뀌므로).
        /// </summary>
        private void CheckOrientation()
        {
            var target = panelSettings.targetTexture;
            int width = target != null ? target.width : Screen.width;
            int height = target != null ? target.height : Screen.height;
            bool now = height > width;
            if (now == portrait) return;
            portrait = now;
            panelSettings.referenceResolution = portrait ? new Vector2Int(PortraitWidth, 800) : new Vector2Int(1280, 800);
            dirty = true;
        }

        // ---------------------------------------------------------------
        // 화면 구성
        // ---------------------------------------------------------------

        private void Build()
        {
            var previousOverlay = modalOverlay;
            bool resultWasShown = resultPopupShown;
            modalOverlay = modalBox = null; // 결과 팝업은 화면(게임) 안에서 만들어지므로 화면보다 먼저 비운다
            resultPopupShown = false;
            root.Clear();
            if (builtScreen != CurrentScreen)
            {
                if (builtScreen != null) screenStart = Time.unscaledTime; // 처음 뜨는 화면은 바로 보인다
                builtScreen = CurrentScreen;
            }
            if (CurrentScreen == UiScreen.Intro) BuildIntro();
            else if (CurrentScreen == UiScreen.Main) BuildMain();
            else if (CurrentScreen == UiScreen.DifficultySelect) BuildDifficultySelect();
            else BuildGame();

            screenCover = new VisualElement { pickingMode = PickingMode.Ignore }; // 클릭은 통과시킨다
            screenCover.style.position = Position.Absolute;
            screenCover.style.left = screenCover.style.top = screenCover.style.right = screenCover.style.bottom = 0;
            screenCover.style.backgroundColor = PageColor;
            root.Add(screenCover);

            if (menuOpen) BuildMenu();
            if (settingsOpen) BuildSettings();
            if (codeOpen && CurrentScreen == UiScreen.DifficultySelect) BuildCodePopup();
            if (IsConfirming) BuildConfirm();
            if (askTutorial && CurrentScreen == UiScreen.Main) BuildTutorialPrompt();
            if (quitOpen && CurrentScreen == UiScreen.Main) BuildQuitConfirm();
            // 새 창이 열렸을 때만 다시 튀어나오게 한다(같은 창이 다시 그려질 때는 이어서).
            string modal = modalOverlay == null ? null : quitOpen ? "quit" : askTutorial ? "tutorial" : IsConfirming ? "confirm" : settingsOpen ? "settings" : codeOpen ? "code" : menuOpen ? "menu" : "result";
            if (modal != builtModal)
            {
                bool resultStays = builtModal == "result" && resultPopupShown; // 결과 위에 다른 창이 뜬 것: 결과는 사라지지 않는다
                if (builtModal != null && previousOverlay != null && !resultStays)
                {
                    closingOverlay = previousOverlay;
                    closeStart = Time.unscaledTime;
                    closingOverlay.AddToClassList(ClosingClass);
                    closingOverlay.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
                }
                if (!(modal == "result" && resultWasShown)) modalStart = Time.unscaledTime; // 위의 창이 닫혀 결과로 돌아올 때는 다시 튀어나오지 않는다
                builtModal = modal;
            }
            // 잔상은 새 창 아래, 화면 위에 둔다. 다시 그려도 사라질 때까지 계속 붙인다.
            if (closingOverlay != null) root.Insert(root.IndexOf(screenCover) + 1, closingOverlay);
            BuildTooltip();
            Animate();
        }

        // --- 툴팁: tooltip 문장이 달린 요소에 마우스를 잠깐 올리면 그 문장을 띄운다 ---

        private void BuildTooltip()
        {
            tooltipTimer?.Pause();
            tooltipOwner = null; // 다시 그리면 요소가 바뀌므로 마우스가 다시 들어올 때까지 숨긴다
            tooltipLabel = new Label { name = "tooltip", pickingMode = PickingMode.Ignore };
            tooltipLabel.style.position = Position.Absolute;
            tooltipLabel.style.display = DisplayStyle.None;
            tooltipLabel.style.fontSize = 12;
            tooltipLabel.style.color = TextColor;
            tooltipLabel.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.95f);
            tooltipLabel.style.paddingLeft = tooltipLabel.style.paddingRight = 8;
            tooltipLabel.style.paddingTop = tooltipLabel.style.paddingBottom = 4;
            tooltipLabel.style.borderTopLeftRadius = tooltipLabel.style.borderTopRightRadius = tooltipLabel.style.borderBottomLeftRadius = tooltipLabel.style.borderBottomRightRadius = 6;
            root.Add(tooltipLabel); // 맨 위에 그려지도록 마지막에
        }

        /// <summary>요소나 그 부모 중 tooltip 문장이 달린 첫 요소(잔상 안은 제외).</summary>
        private static VisualElement TooltipOwner(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element.ClassListContains(ClosingClass)) return null;
                if (!string.IsNullOrEmpty(element.tooltip)) return element;
            }
            return null;
        }

        private void OnPointerOver(VisualElement target)
        {
            var owner = TooltipOwner(target);
            if (owner == tooltipOwner) return;
            HideTooltip();
            if (owner == null || tooltipLabel == null) return;
            tooltipOwner = owner;
            tooltipTimer = tooltipLabel.schedule.Execute(() => ShowTooltip(owner)).StartingIn(TooltipDelayMs);
        }

        private void OnPointerOut(VisualElement target)
        {
            if (tooltipOwner != null && TooltipOwner(target) == tooltipOwner) HideTooltip(); // 안쪽 요소로 옮기면 Over가 다시 띄운다
        }

        private void HideTooltip()
        {
            tooltipTimer?.Pause();
            tooltipOwner = null;
            if (tooltipLabel != null) tooltipLabel.style.display = DisplayStyle.None;
        }

        /// <summary>화면 위쪽 절반의 버튼이면 아래에, 아래쪽이면 위에. 화면 가장자리 근처면 그쪽 끝에 맞춘다.</summary>
        private void ShowTooltip(VisualElement owner)
        {
            if (owner.panel == null || owner != tooltipOwner) return;
            Rect screen = root.worldBound, b = owner.worldBound;
            var tip = tooltipLabel;
            tip.text = owner.tooltip;
            tip.style.left = tip.style.right = tip.style.top = tip.style.bottom = StyleKeyword.Auto;
            tip.style.translate = new Translate(0, 0);
            float center = b.center.x - screen.x;
            const float edge = 160; // 툴팁 폭의 절반쯤: 이보다 가장자리에 가까우면 가운데 정렬 대신 끝에 맞춘다
            if (center < edge) tip.style.left = Mathf.Max(4, b.xMin - screen.x);
            else if (center > screen.width - edge) tip.style.right = Mathf.Max(4, screen.xMax - b.xMax);
            else
            {
                tip.style.left = center;
                tip.style.translate = new Translate(Length.Percent(-50), 0);
            }
            if (b.center.y - screen.y < screen.height / 2) tip.style.top = b.yMax - screen.y + 6;
            else tip.style.bottom = screen.yMax - b.yMin + 6;
            tip.style.display = DisplayStyle.Flex;
        }

        /// <summary>화면 전환(어두운 막이 걷힘)과 창 열기(서서히 나타나며 살짝 커짐)를 현재 시각에 맞춰 그린다.</summary>
        private void Animate()
        {
            float now = Time.unscaledTime;
            float s = Mathf.SmoothStep(0f, 1f, (now - screenStart) / ScreenFade);
            screenCover.style.opacity = 1f - s;
            screenCover.style.display = s >= 1f ? DisplayStyle.None : DisplayStyle.Flex;
            if (closingOverlay != null)
            {
                float c = Mathf.SmoothStep(0f, 1f, (now - closeStart) / ModalPop);
                closingOverlay.style.opacity = 1f - c;
                closingOverlay[0].style.scale = new Scale(Vector2.one * (1f - 0.04f * c));
                if (c >= 1f)
                {
                    closingOverlay.RemoveFromHierarchy();
                    closingOverlay = null;
                }
            }
            if (modalOverlay == null) return;
            float m = Mathf.SmoothStep(0f, 1f, (now - modalStart) / ModalPop);
            modalOverlay.style.opacity = m;
            modalBox.style.scale = new Scale(Vector2.one * (0.94f + 0.06f * m));
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
            box.style.width = portrait ? Mathf.Min(width, PortraitWidth - 32) : width;
            box.style.backgroundColor = PanelColor;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 20;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 16;
            Border(box, LineColor, 1);
            overlay.Add(box);
            modalOverlay = overlay;
            modalBox = box;
            return box;
        }

        // ---------------------------------------------------------------
        // 작은 도우미
        // ---------------------------------------------------------------

        /// <summary>메뉴 화면의 바탕: 화면 전체를 덮고 내용을 가운데 정렬한다.</summary>
        private VisualElement Page()
        {
            var page = new VisualElement();
            page.style.flexGrow = 1;
            page.style.backgroundColor = PageColor;
            page.style.alignItems = Align.Center;
            page.style.justifyContent = Justify.Center;
            root.Add(page);
            return page;
        }

        private static Button BigButton(VisualElement parent, string title, string detail, Action onClick)
        {
            var button = new Button(onClick) { text = detail == "" ? title : $"{title}\n<size=12>{detail}</size>" };
            button.style.width = 240;
            button.style.height = detail == "" ? 52 : 64;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 14;
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

        /// <summary>정사각형 아이콘 버튼. 이름은 테스트가 찾는 데, 툴팁은 이름과 단축키를 알려 주는 데 쓴다.</summary>
        private static Button IconButton(VisualElement parent, string name, Icon icon, string tooltip, Action onClick, float size = 44f)
        {
            var button = new Button(onClick) { name = name, tooltip = tooltip, text = "" };
            button.AddToClassList("icon-button");
            button.style.flexShrink = 0; // 줄이 좁아도 44px 터치 크기를 지킨다
            button.style.width = button.style.height = size;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.marginLeft = button.style.marginRight = 3;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = CardColor;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = size * 0.25f;
            Border(button, LineColor, 1);
            button.Add(Icons.Make(icon, size * 0.6f, TextColor));
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
