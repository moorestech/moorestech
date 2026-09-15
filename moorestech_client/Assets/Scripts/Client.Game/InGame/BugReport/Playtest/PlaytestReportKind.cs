namespace Client.Game.InGame.BugReport.Playtest
{
    // プレイ報告の種別。C#の内部はこの enum で持ち回し、契約値の文字列へは PlaytestReportKindText だけが綴る（ADR 0058）
    // The play-report kind; C# carries this enum everywhere, and only PlaytestReportKindText spells it as the contract string (ADR 0058)
    public enum PlaytestReportKind
    {
        Bug,
        Feedback,

        // クラッシュは前回異常終了ゲートだけが作る。ポーズメニューからは選ばせない
        // Only the previous-crash gate creates the crash kind; the pause menu must not offer it
        Crash,
    }
}
