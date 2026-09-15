namespace Client.WebUiHost.Game.StartGates
{
    // 開始ゲート3枚のtopic名と答えさせる順。precedenceは起動時に待つ順（言語→同意→前回異常終了）の正本で、Web側は比べるだけ
    // The three start gates' topic names and answer order; precedence owns the boot-time wait order (language, consent, crash) and the web only compares it
    internal static class StartGateTopics
    {
        public const string EventLanguageName = "event_mode.language_gate";
        public const string ConsentName = "playtest.consent_gate";
        public const string CrashReportName = "playtest.crash_report_gate";

        public const int EventLanguagePrecedence = 0;
        public const int ConsentPrecedence = 1;
        public const int CrashReportPrecedence = 2;
    }
}
