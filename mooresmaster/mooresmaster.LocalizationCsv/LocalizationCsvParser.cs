using System.Collections.Generic;
using System.Text;

namespace Mooresmaster.LocalizationCsv
{
    public static class LocalizationCsvParser
    {
        private const int LanguageStartColumn = 2;

        public static LocalizationCsv Parse(string csvText)
        {
            // クォートを考慮してCSV全体をレコードへ分割する
            // Split the entire CSV into quote-aware records
            var records = ParseRecords(csvText);
            if (records.Count == 0)
            {
                throw new LocalizationCsvException("localization.csv is empty");
            }

            var header = records[0];
            if (header.Count < LanguageStartColumn)
            {
                throw new LocalizationCsvException("localization.csv header must contain key and Source columns");
            }

            // ヘッダの3列目以降を翻訳言語として保持する
            // Preserve columns after Source as translation languages
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
        }

        public static List<List<string>> ParseRecords(string csvText)
        {
            var records = new List<List<string>>();
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var closedQuote = false;
            // 空値でも明示quote構文があればレコードとして保持する
            // Preserve records with explicit quote syntax even when their value is empty
            var recordHasSyntax = false;

            // 文字単位の状態遷移で埋め込み改行とエスケープquoteを識別する
            // Use character-level state transitions for embedded newlines and escaped quotes
            for (var index = 0; index < csvText.Length; index++)
            {
                var character = csvText[index];
                if (inQuotes)
                {
                    if (character != '"')
                    {
                        field.Append(character);
                        continue;
                    }

                    if (index + 1 < csvText.Length && csvText[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                        continue;
                    }

                    inQuotes = false;
                    closedQuote = true;
                    continue;
                }

                // 閉じquote後はfield境界以外を不正入力として拒否する
                // Reject anything except a field boundary after a closing quote
                if (closedQuote)
                {
                    if (character == ',')
                    {
                        AddField();
                        closedQuote = false;
                        continue;
                    }

                    if (character == '\r' || character == '\n')
                    {
                        CompleteRecord(character, ref index);
                        closedQuote = false;
                        continue;
                    }

                    throw new LocalizationCsvException("Unexpected character after closing quote");
                }

                if (character == '"')
                {
                    recordHasSyntax = true;
                    if (field.Length != 0)
                    {
                        throw new LocalizationCsvException("Quote must begin at the start of a field");
                    }

                    inQuotes = true;
                }
                else if (character == ',')
                {
                    AddField();
                }
                else if (character == '\r' || character == '\n')
                {
                    CompleteRecord(character, ref index);
                }
                else
                {
                    field.Append(character);
                }
            }

            if (inQuotes)
            {
                throw new LocalizationCsvException("Unterminated quoted field");
            }

            // 終端改行後の空レコードを除き、末尾の空fieldは保持する
            // Preserve a trailing empty field while excluding an empty record after a final newline
            if (closedQuote || field.Length > 0 || fields.Count > 0)
            {
                AddRecord();
            }

            return records;

            #region Internal

            void AddField()
            {
                fields.Add(field.ToString());
                field.Clear();
            }

            void CompleteRecord(char delimiter, ref int index)
            {
                AddRecord();
                if (delimiter == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index++;
                }
            }

            void AddRecord()
            {
                AddField();
                if (fields.Count > 1 || fields[0].Length > 0 || recordHasSyntax)
                {
                    records.Add(fields);
                }

                fields = new List<string>();
                recordHasSyntax = false;
            }

            #endregion
        }

        private static string ConvertEscapedNewlines(string text)
        {
            return text.Replace("\\n", "\n");
        }
    }
}
