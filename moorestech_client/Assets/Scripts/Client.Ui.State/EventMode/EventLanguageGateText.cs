namespace Client.Ui.State.EventMode
{
    /// <summary>
    /// 言語ゲートの辞書を通さない文言（選ばせる対象が辞書そのもの。ADR 0040・Web EventLanguageGate.tsx の見出し）
    /// Dictionary-independent copy of the language gate (the dictionary itself is what gets chosen; ADR 0040, the Web EventLanguageGate.tsx heading)
    /// </summary>
    public static class EventLanguageGateText
    {
        // ボタンが母国語表記なので見出しは英語 1 行で足りる（.decisions 2026-08-28）
        // Buttons are in native names, so one English heading line suffices (.decisions 2026-08-28)
        public const string Title = "Select Language";
    }
}
