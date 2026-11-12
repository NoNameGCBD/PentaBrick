namespace PentaBrick.Game
{
    public enum Sound { None, Place, Erase, Paint, Correct, Wrong }

    /// <summary>
    /// 상태가 바뀌기 전과 후를 비교해 낼 효과음을 고른다.
    /// 동작 대신 결과를 보므로 되돌리기·다시하기·초기화에도 그대로 맞는다.
    /// </summary>
    public static class SoundCue
    {
        public readonly struct Snapshot
        {
            public readonly int Serial;
            public readonly Phase Phase;
            public readonly Core.Model Model;

            private Snapshot(GameState game) { Serial = game.PuzzleSerial; Phase = game.Phase; Model = game.Model; }

            public static Snapshot Of(GameState game) => new Snapshot(game);
        }

        public static Sound Pick(Snapshot before, GameState after)
        {
            if (before.Serial != after.PuzzleSerial) return Sound.None; // 새 문제
            if (after.Phase == Phase.Result)
                return before.Phase == Phase.Playing ? (after.Result.Correct ? Sound.Correct : Sound.Wrong) : Sound.None;
            int delta = after.Model.TotalBlocks - before.Model.TotalBlocks;
            if (delta > 0) return Sound.Place;
            if (delta < 0) return Sound.Erase;
            return after.Model.SameAs(before.Model) ? Sound.None : Sound.Paint;
        }
    }
}
