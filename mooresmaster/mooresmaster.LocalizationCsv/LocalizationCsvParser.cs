using System.Collections.Generic;

namespace Mooresmaster.LocalizationCsv
{
    public static class LocalizationCsvParser
    {
        private const int LanguageStartColumn = 2;

        public static LocalizationCsv Parse(string csvText)
        {
            // クォート考慮でCSVをレコード分割
            // Split the CSV into quote-aware records
            var records = LocalizationCsvRecordReader.ParseRecords(csvText);
            if (records.Count == 0)
            {
                throw new LocalizationCsvException("localization.csv is empty");
            }

            var header = records[0];
            if (header.Count < LanguageStartColumn ||
                header[0] != "key" ||
                header[1] != "Source")
            {
                throw new LocalizationCsvException("localization.csv header must contain key and Source columns");
            }

            // 3列目以降を翻訳言語として保持
            // Preserve columns after Source as languages
            var languageCodes = new string[header.Count - LanguageStartColumn];
            for (var i = LanguageStartColumn; i < header.Count; i++)
            {
                languageCodes[i - LanguageStartColumn] = header[i];
            }

            var rows = new List<LocalizationRow>();
            var seenKeys = new HashSet<string>();
            for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
            {
                var fields = records[recordIndex];
                if (fields.Count != header.Count)
                {
                    throw new LocalizationCsvException(
                        $"Column count mismatch at record {recordIndex + 1}: expected {header.Count}, got {fields.Count}");
                }

                // キーの一意性をCSV入力境界で保証する
                // Enforce key uniqueness at the CSV input boundary
                var key = fields[0];
                if (!seenKeys.Add(key))
                {
                    throw new LocalizationCsvException($"Duplicated key: {key}");
                }

                var source = ConvertEscapedNewlines(fields[1]);
                var texts = new string[languageCodes.Length];
                for (var languageIndex = 0; languageIndex < languageCodes.Length; languageIndex++)
                {
                    texts[languageIndex] = ConvertEscapedNewlines(fields[languageIndex + LanguageStartColumn]);
                }

                rows.Add(new LocalizationRow(key, source, texts));
            }

            return new LocalizationCsv(languageCodes, rows.ToArray());

            #region Internal

            string ConvertEscapedNewlines(string text)
            {
                return text.Replace("\\n", "\n");
            }

            #endregion
        }
    }
}
