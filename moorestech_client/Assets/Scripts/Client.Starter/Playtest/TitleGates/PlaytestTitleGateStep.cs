namespace Client.Starter.Playtest.TitleGates
{
    // タイトルのゲートの段階。Passed になるまで Play locally とサーバー接続は開始しない（ADR 0065）
    // The title gates' step; Play locally and server connection do not start until it reaches Passed (ADR 0065)
    public enum PlaytestTitleGateStep
    {
        NotStarted,
        Consent,
        CrashReport,
        Passed,
    }
}
