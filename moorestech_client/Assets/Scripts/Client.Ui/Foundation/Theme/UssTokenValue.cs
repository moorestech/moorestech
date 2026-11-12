using System.Globalization;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// customStyle から文字列で読んだ USS の長さと時間を数に直す
    /// Converts USS lengths and durations read as strings from customStyle into numbers
    /// </summary>
    public static class UssTokenValue
    {
        public static bool TryParsePixels(string text, out float pixels)
        {
            var body = text.EndsWith("px") ? text.Substring(0, text.Length - 2) : text;
            return TryParseNumber(body, out pixels);
        }

        public static bool TryParseMilliseconds(string text, out float milliseconds)
        {
            // ms を先に見ないと "160ms" を秒として読んでしまう
            // Check ms first or "160ms" would be read as seconds
            if (text.EndsWith("ms")) return TryParseNumber(text.Substring(0, text.Length - 2), out milliseconds);
            if (text.EndsWith("s") && TryParseNumber(text.Substring(0, text.Length - 1), out var seconds))
            {
                milliseconds = seconds * 1000f;
                return true;
            }
            return TryParseNumber(text, out milliseconds);
        }

        private static bool TryParseNumber(string text, out float value)
        {
            // NaN・Infinity は尺や長さとして使えないので数として読まない
            // NaN and Infinity are unusable as lengths or durations, so they are not read as numbers
            return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // "117deg" を 117 へ。customStyle は角度も文字列でしか返さない
        // Turns "117deg" into 117; customStyle returns angles only as strings
        public static bool TryParseDegrees(string text, out float degrees)
        {
            degrees = 0f;
            return text.EndsWith("deg") && TryParseNumber(text.Substring(0, text.Length - 3), out degrees);
        }

        // "82%" を 0.82 へ。customStyle は % の値を文字列でしか返さない
        // Turns "82%" into 0.82; customStyle returns percentages only as strings
        public static bool TryParsePercent(string text, out float ratio)
        {
            ratio = 0f;
            if (!text.EndsWith("%") || !TryParseNumber(text.Substring(0, text.Length - 1), out var percent)) return false;
            ratio = percent / 100f;
            return true;
        }
    }
}
