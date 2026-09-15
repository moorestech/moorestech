using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Json
{
    /// <summary>セーブJSONの文字列を木へ読む唯一の入口。日付らしい文字列を日付型へ化けさせない</summary>
    /// <summary>The single entry that reads save JSON text into a tree without turning date-like strings into dates</summary>
    public static class SaveJsonObjectReader
    {
        // 既定のDateParseHandlingは日付らしい文字列をDateTimeへ変え、書き戻すと綴りが変わる
        // The default DateParseHandling turns date-like strings into DateTime, and writing them back changes their spelling
        public static JObject Read(string saveJsonText)
        {
            using var textReader = new StringReader(saveJsonText);
            using var jsonReader = new JsonTextReader(textReader) { DateParseHandling = DateParseHandling.None };
            var save = JObject.Load(jsonReader);

            // 先頭の値の後ろにコメント以外が残るテキストはセーブとして完結していない（JObject.Parseと同じ判定）
            // Text with anything but comments left after the first value is not a complete save, matching JObject.Parse
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType != JsonToken.Comment) throw new JsonReaderException($"セーブJSONの末尾に余分な内容があります。 path={jsonReader.Path}");
            }

            return save;
        }
    }
}
