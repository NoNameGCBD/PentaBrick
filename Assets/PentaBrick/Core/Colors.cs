namespace PentaBrick.Core
{
    /// <summary>블록 색 번호. 0은 빈칸, 1~6이 색이다. (명세서 5.8)</summary>
    public static class Colors
    {
        public const int Empty = 0;
        public const int Red = 1;
        public const int Orange = 2;
        public const int Yellow = 3;
        public const int Green = 4;
        public const int Blue = 5;
        public const int Purple = 6;

        /// <summary>화면에 보이는 색 이름. 인덱스 = 색 번호.</summary>
        public static readonly string[] Names = { "빈칸", "빨강", "주황", "노랑", "초록", "파랑", "보라" };

        /// <summary>색의 가짓수(6).</summary>
        public const int Count = 6;

        /// <summary>유효한 색 번호(1~6)인지 검사한다.</summary>
        public static bool IsColor(int value) => value >= 1 && value <= Count;
    }
}
