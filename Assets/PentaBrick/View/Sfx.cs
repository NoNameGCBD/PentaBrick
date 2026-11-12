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

        private void Start()
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
            state = GetComponent<BlockEditor>().State;
            last = SoundCue.Snapshot.Of(state);
            state.Changed += () =>
            {
                Sound sound = SoundCue.Pick(last, state);
                last = SoundCue.Snapshot.Of(state);
                if (sound != Sound.None) source.PlayOneShot(clips[(int)sound]);
            };
        }

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
