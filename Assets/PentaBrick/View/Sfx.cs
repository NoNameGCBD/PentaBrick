using PentaBrick.Game;
using UnityEngine;

namespace PentaBrick.View
{
    /// <summary>
    /// 효과음. 소리 파일 없이 시작할 때 파형을 합성해 둔다.
    /// 크기는 설정의 "소리 크기"(AudioListener.volume)를 따른다.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int Rate = 44100;

        private AudioSource source;
        private AudioClip[] clips; // 인덱스 = (int)Sound
        private GameState state;
        private SoundCue.Snapshot last;
        private AudioClip[] introBlocks; // 인트로 블록 여섯 개, 떨어질수록 한 음씩 높게(5음계)
        private AudioClip introTitle;
        private AudioClip click;
        private bool clickPending;
        /// <summary>실제로 낸 버튼 소리 수(테스트용).</summary>
        public int ClicksPlayed { get; private set; }
        private int cueFrame = -1; // 게임 효과음을 낸 프레임. 같은 프레임의 버튼 소리는 생략한다

        // 인트로는 GameUI.Start 직후부터 소리를 내므로 클립은 Awake에서 만든다.
        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            clips = new[]
            {
                null,
                Clip("place", (1320, 0.05f, false)),                                            // 짧고 높은 틱
                Clip("erase", (180, 0.12f, false)),                                             // 낮은 퉁
                Clip("paint", (880, 0.05f, false), (1175, 0.06f, false)),                      // 짧은 두 음
                Clip("correct", (523, 0.09f, false), (659, 0.09f, false), (784, 0.22f, false)), // 올라가는 세 음(도미솔)
                Clip("wrong", (330, 0.12f, true), (247, 0.25f, true)),                          // 내려가는 두 음, 사각파
            };
            float[] scale = { 523, 587, 659, 784, 880, 1047 }; // 도 레 미 솔 라 도
            introBlocks = new AudioClip[scale.Length];
            for (int i = 0; i < scale.Length; i++) introBlocks[i] = Clip("intro-block", (scale[i], 0.12f, false));
            click = Clip("click", (1000, 0.03f, false));
            introTitle = Clip("intro-title", (784, 0.07f, false), (1047, 0.07f, false), (1568, 0.6f, false)); // 솔-도-솔, 마지막 음을 길게
        }

        private void Start()
        {
            state = GetComponent<BlockEditor>().State;
            last = SoundCue.Snapshot.Of(state);
            state.Changed += () =>
            {
                Sound sound = SoundCue.Pick(last, state);
                last = SoundCue.Snapshot.Of(state);
                if (sound == Sound.None) return;
                source.PlayOneShot(clips[(int)sound]);
                cueFrame = Time.frameCount;
            };
        }

        /// <summary>버튼 소리. 그 버튼이 게임 효과음(실행 취소, 제출 등)을 냈으면 겹치지 않게 그것만 들린다.</summary>
        public void Click() => clickPending = true;

        // 버튼 콜백과 상태 변경의 순서에 상관없이 같은 프레임 안에서 판단하려고 프레임 끝에 낸다.
        private void LateUpdate()
        {
            if (clickPending && cueFrame != Time.frameCount)
            {
                source.PlayOneShot(click, 0.6f);
                ClicksPlayed++;
            }
            clickPending = false;
        }

        public void PlayIntroBlock(int index) => source.PlayOneShot(introBlocks[index % introBlocks.Length]);

        public void PlayIntroTitle() => source.PlayOneShot(introTitle);

        /// <summary>음들을 이어 붙인 클립. 음마다 빠르게 줄어드는 소리(타악기처럼)로 만든다.</summary>
        private static AudioClip Clip(string name, params (float freq, float seconds, bool square)[] notes)
        {
            int total = 0;
            foreach (var note in notes) total += (int)(note.seconds * Rate);
            var data = new float[total];
            int start = 0;
            foreach (var note in notes)
            {
                int length = (int)(note.seconds * Rate);
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / Rate;
                    float wave = Mathf.Sin(2 * Mathf.PI * note.freq * t);
                    if (note.square) wave = Mathf.Sign(wave) * 0.5f; // 사각파는 더 시끄럽게 들려 절반으로
                    float attack = Mathf.Min(1f, i / (0.004f * Rate)); // 시작 딸깍 소리 방지
                    float decay = Mathf.Exp(-5f * i / length);
                    data[start + i] = 0.4f * wave * attack * decay;
                }
                start += length;
            }
            var clip = AudioClip.Create(name, total, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
