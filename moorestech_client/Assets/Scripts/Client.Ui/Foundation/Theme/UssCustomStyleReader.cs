using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// 部品の USS が var() で受け直したトークンを、解決後の値で C# へ読む
    /// Reads tokens that a component's USS re-declared through var() as resolved values in C#
    /// </summary>
    /// <remarks>
    /// :root のトークンは当てた要素の customStyle にしか出ず子へ継承されない。部品は自クラスのルールで --部品-名前: var(--token) と受け直し CustomStyleResolvedEvent で読む。px・ms 付きの値は float では読めないので文字列で読む（docs/uitk/TODO.md の customStyle）
    /// :root tokens appear only in the customStyle of the element they apply to and are not inherited; components re-declare --component-name: var(--token) in their own class rule and read it in CustomStyleResolvedEvent; values with px or ms cannot be read as float, so they are read as strings (customStyle in docs/uitk/TODO.md)
    /// </remarks>
    public static class UssCustomStyleReader
    {
        public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParsePixels(text, out pixels)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved length (got '{text}')");
            pixels = 0f;
            return false;
        }

        public static bool TryReadMilliseconds(ICustomStyle style, string propertyName, out float milliseconds)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParseMilliseconds(text, out milliseconds)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved duration (got '{text}')");
            milliseconds = 0f;
            return false;
        }

        public static bool TryReadColor(ICustomStyle style, string propertyName, out Color color)
        {
            if (style.TryGetValue(new CustomStyleProperty<Color>(propertyName), out color)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved color");
            color = Color.clear;
            return false;
        }

        // 単位なしはfloatで読める
        // Unitless numbers read as float
        public static bool TryReadNumber(ICustomStyle style, string propertyName, out float number)
        {
            if (style.TryGetValue(new CustomStyleProperty<float>(propertyName), out number)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved number");
            number = 0f;
            return false;
        }

        // % は文字列で読み、比（0.82 等）に直す
        // Percentages read as strings and become ratios such as 0.82
        public static bool TryReadPercent(ICustomStyle style, string propertyName, out float ratio)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParsePercent(text, out ratio)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved percentage (got '{text}')");
            ratio = 0f;
            return false;
        }

        // 角度（"117deg"）は文字列で読み、度の数に直す
        // Angles ("117deg") read as strings and become degrees
        public static bool TryReadDegrees(ICustomStyle style, string propertyName, out float degrees)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParseDegrees(text, out degrees)) return true;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved angle (got '{text}')");
            degrees = 0f;
            return false;
        }

        // inset・outset などのキーワードは文字列で読む。空も読めない扱い
        // Keywords such as inset and outset read as strings; empty counts as unreadable
        public static bool TryReadKeyword(ICustomStyle style, string propertyName, out string keyword)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out keyword) && !string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                return true;
            }
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved keyword (got '{keyword}')");
            keyword = string.Empty;
            return false;
        }
    }
}
