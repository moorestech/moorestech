using System;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 報告種別と契約値の文字列の変換を1箇所に閉じる。受け口・取り込み側と共有する綴りの正本（ADR 0058）
    // Keeps the conversion between the report kind and its contract string in one place; the source of truth for the spelling shared with the receiver and ingest (ADR 0058)
    public static class PlaytestReportKindText
    {
        private const string BugText = "bug";
        private const string FeedbackText = "feedback";
        private const string CrashText = "crash";

        public static string ToContractText(PlaytestReportKind kind)
        {
            return kind switch
            {
                PlaytestReportKind.Bug => BugText,
                PlaytestReportKind.Feedback => FeedbackText,
                PlaytestReportKind.Crash => CrashText,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "契約値の無い報告種別"),
            };
        }

        public static bool TryParse(string text, out PlaytestReportKind kind)
        {
            switch (text)
            {
                case BugText:
                    kind = PlaytestReportKind.Bug;
                    return true;
                case FeedbackText:
                    kind = PlaytestReportKind.Feedback;
                    return true;
                case CrashText:
                    kind = PlaytestReportKind.Crash;
                    return true;
                default:
                    kind = PlaytestReportKind.Bug;
                    return false;
            }
        }

        // ポーズメニューから送れる種別の正本。クラッシュは前回異常終了ゲートだけが作る
        // The source of truth for kinds sendable from the pause menu; crash is created only by the previous-crash gate
        public static bool IsSubmittableFromPauseMenu(PlaytestReportKind kind)
        {
            return kind == PlaytestReportKind.Bug || kind == PlaytestReportKind.Feedback;
        }
    }
}
