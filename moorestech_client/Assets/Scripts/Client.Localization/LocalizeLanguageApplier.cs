namespace Client.Localization
{
    // 要求言語を適用し、不可なら既定言語へ落とす唯一の窓口
    // The single entry that applies a requested language and falls back to the default when refused
    public static class LocalizeLanguageApplier
    {
        public static LanguageApplyResult ApplyOrDefault(string requestedLanguageCode)
        {
            // 未指定は既定言語を適用して正常扱い
            // Unset applies the default language and counts as normal
            if (string.IsNullOrEmpty(requestedLanguageCode))
            {
                Localize.TrySetLanguage(Localize.DefaultLanguageCode);
                return new LanguageApplyResult(LanguageResolution.Unset, Localize.DefaultLanguageCode);
            }

            // 可否判定はTrySetLanguage（公開辞書）だけに任せる
            // Acceptance is decided only by TrySetLanguage against the published dictionary
            if (Localize.TrySetLanguage(requestedLanguageCode))
                return new LanguageApplyResult(LanguageResolution.Accepted, requestedLanguageCode);

            Localize.TrySetLanguage(Localize.DefaultLanguageCode);
            return new LanguageApplyResult(LanguageResolution.UnknownFallback, Localize.DefaultLanguageCode);
        }
    }
}
