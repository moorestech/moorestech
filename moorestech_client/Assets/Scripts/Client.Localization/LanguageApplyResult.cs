namespace Client.Localization
{
    // 要求言語の適用結果の種別
    // Kind of outcome when applying a requested language
    public enum LanguageResolution
    {
        Unset,
        Accepted,
        UnknownFallback,
    }

    // 適用結果と実際に適用された言語コード
    // The apply outcome paired with the language code actually applied
    public readonly struct LanguageApplyResult
    {
        public readonly LanguageResolution Resolution;
        public readonly string AppliedLanguageCode;

        public LanguageApplyResult(LanguageResolution resolution, string appliedLanguageCode)
        {
            Resolution = resolution;
            AppliedLanguageCode = appliedLanguageCode;
        }
    }
}
