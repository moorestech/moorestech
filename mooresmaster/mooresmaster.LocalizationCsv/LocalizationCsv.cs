namespace Mooresmaster.LocalizationCsv
{
    public sealed class LocalizationCsv
    {
        public readonly string[] LanguageCodes;
        public readonly LocalizationRow[] Rows;

        public LocalizationCsv(string[] languageCodes, LocalizationRow[] rows)
        {
            LanguageCodes = languageCodes;
            Rows = rows;
        }
    }

    public sealed class LocalizationRow
    {
        public readonly string Key;
        public readonly string Source;
        public readonly string[] Texts;

        public LocalizationRow(string key, string source, string[] texts)
        {
            Key = key;
            Source = source;
            Texts = texts;
        }
    }
}
