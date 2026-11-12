using System.Collections;
using System.IO;
using NUnit.Framework;
using PentaBrick.Core;
using PentaBrick.View;
using UnityEngine;
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

        private int CountLabels(string text) => Live<Label>().Where(l => l.text == text).ToList().Count;
        private int CountButtons(string start) => Live<Button>().Where(b => b.text.StartsWith(start)).ToList().Count;

        [UnityTest]
        public IEnumerator 메인에서_게임_시작을_누르면_난이도_선택을_거쳐_게임이_시작된다()
        {
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled, "메뉴에서는 3D 편집 입력이 꺼져 있다");
            Assert.AreEqual(1, CountButtons("게임 시작"));
            Assert.AreEqual(0, CountButtons("1 빨강"), "메인 화면에는 게임 패널이 없다");
            yield return Save("ui-main.png");

            ui.Show(UiScreen.DifficultySelect);
            yield return null;
            Assert.AreEqual(1, CountButtons("샘플"));
            Assert.AreEqual(1, CountButtons("쉬움"));
            Assert.AreEqual(1, CountButtons("보통"));
            Assert.AreEqual(1, CountButtons("어려움"));
            Assert.IsNotNull(root.Q<TextField>(), "문제 코드 입력 칸");
            Assert.IsFalse(ui.IsTyping);
            yield return Save("ui-difficulty.png");

            ui.Play(Difficulty.Hard);
            yield return null;
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsTrue(editor.enabled);
            Assert.AreEqual(Difficulty.Hard, editor.State.Mode);
            Assert.AreEqual(1, CountLabels("상"), "평면도 카드");
            Assert.AreEqual(1, CountButtons("메뉴 (Esc)"));
            Assert.AreEqual(0, CountButtons("메인 메뉴"), "메인 메뉴·난이도 변경은 Esc 메뉴 안으로 옮겼다");
            Assert.AreEqual(0, CountButtons("난이도 변경"));
            Assert.AreEqual(0, Live<Toggle>().ToList().Count, "평면도 표시 옵션은 설정으로 옮겼다");
            var basicView = Live<Button>().Where(b => b.text == "기본").First();
            Assert.IsNotNull(basicView, "시점 버튼");
            Assert.Greater(basicView.worldBound.height, 0);
            Assert.LessOrEqual(basicView.worldBound.yMax, root.worldBound.yMax + 0.5f, "시점 버튼이 화면 안에 있다");
            yield return Save("ui-game-hard.png");

            ui.Show(UiScreen.Main);
            yield return null;
            Assert.IsFalse(editor.enabled);
            Assert.AreEqual(1, CountButtons("게임 시작"));
        }

        /// <summary>버튼을 실제로 누른 것처럼 클릭 이벤트를 보내고 화면이 다시 그려지기를 기다린다.</summary>
        private IEnumerator Click(string text)
        {
            var button = Live<Button>().Where(b => b.text == text).First();
            Assert.IsNotNull(button, $"\"{text}\" 버튼이 없음");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator 풀던_중에_나가려고_하면_확인_창을_띄운다()
        {
            ui.Play(null);
            yield return null;
            var state = editor.State;

            // 아무것도 안 놓았으면 바로 나간다.
            yield return Click("메뉴 (Esc)");
            yield return Click("메인 메뉴");
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(ui.IsConfirming);

            ui.Play(null);
            yield return null;
            state.Place(0, 0);
            yield return null;

            // 메인 메뉴 → 확인 창 → 계속 풀기: 그대로 남는다.
            yield return Click("메뉴 (Esc)");
            yield return Click("메인 메뉴");
            Assert.IsTrue(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled, "확인 창이 떠 있는 동안 편집 입력을 막는다");
            Assert.IsTrue(ui.IsPointerOverUI(new Vector2(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f)), "3D 화면 클릭도 막는다");
            yield return Save("ui-confirm.png");
            yield return Click("계속 풀기");
            Assert.IsFalse(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.IsTrue(editor.enabled);
            Assert.AreEqual(1, state.UsedTotal);

            // 난이도 변경 → 나가기
            yield return Click("메뉴 (Esc)");
            yield return Click("난이도 변경");
            Assert.IsTrue(ui.IsConfirming);
            yield return Click("나가기");
            Assert.AreEqual(UiScreen.DifficultySelect, ui.CurrentScreen);
            Assert.IsFalse(editor.enabled);

            // 다음 샘플 → 나가기: 다음 문제로
            ui.Play(null);
            yield return null;
            state.Place(0, 0);
            state.Reset(); // 블록은 없지만 초기화를 되돌릴 수 있으므로 잃을 것이 있다
            yield return null;
            yield return Click("메뉴 (Esc)");
            yield return Click("다음 샘플");
            Assert.IsTrue(ui.IsConfirming);
            yield return Click("나가기");
            Assert.AreEqual("p2", state.Puzzle.Id);

            // 제출한 뒤에는 묻지 않는다.
            state.Place(0, 0);
            state.DeclareImpossible();
            yield return null;
            yield return Click("메뉴 (Esc)");
            yield return Click("메인 메뉴");
            Assert.IsFalse(ui.IsConfirming);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator 화면이_바뀌면_전환_애니메이션이_재생되고_그동안에도_클릭할_수_있다()
        {
            yield return Click("게임 시작");
            Assert.AreEqual(UiScreen.DifficultySelect, ui.CurrentScreen);
            Assert.IsTrue(ui.IsTransitioning);
            yield return Save("ui-transition.png");

            // 전환 중에 덮인 막이 클릭을 막지 않는다.
            yield return Click("← 메인으로");
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
            yield return Click("취소");
            Assert.IsFalse(ui.IsQuitOpen);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.AreEqual(1, root.Query(className: GameUI.ClosingClass).ToList().Count, "닫힌 창이 서서히 사라지는 중");
            var start = Live<Button>().Where(b => b.text == "게임 시작").First();
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
            Assert.AreEqual(0, CountButtons("게임 시작"), "인트로에서는 메인 화면이 아직 없다");

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
            Assert.AreEqual(1, CountButtons("게임 시작"));
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
            yield return Click("건너뛰기");
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
            yield return Click("튜토리얼 시작");
            Assert.IsTrue(ui.InTutorial);
            Assert.AreEqual(UiScreen.Game, ui.CurrentScreen);
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameUI.TutorialSeenKey, 0), "튜토리얼을 시작하면 다시 묻지 않는다");
            Assert.AreEqual(1, CountLabels("튜토리얼 1 / 10"));
            Assert.IsTrue(editor.State.ShowMine, "튜토리얼에서는 내 모형 평면도를 보여 준다");

            yield return Click("다음 →");
            yield return Click("다음 →");
            Assert.AreEqual(1, CountLabels("블록 놓기"));
            Assert.AreEqual(1, CountLabels("▶ 직접 해 보세요"));
            yield return Save("ui-tutorial.png");
            editor.State.Place(0, 0);
            yield return null;
            yield return null;
            Assert.AreEqual(1, CountLabels("쌓기"), "블록을 놓으면 다음 단계로");

            yield return Click("그만하기");
            Assert.IsFalse(ui.InTutorial);
            Assert.AreEqual(UiScreen.Main, ui.CurrentScreen);
            Assert.IsFalse(editor.State.ShowMineSetting, "튜토리얼 전 설정(꺼짐)으로 돌아간다");

            yield return Click("튜토리얼"); // 메인 화면의 버튼
            Assert.IsTrue(ui.InTutorial);
            yield return Click("메뉴 (Esc)");
            yield return Click("다음 샘플"); // 다른 문제로 넘어가면 튜토리얼은 끝난다
            yield return null;
            Assert.IsFalse(ui.InTutorial);

            yield return Click("메뉴 (Esc)");
            yield return Click("튜토리얼"); // Esc 메뉴의 버튼
            Assert.IsTrue(ui.InTutorial);
            Assert.AreEqual(1, CountLabels("튜토리얼 1 / 10"));
        }

        [UnityTest]
        public IEnumerator Esc_메뉴와_설정()
        {
            ui.Play(null);
            yield return null;
            ui.OnEscape();
            yield return null;
            Assert.IsTrue(ui.IsMenuOpen);
            Assert.IsFalse(editor.enabled, "메뉴가 떠 있는 동안 편집 입력을 막는다");
            foreach (string text in new[] { "계속하기", "다음 샘플", "난이도 변경", "설정", "메인 메뉴" })
                Assert.AreEqual(1, CountButtons(text), text);
            yield return Save("ui-menu.png");

            yield return Click("설정");
            var volume = Live<Slider>().Where(x => x.label == "소리 크기").First(); // 스크롤바도 Slider라서 이름으로 찾는다
            Assert.IsNotNull(volume, "소리 크기");
            var showMine = Live<Toggle>().Where(x => x.label.StartsWith("내 모형")).First();
            Assert.IsNotNull(showMine, "평면도 표시");
            volume.value = 0.25f;
            showMine.value = true;
            yield return null;
            // 배치 모드는 오디오 장치가 없어 AudioListener.volume이 항상 1이므로 저장된 값으로 확인한다.
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("volume"), 1e-4f, "다음 실행에도 유지");
            Assert.IsTrue(editor.State.ShowMine);
            Assert.IsTrue(showMine.enabledSelf, "연습 모드에서는 켤 수 있다");
            Assert.AreEqual(1, CountButtons("점수 초기화"));
            yield return Save("ui-settings.png");

            ui.OnEscape(); // 설정 → 메뉴
            yield return null;
            Assert.IsTrue(ui.IsMenuOpen);
            Assert.AreEqual(1, CountButtons("계속하기"));
            ui.OnEscape(); // 메뉴 닫기
            yield return null;
            Assert.IsFalse(ui.IsMenuOpen);
            Assert.IsTrue(editor.enabled);

            yield return Click("메뉴 (Esc)");
            yield return Click("계속하기");
            Assert.IsFalse(ui.IsMenuOpen);
            Assert.IsTrue(editor.enabled);

            ui.Play(Difficulty.Easy); // 난이도 모드: 평면도 표시를 켤 수 없다
            yield return null;
            ui.OnEscape();
            yield return null;
            yield return Click("설정");
            Assert.IsFalse(Live<Toggle>().Where(x => x.label.StartsWith("내 모형")).First().enabledSelf);
            Assert.IsFalse(editor.State.ShowMine);
            Assert.AreEqual(0, CountLabels("내 모형"), "평면도 카드에 내 모형 격자가 없다");

            ui.Show(UiScreen.Main);
            yield return null;
            ui.OnEscape(); // 메인 화면에서는 메뉴 대신 종료 확인 창
            Assert.IsFalse(ui.IsMenuOpen);

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

            // 앞-오른쪽 칸(x=2, z=2)의 노랑 블록은 월드 (1, 0.5, -1)에 있어야 하고 화면에서 클릭된다.
            var cam = Camera.main;
            var ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(new Vector3(1, 0.5f, -1)));
            Assert.IsTrue(Physics.Raycast(ray, out RaycastHit hit));
            Assert.AreEqual(new Vector3(1, 0.5f, -1), hit.transform.position);

            // UI: 문제에 없는 색(p1의 주황·보라)은 비활성, 정답 모형이라 다섯 평면도 모두 일치.
            var swatches = Live<Button>().Where(b => b.text.Contains("/")).ToList();
            Assert.AreEqual(6, swatches.Count, "팔레트 6칸");
            Assert.IsFalse(swatches[1].enabledSelf, "주황");
            Assert.IsFalse(swatches[5].enabledSelf, "보라");
            Assert.IsTrue(swatches[0].enabledSelf, "빨강");
            Assert.AreEqual(5, CountLabels("일치"));
            yield return Save("ui-game-sample.png");

            state.Submit();
            yield return null;
            yield return null;
            Assert.AreEqual(1, Live<Label>().Where(l => l.text.StartsWith("정답입니다")).ToList().Count);
            Assert.AreEqual(1, CountLabels("점수 미반영 (내 모형의 평면도 표시를 켠 문제)"));
            yield return Save("ui-result.png");
        }
    }
}
