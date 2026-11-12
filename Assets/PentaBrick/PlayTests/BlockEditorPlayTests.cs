using System.Collections;
using System.IO;
using NUnit.Framework;
using PentaBrick.Core;
using PentaBrick.Game;
using PentaBrick.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PentaBrick.PlayTests
{
    public class BlockEditorPlayTests
    {
        private BlockEditor editor;
        private GameUI ui;
        private VisualElement root;
        private RenderTexture capture;

        // 테스트는 실제 게임과 같은 저장소(PlayerPrefs)를 쓰므로, 끝나면 사용자의 값을 그대로 돌려놓는다.
        private static readonly string[] SavedKeys = { GameUI.TutorialSeenKey, "volume" };
        private readonly System.Collections.Generic.Dictionary<string, string> savedPrefs = new System.Collections.Generic.Dictionary<string, string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedPrefs.Clear();
            foreach (string key in SavedKeys)
                if (PlayerPrefs.HasKey(key)) savedPrefs[key] = PlayerPrefs.GetFloat(key, float.NaN).ToString("R") + "|" + PlayerPrefs.GetInt(key, int.MinValue);
            PlayerPrefs.SetInt(GameUI.TutorialSeenKey, 1); // 다른 테스트에서는 첫 접속 안내를 띄우지 않는다

            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Create();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in SavedKeys)
            {
                PlayerPrefs.DeleteKey(key);
                if (!savedPrefs.TryGetValue(key, out string saved)) continue;
                string[] parts = saved.Split('|');
                if (key == "volume") PlayerPrefs.SetFloat(key, float.Parse(parts[0]));
                else PlayerPrefs.SetInt(key, int.Parse(parts[1]));
            }
            PlayerPrefs.Save();
        }

        /// <summary>게임 오브젝트를 (다시) 만든다. 첫 접속 안내는 GameUI가 시작할 때 정해진다. 기본으로 인트로는 넘긴다.</summary>
        private IEnumerator Create(bool keepIntro = false)
        {
            if (editor != null) Object.Destroy(editor.gameObject);
            yield return null;
            editor = new GameObject("PentaBrick").AddComponent<BlockEditor>();
            ui = editor.gameObject.AddComponent<GameUI>();
            editor.gameObject.AddComponent<Sfx>(); // 효과음이 예외 없이 도는지도 함께 본다
            yield return null; // GameUI.Start와 첫 화면 구성
            yield return null;
            root = editor.GetComponent<UIDocument>().rootVisualElement;
            if (!keepIntro)
            {
                ui.EnterMain();
                yield return null;
            }

            // 배치 모드에서는 화면을 캡처할 수 없어서 카메라와 UI를 같은 텍스처에 그려 눈으로 확인할 수 있게 저장한다.
            capture = new RenderTexture(1280, 800, 24);
            Camera.main.targetTexture = capture;
            var settings = editor.GetComponent<UIDocument>().panelSettings;
            settings.targetTexture = capture;
            settings.clearColor = false;
        }

        private IEnumerator Save(string file)
        {
            yield return new WaitForSecondsRealtime(0.35f); // 전환·창 애니메이션이 끝난 모습을 찍는다
            yield return null; // 카메라가 먼저, UI가 그 위에 그려지는 실제 순서 그대로 한 프레임을 그린다
            yield return null;
            var panel = editor.GetComponent<UIDocument>().panelSettings;
            RenderTexture.active = capture;
            var image = new Texture2D(capture.width, capture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + file, image.EncodeToPNG());
            Assert.IsNotNull(panel);
        }

        /// <summary>닫히며 사라지는 중인 창(잔상)을 뺀 요소만 찾는다.</summary>
        private UQueryBuilder<T> Live<T>() where T : VisualElement => root.Query<T>().Where(e => IsLive(e));

        private static bool IsLive(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e.ClassListContains(GameUI.ClosingClass)) return false;
            return true;
        }

        /// <summary>잔상을 뺀 요소 중 이름이 같은 첫 요소. 없으면 실패.</summary>
        private T ByName<T>(string name) where T : VisualElement
        {
            var element = Live<T>().Where(e => e.name == name).First();
            if (element == null) Assert.Fail($"\"{name}\" 없음");
            return element;
        }

        private IEnumerator ClickName(string name)
        {
            var button = ByName<Button>(name);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
            yield return null;
        }

        /// <summary>화면(캡처 텍스처) 크기를 바꾸고 새 배치가 그려질 때까지 기다린다.</summary>
        private IEnumerator Resize(int width, int height)
        {
            capture = new RenderTexture(width, height, 24);
            Camera.main.targetTexture = capture;
            editor.GetComponent<UIDocument>().panelSettings.targetTexture = capture;
            for (int i = 0; i < 4; i++) yield return null;
        }

        /// <summary>마우스가 요소 위로 들어오거나(true) 나간(false) 것처럼 포인터 이벤트를 보낸다.</summary>
        private IEnumerator Hover(VisualElement element, bool over)
        {
            var system = new Event { type = EventType.MouseMove, mousePosition = element.worldBound.center };
            if (over)
                using (var e = PointerOverEvent.GetPooled(system)) { e.target = element; element.SendEvent(e); }
            else
                using (var e = PointerOutEvent.GetPooled(system)) { e.target = element; element.SendEvent(e); }
            yield return null;
        }

        private int CountLabels(string text) => Live<Label>().Where(l => l.text == text).ToList().Count;
        private int CountButtons(string start) => Live<Button>().Where(b => b.text.StartsWith(start)).ToList().Count;

        [UnityTest]
        public IEnumerator 메인에서_플레이를_누르면_모드_선택을_거쳐_게임이_시작된다()
        {
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled, "메뉴에서는 3D 편집 입력이 꺼져 있다");
            Assert.IsNull(root.Q("tray"), "메인 화면에는 게임 트레이가 없다");
            Assert.AreEqual(0, Live<Label>().Where(l => l.text.StartsWith("평면도와 블록 수를")).ToList().Count, "규칙 요약 없음");
            Assert.AreEqual(ByName<VisualElement>("main-play").resolvedStyle.width, ByName<VisualElement>("main-tutorial").resolvedStyle.width, 0.5f, "플레이와 튜토리얼은 같은 크기");
            yield return Save("ui-main.png");

            yield return ClickName("main-play");
            Assert.AreEqual(UiScreen.DifficultySelect, ui.CurrentScreen);
            foreach (string mode in new[] { "mode-sample", "mode-easy", "mode-normal", "mode-hard" }) Assert.IsNotNull(ByName<Button>(mode), mode);
            Assert.IsNull(root.Q<TextField>(), "코드 입력 칸은 숨김");
            yield return Save("ui-mode.png");
            yield return ClickName("mode-code");
            Assert.IsTrue(ui.IsCodeOpen);
            ByName<TextField>("code-field").value = "  NORMAL-1P0WP ";
            yield return ClickName("code-open");
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen, "대문자·공백도 같은 코드");
            Assert.AreEqual(Difficulty.Normal, editor.State.Mode);
            Assert.IsFalse(ui.IsCodeOpen);

            ui.Show(UiScreen.DifficultySelect);
            yield return null;
            yield return ClickName("mode-code");
            ByName<TextField>("code-field").value = "@@";
            yield return ClickName("code-open");
            Assert.IsTrue(ui.IsCodeOpen, "잘못된 코드면 창이 남는다");
            Assert.AreEqual(1, Live<Label>().Where(l => l.text.StartsWith("코드를 읽을 수 없습니다")).ToList().Count);
            ui.OnEscape();
            yield return null;
            Assert.IsFalse(ui.IsCodeOpen);
            Assert.AreEqual(UiScreen.DifficultySelect, ui.CurrentScreen, "Esc는 코드 창만 닫음");
            ui.OnEscape();
            yield return null;
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen, "모드 선택에서 Esc는 메인으로");

            yield return ClickName("main-settings"); // 메인에서 여는 설정
            Assert.IsTrue(ui.IsSettingsOpen);
            Assert.IsNotNull(ByName<Toggle>("settings-showmine"));
            ui.OnEscape();
            yield return null;
            Assert.IsFalse(ui.IsSettingsOpen);
            Assert.IsFalse(ui.IsQuitOpen, "설정을 닫는 Esc는 종료 창을 열지 않는다");

            yield return ClickName("main-play");
            yield return ClickName("mode-hard");
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsTrue(editor.enabled);
            Assert.AreEqual(Difficulty.Hard, editor.State.Mode);
            var rightView = ByName<VisualElement>("view-right");
            Assert.Greater(rightView.worldBound.height, 0);
            Assert.LessOrEqual(rightView.worldBound.xMax, root.worldBound.xMax + 0.5f, "평면도 카드가 화면 안에 있다");
            Assert.AreEqual(0, Live<Toggle>().ToList().Count, "평면도 표시 옵션은 설정 안에만");
            yield return Save("ui-game-hard.png");

            ui.Show(UiScreen.Main);
            yield return null;
            Assert.IsFalse(editor.enabled);
            Assert.IsNotNull(ByName<Button>("main-play"));
        }

        /// <summary>버튼을 실제로 누른 것처럼 클릭 이벤트를 보내고 화면이 다시 그려지기를 기다린다.</summary>
        [UnityTest]
        public IEnumerator 버튼_소리는_게임_효과음과_겹치지_않는다()
        {
            ui.Play(null);
            yield return null;
            var sfx = editor.GetComponent<Sfx>();
            int before = sfx.ClicksPlayed;

            yield return ClickName("camera"); // 상태가 안 바뀌는 버튼: 버튼 소리
            Assert.AreEqual(before + 1, sfx.ClicksPlayed);

            editor.State.Place(0, 0);
            yield return null;
            yield return ClickName("undo"); // 지우기 소리가 나므로 버튼 소리는 생략
            Assert.AreEqual(before + 1, sfx.ClicksPlayed);
        }

        /// <summary>
        /// 배치 모드는 창에 포커스가 없어 기본 설정이면 입력이 버려진다. 복사본 설정으로 바꾸고, 돌려준 함수로 되돌린다.
        /// </summary>
        private static System.Action AcceptUnfocusedInput()
        {
            var original = InputSystem.settings;
            var settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            return () => { InputSystem.settings = original; Object.Destroy(settings); };
        }

        [UnityTest]
        public IEnumerator 터치_탭은_놓고_드래그는_돌리고_핀치는_확대한다()
        {
            ui.Play(null);
            yield return null;
            var state = editor.State;
            var restore = AcceptUnfocusedInput();
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                IEnumerator Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 pos)
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = phase, position = pos });
                    yield return null;
                }
                // 가운데 바닥 칸(p1은 3×3, 가운데 칸 윗면이 월드 원점)
                Vector2 center = Camera.main.WorldToScreenPoint(Vector3.zero);

                // 탭: 놓기
                yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Began, center);
                Assert.IsTrue(editor.transform.Find("Ghost").gameObject.activeSelf, "누르고 있는 동안 미리보기");
                yield return Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, center);
                Assert.AreEqual(1, state.UsedTotal, "탭하면 놓인다");
                yield return null;
                Assert.IsFalse(editor.transform.Find("Ghost").gameObject.activeSelf, "떼면 미리보기를 숨긴다");

                // 드래그: 돌리기만, 놓지 않는다
                float yaw = editor.Yaw;
                yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Began, center);
                for (int i = 1; i <= 5; i++) yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Moved, center + new Vector2(20 * i, 0));
                yield return Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, center + new Vector2(100, 0));
                Assert.AreNotEqual(yaw, editor.Yaw, "드래그하면 시점이 돈다");
                Assert.AreEqual(1, state.UsedTotal, "드래그는 놓지 않는다");

                // 핀치: 손가락 사이가 벌어지면 가까워진다(확대), 놓지 않는다
                float distance = editor.Distance;
                yield return Touch(3, UnityEngine.InputSystem.TouchPhase.Began, center);
                yield return Touch(4, UnityEngine.InputSystem.TouchPhase.Began, center + new Vector2(50, 0));
                yield return Touch(4, UnityEngine.InputSystem.TouchPhase.Moved, center + new Vector2(150, 0));
                yield return Touch(4, UnityEngine.InputSystem.TouchPhase.Ended, center + new Vector2(150, 0));
                yield return Touch(3, UnityEngine.InputSystem.TouchPhase.Ended, center);
                Assert.Less(editor.Distance, distance, "벌리면 확대");
                Assert.AreEqual(1, state.UsedTotal, "핀치는 놓지 않는다");
            }
            finally
            {
                InputSystem.RemoveDevice(touch);
                restore();
            }
        }

        [UnityTest]
        public IEnumerator 게임_화면의_단축키도_버튼_소리를_낸다()
        {
            var restore = AcceptUnfocusedInput();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                IEnumerator Press(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                var sfx = editor.GetComponent<Sfx>();

                int before = sfx.ClicksPlayed;
                yield return Press(Key.Q); // 메인 화면에서는 소리 없음
                Assert.AreEqual(before, sfx.ClicksPlayed);

                ui.Play(null);
                yield return null;
                yield return Press(Key.W);
                Assert.AreEqual(before + 1, sfx.ClicksPlayed, "도구 단축키");
                yield return Press(Key.LeftCtrl, Key.Z);
                Assert.AreEqual(before + 1, sfx.ClicksPlayed, "되돌릴 것이 없으면 소리 없음");
                yield return Press(Key.Digit6);
                Assert.AreEqual(before + 1, sfx.ClicksPlayed, "p1에 없는 보라");
                yield return Press(Key.Digit1);
                Assert.AreEqual(before + 2, sfx.ClicksPlayed, "색 단축키");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                restore();
            }
        }

        [UnityTest]
        public IEnumerator 버튼은_평소에_원래_색이고_강조_준비가_되어_있다()
        {
            Assert.IsTrue(root.ClassListContains("mouse"), "PC에서는 마우스 강조 방식");
            foreach (var b in Live<Button>().ToList())
            {
                Assert.IsNotNull(b.resolvedStyle.backgroundImage.texture, $"{b.name} 강조용 배경 그림(스타일 시트 적용)");
                Assert.AreEqual(0f, b.resolvedStyle.unityBackgroundImageTintColor.a, $"{b.name} 평소에는 투명");
            }
            // shortcut: 실제 :hover 전환은 배치 모드(창 포커스 없음)에서 UI가 마우스를 무시해 확인 못 함, 에디터에서 눈으로 확인
            yield break;
        }

        [UnityTest]
        public IEnumerator 풀던_중에_나가려고_하면_확인_창을_띄운다()
        {
            ui.Play(null);
            yield return null;
            var state = editor.State;

            // 아무것도 안 놓았으면 바로 나간다.
            yield return ClickName("pause");
            yield return ClickName("pause-home");
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(ui.IsConfirming);

            ui.Play(null);
            yield return null;
            state.Place(0, 0);
            yield return null;

            // 메인 메뉴 → 확인 창 → 계속 풀기: 그대로 남는다.
            yield return ClickName("pause");
            yield return ClickName("pause-home");
            Assert.IsTrue(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled, "확인 창이 떠 있는 동안 편집 입력을 막는다");
            Assert.IsTrue(ui.IsPointerOverUI(new Vector2(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f)), "3D 화면 클릭도 막는다");
            yield return Save("ui-confirm.png");
            yield return ClickName("confirm-stay");
            Assert.IsFalse(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsTrue(editor.enabled);
            Assert.AreEqual(1, state.UsedTotal);

            // 메인 → 나가기
            yield return ClickName("pause");
            yield return ClickName("pause-home");
            Assert.IsTrue(ui.IsConfirming);
            yield return ClickName("confirm-leave");
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled);

            // 다음 샘플 → 나가기: 다음 문제로
            ui.Play(null);
            yield return null;
            state.Place(0, 0);
            state.Reset(); // 블록은 없지만 초기화를 되돌릴 수 있으므로 잃을 것이 있다
            yield return null;
            yield return ClickName("pause");
            yield return ClickName("pause-next");
            Assert.IsTrue(ui.IsConfirming);
            yield return ClickName("confirm-leave");
            Assert.AreEqual("p2", state.Puzzle.Id);

            // 제출한 뒤에는 묻지 않는다.
            state.Place(0, 0);
            state.DeclareImpossible();
            yield return null;
            yield return ClickName("pause");
            yield return ClickName("pause-home");
            Assert.IsFalse(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator 평면도_카드를_누르면_그_방향으로_보고_펼쳐진다()
        {
            ui.Play(null);
            yield return null;
            Assert.AreEqual(0, CountButtons("메뉴 (Esc)"), "상단 글자 바 없음");
            Assert.AreEqual(0, CountButtons("기본"), "시점 버튼 줄 없음");
            Assert.IsNull(root.Q("view-expanded"));
            yield return ClickName("view-front");
            Assert.AreEqual(ViewDir.Front, ui.ExpandedView);
            Assert.IsNotNull(ByName<VisualElement>("view-expanded"));
            yield return ClickName("view-left");
            Assert.AreEqual(ViewDir.Left, ui.ExpandedView, "다른 카드로 바로 바뀜");
            yield return Save("ui-game-hud.png");
            yield return ClickName("view-left");
            Assert.IsNull(ui.ExpandedView, "같은 카드를 다시 누르면 닫힘");
            Assert.IsNull(root.Q("view-expanded"));
            Assert.IsNotNull(ByName<VisualElement>("score"));
            Assert.IsNotNull(ByName<VisualElement>("timer"));
            // 3D 카메라는 가운데 영역에만 그리므로, HUD 띠가 투명하면 지난 프레임 글자(바뀌는 시간)가 지워지지 않고 겹친다.
            Assert.AreEqual(1f, ByName<VisualElement>("hud").resolvedStyle.backgroundColor.a, 0.001f, "HUD 띠는 불투명");
            Assert.IsFalse(ByName<VisualElement>("view-top").ClassListContains("match"), "내 모형 평면도 표시를 끄면 일치 표시 없음");
            yield return ClickName("pause");
            Assert.IsTrue(ui.IsMenuOpen);
        }

        [UnityTest]
        public IEnumerator 트레이에서_도구와_색을_고르고_제출한다()
        {
            ui.Play(null); // 샘플 p1
            yield return null;
            var state = editor.State;
            Assert.AreEqual(0, Live<Label>().Where(l => l.text.StartsWith("블록 사용")).ToList().Count, "오른쪽 패널 소제목 없음");
            for (int c = 1; c <= 6; c++) Assert.AreEqual(state.Puzzle.Counts[c] > 0, root.Q("color-" + c) != null, "문제에 있는 색만 칩: " + c);
            yield return ClickName("tool-paint");
            Assert.AreEqual(Tool.Paint, state.Tool);
            Assert.IsTrue(ByName<VisualElement>("tool-paint").ClassListContains("selected"));
            state.SetTool(Tool.Erase); // 단축키와 같은 경로
            yield return null;
            Assert.IsTrue(ByName<VisualElement>("tool-erase").ClassListContains("selected"), "단축키로 바꿔도 강조가 따라감");
            Assert.IsFalse(ByName<VisualElement>("tool-paint").ClassListContains("selected"));
            yield return ClickName("color-3");
            Assert.AreEqual(3, state.Color);
            Assert.IsTrue(ByName<VisualElement>("color-3").ClassListContains("selected"));
            Assert.IsFalse(ByName<Button>("submit").enabledSelf);
            Assert.IsFalse(ByName<Button>("undo").enabledSelf);
            state.SetTool(Tool.Place);
            state.Place(0, 0);
            yield return null;
            Assert.IsTrue(ByName<Button>("undo").enabledSelf);
            yield return ClickName("undo");
            Assert.AreEqual(0, state.UsedTotal);
            Assert.IsTrue(ByName<Button>("redo").enabledSelf);
            yield return ClickName("view-top");
            yield return ClickName("camera");
            Assert.IsNull(ui.ExpandedView, "카메라 아이콘은 기본 시점으로");
            yield return Save("ui-tray.png");
            yield return ClickName("impossible");
            Assert.AreEqual(Phase.Result, state.Phase);
        }

        [UnityTest]
        public IEnumerator 결과_팝업에서_다음_문제로_넘어간다()
        {
            ui.Play(null); // p1: 만들 수 있는 문제
            yield return null;
            var state = editor.State;
            yield return ClickName("impossible"); // 틀린 불가능 선언
            Assert.IsNotNull(ByName<VisualElement>("result"));
            Assert.AreEqual(1, CountLabels("틀렸습니다, 만들 수 있는 문제"));
            Assert.AreEqual(1, CountLabels("−2"));
            Assert.AreNotEqual(Phase.Playing, state.Phase, "결과 중에는 편집 불가");
            yield return Save("ui-result-popup.png");
            var front = ByName<VisualElement>("view-front");
            var picked = root.panel.Pick(front.worldBound.center);
            Assert.IsTrue(picked == front || front.Contains(picked), "팝업이 떠 있어도 평면도 카드가 눌린다");
            yield return ClickName("view-front");
            Assert.AreEqual(ViewDir.Front, ui.ExpandedView, "결과 중에도 평면도 카드로 시점을 돌릴 수 있다");
            yield return ClickName("result-answer");
            Assert.IsTrue(state.ShowingAnswer);
            Assert.IsNull(root.Q("tray"), "정답을 볼 때는 트레이를 숨김");
            Assert.AreEqual(0, Live<Button>().Where(b => b.name == "result-answer").ToList().Count, "이미 정답을 보는 중");
            yield return Save("ui-result-answer.png");
            yield return ClickName("result-next");
            Assert.AreEqual("p2", state.Puzzle.Id);
            Assert.AreEqual(Phase.Playing, state.Phase);
            Assert.IsNull(ui.ExpandedView, "새 문제는 기본 시점");
            Assert.AreEqual(0, Live<VisualElement>().Where(e => e.name == "result").ToList().Count, "결과가 닫힘(사라지는 잔상 제외)");
        }

        [UnityTest]
        public IEnumerator 오답이면_틀린_평면도_카드에_표시한다()
        {
            ui.Play(null);
            yield return null;
            var state = editor.State;
            // 색 번호 순서대로 왼쪽 뒤 칸부터 채워 틀린 모형을 만든다.
            int x = 0, z = 0;
            for (int c = 1; c <= 6; c++)
                for (int n = 0; n < state.Puzzle.Counts[c]; n++)
                {
                    state.SetColor(c);
                    while (state.Model.Column(x, z).Count >= state.Puzzle.Height)
                    {
                        x++;
                        if (x == state.Puzzle.Size) { x = 0; z++; }
                    }
                    state.Place(x, z);
                }
            Assert.IsTrue(state.CanSubmit);
            yield return null;
            yield return ClickName("submit");
            Assert.IsFalse(state.Result.Correct, "이 채우기는 p1의 정답이 아니어야 한다");
            Assert.AreEqual(1, CountLabels("오답"));
            bool anyWrong = false;
            for (int i = 0; i < 5; i++)
            {
                var card = ByName<VisualElement>("view-" + ((ViewDir)i).ToString().ToLowerInvariant());
                Assert.AreEqual(!state.Result.ViewMatches[i], card.ClassListContains("wrong"), ((ViewDir)i).ToString());
                anyWrong |= card.ClassListContains("wrong");
            }
            Assert.IsTrue(anyWrong);
            yield return Save("ui-result-wrong.png");
        }

        [UnityTest]
        public IEnumerator 세로_화면에서도_모든_버튼이_화면_안에_들어온다()
        {
            ui.Play(Difficulty.Hard); // 6색, 5×5
            yield return null;
            yield return ClickName("view-front");
            Assert.IsFalse(ui.IsPortrait);
            yield return Resize(400, 800);
            Assert.IsTrue(ui.IsPortrait);
            Assert.AreEqual(ViewDir.Front, ui.ExpandedView, "배치가 바뀌어도 펼친 카드 유지");
            Assert.LessOrEqual(root.worldBound.width, 600f, "세로 화면은 폰 폭 기준으로 배치(가로 1280 기준을 줄여 쓰지 않음)");
            var screen = root.worldBound;
            foreach (var name in new[] { "pause", "tool-place", "tool-paint", "tool-erase", "undo", "redo", "camera", "impossible", "submit", "view-top", "view-right", "timer" })
            {
                var e = ByName<VisualElement>(name).worldBound;
                Assert.IsTrue(screen.Contains(e.min) && screen.Contains(e.max), name + " 화면 밖: " + e);
                if (!name.StartsWith("view") && name != "timer") Assert.GreaterOrEqual(Mathf.Min(e.width, e.height), 44f - 1.5f, name + " 44px 미만"); // 실제 픽셀에 맞춰 반올림되므로 1픽셀 여유
            }
            for (int c = 1; c <= 6; c++)
                if (editor.State.Puzzle.Counts[c] > 0)
                {
                    var chip = ByName<VisualElement>("color-" + c).worldBound;
                    Assert.IsTrue(screen.Contains(chip.min) && screen.Contains(chip.max), "color-" + c);
                }
            yield return Save("ui-portrait-game.png");

            editor.State.DeclareImpossible();
            yield return null;
            Assert.IsNotNull(ByName<VisualElement>("result"));
            var popup = ByName<VisualElement>("result").worldBound;
            Assert.IsTrue(screen.Contains(popup.min) && screen.Contains(popup.max), "결과 팝업이 화면 안");
            yield return Resize(1280, 800);
            Assert.IsFalse(ui.IsPortrait);
            Assert.IsNotNull(ByName<VisualElement>("result"), "가로로 돌아와도 결과 팝업 유지");

            yield return Resize(400, 800);
            ui.Show(UiScreen.DifficultySelect);
            yield return null;
            yield return null;
            screen = root.worldBound;
            foreach (var name in new[] { "mode-sample", "mode-hard", "mode-back", "mode-code" })
            {
                var e = ByName<VisualElement>(name).worldBound;
                Assert.IsTrue(screen.Contains(e.min) && screen.Contains(e.max), name + " 화면 밖");
            }
            Assert.AreEqual(ByName<VisualElement>("mode-sample").worldBound.y, ByName<VisualElement>("mode-easy").worldBound.y, 0.5f, "2×2: 연습·쉬움이 한 줄");
            Assert.Greater(ByName<VisualElement>("mode-normal").worldBound.y, ByName<VisualElement>("mode-easy").worldBound.y + 1, "2×2: 보통은 다음 줄");
            yield return Save("ui-portrait-mode.png");
            ui.Show(UiScreen.Main);
            yield return null;
            yield return Save("ui-portrait-main.png");
        }

        [UnityTest]
        public IEnumerator 결과_팝업은_창_애니메이션으로_열리고_닫힌다()
        {
            ui.Play(Difficulty.Easy);
            yield return new WaitForSecondsRealtime(0.4f); // 화면 전환이 끝난 뒤
            editor.State.DeclareImpossible();
            yield return null;
            yield return null;
            var overlay = ByName<VisualElement>("result").parent;
            Assert.Less(overlay.resolvedStyle.opacity, 0.99f, "열리는 중에는 서서히 나타난다");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1f, ByName<VisualElement>("result").parent.resolvedStyle.opacity, 0.01f);

            yield return ClickName("pause"); // 일시정지가 결과 위에 떠도 결과 팝업은 그대로(잔상 없음)
            Assert.AreEqual(0, root.Query(className: GameUI.ClosingClass).ToList().Count, "아래에 남는 결과 팝업은 사라지는 잔상이 아니다");
            yield return ClickName("pause-resume");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1f, ByName<VisualElement>("result").parent.resolvedStyle.opacity, 0.01f, "일시정지를 닫으면 결과 팝업이 다시 튀어나오지 않는다");

            yield return ClickName("result-copy");
            Assert.AreEqual(1, Live<Label>().Where(l => l.text.StartsWith("복사했습니다")).ToList().Count);
            yield return ClickName("result-next");
            Assert.AreEqual(1, root.Query(className: GameUI.ClosingClass).ToList().Count, "닫힌 결과 팝업이 서서히 사라진다");
            editor.State.DeclareImpossible();
            yield return null;
            yield return null;
            Assert.AreEqual(0, Live<Label>().Where(l => l.text.StartsWith("복사했습니다")).ToList().Count, "이전 문제의 복사 문장이 남지 않는다");
        }

        [UnityTest]
        public IEnumerator 메인에서_연_설정은_난이도_게임_뒤에도_내_모형_표시를_바꿀_수_있다()
        {
            ui.Play(Difficulty.Easy);
            yield return null;
            ui.Show(UiScreen.Main);
            yield return null;
            yield return ClickName("main-settings");
            var toggle = ByName<Toggle>("settings-showmine");
            Assert.IsTrue(toggle.enabledSelf, "게임 밖에서는 설정을 바꿀 수 있다");
            toggle.value = true;
            yield return null;
            Assert.IsTrue(editor.State.ShowMineSetting);
            ui.OnEscape();
            yield return null;
            yield return ClickName("main-settings");
            Assert.IsTrue(ByName<Toggle>("settings-showmine").value, "켜 둔 설정이 그대로 보인다");
            ByName<Toggle>("settings-showmine").value = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator 글자가_없는_버튼에_마우스를_올리면_이름이_뜬다()
        {
            ui.Play(null);
            yield return new WaitForSecondsRealtime(0.4f);
            var tip = ByName<Label>("tooltip");
            Assert.AreEqual(DisplayStyle.None, tip.resolvedStyle.display, "처음에는 숨김");

            var pause = ByName<Button>("pause");
            yield return Hover(pause, true);
            Assert.AreEqual(DisplayStyle.None, ByName<Label>("tooltip").resolvedStyle.display, "바로 뜨지 않고 잠깐 머물러야 뜬다");
            yield return new WaitForSecondsRealtime(0.45f);
            tip = ByName<Label>("tooltip");
            Assert.AreEqual(DisplayStyle.Flex, tip.resolvedStyle.display);
            Assert.AreEqual("일시정지 (Esc)", tip.text);
            var screen = root.worldBound;
            Assert.IsTrue(screen.Contains(tip.worldBound.min) && screen.Contains(tip.worldBound.max), "화면 왼쪽 끝 버튼이어도 툴팁이 화면 안: " + tip.worldBound);
            Assert.GreaterOrEqual(tip.worldBound.yMin, pause.worldBound.yMax, "위쪽 버튼은 아래에 뜬다");
            Assert.AreNotEqual(tip, tip.panel.Pick(tip.worldBound.center), "툴팁은 클릭을 가로채지 않는다");
            yield return Save("ui-tooltip.png");
            yield return Hover(pause, false);
            Assert.AreEqual(DisplayStyle.None, ByName<Label>("tooltip").resolvedStyle.display, "벗어나면 숨김");

            var chip = ByName<Button>("color-1");
            yield return Hover(chip, true);
            yield return new WaitForSecondsRealtime(0.45f);
            tip = ByName<Label>("tooltip");
            Assert.AreEqual("빨강 (1)", tip.text);
            Assert.LessOrEqual(tip.worldBound.yMax, chip.worldBound.yMin, "아래쪽 버튼은 위에 뜬다");
            Assert.IsTrue(screen.Contains(tip.worldBound.min) && screen.Contains(tip.worldBound.max));

            yield return Hover(ByName<Button>("submit"), true); // 글자가 있는 버튼으로 옮기면
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.AreEqual("블록을 모두 쓴 모형을 제출 (정답 +1, 오답 −1)", ByName<Label>("tooltip").text, "툴팁 문장이 있는 버튼은 모두 보여 준다");
        }

        [UnityTest]
        public IEnumerator 모든_아이콘을_그릴_수_있다()
        {
            // 메인 화면 위에 모든 아이콘을 한 줄로 붙이고 캡처한다.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.position = Position.Absolute;
            row.style.top = 10;
            row.style.left = 10;
            foreach (Icon icon in System.Enum.GetValues(typeof(Icon))) row.Add(Icons.Make(icon, 40, Color.white));
            root.Add(row);
            yield return Save("ui-icons.png");
            Assert.AreEqual(System.Enum.GetValues(typeof(Icon)).Length, row.childCount);
            foreach (var child in row.Children()) Assert.AreEqual(40f, child.resolvedStyle.width, 0.5f);
        }

        [UnityTest]
        public IEnumerator 화면이_바뀌면_전환_애니메이션이_재생되고_그동안에도_클릭할_수_있다()
        {
            yield return ClickName("main-play");
            Assert.AreEqual(UiScreen.DifficultySelect, ui.CurrentScreen);
            Assert.IsTrue(ui.IsTransitioning);

            // 전환 중에 덮인 막이 클릭을 막지 않는다.
            yield return ClickName("mode-back");
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(ui.IsTransitioning);
        }

        [UnityTest]
        public IEnumerator 메인_화면에서_Esc를_누르면_종료_확인_창을_띄운다()
        {
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            ui.OnEscape();
            yield return null;
            Assert.IsTrue(ui.IsQuitOpen);
            yield return Save("ui-quit.png");

            // "종료"는 에디터에서 플레이 모드를 끝내므로 누르지 않는다.
            yield return ClickName("quit-cancel");
            Assert.IsFalse(ui.IsQuitOpen);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.AreEqual(1, root.Query(className: GameUI.ClosingClass).ToList().Count, "닫힌 창이 서서히 사라지는 중");
            var start = ByName<Button>("main-play");
            var picked = root.panel.Pick(start.worldBound.center);
            Assert.IsTrue(picked == start || start.Contains(picked), "잔상이 남아 있어도 뒤의 버튼에 클릭이 닿는다");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0, root.Query(className: GameUI.ClosingClass).ToList().Count, "다 사라지면 치운다");

            ui.OnEscape();
            ui.OnEscape(); // 한 번 더 누르면 닫힌다
            yield return null;
            Assert.IsFalse(ui.IsQuitOpen);

            // 게임 화면의 Esc는 그대로 메뉴를 연다.
            ui.Play(null);
            yield return null;
            ui.OnEscape();
            Assert.IsFalse(ui.IsQuitOpen);
            Assert.IsTrue(ui.IsMenuOpen);
        }

        [UnityTest]
        public IEnumerator 인트로_애니메이션_뒤_아무_버튼으로_메인에_들어간다()
        {
            PlayerPrefs.DeleteKey(GameUI.TutorialSeenKey);
            yield return Create(keepIntro: true);
            Assert.AreEqual(UiScreen.Intro, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled);
            var press = Live<Label>().Where(l => l.text == "PRESS ANY BUTTON").First();
            Assert.IsNotNull(press);
            Assert.IsNull(root.Q("main-play"), "인트로에서는 메인 화면이 아직 없다");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Save("ui-intro-1.png"); // 블록이 떨어지는 중
            Assert.AreEqual(0f, press.resolvedStyle.opacity, 0.01f, "애니메이션 중에는 아직 안 보인다");

            yield return new WaitForSecondsRealtime(2.2f);
            yield return Save("ui-intro-2.png");
            float min = 1, max = 0; // 깜박임 한 주기(1.2초) 넘게 지켜본다
            for (int i = 0; i < 14; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                min = Mathf.Min(min, press.resolvedStyle.opacity);
                max = Mathf.Max(max, press.resolvedStyle.opacity);
            }
            Assert.Greater(max - min, 0.5f, $"깜박여야 한다 ({min} ~ {max})");

            ui.EnterMain(); // 실제로는 아무 키·마우스 클릭·터치가 이것을 부른다
            yield return null;
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsNotNull(ByName<Button>("main-play"));
            Assert.IsTrue(ui.IsAskingTutorial, "첫 접속이면 메인에서 튜토리얼 안내가 뜬다");
        }

        [UnityTest]
        public IEnumerator 첫_접속에만_튜토리얼_안내를_띄우고_건너뛰면_다시_묻지_않는다()
        {
            Assert.IsFalse(ui.IsAskingTutorial, "이미 본 사람에게는 묻지 않는다");

            PlayerPrefs.DeleteKey(GameUI.TutorialSeenKey);
            yield return Create();
            Assert.IsTrue(ui.IsAskingTutorial);
            Assert.AreEqual(1, CountButtons("튜토리얼 시작"));
            yield return Save("ui-tutorial-prompt.png");
            yield return ClickName("tutorial-skip");
            Assert.IsFalse(ui.IsAskingTutorial);
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameUI.TutorialSeenKey, 0));

            yield return Create(); // 다시 접속
            Assert.IsFalse(ui.IsAskingTutorial);
        }

        [UnityTest]
        public IEnumerator 튜토리얼을_진행하고_메뉴에서_다시_시작할_수_있다()
        {
            PlayerPrefs.DeleteKey(GameUI.TutorialSeenKey);
            yield return Create();
            yield return ClickName("tutorial-start");
            Assert.IsTrue(ui.InTutorial);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameUI.TutorialSeenKey, 0), "튜토리얼을 시작하면 다시 묻지 않는다");
            Assert.AreEqual(1, CountLabels("튜토리얼 1 / 10"));
            Assert.IsTrue(editor.State.ShowMine, "튜토리얼에서는 내 모형 평면도를 보여 준다");

            yield return ClickName("tutorial-next");
            yield return ClickName("tutorial-next");
            Assert.AreEqual(1, CountLabels("블록 놓기"));
            Assert.AreEqual(1, CountLabels("▶ 직접 해 보세요"));
            var card = ByName<VisualElement>("tutorial-card");
            Assert.LessOrEqual(card.worldBound.width, 420.5f, "안내 카드는 화면 폭만큼 넓지 않다");
            yield return ClickName("view-front");
            var expanded = ByName<VisualElement>("view-expanded").worldBound;
            card = ByName<VisualElement>("tutorial-card");
            Assert.IsFalse(card.worldBound.Overlaps(expanded), "펼친 평면도와 안내 카드가 겹치지 않는다");
            Assert.IsFalse(card.worldBound.Overlaps(ByName<VisualElement>("tray").worldBound), "안내 카드가 트레이를 가리지 않는다");
            yield return ClickName("view-front");
            yield return Save("ui-tutorial.png");
            editor.State.Place(0, 0);
            yield return null;
            yield return null;
            Assert.AreEqual(1, CountLabels("쌓기"), "블록을 놓으면 다음 단계로");

            yield return ClickName("tutorial-stop");
            Assert.IsFalse(ui.InTutorial);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(editor.State.ShowMineSetting, "튜토리얼 전 설정(꺼짐)으로 돌아간다");

            yield return ClickName("main-tutorial"); // 메인 화면의 버튼
            Assert.IsTrue(ui.InTutorial);
            yield return ClickName("pause");
            yield return ClickName("pause-next"); // 다른 문제로 넘어가면 튜토리얼은 끝난다
            yield return null;
            Assert.IsFalse(ui.InTutorial);

            yield return ClickName("pause");
            yield return ClickName("pause-tutorial"); // 일시정지 창의 버튼
            Assert.IsTrue(ui.InTutorial);
            Assert.AreEqual(1, CountLabels("튜토리얼 1 / 10"));
        }

        [UnityTest]
        public IEnumerator 일시정지와_설정()
        {
            ui.Play(null);
            yield return null;
            ui.OnEscape();
            yield return null;
            Assert.IsTrue(ui.IsMenuOpen);
            Assert.IsFalse(editor.enabled, "일시정지 창이 떠 있는 동안 편집 입력을 막는다");
            Assert.AreEqual(1, CountLabels("샘플 1 / 6"), "제목은 문제 이름");
            Assert.AreEqual(0, CountButtons("난이도 변경"));
            Assert.IsFalse(ByName<Button>("pause-restart").enabledSelf, "놓은 블록이 없으면 다시 시작 비활성");
            Assert.IsNull(root.Q("pause-copy"), "샘플은 코드 복사 없음");
            yield return Save("ui-pause.png");

            yield return ClickName("pause-settings");
            Assert.IsTrue(ui.IsSettingsOpen);
            Assert.IsTrue(ui.IsMenuOpen, "설정은 일시정지 위에 뜬다");
            var volume = ByName<Slider>("settings-volume");
            var showMine = ByName<Toggle>("settings-showmine");
            volume.value = 0.25f;
            showMine.value = true;
            yield return null;
            // 배치 모드는 오디오 장치가 없어 AudioListener.volume이 항상 1이므로 저장된 값으로 확인한다.
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("volume"), 1e-4f, "다음 실행에도 유지");
            Assert.IsTrue(editor.State.ShowMine);
            Assert.IsTrue(showMine.enabledSelf, "연습 모드에서는 켤 수 있다");
            Assert.IsNotNull(ByName<Button>("settings-reset"));
            yield return Save("ui-settings.png");
            ui.OnEscape();
            yield return null;
            Assert.IsFalse(ui.IsSettingsOpen);
            Assert.IsTrue(ui.IsMenuOpen, "설정을 닫으면 일시정지로");
            yield return ClickName("pause-resume");
            Assert.IsFalse(ui.IsMenuOpen);
            Assert.IsTrue(editor.enabled);

            editor.State.Place(0, 0);
            ui.OnEscape();
            yield return null;
            yield return ClickName("pause-restart");
            Assert.IsTrue(ui.IsConfirming, "다시 시작은 확인을 거친다");
            yield return ClickName("confirm-leave");
            Assert.AreEqual(0, editor.State.UsedTotal);
            Assert.IsTrue(editor.State.CanUndo, "다시 시작도 실행 취소로 되돌릴 수 있다");

            editor.State.DeclareImpossible(); // 결과 중 Esc → 일시정지
            yield return null;
            ui.OnEscape();
            yield return null;
            Assert.IsTrue(ui.IsMenuOpen);

            ui.Play(Difficulty.Easy); // 난이도 모드: 평면도 표시를 켤 수 없고, 코드 복사가 있다
            yield return null;
            ui.OnEscape();
            yield return null;
            Assert.IsNotNull(ByName<Button>("pause-copy"));
            yield return ClickName("pause-settings");
            Assert.IsFalse(ByName<Toggle>("settings-showmine").enabledSelf);
            Assert.IsFalse(editor.State.ShowMine);
            Assert.AreEqual(0, CountLabels("내 모형"), "평면도 카드에 내 모형 격자가 없다");
            AudioListener.volume = 1f;
        }

        [UnityTest]
        public IEnumerator 정답_모형을_쌓으면_블록이_그려지고_클릭_판정에_잡힌다()
        {
            ui.Play(null); // 샘플 p1 (3×3, 블록 7개)
            var state = editor.State;
            state.SetShowMine(true); // 평면도 "일치" 표시를 보려고 켠다(이 문제는 점수 미반영)
            var answer = state.Puzzle.Answer;

            for (int z = 0; z < answer.Size; z++)
                for (int x = 0; x < answer.Size; x++)
                    foreach (int color in answer.Column(x, z))
                    {
                        state.SetColor(color);
                        state.Place(x, z);
                    }
            yield return null; // 이전 큐브들의 Destroy가 끝나기를 기다린다
            yield return null;

            Assert.IsTrue(state.CanSubmit);
            Assert.AreEqual(9 + 7, editor.transform.Find("Blocks").childCount, "바닥 9칸 + 블록 7개");
            foreach (var r in editor.GetComponentsInChildren<Renderer>(true))
                Assert.AreEqual("Block", r.sharedMaterial.name, $"{r.name}: 빌드에 포함되는 Resources 머티리얼이어야 한다(기본 머티리얼은 빌드에서 보라색)");

            // 앞-오른쪽 칸(x=2, z=2)의 노랑 블록은 월드 (1, 0.5, -1)에 있어야 하고 화면에서 클릭된다.
            var cam = Camera.main;
            var ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(new Vector3(1, 0.5f, -1)));
            Assert.IsTrue(Physics.Raycast(ray, out RaycastHit hit));
            Assert.AreEqual(new Vector3(1, 0.5f, -1), hit.transform.position);

            // UI: 문제에 없는 색(p1의 주황·보라)은 칩이 없고, 정답 모형이라 다섯 평면도 모두 일치.
            Assert.IsNull(root.Q("color-2"), "주황 칩 없음");
            Assert.IsNull(root.Q("color-6"), "보라 칩 없음");
            Assert.IsNotNull(ByName<Button>("color-1"), "빨강 칩");
            foreach (var dir in new[] { "top", "front", "back", "left", "right" })
                Assert.IsTrue(ByName<VisualElement>("view-" + dir).ClassListContains("match"), dir);
            yield return Save("ui-game-sample.png");

            state.Submit();
            yield return null;
            yield return null;
            Assert.AreEqual(1, CountLabels("정답!"));
            Assert.AreEqual(1, CountLabels("점수 미반영 (내 모형의 평면도 표시를 켠 문제)"));
            yield return Save("ui-result.png");
        }
    }
}
