using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// ラベルの文からリッチテキストの印を外して読む
    /// Reads a label's text without its rich-text tags
    /// </summary>
    /// <remarks>
    /// LocalizedLabel は行の高さ（Typography の行の箱・画面の行送り）を &lt;line-height=Npx&gt; で前置するので、文の中身を比べるテストはこれで外す
    /// LocalizedLabel prefixes the line height (Typography line box or a screen pitch) as &lt;line-height=Npx&gt;, so tests comparing the words strip it here
    /// </remarks>
    public static class LabelPlainText
    {
        private static readonly Regex RichTextTag = new("<[^>]+>");

        public static string PlainText(this Label label)
        {
            return RichTextTag.Replace(label.text, string.Empty);
        }
    }
}
