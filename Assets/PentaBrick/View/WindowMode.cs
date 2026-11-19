using UnityEngine;

namespace PentaBrick.View
{
    /// <summary>
    /// 전체화면 / 창모드. PC 빌드는 전체화면이 기본이고 설정에서 창모드로 바꾸면 다음 실행에도 유지된다.
    /// 브라우저(WebGL)는 사용자 입력 없이 전체화면을 시작할 수 없으므로 저장하지 않고 그때그때 전환만 한다.
    /// </summary>
    public static class WindowMode
    {
        private const string WindowedKey = "windowed"; // 1이면 창모드
        private const float Aspect = 1.6f;             // 창모드 비율(UI 기준 해상도 1280x800과 같은 16:10)
        private const int MaxWindowHeight = 1080;      // 큰 모니터에서 창이 지나치게 커지지 않도록

        /// <summary>설정에 전체화면 항목을 보여 줄지. 에디터의 Game 뷰와 모바일에서는 바꿀 수 없다.</summary>
        public static bool Supported => !Application.isEditor && !Application.isMobilePlatform;

        /// <summary>설정 창에 표시할 값.</summary>
        public static bool IsFullscreen => Web.IsBrowser ? Screen.fullScreen : PlayerPrefs.GetInt(WindowedKey, 0) == 0;

        /// <summary>시작할 때 저장된 모드를 적용한다(처음이면 전체화면).</summary>
        public static void ApplySaved()
        {
            if (!Supported || Web.IsBrowser) return;
            Apply(IsFullscreen);
        }

        public static void Set(bool fullscreen)
        {
            if (!Supported) return;
            if (Web.IsBrowser)
            {
                Screen.fullScreen = fullscreen; // 브라우저는 다음 클릭 때 전체화면을 요청한다
                return;
            }
            PlayerPrefs.SetInt(WindowedKey, fullscreen ? 0 : 1);
            PlayerPrefs.Save();
            Apply(fullscreen);
        }

        private static void Apply(bool fullscreen)
        {
            var display = Screen.mainWindowDisplayInfo;
            int displayW = display.width > 0 ? display.width : Screen.currentResolution.width;
            int displayH = display.height > 0 ? display.height : Screen.currentResolution.height;
            if (fullscreen)
            {
                Screen.SetResolution(displayW, displayH, FullScreenMode.FullScreenWindow);
                return;
            }
            // 창모드: 화면 높이의 85%(최대 1080), 폭이 화면의 90%를 넘으면 폭에 맞춘다.
            int height = Mathf.Min(MaxWindowHeight, Mathf.RoundToInt(displayH * 0.85f));
            int width = Mathf.RoundToInt(height * Aspect);
            int maxWidth = Mathf.RoundToInt(displayW * 0.9f);
            if (width > maxWidth)
            {
                width = maxWidth;
                height = Mathf.RoundToInt(width / Aspect);
            }
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
    }
}
