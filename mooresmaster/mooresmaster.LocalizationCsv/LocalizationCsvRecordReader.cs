using System.Collections.Generic;
using System.Text;

namespace Mooresmaster.LocalizationCsv
{
    public static class LocalizationCsvRecordReader
    {
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
            if (closedQuote || 0 < field.Length || 0 < fields.Count)
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
                if (1 < fields.Count || 0 < fields[0].Length || recordHasSyntax)
                {
                    records.Add(fields);
                }

                fields = new List<string>();
                recordHasSyntax = false;
            }

            #endregion
        }
    }
}
