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
        public static float ReadPixels(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParsePixels(text, out var pixels)) return pixels;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved length (got '{text}')");
            return float.NaN;
        }

        public static float ReadMilliseconds(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<string>(propertyName), out var text) && UssTokenValue.TryParseMilliseconds(text, out var milliseconds)) return milliseconds;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved duration (got '{text}')");
            return float.NaN;
        }

        public static Color ReadColor(ICustomStyle style, string propertyName)
        {
            if (style.TryGetValue(new CustomStyleProperty<Color>(propertyName), out var color)) return color;
            Debug.LogError($"[UssCustomStyleReader] {propertyName} is not a resolved color");
            return Color.magenta;
        }
    }
}
