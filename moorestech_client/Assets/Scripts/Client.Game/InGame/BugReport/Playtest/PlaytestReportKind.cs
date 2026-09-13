namespace Client.Game.InGame.BugReport.Playtest
{
    // プレイ報告の種別。文字列は受け口・取り込み側と共有する契約値なのでここが正本（ADR 0058）
    // The play-report kind; these strings are the contract shared with the receiver and the ingest side (ADR 0058)
    public static class PlaytestReportKind
    {
        public const string Bug = "bug";
        public const string Feedback = "feedback";
        public const string Crash = "crash";

        // クラッシュは前回異常終了ゲートだけが作る。ポーズメニューからは選ばせない
        // Only the previous-crash gate creates the crash kind; the pause menu must not offer it
        public static bool IsSubmittableFromPauseMenu(string kind)
        {
            return kind == Bug || kind == Feedback;
        }

        public static bool IsKnown(string kind)
        {
            return kind == Bug || kind == Feedback || kind == Crash;
        }
    }
}
