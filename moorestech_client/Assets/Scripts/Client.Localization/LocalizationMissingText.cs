namespace Client.Localization
{
    /// <summary>
    /// 欠けの印の書式を書く唯一の場所。印を作る側（resolver）と読む側（通知の口など）が同じ定義を共有する
    /// The only place that writes the missing-text marker format; the maker (resolver) and readers (e.g. the notification lookup) share one definition
    /// </summary>
    public static class LocalizationMissingText
    {
        // 全言語に無いキーに対して resolver が返す印（Web の i18nStore も同じ「[!key]」）
        // The marker the resolver returns for a key absent in every language (same "[!key]" as the Web i18nStore)
        internal static string Mark(string key)
        {
            return $"[!{key}]";
        }

        public static bool IsMissingText(string text, string key)
        {
            return text == Mark(key);
        }
    }
}
