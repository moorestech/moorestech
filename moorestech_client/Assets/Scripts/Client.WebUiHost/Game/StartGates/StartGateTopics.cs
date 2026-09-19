namespace Client.WebUiHost.Game.StartGates
{
    // 開始ゲートのtopic名と答えさせる順。プレイテストの同意と前回異常終了の確認はタイトル（uGUI）へ移り、WebUIに残るのは出展モードの言語選択だけ（ADR 0065）
    // Start-gate topic names and answer order; the playtest consent and crash confirmation moved to the title (uGUI), leaving only event mode's language selection in the WebUI (ADR 0065)
    internal static class StartGateTopics
    {
        public const string EventLanguageName = "event_mode.language_gate";

        public const int EventLanguagePrecedence = 0;
    }
}
