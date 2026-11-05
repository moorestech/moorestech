namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 言語コードから、ルートに付けるロケールのクラスを決める
    /// Decides the locale class put on the root from a language code
    /// </summary>
    public static class GameUiLocaleClass
    {
        public const string CjkClassName = "game-ui-locale--cjk";

        // Web の :root[data-locale] と同じ 4 言語で JP フォントを先頭にする
        // Lead with the JP font for the same four languages as the Web's :root[data-locale]
        public static bool IsCjk(string languageCode)
        {
            return languageCode == "japanese" || languageCode == "korean" || languageCode == "chinese-simplified" || languageCode == "chinese-traditional";
        }
    }
}
