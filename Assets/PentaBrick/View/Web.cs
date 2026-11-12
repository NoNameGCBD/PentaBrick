using System.Runtime.InteropServices;
using PentaBrick.Core;
using PentaBrick.Game;
using UnityEngine;

namespace PentaBrick.View
{
    /// <summary>WebGL(브라우저)과 그 밖(에디터·PC)에서 다르게 동작하는 기능. 브라우저 쪽은 Plugins/WebGL/PentaBrick.jslib.</summary>
    public static class Web
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool IsBrowser = true;
        [DllImport("__Internal")] private static extern void PentaBrick_CopyText(string text);
        [DllImport("__Internal")] private static extern void PentaBrick_SetQuery(string query);
        public static void Copy(string text) => PentaBrick_CopyText(text);
        public static void SetQuery(string query) => PentaBrick_SetQuery(query);
#else
        public const bool IsBrowser = false;
        public static void Copy(string text) => GUIUtility.systemCopyBuffer = text;
        public static void SetQuery(string query) { }
#endif

        /// <summary>같은 문제로 바로 들어오는 주소의 ?뒤. 생성 문제가 아니면(샘플·튜토리얼) 빈 문자열.</summary>
        public static string Query(GameState state) =>
            state.Mode != null && state.Puzzle.Seed != null
                ? $"?mode={DifficultySettings.Get(state.Mode.Value).Key}&seed={SeedCode.Encode(state.Puzzle.Seed.Value)}"
                : "";
    }
}
